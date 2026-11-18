namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Keeps the mail digest: has the AI read the mail of the last seven days with the user's Gmail tools and group it into topics once a day and whenever a client asks, and tells every client when it changes.
/// </summary>
internal interface IMailDigestService
{
    /// <summary>
    /// Reads the digest and what is being done about the next one.
    /// </summary>
    /// <param name="refresh">Whether to make a new digest now even when one exists; without it a new one is only started when there is none yet.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The state, which is also pushed to the clients as an event when it changes.</returns>
    Task<MailDigestState> Get(bool refresh, CancellationToken cancellation = default);

    /// <summary>
    /// Starts the daily refresh.
    /// </summary>
    /// <param name="stopping">Ends the schedule when cancelled.</param>
    void Start(CancellationToken stopping);

    /// <summary>
    /// Ends the schedule and waits for the work in progress to stop.
    /// </summary>
    /// <returns>A task that completes when everything has stopped.</returns>
    Task Close();
}

/// <inheritdoc />
internal sealed class MailDigestService(IMailDigestAi ai, IJsonFile<MailDigest> store, IPreferenceService preferences, IDailySchedule schedule, IEventHub events, ILog log, TimeProvider time) : IMailDigestService
{
    private readonly string query = "newer_than:7d -in:sent -in:drafts";

    private readonly int limit = 200;

    private readonly SemaphoreSlim loading = new(1, 1);

    private readonly SemaphoreSlim generation = new(1, 1);

    private readonly CancellationTokenSource closing = new();

    private readonly List<Task> running = [];

    private MailDigest? digest;

    private bool loaded;

    private bool generating;

    private string? error;

    /// <inheritdoc />
    public async Task<MailDigestState> Get(bool refresh, CancellationToken cancellation = default)
    {
        await Load(cancellation);

        if (refresh || digest is null)
        {
            Track(Generate());
        }

        return State();
    }

    /// <inheritdoc />
    public void Start(CancellationToken stopping)
        => Track(Schedule(CancellationTokenSource.CreateLinkedTokenSource(stopping, closing.Token)));

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

    private MailDigestState State()
        => new() { Digest = digest is null ? null : digest with { Topics = [.. digest.Topics.Select(topic => topic with { Reaction = preferences.Of(ReactionTarget.Mail, topic.ReactionKey) })] }, Generating = generating, Error = error };

    private async Task Load(CancellationToken cancellation = default)
    {
        await loading.WaitAsync(cancellation);

        try
        {
            if (!loaded)
            {
                digest = await store.Load(cancellation);
                await preferences.Ready(cancellation);
                loaded = true;
            }
        }
        finally
        {
            loading.Release();
        }
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

    private async Task Schedule(CancellationTokenSource stopping)
    {
        using (stopping)
        {
            try
            {
                await Load(stopping.Token);
                await schedule.Run(() => digest?.Generated, Generate, stopping.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task Generate()
    {
        if (!await generation.WaitAsync(0))
        {
            return;
        }

        try
        {
            generating = true;
            Publish();

            try
            {
                MailDigestResult result = await ai.Digest(query, limit, time.GetUtcNow(), preferences.Background(false), closing.Token);

                if (!string.IsNullOrWhiteSpace(result.Problem))
                {
                    error = result.Problem.Trim();
                    log.Error($"The mail digest could not be made: {error}");
                }
                else
                {
                    IReadOnlyList<MailTopic> topics = Arrange(result.Topics);
                    MailDigest made = new() { Generated = time.GetUtcNow(), Topics = topics };
                    await store.Save(made, closing.Token);
                    digest = made;
                    error = null;
                    log.Info($"Mail digest made: {topics.Count} topics from {topics.Sum(topic => topic.Messages.Count)} messages");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                error = exception.Message;
                log.Error("The mail digest could not be made", exception);
            }
            catch (OperationCanceledException) when (closing.IsCancellationRequested)
            {
            }
        }
        finally
        {
            generating = false;
            Publish();
            generation.Release();
        }
    }

    private void Publish()
        => events.Publish(new() { Kind = RemoteEventKind.MailDigestChanged, MailDigest = State() });

    private IReadOnlyList<MailTopic> Arrange(IReadOnlyList<MailTopicDraft> drafts)
    {
        HashSet<string> assigned = [];
        List<(MailTopic Topic, DateTimeOffset Latest)> topics = [];

        foreach (MailTopicDraft draft in drafts)
        {
            List<MailSummary> own = [.. draft.Messages.Where(message => !string.IsNullOrWhiteSpace(message.Id) && assigned.Add(message.Id)).OrderByDescending(message => message.Received)];

            if (own.Count > 0 && Enum.IsDefined(draft.Importance))
            {
                topics.Add((new() { Id = string.Empty, Title = Text(draft.Title, own[0].Subject), Summary = Text(draft.Summary, own[0].Preview), Importance = draft.Importance, Messages = own }, own[0].Received));
            }
            else
            {
                foreach (MailSummary message in own)
                {
                    assigned.Remove(message.Id);
                }
            }
        }

        return [.. topics.OrderBy(entry => entry.Topic.Importance).ThenByDescending(entry => entry.Latest).Select((entry, index) => entry.Topic with { Id = $"t{index + 1}" })];
    }

    private string Text(string? text, string fallback)
        => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
}
