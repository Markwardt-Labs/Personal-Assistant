namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Learns what the user wants. The agent keeps its knowledge about the user as files in the Preferences folder of its workspace and organizes them itself; this service keeps the likes and dislikes of the user, writes them to the workspace for the agent to read, has the agent fold every few new ones into its files, and tells every AI session how to use the workspace.
/// </summary>
internal interface IPreferenceService
{
    /// <summary>
    /// Loads what was saved, which has to be done before <see cref="Of" /> knows anything.
    /// </summary>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>A task that completes once the reactions are loaded.</returns>
    Task Ready(CancellationToken cancellation = default);

    /// <summary>
    /// Tells what the user thought of an entry.
    /// </summary>
    /// <param name="target">What kind of entry it is.</param>
    /// <param name="key">What identifies the entry.</param>
    /// <returns>The reaction, or <see cref="ReactionKind.None" /> when there is none or the reactions are not loaded.</returns>
    ReactionKind Of(ReactionTarget target, string key);

    /// <summary>
    /// Records a reaction, replacing the earlier one to the same entry, writes the reactions file of the workspace, and has the agent fold the new reactions into its preference files when enough came in.
    /// </summary>
    /// <param name="target">What kind of entry it is.</param>
    /// <param name="key">What identifies the entry.</param>
    /// <param name="title">The title of the entry.</param>
    /// <param name="context">What else describes the entry, or <see langword="null" />.</param>
    /// <param name="kind">What the user thinks; <see cref="ReactionKind.None" /> takes the reaction back.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <exception cref="RequestException">The key or the title is missing.</exception>
    Task React(ReactionTarget target, string? key, string? title, string? context, ReactionKind kind, CancellationToken cancellation = default);

    /// <summary>
    /// Writes the instructions that point an AI session at the preference files of its workspace.
    /// </summary>
    /// <param name="canWrite">Whether the session may change the files (the assistant, which records what the user tells it, and the session that folds in reactions) or only reads them (the searches).</param>
    /// <returns>The instructions to append to the ones of the session.</returns>
    string Background(bool canWrite);

    /// <summary>
    /// Waits for the work in progress to stop.
    /// </summary>
    /// <returns>A task that completes when everything has stopped.</returns>
    Task Close();
}

/// <inheritdoc />
internal sealed class PreferenceService(IPreferenceAi ai, IJsonFile<StoredReactions> store, IAgentWorkspace workspace, ILog log, TimeProvider time) : IPreferenceService
{
    private readonly int kept = 150;

    private readonly int batch = 10;

    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly SemaphoreSlim learning = new(1, 1);

    private readonly CancellationTokenSource closing = new();

    private readonly List<Task> running = [];

    private StoredReactions state = new() { Reactions = [] };

    private bool loaded;

    /// <inheritdoc />
    public async Task Ready(CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);

        try
        {
            await Load(cancellation);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public ReactionKind Of(ReactionTarget target, string key)
        => state.Reactions.FirstOrDefault(reaction => reaction.Target == target && reaction.Key == key)?.Kind ?? ReactionKind.None;

    /// <inheritdoc />
    public async Task React(ReactionTarget target, string? key, string? title, string? context, ReactionKind kind, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(title) || !Enum.IsDefined(kind))
        {
            throw new RequestException("The entry to react to is missing.");
        }

        await gate.WaitAsync(cancellation);

        try
        {
            await Load(cancellation);
            List<StoredReaction> reactions = [.. state.Reactions.Where(reaction => !(reaction.Target == target && reaction.Key == key))];

            if (kind != ReactionKind.None)
            {
                reactions.Add(new() { Target = target, Key = key, Title = Clip(title, 200), Context = string.IsNullOrWhiteSpace(context) ? null : Clip(context, 200), Kind = kind, Time = time.GetUtcNow() });
            }

            state = state with { Reactions = [.. reactions.TakeLast(kept)] };
            await store.Save(state, cancellation);
            await Write(cancellation);
        }
        finally
        {
            gate.Release();
        }

        if (Pending().Count >= batch)
        {
            Track(Learn());
        }
    }

    /// <inheritdoc />
    public string Background(bool canWrite)
        => "\n\nYour workspace folder is your memory about this user. Its Preferences folder holds what they want to see in their mail, events and news and how they want things done: read README.md there and every file in the folder before you start, and follow what they say. Reactions.md lists the entries they liked and disliked, newest last."
        + (canWrite
            ? " When the user tells you a lasting preference or says to remember something, write it into the right file of the Preferences folder as the README describes (create a file for a new topic, merge or split files so each stays short and organized, replace statements that no longer hold) and tell them; the mail, events and news tabs use it the next time they refresh. Never edit Reactions.md."
            : " Only read the workspace: never create, change or delete a file in it.");

    /// <inheritdoc />
    public async Task Close()
    {
        await closing.CancelAsync();
        Task[] pending;

        lock (running)
        {
            pending = [.. running];
        }

        await Task.WhenAll(pending);
    }

    private async Task Load(CancellationToken cancellation = default)
    {
        if (!loaded)
        {
            state = await store.Load(cancellation) ?? state;
            loaded = true;
        }
    }

    private string Clip(string text, int length)
    {
        string trimmed = text.Trim().ReplaceLineEndings(" ");
        return trimmed.Length > length ? trimmed[..length] : trimmed;
    }

    private List<StoredReaction> Pending()
        => [.. state.Reactions.Where(reaction => state.ProfiledThrough is not { } through || reaction.Time > through)];

    private async Task Write(CancellationToken cancellation = default)
    {
        await workspace.Prepare(cancellation);
        StringBuilder text = new();
        text.Append("# Reactions\n\nWritten by the server, newest last. Never edit this file.\n\n");

        foreach (StoredReaction reaction in state.Reactions)
        {
            text.Append("- ").Append(reaction.Time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(' ').Append(reaction.Kind == ReactionKind.Like ? "liked" : "disliked").Append(' ').Append(reaction.Target.ToString().ToLowerInvariant()).Append(" \"").Append(reaction.Title).Append('"').Append(reaction.Context is null ? string.Empty : $" ({reaction.Context})").Append('\n');
        }

        await File.WriteAllTextAsync(Path.Combine(workspace.Preferences, "Reactions.md"), text.ToString(), cancellation);
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

    private async Task Learn()
    {
        if (!await learning.WaitAsync(0))
        {
            return;
        }

        try
        {
            List<StoredReaction> pending = Pending();

            if (pending.Count < batch)
            {
                return;
            }

            await ai.Organize(pending, Background(true), closing.Token);
            await gate.WaitAsync(closing.Token);

            try
            {
                state = state with { ProfiledThrough = pending.Max(reaction => reaction.Time) };
                await store.Save(state, closing.Token);
            }
            finally
            {
                gate.Release();
            }

            log.Info($"Preference files updated from {pending.Count} reactions");
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            log.Error("The preference files could not be updated", exception);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
        }
        finally
        {
            learning.Release();
        }
    }
}
