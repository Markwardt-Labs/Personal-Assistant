namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class NewsViewModelTests
{
    private readonly Mock<IServerConnection> connection = new();

    private readonly Mock<ILinkLauncher> launcher = new();

    private readonly Eventually eventually = new();

    private NewsViewModel Create()
        => new(connection.Object, launcher.Object, CultureInfo.GetCultureInfo("en-US"));

    private NewsItem Item(string id, string feedId, string title)
        => new() { Id = id, FeedId = feedId, Title = title, Summary = $"{title} happened.", Source = "Wire", Published = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero), Url = $"https://example.com/{id}" };

    private NewsState State(IReadOnlyList<NewsFeed>? feeds = null, IReadOnlyList<NewsItem>? items = null, bool generating = false, string? error = null)
        => new() { Feeds = feeds ?? [new() { Id = "f1", Name = "Tech", Description = "chips" }, new() { Id = "f2", Name = "World", Description = "politics" }], Items = items ?? [], Generating = generating, Error = error, Generated = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero) };

    [Fact]
    public async Task ShowsTheEntriesUnderTheFeedTheyWereFoundFor()
    {
        connection.Setup(item => item.GetNews(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(items: [Item("n1", "f1", "New chip"), Item("n2", "f2", "Election"), Item("n3", "f1", "New model")]));
        NewsViewModel news = Create();

        await news.Load();

        Assert.Equal(["Tech", "World"], news.Sections.Select(section => section.Name));
        Assert.Equal(["New chip", "New model"], news.Sections[0].Items.Select(item => item.Title));
        Assert.Equal(["Election"], news.Sections[1].Items.Select(item => item.Title));
        Assert.Equal("Wire - 11/15/2026", news.Sections[0].Items[0].Meta);
    }

    [Fact]
    public async Task SaysSoUnderAFeedThatHasNoEntriesAndWhileTheServerSearches()
    {
        connection.Setup(item => item.GetNews(false, It.IsAny<CancellationToken>())).ReturnsAsync(State());
        NewsViewModel news = Create();
        await news.Load();

        Assert.All(news.Sections, section => Assert.True(section.IsEmpty));
        Assert.Equal("Nothing found yet.", news.Sections[0].EmptyText);

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.NewsChanged, News = State(generating: true) });

        Assert.Equal("Searching...", news.Sections[0].EmptyText);
        Assert.True(news.IsGenerating);
    }

    [Fact]
    public async Task ExplainsHowToGetNewsWhenThereAreNoFeeds()
    {
        connection.Setup(item => item.GetNews(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(feeds: []));
        NewsViewModel news = Create();
        Assert.False(news.NeedsFeeds);

        await news.Load();

        Assert.True(news.NeedsFeeds);
        Assert.False(news.HasFeeds);
        Assert.Empty(news.Sections);
    }

    [Fact]
    public async Task ReadsTheNewsOnlyTheFirstTime()
    {
        connection.Setup(item => item.GetNews(false, It.IsAny<CancellationToken>())).ReturnsAsync(State());
        NewsViewModel news = Create();

        await news.Load();
        await news.Load();

        connection.Verify(item => item.GetNews(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddsAFeedWithANameAndADescriptionAndClosesTheList()
    {
        connection.Setup(item => item.AddNewsFeed("Tech", "chips and models", It.IsAny<CancellationToken>())).ReturnsAsync(State(feeds: [new() { Id = "f1", Name = "Tech", Description = "chips and models" }]));
        NewsViewModel news = Create();
        news.ManageFeedsCommand.Execute(null);
        Assert.False(news.AddFeedCommand.CanExecute(null));
        (news.NewName, news.NewDescription) = ("  Tech  ", "  chips and models  ");
        Assert.True(news.AddFeedCommand.CanExecute(null));

        news.AddFeedCommand.Execute(null);
        await eventually.Be(() => news.Sections.Count == 1);

        Assert.Equal((string.Empty, string.Empty, false), (news.NewName, news.NewDescription, news.IsManagingFeeds));
    }

    [Fact]
    public async Task NeedsBothANameAndADescriptionToAddAFeed()
    {
        NewsViewModel news = Create();

        news.NewName = "Tech";
        Assert.False(news.AddFeedCommand.CanExecute(null));
        news.NewDescription = "ab";
        Assert.False(news.AddFeedCommand.CanExecute(null));
        news.NewDescription = "abc";
        Assert.True(news.AddFeedCommand.CanExecute(null));
        news.NewName = " ";
        Assert.False(news.AddFeedCommand.CanExecute(null));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task KeepsTheFormAndShowsTheReasonWhenAFeedIsRefused()
    {
        connection.Setup(item => item.AddNewsFeed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("A news feed with that name already exists."));
        NewsViewModel news = Create();
        (news.NewName, news.NewDescription) = ("Tech", "chips");

        news.AddFeedCommand.Execute(null);
        await eventually.Be(() => news.HasError);

        Assert.Equal(("Tech", "chips", "A news feed with that name already exists."), (news.NewName, news.NewDescription, news.Error));
    }

    [Fact]
    public async Task RemovesAFeedWithItsEntries()
    {
        connection.Setup(item => item.GetNews(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(items: [Item("n1", "f1", "New chip")]));
        connection.Setup(item => item.RemoveNewsFeed("f1", It.IsAny<CancellationToken>())).ReturnsAsync(State(feeds: [new() { Id = "f2", Name = "World", Description = "politics" }]));
        NewsViewModel news = Create();
        await news.Load();

        news.Sections[0].RemoveCommand.Execute(null);
        await eventually.Be(() => news.Sections.Count == 1);

        Assert.Equal("World", news.Sections[0].Name);
    }

    [Fact]
    public async Task OpensTheArticleInTheBrowser()
    {
        connection.Setup(item => item.GetNews(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(items: [Item("n1", "f1", "New chip")]));
        NewsViewModel news = Create();
        await news.Load();

        news.Sections[0].Items[0].OpenCommand.Execute(null);

        await eventually.Be(() => launcher.Invocations.Count > 0);
        launcher.Verify(item => item.Open("https://example.com/n1"), Times.Once);
    }

    [Fact]
    public async Task GivesEachEntryLikeAndDislikeButtonsThatNameItsFeed()
    {
        connection.Setup(item => item.GetNews(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(items: [Item("n1", "f1", "New chip") with { Reaction = ReactionKind.Like }]));
        NewsViewModel news = Create();
        await news.Load();
        ReactionViewModel reaction = news.Sections[0].Items[0].Reaction;

        Assert.True(reaction.IsLiked);
        reaction.LikeCommand.Execute(null);
        await eventually.Be(() => !reaction.IsLiked);

        connection.Verify(item => item.React(ReactionTarget.News, "https://example.com/n1", "New chip", "Tech", ReactionKind.None, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ShowsTheSearchErrorAndClosesTheListOfFeedsWithTheBackButton()
    {
        NewsViewModel news = Create();
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.NewsChanged, News = State(error: "The AI could not search the news: offline.") });

        Assert.True(news.HasSearchError);
        Assert.Equal("The AI could not search the news: offline.", news.SearchError);

        news.ManageFeedsCommand.Execute(null);
        Assert.True(news.GoBack());
        Assert.False(news.GoBack());
    }
}
