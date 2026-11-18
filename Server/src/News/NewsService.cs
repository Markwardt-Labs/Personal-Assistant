namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Keeps the news tab: the feeds the user follows and the summary entries the AI finds for them once a day, whenever a client asks, and for a feed when it is added.
/// </summary>
internal interface INewsService
{
    /// <summary>
    /// Reads the feeds and their entries.
    /// </summary>
    /// <param name="refresh">Whether to search again now even when there are entries; without it a search only starts when nothing was searched yet and there are feeds.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The state, which is also pushed to the clients as an event when it changes.</returns>
    Task<NewsState> Get(bool refresh, CancellationToken cancellation = default);

    /// <summary>
    /// Adds a news feed and searches for its entries.
    /// </summary>
    /// <param name="name">The name of the feed.</param>
    /// <param name="description">What type of news to search for.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>The state with the new feed.</returns>
    /// <exception cref="RequestException">The name or the description is too short or too long, the feed exists, or there are too many feeds.</exception>
    Task<NewsState> AddFeed(string? name, string? description, CancellationToken cancellation = default);

    /// <summary>
    /// Removes a news feed and its entries.
    /// </summary>
    /// <param name="id">The identifier of the feed.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>The state without the feed.</returns>
    /// <exception cref="RequestException">There is no such feed.</exception>
    Task<NewsState> RemoveFeed(string? id, CancellationToken cancellation = default);

    /// <summary>
    /// Starts the daily search.
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
internal sealed class NewsService(INewsAi ai, IJsonFile<StoredNews> store, IJsonFile<StoredNewsFeeds> feedStore, IPreferenceService preferences, IDailySchedule schedule, IEventHub events, ILog log, TimeProvider time) : INewsService
{
    private readonly int maxFeeds = 10;

    private readonly int perFeed = 8;

    private readonly TimeSpan age = TimeSpan.FromDays(7);

    private readonly SemaphoreSlim loading = new(1, 1);

    private readonly SemaphoreSlim generation = new(1, 1);

    private readonly CancellationTokenSource closing = new();

    private readonly List<Task> running = [];

    private readonly List<NewsFeed> feeds = [];

    private StoredNews? news;

    private bool loaded;

    private bool generating;

    private string? error;

    /// <inheritdoc />
    public async Task<NewsState> Get(bool refresh, CancellationToken cancellation = default)
    {
        await Load(cancellation);

        if (refresh || (news is null && Feeds().Length > 0))
        {
            Track(Generate(null));
        }

        return State();
    }

