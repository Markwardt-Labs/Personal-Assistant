namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// An authenticated connection to the server. Requests may overlap; each call completes when its own response arrives.
/// </summary>
internal interface IServerConnection : IAsyncDisposable
{
    /// <summary>
    /// Raised when the server pushes an event, on the synchronization context the connection was opened on when it has one.
    /// </summary>
    event Action<RemoteEvent>? EventReceived;

    /// <summary>
    /// Raised with the reason when the connection to the server is lost; it is not raised for a connection that was disposed.
    /// </summary>
    event Action<string>? Lost;

    /// <summary>
    /// Gets the version the server reported when the connection was opened.
    /// </summary>
    string? ServerVersion { get; }

    /// <summary>
    /// Reads the newest messages of the conversation.
    /// </summary>
    /// <param name="limit">The most messages to read, or <see langword="null" /> for the server's default.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The messages and whether the assistant is working.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<ChatHistory> GetHistory(int? limit, CancellationToken cancellation = default);

    /// <summary>
    /// Gives the assistant a prompt; the replies arrive as events.
    /// </summary>
    /// <param name="text">The prompt.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task Prompt(string text, CancellationToken cancellation = default);

    /// <summary>
    /// Stops what the assistant is doing.
    /// </summary>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task Interrupt(CancellationToken cancellation = default);

    /// <summary>
    /// Reads the mail digest and, when there is none yet or a refresh is asked for, makes the server start a new one.
    /// </summary>
    /// <param name="refresh">Whether to make a new digest even when one exists.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The digest and whether a new one is being made.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<MailDigestState> GetMailDigest(bool refresh = false, CancellationToken cancellation = default);

    /// <summary>
    /// Reads one message in full.
    /// </summary>
    /// <param name="id">The identifier of the message.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The message.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<MailMessage> ReadMail(string id, CancellationToken cancellation = default);

    /// <summary>
    /// Reads the calendar events, the event feeds and the suggested events and, when there are no suggestions yet or a refresh is asked for, makes the server start looking for new ones.
    /// </summary>
    /// <param name="refresh">Whether to look for new suggestions even when there are some.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The state.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<AgendaState> GetAgenda(bool refresh = false, CancellationToken cancellation = default);

    /// <summary>
    /// Adds an event feed.
    /// </summary>
    /// <param name="name">The name of the feed.</param>
    /// <param name="description">What to search for.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The state with the new feed.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<AgendaState> AddEventFeed(string name, string description, CancellationToken cancellation = default);

    /// <summary>
    /// Removes an event feed.
    /// </summary>
    /// <param name="id">The identifier of the feed.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The state without the feed.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<AgendaState> RemoveEventFeed(string id, CancellationToken cancellation = default);

    /// <summary>
    /// Makes the server update itself to the newest release, after which it restarts.
    /// </summary>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <exception cref="RemoteException">The server refused, for example because there is no newer release, or it is too old to know the command.</exception>
    Task UpdateServer(CancellationToken cancellation = default);

    /// <summary>
    /// Reads the news feeds and their entries and, when there are no entries yet or a refresh is asked for, makes the server start a search.
    /// </summary>
    /// <param name="refresh">Whether to search again even when there are entries.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The state.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<NewsState> GetNews(bool refresh = false, CancellationToken cancellation = default);

    /// <summary>
    /// Adds a news feed.
    /// </summary>
    /// <param name="name">The name of the feed.</param>
    /// <param name="description">What type of news to search for.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The state with the new feed.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<NewsState> AddNewsFeed(string name, string description, CancellationToken cancellation = default);

    /// <summary>
    /// Removes a news feed.
    /// </summary>
    /// <param name="id">The identifier of the feed.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The state without the feed.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<NewsState> RemoveNewsFeed(string id, CancellationToken cancellation = default);

    /// <summary>
    /// Likes or dislikes an entry, or takes the reaction back, which the server learns from.
    /// </summary>
    /// <param name="target">What kind of entry it is.</param>
    /// <param name="key">What identifies the entry, see <see cref="ReactionKeys" />.</param>
    /// <param name="title">The title of the entry.</param>
    /// <param name="context">What else describes the entry, or <see langword="null" />.</param>
    /// <param name="kind">What the user thinks.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task React(ReactionTarget target, string key, string title, string? context, ReactionKind kind, CancellationToken cancellation = default);

    /// <summary>
    /// Reads the settings of the server.
    /// </summary>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<ServerSettings> GetSettings(CancellationToken cancellation = default);

