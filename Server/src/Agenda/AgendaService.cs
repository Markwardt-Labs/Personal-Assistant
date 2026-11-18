namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Keeps what the events tab shows: the calendar, which the AI reads with the user's Google Calendar tools once a day, whenever a client asks for a refresh and after the assistant changed it, the event feeds the user follows, and the events suggested for the local area that relate to those feeds, which the AI looks for once a day and whenever a client asks or the feeds change.
/// </summary>
internal interface IAgendaService
{
    /// <summary>
    /// Reads what is known of the calendar and the suggestions.
    /// </summary>
    /// <param name="refresh">Whether to read the calendar and look for new suggestions for every feed now even when there are some; without it they are only done when there is nothing yet.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The state, which is also pushed to the clients as an event when the suggestions change.</returns>
    Task<AgendaState> Get(bool refresh, CancellationToken cancellation = default);

    /// <summary>
    /// Adds an event feed and starts a search for events that relate to it, which searches that feed alone and keeps the suggestions of the others.
    /// </summary>
    /// <param name="name">The name of the feed.</param>
    /// <param name="description">What to search for.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>The state with the new feed.</returns>
    /// <exception cref="RequestException">The name or the description is too short or too long, the name exists, or there are too many feeds.</exception>
    Task<AgendaState> AddFeed(string? name, string? description, CancellationToken cancellation = default);

    /// <summary>
    /// Removes an event feed and the suggestions that came from it.
    /// </summary>
    /// <param name="id">The identifier of the feed.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>The state without the feed.</returns>
    /// <exception cref="RequestException">There is no such feed.</exception>
    Task<AgendaState> RemoveFeed(string? id, CancellationToken cancellation = default);

    /// <summary>
    /// Starts reading the calendar again without waiting for it, which is what follows a change the assistant made to it.
    /// </summary>
    void RefreshCalendar();

    /// <summary>
    /// Starts the daily read of the calendar and search for suggestions.
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
internal sealed class AgendaService(ICalendarAi calendar, IJsonFile<StoredCalendar> calendarStore, IEventSuggestionsAi ai, IJsonFile<StoredSuggestions> store, IJsonFile<StoredFeeds> feedStore, IPreferenceService preferences, IDailySchedule schedule, IEventHub events, ILog log, TimeProvider time, ISettingsService settings) : IAgendaService
{
    private readonly int perFeed = 8;

    private readonly int maxFeeds = 10;

    private readonly SemaphoreSlim loading = new(1, 1);

    private readonly SemaphoreSlim generation = new(1, 1);

    private readonly CancellationTokenSource closing = new();

    private readonly List<Task> running = [];

    private StoredSuggestions? suggestions;

    private List<EventFeed> feeds = [];

    private IReadOnlyList<CalendarEvent> latest = [];

    private DateTimeOffset? calendarRead;

    private bool loaded;

    private bool generating;

    private string? error;

    private string? calendarError;

    /// <inheritdoc />
    public async Task<AgendaState> Get(bool refresh, CancellationToken cancellation = default)
    {
        await Load(cancellation);
        bool readCalendar = refresh || (calendarRead is null && calendarError is null);
        bool search = refresh || suggestions is null;

        if (readCalendar || search)
        {
            Track(Generate(readCalendar, search, null));
        }

        return State();
    }

