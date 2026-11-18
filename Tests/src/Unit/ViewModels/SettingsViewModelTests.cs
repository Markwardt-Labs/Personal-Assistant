namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class SettingsViewModelTests
{
    public SettingsViewModelTests()
    {
        info.SetupGet(item => item.Version).Returns("1.0.0");
    }

    private readonly Mock<IServerConnection> connection = new();

    private readonly Mock<IAppInfo> info = new();

    private readonly Eventually eventually = new();

    private bool changed;

    private bool left;

    private SettingsViewModel Create()
        => new(connection.Object, new UpdateViewModel(info.Object, null, null), () => changed = true, () => left = true);

    [Fact]
    public async Task ShowsTheSettingsOfTheServer()
    {
        connection.Setup(item => item.GetSettings(It.IsAny<CancellationToken>())).ReturnsAsync(new ServerSettings { Location = "Austin" });
        SettingsViewModel settings = Create();

        await settings.Load();

        Assert.Equal("Austin", settings.Location);
    }

    [Fact]
    public async Task ShowsAnEmptyLocationWhenNoneIsSet()
    {
        connection.Setup(item => item.GetSettings(It.IsAny<CancellationToken>())).ReturnsAsync(new ServerSettings());
        SettingsViewModel settings = Create();
        settings.Location = "typed";

        await settings.Load();

        Assert.Equal(string.Empty, settings.Location);
    }

    [Fact]
    public async Task ShowsWhyTheSettingsCouldNotBeRead()
    {
        connection.Setup(item => item.GetSettings(It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The server is busy."));
        SettingsViewModel settings = Create();

        await settings.Load();

        Assert.Equal("The server is busy.", settings.Error);
    }

    [Fact]
    public async Task SavesTheLocationAndSaysSo()
    {
        connection.Setup(item => item.SetSettings(It.IsAny<ServerSettingsUpdate>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServerSettings { Location = "Austin" });
        SettingsViewModel settings = Create();
        settings.Location = "Austin";

        settings.SaveCommand.Execute(null);
        await eventually.Be(() => settings.HasSaved);

        connection.Verify(item => item.SetSettings(It.Is<ServerSettingsUpdate>(update => update.Location == "Austin"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Saved.", settings.Saved);
    }

    [Fact]
    public async Task ShowsWhyTheSettingsWereRefused()
    {
        connection.Setup(item => item.SetSettings(It.IsAny<ServerSettingsUpdate>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("Refused."));
        SettingsViewModel settings = Create();

        settings.SaveCommand.Execute(null);
        await eventually.Be(() => settings.HasError);

        Assert.Equal("Refused.", settings.Error);
        Assert.False(settings.HasSaved);
    }

    [Fact]
    public void FollowsAChangeOfTheSettingsPushedByTheServer()
    {
        SettingsViewModel settings = Create();

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.SettingsChanged, Settings = new ServerSettings { Location = "Dallas" } });

        Assert.Equal("Dallas", settings.Location);
    }

    [Fact]
    public void IgnoresOtherEvents()
    {
        SettingsViewModel settings = Create();
        settings.Location = "Dallas";

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.WorkingChanged, Working = true });

        Assert.Equal("Dallas", settings.Location);
    }

    [Fact]
    public void LeavesAndChangesTheServerOnRequest()
    {
        SettingsViewModel settings = Create();

        settings.BackCommand.Execute(null);
        settings.ChangeServerCommand.Execute(null);

        Assert.True(left);
        Assert.True(changed);
    }

    [Fact]
    public async Task AsksTheServerToRefreshEachTabOnRequest()
    {
        connection.Setup(item => item.GetMailDigest(true, It.IsAny<CancellationToken>())).ReturnsAsync(new MailDigestState { Generating = true });
        connection.Setup(item => item.GetAgenda(true, It.IsAny<CancellationToken>())).ReturnsAsync(new AgendaState { From = DateTimeOffset.UnixEpoch, To = DateTimeOffset.UnixEpoch, Events = [], Feeds = [], Suggestions = [], Generating = true });
        connection.Setup(item => item.GetNews(true, It.IsAny<CancellationToken>())).ReturnsAsync(new NewsState { Generating = true, Feeds = [], Items = [] });
        SettingsViewModel settings = Create();

        settings.RefreshMailCommand.Execute(null);
        settings.RefreshEventsCommand.Execute(null);
        settings.RefreshNewsCommand.Execute(null);
        await eventually.Be(() => connection.Invocations.Count(call => call.Method.Name is "GetMailDigest" or "GetAgenda" or "GetNews") == 3);

        connection.Verify(item => item.GetMailDigest(true, It.IsAny<CancellationToken>()), Times.Once);
        connection.Verify(item => item.GetAgenda(true, It.IsAny<CancellationToken>()), Times.Once);
        connection.Verify(item => item.GetNews(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShowsWhyARefreshWasRefused()
    {
        connection.Setup(item => item.GetMailDigest(true, It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The server is busy."));
        SettingsViewModel settings = Create();

        settings.RefreshMailCommand.Execute(null);
        await eventually.Be(() => settings.HasError);

        Assert.Equal("The server is busy.", settings.Error);
    }

    [Fact]
    public async Task TakesTheSavedMarkAwayWhenTheLocationIsEditedAgain()
    {
        connection.Setup(item => item.SetSettings(It.IsAny<ServerSettingsUpdate>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServerSettings { Location = "Austin" });
        SettingsViewModel settings = Create();
        settings.Location = "Austin";
        settings.SaveCommand.Execute(null);
        await eventually.Be(() => settings.HasSaved);

        settings.Location = "Dallas";

        Assert.False(settings.HasSaved);
    }
}