    /// <summary>
    /// Changes the settings of the server.
    /// </summary>
    /// <param name="update">The new settings.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>The settings as they are now.</returns>
    /// <exception cref="RemoteException">The server refused or the connection was lost.</exception>
    Task<ServerSettings> SetSettings(ServerSettingsUpdate update, CancellationToken cancellation = default);

    /// <summary>
    /// Opens <see cref="IServerConnection" /> instances.
    /// </summary>
    public interface IFactory
    {
        /// <summary>
        /// Connects to the server and sends the access key.
        /// </summary>
        /// <param name="settings">Where the server is and the key it requires.</param>
        /// <param name="cancellation">Cancels the connecting.</param>
        /// <returns>The open connection.</returns>
        /// <exception cref="RemoteException">The server cannot be reached or does not accept the key.</exception>
        Task<IServerConnection> Open(ConnectionSettings settings, CancellationToken cancellation = default);
    }

    /// <inheritdoc />
    public sealed class Factory(IMessageChannel.IFactory channels) : IFactory
    {
        private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);

        /// <inheritdoc />
        public async Task<IServerConnection> Open(ConnectionSettings settings, CancellationToken cancellation = default)
        {
            ClientWebSocket socket = new();
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            socket.Options.KeepAliveTimeout = TimeSpan.FromSeconds(20);
            socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            limit.CancelAfter(timeout);

            try
            {
                await socket.ConnectAsync(new UriBuilder("wss", settings.Host, settings.Port).Uri, limit.Token);
                IMessageChannel channel = channels.Create(socket);
                await channel.Send(new HandshakeRequest { AccessKey = settings.AccessKey }, limit.Token);
                HandshakeResponse? response = await channel.Receive<HandshakeResponse>(1024, limit.Token);

                if (response is not { Accepted: true })
                {
                    throw new RemoteException(response is null ? "The server closed the connection." : "The server did not accept the access key.");
                }

                ServerConnection connection = new(socket, channel, response.Version, SynchronizationContext.Current);
                connection.Run();
                return connection;
            }
            catch (Exception exception) when (exception is WebSocketException or SocketException or InvalidDataException or OperationCanceledException or UriFormatException or HttpRequestException or RemoteException)
            {
                socket.Dispose();

                if (exception is RemoteException || (exception is OperationCanceledException && cancellation.IsCancellationRequested))
                {
                    throw;
                }

                throw new RemoteException(exception is OperationCanceledException ? "The server did not answer in time." : $"The server could not be reached: {exception.Message}");
            }
        }
    }
}

/// <inheritdoc />
internal sealed class ServerConnection(WebSocket socket, IMessageChannel channel, string? version, SynchronizationContext? context) : IServerConnection
{
    private readonly Dictionary<int, TaskCompletionSource<RemoteResponse>> pending = [];

    private readonly CancellationTokenSource stopping = new();

    private readonly Lock gate = new();

    private int lastId;

    private Task reader = Task.CompletedTask;

    private bool disposed;

    /// <inheritdoc />
    public event Action<RemoteEvent>? EventReceived;

    /// <inheritdoc />
    public event Action<string>? Lost;

    /// <inheritdoc />
    public string? ServerVersion { get; } = version;

