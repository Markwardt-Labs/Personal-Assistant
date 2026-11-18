namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class EventsViewModelTests
{
    private readonly Mock<IServerConnection> connection = new();

    private readonly Mock<ILinkLauncher> launcher = new();

    private readonly CultureInfo culture = CultureInfo.GetCultureInfo("en-US");

    private readonly Eventually eventually = new();

    private readonly DateTimeOffset now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private EventsViewModel Create()
        => new(connection.Object, launcher.Object, culture, new FixedTime(now));

    private CalendarEvent Event(string id, string title, DateTimeOffset start, int hours = 1, bool allDay = false)
        => new() { Id = id, Title = title, Start = start, End = allDay ? start.AddDays(1) : start.AddHours(hours), IsAllDay = allDay, Location = "Office", Attendees = ["sam@example.com"] };

    private SuggestedEvent Suggestion(string id, string title, DateTimeOffset start, string feedId = "f1")
        => new() { Id = id, FeedId = feedId, Title = title, Start = start, Description = $"About {title}.", Url = $"https://example.com/{id}", Venue = "The Club", Category = "music" };

    private AgendaState State(IReadOnlyList<CalendarEvent>? events = null, IReadOnlyList<SuggestedEvent>? suggestions = null, IReadOnlyList<EventFeed>? feeds = null, bool generating = false, string? error = null, string? calendarError = null)
        => new() { From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), To = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), Events = events ?? [], Suggestions = suggestions ?? [], Feeds = feeds ?? [new() { Id = "f1", Name = "Jazz", Description = "live jazz" }], Generating = generating, Error = error, CalendarError = calendarError, SuggestionsGenerated = now };

    private void Push(AgendaState state)
        => connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.AgendaChanged, Agenda = state });

    [Fact]
    public void StartsOnTheSevenDaysFromToday()
    {
        EventsViewModel events = Create();

        Assert.True(events.IsWeek);
        Assert.Equal(7, events.Days.Count);
        Assert.Equal(new DateOnly(2026, 11, 18), events.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 11, 24), events.Days[6].Date);
        Assert.Equal("Nov 18 - Nov 24", events.Title);
        Assert.True(events.Days[0].IsToday);
        Assert.All(events.Days, day => Assert.True(day.ShowLabel));
    }

    [Fact]
    public void ShowsOneDayInTheDayView()
    {
        EventsViewModel events = Create();

        events.DayCommand.Execute(null);

        Assert.True(events.IsDay);
        Assert.Equal(new DateOnly(2026, 11, 18), Assert.Single(events.Days).Date);
        Assert.Equal("Wednesday, November 18", events.Title);
        Assert.False(events.Days[0].ShowLabel);
    }

    [Fact]
    public void ShowsSixWeeksOfCellsInTheMonthViewWithTheWeekdaysOfTheCulture()
    {
        EventsViewModel events = Create();

        events.MonthCommand.Execute(null);

        Assert.True(events.IsMonth);
        Assert.Equal(42, events.Cells.Count);
        Assert.Equal(["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"], events.Weekdays);
        Assert.Equal(new DateOnly(2026, 11, 1), events.Cells[0].Date);
        Assert.True(events.Cells[0].IsInMonth);
        Assert.False(events.Cells[41].IsInMonth);
        Assert.Equal("November 2026", events.Title);
        Assert.True(events.Cells.Single(cell => cell.IsToday).Date == new DateOnly(2026, 11, 18));
    }

    [Fact]
    public void MovesByTheDayTheWeekOrTheMonthAndBackToToday()
    {
        EventsViewModel events = Create();

        events.NextCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 11, 25), events.Anchor);
        events.PreviousCommand.Execute(null);
        events.PreviousCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 11, 11), events.Anchor);

        events.MonthCommand.Execute(null);
        events.NextCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 12, 11), events.Anchor);

        events.DayCommand.Execute(null);
        events.NextCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 12, 12), events.Anchor);

        events.TodayCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 11, 18), events.Anchor);
        Assert.True(events.IsDay);
    }

    [Fact]
    public async Task ShowsCalendarEventsAndSuggestionsOnTheirDaysWithTheAllDayOnesFirst()
    {
        connection.Setup(item => item.GetAgenda(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(
            [Event("e1", "Dentist", new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero)), Event("e2", "Holiday", new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero), allDay: true), Event("e3", "Standup", new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero))],
            [Suggestion("s1", "Jazz Night", new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero)), Suggestion("s2", "Hike", new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero))]));
        EventsViewModel events = Create();

        await events.Load();

        AgendaDayViewModel today = events.Days.Single(day => day.IsToday);
        Assert.Equal(["Holiday", "Standup", "Dentist", "Jazz Night"], today.Items.Select(item => item.Title));
        Assert.Equal(["All day", "9:00 AM - 10:00 AM", "3:00 PM - 4:00 PM", "8:00 PM"], today.Items.Select(item => item.TimeText.Replace('\u202f', ' ')));
        Assert.Equal(["Hike"], events.Days.Single(day => day.Date == new DateOnly(2026, 11, 19)).Items.Select(item => item.Title));
        Assert.True(events.Days.Single(day => day.Date == new DateOnly(2026, 11, 20)).IsEmpty);
    }

    [Fact]
    public async Task ShowsAMultiDayEventAndAnAllDayEventOnEveryDayItCovers()
    {
        connection.Setup(item => item.GetAgenda(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(
            [Event("e1", "Conference", new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero), hours: 30), new() { Id = "e2", Title = "Trip", Start = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero), End = new(2026, 11, 23, 0, 0, 0, TimeSpan.Zero), IsAllDay = true }]));
        EventsViewModel events = Create();

        await events.Load();

        Assert.Equal([new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 19)], events.Days.Where(day => day.Items.Any(item => item.Title == "Conference")).Select(day => day.Date));
        Assert.Equal([new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 22)], events.Days.Where(day => day.Items.Any(item => item.Title == "Trip")).Select(day => day.Date));
    }

    [Fact]
    public async Task CountsEventsAndSuggestionsOnTheCellsOfTheMonthAndOpensADayWhenTapped()
    {
        connection.Setup(item => item.GetAgenda(false, It.IsAny<CancellationToken>())).ReturnsAsync(State([Event("e1", "Dentist", new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero)), Event("e2", "Standup", new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero))], [Suggestion("s1", "Jazz Night", new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero))]));
        EventsViewModel events = Create();
        await events.Load();
        events.MonthCommand.Execute(null);

        MonthCellViewModel cell = events.Cells.Single(candidate => candidate.Date == new DateOnly(2026, 11, 18));

        Assert.Equal((2, 1, "2", "1"), (cell.Events, cell.Suggestions, cell.EventsText, cell.SuggestionsText));
        Assert.Equal(string.Empty, events.Cells.Single(candidate => candidate.Date == new DateOnly(2026, 11, 19)).EventsText);

        cell.SelectCommand.Execute(null);

        Assert.True(events.IsDay);
        Assert.Equal(new DateOnly(2026, 11, 18), events.Anchor);
        Assert.Equal(3, Assert.Single(events.Days).Items.Count);
    }

    [Fact]
    public async Task OpensTheDetailsOfAnEntryAndStepsBack()
    {
        connection.Setup(item => item.GetAgenda(false, It.IsAny<CancellationToken>())).ReturnsAsync(State([Event("e1", "Dentist", new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero))], [Suggestion("s1", "Jazz Night", new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero))]));
        EventsViewModel events = Create();
        await events.Load();
        AgendaItemViewModel dentist = events.Days.Single(day => day.IsToday).Items[0];
        AgendaItemViewModel jazz = events.Days.Single(day => day.IsToday).Items[1];

        dentist.OpenCommand.Execute(null);

        Assert.Same(dentist, events.Selected);
        Assert.Equal(("Office", "sam@example.com", false, false), (dentist.Place, dentist.Attendees, dentist.IsSuggestion, dentist.HasReaction));
        Assert.True(events.GoBack());
        Assert.Null(events.Selected);

        jazz.OpenCommand.Execute(null);

        Assert.Equal(("The Club", "Jazz", "music", true, true, "https://example.com/s1"), (jazz.Place, jazz.FeedName, jazz.Category, jazz.IsSuggestion, jazz.HasReaction, jazz.Url));
    }

    [Fact]
    public async Task OpensThePageOfASuggestionInTheBrowser()
    {
        connection.Setup(item => item.GetAgenda(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(suggestions: [Suggestion("s1", "Jazz Night", new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero))]));
        EventsViewModel events = Create();
        await events.Load();

        events.Days.Single(day => day.IsToday).Items[0].OpenUrlCommand!.Execute(null);

        await eventually.Be(() => launcher.Invocations.Count > 0);
        launcher.Verify(item => item.Open("https://example.com/s1"), Times.Once);
    }

    [Fact]
    public async Task ExplainsHowToGetSuggestionsWhenThereAreNoFeeds()
    {
        connection.Setup(item => item.GetAgenda(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(feeds: []));
        EventsViewModel events = Create();
        Assert.False(events.NeedsFeeds);

        await events.Load();

        Assert.True(events.NeedsFeeds);
        Assert.False(events.HasFeeds);
    }

    [Fact]
    public async Task AddsAndRemovesEventFeedsWithANameAndClosesTheList()
    {
        AgendaState withFeed = State(feeds: [new() { Id = "f2", Name = "Outdoors", Description = "free outdoor activities" }]);
        connection.Setup(item => item.AddEventFeed("Outdoors", "free outdoor activities", It.IsAny<CancellationToken>())).ReturnsAsync(withFeed);
        connection.Setup(item => item.RemoveEventFeed("f2", It.IsAny<CancellationToken>())).ReturnsAsync(State(feeds: []));
        EventsViewModel events = Create();
        events.ManageFeedsCommand.Execute(null);
        Assert.False(events.AddFeedCommand.CanExecute(null));
        (events.NewFeedName, events.NewFeedDescription) = ("  Outdoors  ", "  free outdoor activities  ");
        Assert.True(events.AddFeedCommand.CanExecute(null));

        events.AddFeedCommand.Execute(null);
        await eventually.Be(() => events.Feeds.Count == 1);

        Assert.Equal((string.Empty, string.Empty, false), (events.NewFeedName, events.NewFeedDescription, events.IsManagingFeeds));
        Assert.Equal(("Outdoors", "free outdoor activities"), (events.Feeds[0].Name, events.Feeds[0].Description));

        events.Feeds[0].RemoveCommand.Execute(null);
        await eventually.Be(() => events.Feeds.Count == 0);

        connection.Verify(item => item.RemoveEventFeed("f2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NeedsBothANameAndADescriptionToAddAFeed()
    {
        EventsViewModel events = Create();

        events.NewFeedName = "Jazz";
        Assert.False(events.AddFeedCommand.CanExecute(null));
        events.NewFeedDescription = "ab";
        Assert.False(events.AddFeedCommand.CanExecute(null));
        events.NewFeedDescription = "live jazz";
        Assert.True(events.AddFeedCommand.CanExecute(null));
        events.NewFeedName = " ";
        Assert.False(events.AddFeedCommand.CanExecute(null));
    }

    [Fact]
    public async Task KeepsTheFormAndShowsTheReasonWhenAFeedIsRefused()
    {
        connection.Setup(item => item.AddEventFeed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("An event feed with that name already exists."));
        EventsViewModel events = Create();
        (events.NewFeedName, events.NewFeedDescription) = ("Jazz", "live jazz");

        events.AddFeedCommand.Execute(null);
        await eventually.Be(() => events.HasError);

        Assert.Equal(("Jazz", "live jazz", "An event feed with that name already exists."), (events.NewFeedName, events.NewFeedDescription, events.Error));
    }

    [Fact]
    public void OpensAndClosesTheListOfFeeds()
    {
        EventsViewModel events = Create();

        events.ManageFeedsCommand.Execute(null);
        Assert.True(events.IsManagingFeeds);
        Assert.True(events.GoBack());
        Assert.False(events.IsManagingFeeds);
        Assert.False(events.GoBack());
    }

    [Fact]
    public async Task ShowsTheErrorsOfTheCalendarAndOfTheSuggestions()
    {
        connection.Setup(item => item.GetAgenda(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(error: "Add your location.", calendarError: "The Google Calendar connector is not connected."));
        EventsViewModel events = Create();

        await events.Load();

        Assert.Equal(("Add your location.", "The Google Calendar connector is not connected.", true, true), (events.SuggestionsError, events.CalendarError, events.HasSuggestionsError, events.HasCalendarError));
    }

    [Fact]
    public async Task FollowsTheChangesPushedByTheServerAndNoticesWhenViewingBeyondTheLoadedMonths()
    {
        EventsViewModel events = Create();
        Push(State(suggestions: [Suggestion("s1", "Jazz Night", new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero))]));

        Assert.Single(events.Days.Single(day => day.IsToday).Items);
        Assert.False(events.HasOutsideNotice);

        events.MonthCommand.Execute(null);
        for (int month = 0; month < 3; month++)
        {
            events.NextCommand.Execute(null);
        }

        Assert.True(events.HasOutsideNotice);
        Assert.Contains("Calendar events are loaded from Oct 1 to Dec 31.", events.OutsideNotice);
        await Task.CompletedTask;
    }
}
