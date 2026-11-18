namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The assistant: owns the AI session, gives it the user's prompts and turns what it writes into the conversation and events every client sees.
/// </summary>
internal interface IAssistantService
{
    /// <summary>
    /// Raised when the assistant has finished work in which it changed the calendar, so that what is shown of the calendar can be read again.
    /// </summary>
    event Action? CalendarEdited;

    /// <summary>
    /// Reads the newest part of the conversation.
    /// </summary>
    /// <param name="limit">The most messages to read, or <see langword="null" /> for the default.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The messages and whether the assistant is working.</returns>
    Task<ChatHistory> History(int? limit, CancellationToken cancellation = default);

    /// <summary>
    /// Gives the assistant a prompt. A prompt sent while it is working is given to the work in progress; the replies arrive as events.
    /// </summary>
    /// <param name="text">The prompt.</param>
    /// <param name="cancellation">Cancels the delivery.</param>
    /// <exception cref="RequestException">The prompt is empty, or the AI could not take it.</exception>
    Task Prompt(string? text, CancellationToken cancellation = default);

    /// <summary>
    /// Stops what the assistant is doing now.
    /// </summary>
    /// <param name="cancellation">Cancels the request to stop.</param>
    /// <returns><see langword="true" /> if it was working.</returns>
    Task<bool> Interrupt(CancellationToken cancellation = default);

    /// <summary>
    /// Ends the AI session, waiting for the work in progress to stop.
    /// </summary>
    /// <returns>A task that completes when the session is closed.</returns>
    Task Close();
}

/// <inheritdoc />
internal sealed class AssistantService(IAssistantSessions sessions, IAssistantInstructions instructions, IAiSetups setups, IPreferenceService preferences, IChatStore chat, IEventHub events, ILog log) : IAssistantService, IObserver<PromptEvent>
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly SemaphoreSlim recording = new(1, 1);

    private readonly Channel<PromptEvent> incoming = Channel.CreateUnbounded<PromptEvent>();

    private readonly List<Task> running = [];

    private readonly string[] calendarEdits = ["create_event", "update_event", "delete_event", "respond_to_event"];

    private IPromptSession? session;

    private IDisposable? subscription;

    private Task? consumer;

    private bool working;

    private bool failureReported;

    private bool calendarEdited;

    /// <inheritdoc />
    public event Action? CalendarEdited;

    /// <inheritdoc />
    public async Task<ChatHistory> History(int? limit, CancellationToken cancellation = default)
        => new() { Messages = await chat.Read(Math.Clamp(limit ?? 100, 1, 500), cancellation), Working = working };

    /// <inheritdoc />
    public async Task Prompt(string? text, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new RequestException("The prompt is empty.");
        }

        IPromptSession current = await Open(cancellation);
        current.Configuration.Instructions = setups.WithConnectors(instructions.Text + preferences.Background(true));
        await Record(ChatRole.User, text, cancellation);

        try
        {
            if (current.State == PromptSessionState.Waiting)
            {
                failureReported = false;
                Track(Run(current, text));
            }
            else
            {
                await current.Send(text, null, cancellation);
            }
        }
        catch (PromptSessionException exception)
        {
            throw new RequestException($"The AI could not take the prompt: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> Interrupt(CancellationToken cancellation = default)
        => session is { } current && await current.Interrupt(cancellation);

    /// <inheritdoc />
    public async Task Close()
    {
        Task[] pending;

        await gate.WaitAsync();

        try
        {
            if (session is not null)
            {
                await session.Interrupt();
            }

            lock (running)
            {
                pending = [.. running];
            }
        }
        finally
        {
            gate.Release();
        }

        await Task.WhenAll(pending);
        subscription?.Dispose();

        if (session is not null)
        {
            await session.DisposeAsync();
            session = null;
        }

        incoming.Writer.TryComplete();

        if (consumer is not null)
        {
            await consumer;
        }
    }

    /// <inheritdoc />
    public void OnNext(PromptEvent value)
        => incoming.Writer.TryWrite(value);

    /// <inheritdoc />
    public void OnError(Exception error)
        => log.Error("The AI session reported an error", error);

    /// <inheritdoc />
    public void OnCompleted()
    {
    }

    private async Task<IPromptSession> Open(CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);

        try
        {
            if (session is null)
            {
                try
                {
                    session = await sessions.Open(cancellation);
                }
                catch (PromptSessionException exception)
                {
                    throw new RequestException($"The AI could not be started: {exception.Message}");
                }

                subscription = session.Subscribe(this);
                consumer ??= Consume();
            }

            return session;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task Consume()
    {
        await foreach (PromptEvent value in incoming.Reader.ReadAllAsync())
        {
            switch (value)
            {
                case PromptMessageEvent message:
                    await Record(ChatRole.Assistant, message.Text, CancellationToken.None);
                    break;
                case PromptFailureEvent failure:
                    failureReported = true;
                    await Record(ChatRole.Assistant, $"I could not finish that: {failure.Message}", CancellationToken.None);
                    break;
                case PromptToolCallEvent call when call.Server == "claude_ai_Google_Calendar" && calendarEdits.Contains(call.Name):
                    calendarEdited = true;
                    break;
                case PromptStateEvent state:
                    SetWorking(state.State != PromptSessionState.Waiting);

                    if (state.State == PromptSessionState.Waiting && calendarEdited)
                    {
                        calendarEdited = false;
                        CalendarEdited?.Invoke();
                    }

                    break;
            }
        }
    }

    private async Task Record(ChatRole role, string text, CancellationToken cancellation = default)
    {
        await recording.WaitAsync(cancellation);

        try
        {
            ChatMessage message = await chat.Add(role, text, cancellation);
            events.Publish(new() { Kind = RemoteEventKind.MessageAdded, Message = message });
        }
        finally
        {
            recording.Release();
        }
    }

    private void SetWorking(bool value)
    {
        if (working == value)
        {
            return;
        }

        working = value;
        events.Publish(new() { Kind = RemoteEventKind.WorkingChanged, Working = value });
    }

    private void Track(Task task)
    {
        lock (running)
        {
            running.Add(task);
        }

        _ = task.ContinueWith(
            finished =>
            {
                lock (running)
                {
                    running.Remove(finished);
                }
            },
            TaskScheduler.Default);
    }

    private async Task Run(IPromptSession current, string text)
    {
        try
        {
            await current.Request(text);
        }
        catch (PromptSessionStateException)
        {
            await current.Send(text);
        }
        catch (PromptSessionException exception)
        {
            log.Error("The AI failed a prompt", exception);

            if (!failureReported)
            {
                await Record(ChatRole.Assistant, $"I could not finish that: {exception.Message}", CancellationToken.None);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
