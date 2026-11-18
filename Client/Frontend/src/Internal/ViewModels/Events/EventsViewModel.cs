namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The events tab: the calendar and the events suggested for the event feeds of the user in a day, a week and a month view, refreshed by the server once a day and by the user whenever they like.
/// </summary>
internal sealed class EventsViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventsViewModel" /> class.
    /// </summary>
    /// <param name="connection">The open connection, whose agenda events the tab follows.</param>
    /// <param name="launcher">Opens the page of a suggested event in the browser.</param>
    /// <param name="culture">The culture the dates and times are written in, or <see langword="null" /> for the current one.</param>
    /// <param name="time">The clock that says which day is today, or <see langword="null" /> for the system's.</param>
    public EventsViewModel(IServerConnection connection, ILinkLauncher launcher, CultureInfo? culture = null, TimeProvider? time = null)
    {
        this.connection = connection;
        this.launcher = launcher;
        this.culture = culture ?? CultureInfo.CurrentCulture;
        this.time = time ?? TimeProvider.System;
        anchor = DateOnly.FromDateTime(this.time.GetLocalNow().DateTime);
        DayCommand = new RelayCommand(() => SetMode(EventsMode.Day));
        WeekCommand = new RelayCommand(() => SetMode(EventsMode.Week));
        MonthCommand = new RelayCommand(() => SetMode(EventsMode.Month));
        PreviousCommand = new RelayCommand(() => Move(-1));
        NextCommand = new RelayCommand(() => Move(1));
        TodayCommand = new RelayCommand(() => Show(DateOnly.FromDateTime(this.time.GetLocalNow().DateTime), mode));
        CloseDetailCommand = new RelayCommand(() => Selected = null);
        ManageFeedsCommand = new RelayCommand(() => IsManagingFeeds = true);
        CloseFeedsCommand = new RelayCommand(() => IsManagingFeeds = false);
        AddFeedCommand = new AsyncCommand(AddFeed, () => newFeedName.Trim().Length > 0 && newFeedDescription.Trim().Length >= 3);
        connection.EventReceived += Receive;
        Rebuild();
    }

    private readonly IServerConnection connection;

    private readonly ILinkLauncher launcher;

    private readonly CultureInfo culture;

    private readonly TimeProvider time;

    private AgendaState? state;

    private DateOnly anchor;

    private EventsMode mode = EventsMode.Week;

    private AgendaItemViewModel? selected;

    private bool isManagingFeeds;

    private string newFeedName = string.Empty;

    private string newFeedDescription = string.Empty;

    /// <summary>
    /// Gets the days of the day and week view.
    /// </summary>
    public ObservableCollection<AgendaDayViewModel> Days { get; } = [];

    /// <summary>
    /// Gets the cells of the month grid, six weeks from the first day of the first week.
    /// </summary>
    public ObservableCollection<MonthCellViewModel> Cells { get; } = [];

    /// <summary>
    /// Gets the event feeds.
    /// </summary>
    public ObservableCollection<EventFeedViewModel> Feeds { get; } = [];

    /// <summary>
    /// Gets the short names of the days of the week in the order the month grid starts with.
    /// </summary>
    public ObservableCollection<string> Weekdays { get; } = [];

    /// <summary>
    /// Gets the command that shows the day view.
    /// </summary>
    public ICommand DayCommand { get; }

    /// <summary>
    /// Gets the command that shows the week view.
    /// </summary>
    public ICommand WeekCommand { get; }

    /// <summary>
    /// Gets the command that shows the month view.
    /// </summary>
    public ICommand MonthCommand { get; }

    /// <summary>
    /// Gets the command that shows the day, week or month before.
    /// </summary>
    public ICommand PreviousCommand { get; }

    /// <summary>
    /// Gets the command that shows the day, week or month after.
    /// </summary>
    public ICommand NextCommand { get; }

    /// <summary>
    /// Gets the command that shows the day, week or month of today.
    /// </summary>
    public ICommand TodayCommand { get; }

    /// <summary>
    /// Gets the command that closes the details of an entry.
    /// </summary>
    public ICommand CloseDetailCommand { get; }

    /// <summary>
    /// Gets the command that opens the list of event feeds.
    /// </summary>
    public ICommand ManageFeedsCommand { get; }

    /// <summary>
    /// Gets the command that closes the list of event feeds.
    /// </summary>
    public ICommand CloseFeedsCommand { get; }

    /// <summary>
    /// Gets the command that adds the feed that was typed.
    /// </summary>
    public AsyncCommand AddFeedCommand { get; }

    /// <summary>
    /// Gets how much of the calendar shows.
    /// </summary>
    public EventsMode Mode
    {
        get => mode;
        private set
        {
            if (Set(ref mode, value))
            {
                Raise(nameof(IsDay));
                Raise(nameof(IsWeek));
                Raise(nameof(IsMonth));
                Raise(nameof(IsDays));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the day view shows.
    /// </summary>
    public bool IsDay => mode == EventsMode.Day;

    /// <summary>
    /// Gets a value indicating whether the week view shows.
    /// </summary>
    public bool IsWeek => mode == EventsMode.Week;

    /// <summary>
    /// Gets a value indicating whether the month view shows.
    /// </summary>
    public bool IsMonth => mode == EventsMode.Month;

    /// <summary>
    /// Gets a value indicating whether a list of days shows, which is the case for the day and week views.
    /// </summary>
    public bool IsDays => mode != EventsMode.Month;

    /// <summary>
    /// Gets the day that the views are about.
    /// </summary>
    public DateOnly Anchor => anchor;

    /// <summary>
    /// Gets what the views show, as text: the day, the week or the month.
    /// </summary>
    public string Title => mode switch
    {
        EventsMode.Day => anchor.ToString("dddd, MMMM d", culture),
        EventsMode.Week => $"{anchor.ToString("MMM d", culture)} - {anchor.AddDays(6).ToString("MMM d", culture)}",
        _ => anchor.ToString("MMMM yyyy", culture),
    };

    /// <summary>
    /// Gets the entry whose details show, or <see langword="null" />.
    /// </summary>
    public AgendaItemViewModel? Selected
    {
        get => selected;
        private set
        {
            if (Set(ref selected, value))
            {
                Raise(nameof(HasSelected));
                Raise(nameof(IsRoot));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the details of an entry show.
    /// </summary>
    public bool HasSelected => selected is not null;

    /// <summary>
    /// Gets a value indicating whether no page (an event, the feeds) is open over the days.
    /// </summary>
    public bool IsRoot => selected is null && !isManagingFeeds;

    /// <summary>
    /// Gets a value indicating whether the list of event feeds shows.
    /// </summary>
    public bool IsManagingFeeds
    {
        get => isManagingFeeds;
        private set
        {
            if (Set(ref isManagingFeeds, value))
            {
                Raise(nameof(IsRoot));
            }
        }
    }

    /// <summary>
    /// Gets or sets the name of the feed that is being typed.
    /// </summary>
    public string NewFeedName
    {
        get => newFeedName;
        set
        {
            if (Set(ref newFeedName, value))
            {
                AddFeedCommand.Refresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets what the feed that is being typed searches for.
    /// </summary>
    public string NewFeedDescription
    {
        get => newFeedDescription;
        set
        {
            if (Set(ref newFeedDescription, value))
            {
                AddFeedCommand.Refresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether there are event feeds.
    /// </summary>
    public bool HasFeeds => Feeds.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the state was read and there are no event feeds, which is when the tab explains how to get suggestions.
    /// </summary>
    public bool NeedsFeeds => state is not null && Feeds.Count == 0;

    /// <summary>
    /// Gets a value indicating whether the server is looking for suggestions.
    /// </summary>
    public bool IsGenerating => state?.Generating == true;

    /// <summary>
    /// Gets why the last search for suggestions failed, or <see langword="null" />.
    /// </summary>
    public string? SuggestionsError => state?.Error;

    /// <summary>
    /// Gets a value indicating whether <see cref="SuggestionsError" /> is set.
    /// </summary>
    public bool HasSuggestionsError => state?.Error is not null;

    /// <summary>
    /// Gets why the calendar could not be read, or <see langword="null" />.
    /// </summary>
    public string? CalendarError => state?.CalendarError;

    /// <summary>
    /// Gets a value indicating whether <see cref="CalendarError" /> is set.
    /// </summary>
    public bool HasCalendarError => state?.CalendarError is not null;

    /// <summary>
    /// Gets a notice when what shows reaches beyond the months the server loaded, or <see langword="null" />.
    /// </summary>
    public string? OutsideNotice
    {
        get
        {
            if (state is null)
            {
                return null;
            }

            (DateOnly first, DateOnly last) = Range();
            DateOnly from = DateOnly.FromDateTime(Local(state.From).DateTime);
            DateOnly to = DateOnly.FromDateTime(Local(state.To).DateTime);
            return first < from || last >= to ? $"Calendar events are loaded from {from.ToString("MMM d", culture)} to {to.AddDays(-1).ToString("MMM d", culture)}." : null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="OutsideNotice" /> is set.
    /// </summary>
    public bool HasOutsideNotice => OutsideNotice is not null;

    /// <summary>
    /// Reads the state each time the tab is shown, which is cheap: the calendar is read afresh by the server, so an event the assistant just added is there, and the suggestions are the ones it has.
    /// </summary>
    /// <returns>A task that completes when the state is shown.</returns>
    public async Task Load()
    {
        AgendaState? read = null;

        if (await Attempt(async () => read = await connection.GetAgenda(false)) && read is not null)
        {
            Apply(read);
        }
    }

    /// <summary>
    /// Closes the details or the list of feeds that show.
    /// </summary>
    /// <returns><see langword="true" /> if there was something to close.</returns>
    public bool GoBack()
    {
        if (selected is not null)
        {
            Selected = null;
            return true;
        }

        if (isManagingFeeds)
        {
            IsManagingFeeds = false;
            return true;
        }

        return false;
    }

    private (DateOnly First, DateOnly Last) Range()
    {
        switch (mode)
        {
            case EventsMode.Day:
                return (anchor, anchor);
            case EventsMode.Week:
                return (anchor, anchor.AddDays(6));
            default:
                DateOnly first = new(anchor.Year, anchor.Month, 1);
                DateOnly gridStart = first.AddDays(-(((int)first.DayOfWeek - (int)culture.DateTimeFormat.FirstDayOfWeek + 7) % 7));
                return (gridStart, gridStart.AddDays(41));
        }
    }

    private void SetMode(EventsMode next)
    {
        Show(anchor, next);
    }

    private void Show(DateOnly day, EventsMode next)
    {
        anchor = day;
        Mode = next;
        Rebuild();
    }

    private void Move(int direction)
    {
        DateOnly next = mode switch
        {
            EventsMode.Day => anchor.AddDays(direction),
            EventsMode.Week => anchor.AddDays(7 * direction),
            _ => anchor.AddMonths(direction),
        };

        Show(next, mode);
    }

    private async Task AddFeed()
    {
        string name = newFeedName.Trim();
        string description = newFeedDescription.Trim();
        AgendaState? read = null;

        if (await Attempt(async () => read = await connection.AddEventFeed(name, description)) && read is not null)
        {
            NewFeedName = string.Empty;
            NewFeedDescription = string.Empty;
            IsManagingFeeds = false;
            Apply(read);
        }
    }

    private void RemoveFeed(EventFeedViewModel feed)
    {
        _ = RemoveFeedAsync(feed);
    }

    private async Task RemoveFeedAsync(EventFeedViewModel feed)
    {
        AgendaState? read = null;

        if (await Attempt(async () => read = await connection.RemoveEventFeed(feed.Feed.Id)) && read is not null)
        {
            Apply(read);
        }
    }

    private void Receive(RemoteEvent remoteEvent)
    {
        if (remoteEvent is { Kind: RemoteEventKind.AgendaChanged, Agenda: { } changed })
        {
            Apply(changed);
        }
    }

    private void Apply(AgendaState current)
    {
        state = current;
        Feeds.Clear();

        foreach (EventFeed feed in current.Feeds)
        {
            Feeds.Add(new EventFeedViewModel(feed, RemoveFeed));
        }

        Raise(nameof(HasFeeds));
        Raise(nameof(NeedsFeeds));
        Raise(nameof(IsGenerating));
        Raise(nameof(SuggestionsError));
        Raise(nameof(HasSuggestionsError));
        Raise(nameof(CalendarError));
        Raise(nameof(HasCalendarError));
        Rebuild();
    }

    private void Rebuild()
    {
        Raise(nameof(Anchor));
        Raise(nameof(Title));
        Raise(nameof(OutsideNotice));
        Raise(nameof(HasOutsideNotice));
        Days.Clear();
        Cells.Clear();
        Weekdays.Clear();
        DateOnly today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        if (mode == EventsMode.Month)
        {
            (DateOnly first, _) = Range();
            DayOfWeek start = culture.DateTimeFormat.FirstDayOfWeek;

            for (int index = 0; index < 7; index++)
            {
                Weekdays.Add(culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(((int)start + index) % 7)));
            }

            for (int index = 0; index < 42; index++)
            {
                DateOnly day = first.AddDays(index);
                (int events, int suggestions) = Counts(day);
                Cells.Add(new MonthCellViewModel(day, day.Month == anchor.Month, day == today, events, suggestions, Pick));
            }

            return;
        }

        DateOnly begin = anchor;
        int count = mode == EventsMode.Day ? 1 : 7;

        for (int index = 0; index < count; index++)
        {
            DateOnly day = begin.AddDays(index);
            Days.Add(new AgendaDayViewModel(day, day.ToString("dddd, MMMM d", culture), day == today, mode == EventsMode.Week, ItemsOn(day)));
        }
    }

    private void Pick(DateOnly day)
    {
        Show(day, EventsMode.Day);
    }

    private (int Events, int Suggestions) Counts(DateOnly day)
    {
        IReadOnlyList<AgendaItemViewModel> items = ItemsOn(day);
        return (items.Count(item => !item.IsSuggestion), items.Count(item => item.IsSuggestion));
    }

    private IReadOnlyList<AgendaItemViewModel> ItemsOn(DateOnly day)
    {
        if (state is null)
        {
            return [];
        }

        List<AgendaItemViewModel> items = [];

        foreach (CalendarEvent calendarEvent in state.Events)
        {
            if (Covers(calendarEvent, day))
            {
                items.Add(Item(calendarEvent));
            }
        }

        foreach (SuggestedEvent suggestion in state.Suggestions)
        {
            if (DateOnly.FromDateTime(Local(suggestion.Start).DateTime) == day)
            {
                items.Add(Item(suggestion));
            }
        }

        return [.. items.OrderByDescending(item => item.IsAllDay).ThenBy(item => item.Start)];
    }

    private DateTimeOffset Local(DateTimeOffset moment)
        => TimeZoneInfo.ConvertTime(moment, time.LocalTimeZone);

    private bool Covers(CalendarEvent calendarEvent, DateOnly day)
    {
        if (calendarEvent.IsAllDay)
        {
            DateOnly first = DateOnly.FromDateTime(calendarEvent.Start.UtcDateTime);
            DateOnly after = DateOnly.FromDateTime(calendarEvent.End.UtcDateTime);
            return day >= first && day < (after > first ? after : first.AddDays(1));
        }

        DateTime start = Local(calendarEvent.Start).DateTime;
        DateTime end = Local(calendarEvent.End).DateTime;
        DateOnly last = end > start ? DateOnly.FromDateTime(end.AddTicks(-1)) : DateOnly.FromDateTime(start);
        return day >= DateOnly.FromDateTime(start) && day <= last;
    }

    private AgendaItemViewModel Item(CalendarEvent calendarEvent)
    {
        DateTime start = calendarEvent.IsAllDay ? calendarEvent.Start.UtcDateTime.Date : Local(calendarEvent.Start).DateTime;
        string when = calendarEvent.IsAllDay ? "All day" : $"{Local(calendarEvent.Start).ToString("t", culture)} - {Local(calendarEvent.End).ToString("t", culture)}";

        return new(calendarEvent.Title, when, start, calendarEvent.IsAllDay, Open)
        {
            Place = string.IsNullOrWhiteSpace(calendarEvent.Location) ? null : calendarEvent.Location,
            Details = string.IsNullOrWhiteSpace(calendarEvent.Notes) ? null : calendarEvent.Notes,
            Attendees = calendarEvent.Attendees is { Count: > 0 } people ? string.Join(", ", people) : null,
        };
    }

    private AgendaItemViewModel Item(SuggestedEvent suggestion)
    {
        DateTimeOffset start = Local(suggestion.Start);
        string when = suggestion.End is { } end ? $"{start.ToString("t", culture)} - {Local(end).ToString("t", culture)}" : start.ToString("t", culture);
        string? feed = state?.Feeds.FirstOrDefault(candidate => candidate.Id == suggestion.FeedId)?.Name;

        return new(suggestion.Title, when, start.DateTime, false, Open)
        {
            Place = suggestion.Venue,
            Details = suggestion.Description,
            IsSuggestion = true,
            FeedName = feed,
            Category = suggestion.Category,
            Url = suggestion.Url,
            OpenUrlCommand = new AsyncCommand(() => launcher.Open(suggestion.Url)),
            Reaction = new ReactionViewModel(connection, ReactionTarget.Event, suggestion.ReactionKey, suggestion.Title, suggestion.Category ?? feed, suggestion.Reaction),
        };
    }

    private void Open(AgendaItemViewModel item)
    {
        Selected = item;
    }
}
