namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class AgendaServiceTests : IAsyncDisposable
{
    public AgendaServiceTests()
    {
        time = new(now);
        preferences.Setup(item => item.Background(false)).Returns("\nlikes jazz");
        Calendar([]);
    }

    private readonly Mock<ICalendarAi> calendar = new();

    private readonly Mock<IJsonFile<StoredCalendar>> calendarStore = new();

    private readonly Mock<IEventSuggestionsAi> ai = new();

    private readonly Mock<IJsonFile<StoredSuggestions>> store = new();

    private readonly Mock<IJsonFile<StoredFeeds>> feedStore = new();

    private readonly Mock<IPreferenceService> preferences = new();

    private readonly Mock<IDailySchedule> schedule = new();

    private readonly Mock<ISettingsService> settings = new();

    private readonly Mock<ILog> log = new();

    private readonly EventHub events = new();

    private readonly DateTimeOffset now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly FixedTime time;

    private AgendaService service = null!;

    public async ValueTask DisposeAsync()
    {
        if (service is not null)
        {
            await service.Close();
        }
    }

    private AgendaService Create(string? location = "Austin, Texas")
    {
        settings.SetupGet(item => item.Location).Returns(location);
        service = new(calendar.Object, calendarStore.Object, ai.Object, store.Object, feedStore.Object, preferences.Object, schedule.Object, events, log.Object, time, settings.Object);
        return service;
    }

    private void Calendar(params CalendarEvent[] found)
        => calendar.Setup(item => item.Read(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CalendarResult { Events = found });

    private CalendarEvent Event(string id, int days, int hours = 1)
        => new() { Id = id, Title = $"Event {id}", Start = now.AddDays(days), End = now.AddDays(days).AddHours(hours) };

    private SuggestedEventDraft Draft(string feedId, string title, int days, string url = "https://example.com/e")
        => new() { FeedId = feedId, Title = title, Start = now.AddDays(days), Description = $"About {title}.", Url = url };

    private async Task<AgendaState> Finished(IEventSubscription subscription)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        await foreach (RemoteEvent remoteEvent in subscription.Events.ReadAllAsync(timeout.Token))
        {
            if (remoteEvent.Agenda is { Generating: false } state)
            {
                return state;
            }
        }

        throw new InvalidOperationException("No state.");
    }

    [Fact]
    public async Task ReadsTheCalendarOfThreeMonthsAroundTodayWhenItWasNeverReadAndKeepsIt()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [] });
        Calendar(Event("e1", 1));
        AgendaService created = Create();
        using IEventSubscription subscription = events.Subscribe();

        await created.Get(false, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), state.From);
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), state.To);
        Assert.Equal("Event e1", Assert.Single(state.Events).Title);
        calendar.Verify(item => item.Read(state.From, state.To, It.IsAny<CancellationToken>()), Times.Once);
        calendarStore.Verify(item => item.Save(It.Is<StoredCalendar>(saved => saved.Read == now && saved.Events.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShowsTheKeptCalendarWithoutReadingItAgain()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [] });
        calendarStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredCalendar { Read = now.AddHours(-5), Events = [Event("kept", 2)] });

        AgendaState state = await Create().Get(false, CancellationToken.None);

        Assert.Equal("Event kept", Assert.Single(state.Events).Title);
        calendar.Verify(item => item.Read(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshReadsTheCalendarAgain()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [] });
        calendarStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredCalendar { Read = now.AddHours(-5), Events = [Event("old", 2)] });
        Calendar(Event("new", 3));
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        Assert.Equal("Event new", Assert.Single(state.Events).Title);
    }

    [Fact]
    public async Task RefreshingTheCalendarAloneReadsItWithoutSearchingForSuggestions()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "f1", Name = "Jazz", Description = "jazz" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [] });
        Calendar(Event("moved", 4));
        AgendaService created = Create();
        await created.Get(false, CancellationToken.None);
        using IEventSubscription subscription = events.Subscribe();

        created.RefreshCalendar();
        AgendaState state = await Finished(subscription);

        Assert.Equal("Event moved", Assert.Single(state.Events).Title);
        ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task KeepsOnlyEventsOfThePeriodWithAnIdAndALaterEndOnceEach()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [] });
        Calendar(Event("late", 5), Event("early", 1), Event("early", 2), Event(" ", 1), Event("far", 100), Event("past", -100), Event("backwards", 3, -2));
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(false, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        Assert.Equal(["early", "late"], state.Events.Select(item => item.Id));
        Assert.Equal(now.AddDays(1), state.Events[0].Start);
    }

    [Fact]
    public async Task ReportsACalendarThatCannotBeReadAndKeepsTheOldOneAndDoesNotTryAgainOnItsOwn()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [] });
        calendar.Setup(item => item.Read(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("The AI could not be started."));
        AgendaService created = Create();
        using IEventSubscription subscription = events.Subscribe();

        await created.Get(false, CancellationToken.None);
        AgendaState state = await Finished(subscription);
        await created.Get(false, CancellationToken.None);

        Assert.Equal("The AI could not be started.", state.CalendarError);
        Assert.Empty(state.Events);
        calendar.Verify(item => item.Read(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportsTheProblemTheAiFoundWithTheCalendarTools()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [] });
        calendarStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredCalendar { Read = now.AddDays(-2), Events = [Event("kept", 2)] });
        calendar.Setup(item => item.Read(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CalendarResult { Events = [], Problem = " The Google Calendar connector is not connected. " });
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        Assert.Equal("The Google Calendar connector is not connected.", state.CalendarError);
        Assert.Equal("Event kept", Assert.Single(state.Events).Title);
        calendarStore.Verify(item => item.Save(It.IsAny<StoredCalendar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "live jazz")]
    [InlineData("Jazz", "")]
    [InlineData("Jazz", "ab")]
    public async Task RefusesAFeedWithoutANameOrAUsefulDescription(string name, string description)
    {
        await Assert.ThrowsAsync<RequestException>(() => Create().AddFeed(name, description, CancellationToken.None));
    }

    [Fact]
    public async Task RefusesTooLongNamesAndDescriptionsDuplicateNamesAndTooManyFeeds()
    {
        AgendaService created = Create();
        await Assert.ThrowsAsync<RequestException>(() => created.AddFeed(new string('x', 41), "live jazz", CancellationToken.None));
        await Assert.ThrowsAsync<RequestException>(() => created.AddFeed("Jazz", new string('x', 301), CancellationToken.None));
        ai.Setup(item => item.Suggest(It.IsAny<string>(), It.IsAny<IReadOnlyList<EventFeed>>(), It.IsAny<IReadOnlyList<BusyTime>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await created.AddFeed("Jazz", "live jazz", CancellationToken.None);
        await Assert.ThrowsAsync<RequestException>(() => created.AddFeed("JAZZ", "something else", CancellationToken.None));

        for (int number = 0; number < 9; number++)
        {
            await created.AddFeed($"Feed {number}", "some events", CancellationToken.None);
        }

        await Assert.ThrowsAsync<RequestException>(() => created.AddFeed("One more", "some events", CancellationToken.None));
    }

    [Fact]
    public async Task AddingAFeedSavesItAndSearchesOnlyForEventsThatRelateToThatFeed()
    {
        IReadOnlyList<EventFeed>? searched = null;
        ai.Setup(item => item.Suggest("Austin, Texas", It.IsAny<IReadOnlyList<EventFeed>>(), It.IsAny<IReadOnlyList<BusyTime>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "\nlikes jazz", It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<EventFeed>, IReadOnlyList<BusyTime>, DateTimeOffset, DateTimeOffset, string, CancellationToken>((_, feeds, _, _, _, _, _) => searched = feeds)
            .Returns((string _, IReadOnlyList<EventFeed> feeds, IReadOnlyList<BusyTime> _, DateTimeOffset _, DateTimeOffset _, string _, CancellationToken _) => Task.FromResult<IReadOnlyList<SuggestedEventDraft>>([Draft(feeds[0].Id, "Jazz Night", 2)]));
        AgendaService created = Create();
        using IEventSubscription subscription = events.Subscribe();

        AgendaState added = await created.AddFeed("  Jazz  ", "  live jazz  ", CancellationToken.None);
        AgendaState done = await Finished(subscription);

        EventFeed feed = Assert.Single(added.Feeds);
        Assert.Equal(("Jazz", "live jazz"), (feed.Name, feed.Description));
        Assert.Equal(feed.Id, Assert.Single(searched!).Id);
        Assert.Equal("Jazz Night", Assert.Single(done.Suggestions).Title);
        feedStore.Verify(item => item.Save(It.Is<StoredFeeds>(saved => saved.Feeds.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddingAFeedKeepsTheSuggestionsOfTheOtherFeedsAndDoesNotSearchThemAgain()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "old", Name = "Jazz", Description = "live jazz" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now.AddHours(-3), Events = [new() { Id = "s1", FeedId = "old", Title = "Kept Jazz", Start = now.AddDays(2), Description = "d", Url = "https://example.com/kept" }] });
        IReadOnlyList<EventFeed>? searched = null;
        ai.Setup(item => item.Suggest(It.IsAny<string>(), It.IsAny<IReadOnlyList<EventFeed>>(), It.IsAny<IReadOnlyList<BusyTime>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyList<EventFeed> feeds, IReadOnlyList<BusyTime> _, DateTimeOffset _, DateTimeOffset _, string _, CancellationToken _) =>
            {
                searched = feeds;
                return Task.FromResult<IReadOnlyList<SuggestedEventDraft>>([Draft(feeds[0].Id, "New Hike", 4, "https://example.com/hike")]);
            });
        AgendaService created = Create();
        using IEventSubscription subscription = events.Subscribe();

        await created.AddFeed("Hiking", "family hikes", CancellationToken.None);
        AgendaState done = await Finished(subscription);

        Assert.Equal("Hiking", Assert.Single(searched!).Name);
        Assert.Equal(["Kept Jazz", "New Hike"], done.Suggestions.Select(item => item.Title));
        Assert.Equal(["s1", "s2"], done.Suggestions.Select(item => item.Id));
    }

    [Fact]
    public async Task RefreshSearchesEveryFeed()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "f1", Name = "Jazz", Description = "live jazz" }, new() { Id = "f2", Name = "Hiking", Description = "family hikes" }] });
        IReadOnlyList<EventFeed>? searched = null;
        ai.Setup(item => item.Suggest(It.IsAny<string>(), It.IsAny<IReadOnlyList<EventFeed>>(), It.IsAny<IReadOnlyList<BusyTime>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyList<EventFeed> feeds, IReadOnlyList<BusyTime> _, DateTimeOffset _, DateTimeOffset _, string _, CancellationToken _) =>
            {
                searched = feeds;
                return Task.FromResult<IReadOnlyList<SuggestedEventDraft>>([]);
            });
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        await Finished(subscription);

        Assert.Equal(["f1", "f2"], searched!.Select(feed => feed.Id));
    }

    [Fact]
    public async Task OnlyKeepsSuggestionsThatRelateToAFeedHaveAnAddressAndFallInThePeriod()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "f1", Name = "Jazz", Description = "jazz" }] });
        ai.Setup(item => item.Suggest(It.IsAny<string>(), It.IsAny<IReadOnlyList<EventFeed>>(), It.IsAny<IReadOnlyList<BusyTime>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Draft("f1", "Good", 3, "https://example.com/good"),
            Draft("other", "Not a feed", 3),
            Draft("f1", "No address", 3, "not a url"),
            Draft("f1", "Wrong scheme", 3, "ftp://example.com/x"),
            Draft("f1", "Too late", 90),
            Draft("f1", "Long ago", -10),
            Draft("f1", "Good", 3, "https://example.com/good-again"),
            Draft("f1", " ", 4),
        ]);
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        SuggestedEvent suggestion = Assert.Single(state.Suggestions);
        Assert.Equal(("Good", "f1", "s1"), (suggestion.Title, suggestion.FeedId, suggestion.Id));
    }

    [Fact]
    public async Task OrdersSuggestionsByStartAndTellsTheAiWhenTheUserIsBusy()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "f1", Name = "Jazz", Description = "jazz" }] });
        Calendar(Event("e1", 1, 2));
        IReadOnlyList<BusyTime>? busy = null;
        ai.Setup(item => item.Suggest(It.IsAny<string>(), It.IsAny<IReadOnlyList<EventFeed>>(), It.IsAny<IReadOnlyList<BusyTime>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<EventFeed>, IReadOnlyList<BusyTime>, DateTimeOffset, DateTimeOffset, string, CancellationToken>((_, _, given, _, _, _, _) => busy = given)
            .ReturnsAsync([Draft("f1", "Later", 5, "https://example.com/later"), Draft("f1", "Sooner", 2, "https://example.com/sooner")]);
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        Assert.Equal(["Sooner", "Later"], state.Suggestions.Select(item => item.Title));
        BusyTime time = Assert.Single(busy!);
        Assert.Equal((now.AddDays(1), now.AddDays(1).AddHours(2)), (time.Start, time.End));
    }

    [Fact]
    public async Task SearchesNothingWithoutFeedsAndSaysSoWithoutAnError()
    {
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(false, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        Assert.Null(state.Error);
        Assert.Empty(state.Suggestions);
        ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AsksForTheLocationWhenThereAreFeedsButNoLocation()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "f1", Name = "Jazz", Description = "jazz" }] });
        using IEventSubscription subscription = events.Subscribe();

        await Create(null).Get(true, CancellationToken.None);
        AgendaState state = await Finished(subscription);

        Assert.Contains("add your location in the settings of the app", state.Error);
        ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RemovingAFeedDropsItsSuggestionsAtOnceAndRefusesAnUnknownFeed()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "f1", Name = "Jazz", Description = "jazz" }, new() { Id = "f2", Name = "Hiking", Description = "hiking" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [new() { Id = "s1", FeedId = "f1", Title = "Jazz Night", Start = now.AddDays(2), Description = "d", Url = "https://example.com/j" }, new() { Id = "s2", FeedId = "f2", Title = "Hike", Start = now.AddDays(3), Description = "d", Url = "https://example.com/h" }] });
        AgendaService created = Create();

        AgendaState state = await created.RemoveFeed("f1", CancellationToken.None);

        Assert.Equal(["f2"], state.Feeds.Select(feed => feed.Id));
        Assert.Equal(["Hike"], state.Suggestions.Select(item => item.Title));
        await Assert.ThrowsAsync<RequestException>(() => created.RemoveFeed("f1", CancellationToken.None));
    }

    [Fact]
    public async Task HidesSuggestionsThatAreOverAndMarksTheReactionOfTheUser()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredFeeds { Feeds = [new() { Id = "f1", Name = "Jazz", Description = "jazz" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredSuggestions { Generated = now, Events = [new() { Id = "s1", FeedId = "f1", Title = "Over", Start = now.AddDays(-2), Description = "d", Url = "https://example.com/over" }, new() { Id = "s2", FeedId = "f1", Title = "Coming", Start = now.AddDays(2), Description = "d", Url = "https://example.com/coming" }] });
        preferences.Setup(item => item.Of(ReactionTarget.Event, "https://example.com/coming")).Returns(ReactionKind.Like);

        AgendaState state = await Create().Get(false, CancellationToken.None);

        SuggestedEvent suggestion = Assert.Single(state.Suggestions);
        Assert.Equal(("Coming", ReactionKind.Like), (suggestion.Title, suggestion.Reaction));
    }

    [Fact]
    public async Task RunsTheDailyScheduleWhenStarted()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        schedule.Setup(item => item.Run(It.IsAny<Func<DateTimeOffset?>>(), It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            started.TrySetResult();
            return Task.CompletedTask;
        });

        Create().Start(CancellationToken.None);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