    /// <inheritdoc />
    public async Task<ChatHistory> GetHistory(int? limit, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.GetHistory, Limit = limit }, cancellation)).History ?? throw new RemoteException("The server sent no conversation.");

    /// <inheritdoc />
    public async Task Prompt(string text, CancellationToken cancellation = default)
        => await Call(new() { Id = 0, Command = RemoteCommand.Prompt, Text = text }, cancellation);

    /// <inheritdoc />
    public async Task Interrupt(CancellationToken cancellation = default)
        => await Call(new() { Id = 0, Command = RemoteCommand.Interrupt }, cancellation);

    /// <inheritdoc />
    public async Task<MailDigestState> GetMailDigest(bool refresh = false, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.GetMailDigest, Refresh = refresh }, cancellation)).MailDigest ?? throw new RemoteException("The server sent no mail digest.");

    /// <inheritdoc />
    public async Task<MailMessage> ReadMail(string id, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.ReadMail, MessageId = id }, cancellation)).Mail ?? throw new RemoteException("The server sent no message.");

    /// <inheritdoc />
    public async Task<AgendaState> GetAgenda(bool refresh = false, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.GetAgenda, Refresh = refresh }, cancellation)).Agenda ?? throw new RemoteException("The server sent no events.");

    /// <inheritdoc />
    public async Task<AgendaState> AddEventFeed(string name, string description, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.AddEventFeed, Name = name, Text = description }, cancellation)).Agenda ?? throw new RemoteException("The server sent no events.");

    /// <inheritdoc />
    public async Task<ServerSettings> GetSettings(CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.GetSettings }, cancellation)).Settings ?? throw new RemoteException("The server sent no settings.");

    /// <inheritdoc />
    public async Task<ServerSettings> SetSettings(ServerSettingsUpdate update, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.SetSettings, Settings = update }, cancellation)).Settings ?? throw new RemoteException("The server sent no settings.");

    /// <inheritdoc />
    public async Task<AgendaState> RemoveEventFeed(string id, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.RemoveEventFeed, FeedId = id }, cancellation)).Agenda ?? throw new RemoteException("The server sent no events.");

    /// <inheritdoc />
    public async Task UpdateServer(CancellationToken cancellation = default)
        => await Call(new() { Id = 0, Command = RemoteCommand.UpdateServer }, cancellation);

    /// <inheritdoc />
    public async Task<NewsState> GetNews(bool refresh = false, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.GetNews, Refresh = refresh }, cancellation)).News ?? throw new RemoteException("The server sent no news.");

    /// <inheritdoc />
    public async Task<NewsState> AddNewsFeed(string name, string description, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.AddNewsFeed, Name = name, Text = description }, cancellation)).News ?? throw new RemoteException("The server sent no news.");

    /// <inheritdoc />
    public async Task<NewsState> RemoveNewsFeed(string id, CancellationToken cancellation = default)
        => (await Call(new() { Id = 0, Command = RemoteCommand.RemoveNewsFeed, FeedId = id }, cancellation)).News ?? throw new RemoteException("The server sent no news.");

    /// <inheritdoc />
    public async Task React(ReactionTarget target, string key, string title, string? context, ReactionKind kind, CancellationToken cancellation = default)
        => await Call(new() { Id = 0, Command = RemoteCommand.React, Target = target, Key = key, Title = title, Context = context, Reaction = kind }, cancellation);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await stopping.CancelAsync();

        try
        {
            await reader;
        }
        finally
        {
            socket.Dispose();
            stopping.Dispose();
        }
    }

    /// <summary>
    /// Starts reading what the server sends.
    /// </summary>
    public void Run()
        => reader = Read();

    private async Task<RemoteResponse> Call(RemoteRequest request, CancellationToken cancellation = default)
    {
        TaskCompletionSource<RemoteResponse> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int id;

        lock (gate)
        {
            id = ++lastId;
            pending[id] = completion;
        }

        try
        {
            await channel.Send(request with { Id = id }, cancellation);
            RemoteResponse response = await completion.Task.WaitAsync(cancellation);
            return response.Error is { } error ? throw new RemoteException(error) : response;
        }
        catch (Exception exception) when (exception is WebSocketException or ObjectDisposedException or InvalidOperationException)
        {
            throw new RemoteException("The connection to the server was lost.");
        }
        finally
        {
            lock (gate)
            {
                pending.Remove(id);
            }
        }
    }

    private async Task Read()
    {
        string reason = "The server closed the connection.";

        try
        {
            while (await channel.Receive<RemoteResponse>(40 * 1024 * 1024, stopping.Token) is { } response)
            {
                if (response.Event is { } remoteEvent)
                {
                    Post(() => EventReceived?.Invoke(remoteEvent));
                }
                else
                {
                    TaskCompletionSource<RemoteResponse>? completion;

                    lock (gate)
                    {
                        pending.Remove(response.Id, out completion);
                    }

                    completion?.TrySetResult(response);
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or InvalidDataException or OperationCanceledException or ObjectDisposedException)
        {
            reason = exception is OperationCanceledException ? reason : $"The connection to the server was lost: {exception.Message}";
        }

        List<TaskCompletionSource<RemoteResponse>> orphaned;

        lock (gate)
        {
            orphaned = [.. pending.Values];
            pending.Clear();
        }

        foreach (TaskCompletionSource<RemoteResponse> completion in orphaned)
        {
            completion.TrySetException(new RemoteException("The connection to the server was lost."));
        }

        if (!disposed)
        {
            Post(() => Lost?.Invoke(reason));
        }
    }

    private void Post(Action action)
    {
        if (context is null)
        {
            action();
        }
        else
        {
            context.Post(_ => action(), null);
        }
    }
}
