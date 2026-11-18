namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class HomeViewModelTests
{
    public HomeViewModelTests()
    {
        info.SetupGet(item => item.Version).Returns("1.0.0");
        update = new(info.Object, null, null);
        home = new(new MailViewModel(connection.Object), new EventsViewModel(connection.Object, launcher.Object), new NewsViewModel(connection.Object, launcher.Object), new ChatViewModel(connection.Object), back => new SettingsViewModel(connection.Object, update, () => serverChanged = true, back), update);
    }

    private readonly Mock<IServerConnection> connection = new();

    private readonly Mock<ILinkLauncher> launcher = new();

    private readonly Mock<IAppInfo> info = new();

    private readonly UpdateViewModel update;

    private readonly HomeViewModel home;

    private bool serverChanged;

    [Fact]
    public void StartsOnTheMailTabAndLoadsIt()
    {
        Assert.Equal(HomeTab.Mail, home.Selected);
        Assert.True(home.IsMailSelected);
        connection.Verify(item => item.GetMailDigest(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SelectsTheTabsAndReadsTheCalendarEachTimeButTheNewsOnlyTheFirstTime()
    {
        home.EventsCommand.Execute(null);
        home.NewsCommand.Execute(null);
        home.EventsCommand.Execute(null);
        home.NewsCommand.Execute(null);

        Assert.True(home.IsNewsSelected);
        connection.Verify(item => item.GetAgenda(false, It.IsAny<CancellationToken>()), Times.Exactly(2));
        connection.Verify(item => item.GetNews(false, It.IsAny<CancellationToken>()), Times.Once);

        home.ChatCommand.Execute(null);

        Assert.True(home.IsChatSelected);
        Assert.False(home.IsEventsSelected);
    }

    [Fact]
    public void OpensTheSettingsOverTheTabsAndStepsBackFromThem()
    {
        home.SettingsCommand.Execute(null);

        Assert.True(home.HasSettings);
        Assert.NotNull(home.SettingsPage);
        Assert.True(home.GoBack());
        Assert.False(home.HasSettings);
        Assert.False(home.GoBack());
    }

    [Fact]
    public void LetsTheSettingsChangeTheServer()
    {
        home.SettingsCommand.Execute(null);

        home.SettingsPage!.ChangeServerCommand.Execute(null);

        Assert.True(serverChanged);
    }

    [Fact]
    public void LetsTheSettingsStepBackThroughTheirOwnBackButton()
    {
        home.SettingsCommand.Execute(null);

        home.SettingsPage!.BackCommand.Execute(null);

        Assert.False(home.HasSettings);
    }

    [Fact]
    public void StepsBackThroughWhatTheSelectedTabHasOpen()
    {
        home.EventsCommand.Execute(null);
        home.Events.ManageFeedsCommand.Execute(null);

        Assert.True(home.GoBack());
        Assert.False(home.Events.IsManagingFeeds);

        home.NewsCommand.Execute(null);
        home.News.ManageFeedsCommand.Execute(null);

        Assert.True(home.GoBack());
        Assert.False(home.News.IsManagingFeeds);
        home.ChatCommand.Execute(null);
        Assert.False(home.GoBack());
    }

    [Fact]
    public async Task OffersTheUpdateOnlyWhileTheSettingsAreClosed()
    {
        Mock<IReleaseFeed> feed = new();
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(new ReleaseInfo { Version = AppVersion.TryParse("2.0.0", out AppVersion? latest) ? latest : null!, Assets = [] });
        Mock<IClientUpdater> updater = new();
        updater.SetupGet(item => item.IsSupported).Returns(true);
        UpdateViewModel offered = new(info.Object, feed.Object, updater.Object);
        connection.SetupGet(item => item.ServerVersion).Returns("1.0.0");
        HomeViewModel withOffer = new(new MailViewModel(connection.Object), new EventsViewModel(connection.Object, launcher.Object), new NewsViewModel(connection.Object, launcher.Object), new ChatViewModel(connection.Object), back => new SettingsViewModel(connection.Object, offered, () => { }, back), offered);
        Assert.False(withOffer.ShowUpdateOffer);

        offered.Attach(connection.Object);
        await new Eventually().Be(() => withOffer.ShowUpdateOffer);
        withOffer.SettingsCommand.Execute(null);

        Assert.False(withOffer.ShowUpdateOffer);
        Assert.True(offered.IsAvailable);
    }

    [Fact]
    public void NamesTheSelectedTabInTheBarAndShowsTheFeedsOnlyWhereTheTabHasThem()
    {
        Assert.Equal(("Mail", true, false), (home.TabTitle, home.ShowTitle, home.CanManageFeeds));

        home.EventsCommand.Execute(null);
        Assert.Equal(("Events", false, true), (home.TabTitle, home.ShowTitle, home.CanManageFeeds));

        home.NewsCommand.Execute(null);
        Assert.Equal(("News", true, true), (home.TabTitle, home.ShowTitle, home.CanManageFeeds));

        home.ChatCommand.Execute(null);
        Assert.Equal(("Chat", true, false), (home.TabTitle, home.ShowTitle, home.CanManageFeeds));
    }

    [Fact]
    public void OpensTheFeedsOfTheSelectedTabFromTheBar()
    {
        home.EventsCommand.Execute(null);
        home.FeedsCommand.Execute(null);
        Assert.True(home.Events.IsManagingFeeds);
        Assert.False(home.News.IsManagingFeeds);

        home.NewsCommand.Execute(null);
        home.FeedsCommand.Execute(null);
        Assert.True(home.News.IsManagingFeeds);
    }

    [Fact]
    public void HidesTheBarOverAPageOfTheTabAndOverTheSettings()
    {
        Assert.True(home.ShowBar);

        home.EventsCommand.Execute(null);
        home.FeedsCommand.Execute(null);
        Assert.False(home.ShowBar);
        home.GoBack();
        Assert.True(home.ShowBar);

        home.SettingsCommand.Execute(null);
        Assert.False(home.ShowBar);
        home.GoBack();
        Assert.True(home.ShowBar);
    }

    [Fact]
    public void ShowsProgressWhileTheServerWorksForTheSelectedTab()
    {
        Assert.False(home.ShowProgress);

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MailDigestChanged, MailDigest = new() { Generating = true } });
        Assert.True(home.ShowProgress);

        home.NewsCommand.Execute(null);
        Assert.False(home.ShowProgress);

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.NewsChanged, News = new() { Generating = true, Feeds = [], Items = [] } });
        Assert.True(home.ShowProgress);

        home.ChatCommand.Execute(null);
        Assert.False(home.ShowProgress);

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.WorkingChanged, Working = true });
        Assert.True(home.ShowProgress);
    }
}
