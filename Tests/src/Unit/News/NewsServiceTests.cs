namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class NewsServiceTests : IAsyncDisposable
{
    public NewsServiceTests()
    {
        preferences.Setup(item => item.Background(false)).Returns("\nno sports");
    }

    private readonly Mock<INewsAi> ai = new();

    private readonly Mock<IJsonFile<StoredNews>> store = new();

    private readonly Mock<IJsonFile<StoredNewsFeeds>> feedStore = new();

    private readonly Mock<IPreferenceService> preferences = new();

    private readonly Mock<IDailySchedule> schedule = new();

    private readonly Mock<ILog> log = new();

    private readonly EventHub events = new();

    private readonly DateTimeOffset now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private NewsService? service;

    public async ValueTask DisposeAsync()
    {
        if (service is not null)
        {
            await service.Close();
        }
    }

    private NewsService Create()
    {
        service = new(ai.Object, store.Object, feedStore.Object, preferences.Object, schedule.Object, events, log.Object, new FixedTime(now));
        return service;
    }

    private NewsItemDraft Draft(string feedId, string title, string url, int hoursAgo = 1)
        => new() { FeedId = feedId, Title = title, Summary = $"{title} happened.", Url = url, Source = "Wire", Published = now.AddHours(-hoursAgo) };

    private void Searches(Func<IReadOnlyList<NewsFeed>, IReadOnlyList<NewsItemDraft>> answer)
        => ai.Setup(item => item.Search(It.IsAny<IReadOnlyList<NewsFeed>>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((IReadOnlyList<NewsFeed> feeds, DateTimeOffset _, string _, CancellationToken _) => Task.FromResult(answer(feeds)));

    private async Task<NewsState> Finished(IEventSubscription subscription)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        await foreach (RemoteEvent remoteEvent in subscription.Events.ReadAllAsync(timeout.Token))
        {
            if (remoteEvent.News is { Generating: false } state)
            {
                return state;
            }
        }

        throw new InvalidOperationException("No state.");
    }

    [Theory]
    [InlineData("", "chips and models")]
    [InlineData("Tech", "ab")]
    public async Task RefusesAFeedWithoutANameOrAUsefulDescription(string name, string description)
    {
        await Assert.ThrowsAsync<RequestException>(() => Create().AddFeed(name, description, CancellationToken.None));
    }

    [Fact]
    public async Task RefusesTooLongNamesDuplicateNamesAndTooManyFeeds()
    {
        NewsService created = Create();
        Searches(_ => []);
        await Assert.ThrowsAsync<RequestException>(() => created.AddFeed(new string('x', 41), "chips", CancellationToken.None));
        await created.AddFeed("Tech", "chips and models", CancellationToken.None);
        await Assert.ThrowsAsync<RequestException>(() => created.AddFeed("tech", "something else", CancellationToken.None));

        for (int number = 0; number < 9; number++)
        {
            await created.AddFeed($"Feed {number}", "some news", CancellationToken.None);
        }

        await Assert.ThrowsAsync<RequestException>(() => created.AddFeed("Eleven", "some news", CancellationToken.None));
    }

    [Fact]
    public async Task AddingAFeedSearchesOnlyForThatFeedAndKeepsTheEntriesOfTheOthers()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "old", Name = "Tech", Description = "chips" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNews { Generated = now.AddHours(-3), Items = [new() { Id = "n1", FeedId = "old", Title = "Kept", Summary = "s", Url = "https://example.com/kept" }] });
        IReadOnlyList<NewsFeed>? searched = null;
        Searches(feeds =>
        {
            searched = feeds;
            return [Draft(feeds[0].Id, "Goal", "https://example.com/goal")];
        });
        NewsService created = Create();
        using IEventSubscription subscription = events.Subscribe();

        NewsState added = await created.AddFeed("Sport", "football", CancellationToken.None);
        NewsState done = await Finished(subscription);

        Assert.Equal(["Tech", "Sport"], added.Feeds.Select(feed => feed.Name));
        Assert.Equal("Sport", Assert.Single(searched!).Name);
        Assert.Equal(["Kept", "Goal"], done.Items.Select(item => item.Title));
        Assert.Equal(now, done.Generated);
    }

    [Fact]
    public async Task GroupsEntriesUnderTheirFeedsNewestFirstAndAsksWithWhatWasLearned()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }, new() { Id = "f2", Name = "World", Description = "politics" }] });
        Searches(_ => [Draft("f2", "World old", "https://example.com/w1", 5), Draft("f1", "Tech new", "https://example.com/t2", 1), Draft("f2", "World new", "https://example.com/w2", 2), Draft("f1", "Tech old", "https://example.com/t1", 9)]);
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        NewsState state = await Finished(subscription);

        Assert.Equal(["Tech new", "Tech old", "World new", "World old"], state.Items.Select(item => item.Title));
        Assert.Equal(["n1", "n2", "n3", "n4"], state.Items.Select(item => item.Id));
        ai.Verify(item => item.Search(It.IsAny<IReadOnlyList<NewsFeed>>(), now - TimeSpan.FromDays(7), "\nno sports", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnlyKeepsEntriesOfAFeedWithAnAddressASummaryAndNoRepeatedAddress()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }] });
        Searches(_ =>
        [
            Draft("f1", "Good", "https://example.com/good"),
            Draft("nope", "Not a feed", "https://example.com/x"),
            Draft("f1", "Bad address", "javascript:alert(1)"),
            Draft("f1", "Same address", "https://example.com/good"),
            new NewsItemDraft { FeedId = "f1", Title = "No summary", Summary = " ", Url = "https://example.com/n" },
        ]);
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        NewsState state = await Finished(subscription);

        Assert.Equal("Good", Assert.Single(state.Items).Title);
    }

    [Fact]
    public async Task KeepsAtMostEightEntriesPerFeed()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }] });
        Searches(_ => [.. Enumerable.Range(0, 12).Select(number => Draft("f1", $"Story {number}", $"https://example.com/{number}", number))]);
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        NewsState state = await Finished(subscription);

        Assert.Equal(8, state.Items.Count);
        Assert.Equal("Story 0", state.Items[0].Title);
    }

    [Fact]
    public async Task SearchesWhenThereAreFeedsButNoNewsYetAndNeverWithoutFeeds()
    {
        Searches(_ => []);
        NewsState none = await Create().Get(false, CancellationToken.None);
        Assert.False(none.Generating);
        ai.VerifyNoOtherCalls();

        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }] });
        TaskCompletionSource<IReadOnlyList<NewsItemDraft>> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ai.Setup(item => item.Search(It.IsAny<IReadOnlyList<NewsFeed>>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(release.Task);
        NewsService other = new(ai.Object, store.Object, feedStore.Object, preferences.Object, schedule.Object, new EventHub(), log.Object, new FixedTime(now));
        NewsState started = await other.Get(false, CancellationToken.None);

        Assert.True(started.Generating);
        release.SetResult([]);
        await other.Close();
    }

    [Fact]
    public async Task ReportsASearchThatFailedAndKeepsTheEntriesOfBefore()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNews { Generated = now.AddDays(-1), Items = [new() { Id = "n1", FeedId = "f1", Title = "Old", Summary = "s", Url = "https://example.com/old" }] });
        ai.Setup(item => item.Search(It.IsAny<IReadOnlyList<NewsFeed>>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("The AI could not search the news: offline."));
        using IEventSubscription subscription = events.Subscribe();

        await Create().Get(true, CancellationToken.None);
        NewsState state = await Finished(subscription);

        Assert.Contains("offline", state.Error);
        Assert.Equal("Old", Assert.Single(state.Items).Title);
    }

    [Fact]
    public async Task RemovingAFeedDropsItsEntriesAtOnceAndRefusesAnUnknownFeed()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }, new() { Id = "f2", Name = "World", Description = "politics" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNews { Generated = now, Items = [new() { Id = "n1", FeedId = "f1", Title = "Tech", Summary = "s", Url = "https://example.com/t" }, new() { Id = "n2", FeedId = "f2", Title = "World", Summary = "s", Url = "https://example.com/w" }] });
        NewsService created = Create();

        NewsState state = await created.RemoveFeed("f1", CancellationToken.None);

        Assert.Equal(["World"], state.Items.Select(item => item.Title));
        await Assert.ThrowsAsync<RequestException>(() => created.RemoveFeed("f1", CancellationToken.None));
    }

    [Fact]
    public async Task MarksTheReactionOfTheUserOnEntries()
    {
        feedStore.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNewsFeeds { Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }] });
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredNews { Generated = now, Items = [new() { Id = "n1", FeedId = "f1", Title = "Chip", Summary = "s", Url = "https://example.com/t" }] });
        preferences.Setup(item => item.Of(ReactionTarget.News, "https://example.com/t")).Returns(ReactionKind.Like);

        NewsState state = await Create().Get(false, CancellationToken.None);

        Assert.Equal(ReactionKind.Like, Assert.Single(state.Items).Reaction);
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