    /// <inheritdoc />
    public async Task<AgendaState> AddFeed(string? name, string? description, CancellationToken cancellation = default)
    {
        string title = (name ?? string.Empty).Trim();
        string text = (description ?? string.Empty).Trim();

        if (title.Length is < 1 or > 40)
        {
            throw new RequestException("Give the feed a name of 1 to 40 characters.");
        }

        if (text.Length is < 3 or > 300)
        {
            throw new RequestException("Describe what to look for in 3 to 300 characters.");
        }

        await Load(cancellation);
        EventFeed added;

        lock (feeds)
        {
            if (feeds.Count >= maxFeeds)
            {
                throw new RequestException($"There can be at most {maxFeeds} event feeds. Remove one first.");
            }

            if (feeds.Any(feed => string.Equals(feed.Name, title, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RequestException("An event feed with that name already exists.");
            }

            added = new() { Id = Guid.NewGuid().ToString("N"), Name = title, Description = text };
            feeds.Add(added);
        }

        await SaveFeeds(cancellation);
        Track(Generate(calendarRead is null && calendarError is null, true, [added.Id]));
        return State();
    }

    /// <inheritdoc />
    public async Task<AgendaState> RemoveFeed(string? id, CancellationToken cancellation = default)
    {
        await Load(cancellation);

        lock (feeds)
        {
            if (feeds.RemoveAll(feed => feed.Id == id) == 0)
            {
                throw new RequestException("There is no such event feed.");
            }
        }

        await SaveFeeds(cancellation);
        Publish();
        return State();
    }

    /// <inheritdoc />
    public void RefreshCalendar()
        => Track(Generate(true, false, null));

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

    private (DateTimeOffset From, DateTimeOffset To) Window()
    {
        DateTimeOffset now = time.GetLocalNow();
        DateTime first = new(now.Year, now.Month, 1);
        return (At(first.AddMonths(-1)), At(first.AddMonths(2)));
    }

    private DateTimeOffset At(DateTime local)
        => new(local, time.LocalTimeZone.GetUtcOffset(local));

    private AgendaState State()
    {
        (DateTimeOffset from, DateTimeOffset to) = Window();
        DateTimeOffset now = time.GetUtcNow();
        EventFeed[] current;

        lock (feeds)
        {
            current = [.. feeds];
        }

        return new()
        {
            From = from,
            To = to,
            Events = latest,
            Feeds = current,
            Suggestions = [.. (suggestions?.Events ?? []).Where(item => (item.End ?? item.Start.AddHours(3)) >= now && current.Any(feed => feed.Id == item.FeedId)).Select(item => item with { Reaction = preferences.Of(ReactionTarget.Event, item.ReactionKey) })],
            SuggestionsGenerated = suggestions?.Generated,
            Generating = generating,
            Error = error,
            CalendarError = calendarError,
        };
    }

    private async Task Load(CancellationToken cancellation = default)
    {
        await loading.WaitAsync(cancellation);

        try
        {
            if (!loaded)
            {
                suggestions = await store.Load(cancellation);

                if (await calendarStore.Load(cancellation) is { } stored)
                {
                    latest = stored.Events;
                    calendarRead = stored.Read;
                }

                feeds = [.. (await feedStore.Load(cancellation))?.Feeds ?? []];
                await preferences.Ready(cancellation);
                loaded = true;
            }
        }
        finally
        {
            loading.Release();
        }
    }

    private async Task SaveFeeds(CancellationToken cancellation = default)
    {
        EventFeed[] current;

        lock (feeds)
        {
            current = [.. feeds];
        }

        await feedStore.Save(new() { Feeds = current }, cancellation);
    }

    private async Task ReadCalendar(CancellationToken cancellation = default)
    {
        (DateTimeOffset from, DateTimeOffset to) = Window();

        try
        {
            CalendarResult result = await calendar.Read(from, to, cancellation);

            if (!string.IsNullOrWhiteSpace(result.Problem))
            {
                calendarError = result.Problem.Trim();
                log.Error($"The calendar could not be read: {calendarError}");
                return;
            }

            IReadOnlyList<CalendarEvent> read = [.. result.Events.Where(item => !string.IsNullOrWhiteSpace(item.Id) && item.End >= item.Start && item.End > from && item.Start < to).GroupBy(item => item.Id).Select(group => group.First()).OrderBy(item => item.Start)];
            DateTimeOffset now = time.GetUtcNow();
            await calendarStore.Save(new() { Read = now, Events = read }, cancellation);
            latest = read;
            calendarRead = now;
            calendarError = null;
            log.Info($"Calendar read: {read.Count} events");
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            calendarError = exception.Message;
            log.Error("The calendar could not be read", exception);
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
                await schedule.Run(() => suggestions?.Generated, () => Generate(true, true, null), stopping.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task Generate(bool readCalendar, bool search, IReadOnlyCollection<string>? only)
    {
        await generation.WaitAsync(closing.Token);

        try
        {
            generating = true;
            Publish();

            try
            {
                if (readCalendar)
                {
                    await ReadCalendar(closing.Token);
                }

                if (search)
                {
                    await Suggest(only);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                error = exception.Message;
                log.Error("The suggested events could not be made", exception);
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

    private async Task Suggest(IReadOnlyCollection<string>? only)
    {
        EventFeed[] current;

        lock (feeds)
        {
            current = [.. feeds];
        }

        await settings.Ready(closing.Token);
        EventFeed[] searched = [.. current.Where(feed => only is null || only.Contains(feed.Id))];

        if (searched.Length > 0 && string.IsNullOrWhiteSpace(settings.Location))
        {
            throw new InvalidOperationException("Events cannot be suggested yet: add your location in the settings of the app, such as Austin, Texas.");
        }

        (DateTimeOffset from, DateTimeOffset to) = Window();
        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset start = now > from ? now : from;
        DateTimeOffset end = start.AddDays(30);
        IReadOnlyList<BusyTime> busy = [.. latest.Where(item => item.End > start && item.Start < end).Select(item => new BusyTime { Start = item.Start, End = item.End })];
        IReadOnlyList<SuggestedEventDraft> drafts = searched.Length == 0 ? [] : await ai.Suggest(settings.Location!, searched, busy, start, end, preferences.Background(false), closing.Token);
        IEnumerable<SuggestedEvent> kept = (suggestions?.Events ?? []).Where(item => !searched.Any(feed => feed.Id == item.FeedId));
        StoredSuggestions made = new() { Generated = now, Events = Arrange(kept, drafts, searched, start, end) };
        await store.Save(made, closing.Token);
        suggestions = made;
        error = null;
        log.Info($"Suggested events made: {made.Events.Count} events");
    }

    private void Publish()
        => events.Publish(new() { Kind = RemoteEventKind.AgendaChanged, Agenda = State() });

    private IReadOnlyList<SuggestedEvent> Arrange(IEnumerable<SuggestedEvent> kept, IReadOnlyList<SuggestedEventDraft> drafts, IReadOnlyList<EventFeed> searched, DateTimeOffset from, DateTimeOffset to)
    {
        List<SuggestedEvent> items = [.. kept];
        HashSet<string> seen = [.. items.Select(item => $"{item.Title.ToUpperInvariant()}|{item.Start.UtcDateTime:O}")];

        foreach (IGrouping<string, SuggestedEventDraft> group in drafts.Where(draft => searched.Any(feed => feed.Id == draft.FeedId)).GroupBy(draft => draft.FeedId))
        {
            foreach (SuggestedEventDraft draft in group
                .Where(draft => !string.IsNullOrWhiteSpace(draft.Title) && draft.Start >= from.AddHours(-12) && draft.Start <= to.AddDays(1) && Uri.TryCreate(draft.Url, UriKind.Absolute, out Uri? url) && url.Scheme is "http" or "https")
                .Where(draft => seen.Add($"{draft.Title.Trim().ToUpperInvariant()}|{draft.Start.UtcDateTime:O}"))
                .OrderBy(draft => draft.Start)
                .Take(perFeed))
            {
                items.Add(new()
                {
                    Id = string.Empty,
                    FeedId = draft.FeedId,
                    Title = draft.Title.Trim(),
                    Start = draft.Start,
                    End = draft.End is { } end && end > draft.Start ? end : null,
                    Venue = string.IsNullOrWhiteSpace(draft.Venue) ? null : draft.Venue.Trim(),
                    Description = draft.Description.Trim(),
                    Url = draft.Url,
                    Category = string.IsNullOrWhiteSpace(draft.Category) ? null : draft.Category.Trim(),
                });
            }
        }

        return [.. items.OrderBy(item => item.Start).Select((item, index) => item with { Id = $"s{index + 1}" })];
    }
}