    /// <inheritdoc />
    public async Task<NewsState> AddFeed(string? name, string? description, CancellationToken cancellation = default)
    {
        string title = (name ?? string.Empty).Trim();
        string text = (description ?? string.Empty).Trim();

        if (title.Length is < 1 or > 40)
        {
            throw new RequestException("Give the feed a name of 1 to 40 characters.");
        }

        if (text.Length is < 3 or > 300)
        {
            throw new RequestException("Describe what news to search for in 3 to 300 characters.");
        }

        await Load(cancellation);
        NewsFeed added;

        lock (feeds)
        {
            if (feeds.Count >= maxFeeds)
            {
                throw new RequestException($"There can be at most {maxFeeds} news feeds. Remove one first.");
            }

            if (feeds.Any(feed => string.Equals(feed.Name, title, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RequestException("A news feed with that name already exists.");
            }

            added = new() { Id = Guid.NewGuid().ToString("N"), Name = title, Description = text };
            feeds.Add(added);
        }

        await SaveFeeds(cancellation);
        Track(Generate([added.Id]));
        return State();
    }

    /// <inheritdoc />
    public async Task<NewsState> RemoveFeed(string? id, CancellationToken cancellation = default)
    {
        await Load(cancellation);

        lock (feeds)
        {
            if (feeds.RemoveAll(feed => feed.Id == id) == 0)
            {
                throw new RequestException("There is no such news feed.");
            }
        }

        await SaveFeeds(cancellation);
        Publish();
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

    private NewsFeed[] Feeds()
    {
        lock (feeds)
        {
            return [.. feeds];
        }
    }

    private NewsState State()
    {
        NewsFeed[] current = Feeds();

        return new()
        {
            Feeds = current,
            Items = [.. (news?.Items ?? []).Where(item => current.Any(feed => feed.Id == item.FeedId)).Select(item => item with { Reaction = preferences.Of(ReactionTarget.News, item.ReactionKey) })],
            Generated = news?.Generated,
            Generating = generating,
            Error = error,
        };
    }

    private async Task Load(CancellationToken cancellation = default)
    {
        await loading.WaitAsync(cancellation);

        try
        {
            if (!loaded)
            {
                news = await store.Load(cancellation);
                await preferences.Ready(cancellation);
                IReadOnlyList<NewsFeed> stored = (await feedStore.Load(cancellation))?.Feeds ?? [];

                lock (feeds)
                {
                    feeds.AddRange(stored);
                }

                loaded = true;
            }
        }
        finally
        {
            loading.Release();
        }
    }

    private async Task SaveFeeds(CancellationToken cancellation = default)
        => await feedStore.Save(new() { Feeds = Feeds() }, cancellation);

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
                await schedule.Run(() => news?.Generated, () => Generate(null), stopping.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task Generate(IReadOnlyCollection<string>? only)
    {
        await generation.WaitAsync(closing.Token);

        try
        {
            generating = true;
            Publish();

            try
            {
                NewsFeed[] current = Feeds();
                NewsFeed[] searched = [.. current.Where(feed => only is null || only.Contains(feed.Id))];
                DateTimeOffset now = time.GetUtcNow();
                IReadOnlyList<NewsItemDraft> drafts = searched.Length == 0 ? [] : await ai.Search(searched, now - age, preferences.Background(false), closing.Token);
                IEnumerable<NewsItem> kept = (news?.Items ?? []).Where(item => !searched.Any(feed => feed.Id == item.FeedId));
                StoredNews made = new() { Generated = now, Items = Arrange(kept, drafts, searched, current) };
                await store.Save(made, closing.Token);
                news = made;
                error = null;
                log.Info($"News searched: {made.Items.Count} entries for {searched.Length} feeds");
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                error = exception.Message;
                log.Error("The news could not be searched", exception);
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
        => events.Publish(new() { Kind = RemoteEventKind.NewsChanged, News = State() });

    private IReadOnlyList<NewsItem> Arrange(IEnumerable<NewsItem> kept, IReadOnlyList<NewsItemDraft> drafts, IReadOnlyList<NewsFeed> searched, IReadOnlyList<NewsFeed> current)
    {
        HashSet<string> urls = [.. kept.Select(item => item.Url)];
        List<NewsItem> items = [.. kept];

        foreach (IGrouping<string, NewsItemDraft> group in drafts.Where(draft => searched.Any(feed => feed.Id == draft.FeedId)).GroupBy(draft => draft.FeedId))
        {
            foreach (NewsItemDraft draft in group
                .Where(draft => !string.IsNullOrWhiteSpace(draft.Title) && !string.IsNullOrWhiteSpace(draft.Summary) && Uri.TryCreate(draft.Url, UriKind.Absolute, out Uri? url) && url.Scheme is "http" or "https")
                .Where(draft => urls.Add(draft.Url))
                .OrderByDescending(draft => draft.Published ?? DateTimeOffset.MinValue)
                .Take(perFeed))
            {
                items.Add(new() { Id = string.Empty, FeedId = draft.FeedId, Title = draft.Title.Trim(), Summary = draft.Summary.Trim(), Source = string.IsNullOrWhiteSpace(draft.Source) ? null : draft.Source.Trim(), Url = draft.Url, Published = draft.Published });
            }
        }

        return
        [
            .. current
                .SelectMany(feed => items.Where(item => item.FeedId == feed.Id).OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue))
                .Select((item, index) => item with { Id = $"n{index + 1}" }),
        ];
    }
}
