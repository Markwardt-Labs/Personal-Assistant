namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class MainViewModelTests
{
    public MainViewModelTests()
    {
        info.SetupGet(item => item.Name).Returns("Personal Assistant");
        info.SetupGet(item => item.Version).Returns("1.0.0");
        connection.SetupGet(item => item.ServerVersion).Returns("1.0.0");
        connection.Setup(item => item.GetHistory(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ChatHistory { Messages = [], Working = false });
        updater.SetupGet(item => item.IsSupported).Returns(true);
    }

    private readonly Mock<IServerConnection.IFactory> connections = new();

    private readonly Mock<ISettingsStore> store = new();

    private readonly Mock<IServerConnection> connection = new();

    private readonly Mock<IAppInfo> info = new();

    private readonly Mock<ILinkLauncher> launcher = new();

    private readonly Mock<IReleaseFeed> feed = new();

    private readonly Mock<IClientUpdater> updater = new();

    private readonly Eventually eventually = new();

    private readonly ConnectionSettings saved = new() { Host = "pc", Port = 52680, AccessKey = "key" };

    private MainViewModel Create(TimeSpan? retry = null, TimeSpan? patience = null)
        => new(info.Object, connections.Object, store.Object, launcher.Object, feed.Object, updater.Object, retry ?? TimeSpan.FromMilliseconds(5), patience);

    private async Task<MainViewModel> StartConnected()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        connections.Setup(item => item.Open(saved, It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        MainViewModel model = Create();
        await model.Start();
        return model;
    }

    private Mock<IServerConnection> Connection(string? version)
    {
        Mock<IServerConnection> other = new();
        other.SetupGet(item => item.ServerVersion).Returns(version);
        other.Setup(item => item.GetHistory(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ChatHistory { Messages = [], Working = false });
        return other;
    }

    [Fact]
    public async Task ShowsTheConnectScreenWhenNothingWasSaved()
    {
        MainViewModel model = Create();

        await model.Start();

        Assert.IsType<ConnectViewModel>(model.Current);
        connections.Verify(item => item.Open(It.IsAny<ConnectionSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConnectsAtOnceWithTheSavedSettingsAndShowsTheHomeScreen()
    {
        MainViewModel model = await StartConnected();

        HomeViewModel home = Assert.IsType<HomeViewModel>(model.Current);
        Assert.Same(home, model.Home);
    }

    [Theory]
    [InlineData("0.9.0")]
    [InlineData(null)]
    public async Task ShowsTheVersionMismatchScreenWhenTheServerRunsAnotherVersion(string? serverVersion)
    {
        connection.SetupGet(item => item.ServerVersion).Returns(serverVersion);

        MainViewModel model = await StartConnected();

        Assert.IsType<VersionMismatchViewModel>(model.Current);
        Assert.Null(model.Home);
    }

    [Fact]
    public async Task StaysOnTheConnectScreenWithTheErrorWhenTheSavedServerIsGone()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        connections.Setup(item => item.Open(It.IsAny<ConnectionSettings>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The server could not be reached."));
        MainViewModel model = Create();

        await model.Start();

        ConnectViewModel connect = Assert.IsType<ConnectViewModel>(model.Current);
        Assert.Equal("The server could not be reached.", connect.Error);
        Assert.Equal("pc", connect.Host);
    }

    [Fact]
    public async Task ReconnectsAtOnceWhenTheConnectionIsLost()
    {
        MainViewModel model = await StartConnected();
        HomeViewModel first = model.Home!;
        Mock<IServerConnection> second = Connection("1.0.0");
        connections.Setup(item => item.Open(saved, It.IsAny<CancellationToken>())).ReturnsAsync(second.Object);

        connection.Raise(item => item.Lost += null, "The server closed the connection.");
        await eventually.Be(() => model.Home is { } home && home != first);

        connection.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task ShowsTheConnectScreenWithTheErrorWhenReconnectingFails()
    {
        MainViewModel model = await StartConnected();
        connections.Setup(item => item.Open(saved, It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The server could not be reached."));

        connection.Raise(item => item.Lost += null, "The server closed the connection.");
        await eventually.Be(() => model.Current is ConnectViewModel { HasError: true });

        ConnectViewModel connect = (ConnectViewModel)model.Current!;
        Assert.Equal("The server could not be reached.", connect.Error);
        Assert.Equal(("pc", "52680", "key"), (connect.Host, connect.Port, connect.AccessKey));
    }

    [Fact]
    public async Task ReconnectsWithTheSettingsTheUserTypedNotTheOnesSavedBefore()
    {
        MainViewModel model = Create();
        await model.Start();
        ConnectViewModel connect = (ConnectViewModel)model.Current!;
        ConnectionSettings typed = new() { Host = "laptop", Port = 52681, AccessKey = "typed" };
        connections.Setup(item => item.Open(typed, It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        (connect.Host, connect.Port, connect.AccessKey) = ("laptop", "52681", "typed");
        await connect.Connect();
        connections.Setup(item => item.Open(typed, It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The server could not be reached."));

        connection.Raise(item => item.Lost += null, "The server closed the connection.");
        await eventually.Be(() => model.Current is ConnectViewModel { HasError: true });

        Assert.Equal("laptop", ((ConnectViewModel)model.Current!).Host);
        connections.Verify(item => item.Open(typed, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReturnsToTheConnectScreenWithoutReconnectingWhenTheUserChangesServer()
    {
        MainViewModel model = await StartConnected();

        model.Home!.SettingsCommand.Execute(null);
        model.Home.SettingsPage!.ChangeServerCommand.Execute(null);
        await eventually.Be(() => model.Current is ConnectViewModel);

        ConnectViewModel connect = (ConnectViewModel)model.Current!;
        Assert.False(connect.HasError);
        Assert.Equal("pc", connect.Host);
        connections.Verify(item => item.Open(saved, It.IsAny<CancellationToken>()), Times.Once);
        connection.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task StepsBackThroughTheHomeScreen()
    {
        MainViewModel model = await StartConnected();

        Assert.False(model.GoBack());

        model.Home!.SettingsCommand.Execute(null);

        Assert.True(model.GoBack());
        Assert.False(model.Home.HasSettings);
    }

    [Fact]
    public async Task UpdatesTheServerShowsTheUpdatingScreenAndReturnsHomeOnceTheServerIsBack()
    {
        info.SetupGet(item => item.Version).Returns("2.0.0");
        connection.SetupGet(item => item.ServerVersion).Returns("1.0.0");
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(new ReleaseInfo { Version = AppVersion.TryParse("2.0.0", out AppVersion? latest) ? latest : null!, Assets = [] });
        updater.SetupGet(item => item.IsSupported).Returns(false);
        Mock<IServerConnection> restarted = Connection("2.0.0");
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        connections.SetupSequence(item => item.Open(saved, It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object).ThrowsAsync(new RemoteException("down")).ReturnsAsync(restarted.Object);
        MainViewModel model = Create();
        await model.Start();
        Assert.IsType<VersionMismatchViewModel>(model.Current);
        UpdateViewModel update = ((VersionMismatchViewModel)model.Current!).Update;
        await eventually.Be(() => update.IsAvailable);

        update.BeginCommand.Execute(null);
        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.Phase == UpdatePhase.Waiting);

        Assert.Same(update, model.Current);
        connection.Verify(item => item.UpdateServer(It.IsAny<CancellationToken>()), Times.Once);

        connection.Raise(item => item.Lost += null, "The server closed the connection.");
        await eventually.Be(() => model.Current is HomeViewModel);

        Assert.Equal(UpdatePhase.Idle, update.Phase);
        connections.Verify(item => item.Open(saved, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GivesUpWaitingForAServerThatDoesNotComeBackAfterAnUpdate()
    {
        info.SetupGet(item => item.Version).Returns("2.0.0");
        connection.SetupGet(item => item.ServerVersion).Returns("1.0.0");
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(new ReleaseInfo { Version = AppVersion.TryParse("2.0.0", out AppVersion? latest) ? latest : null!, Assets = [] });
        updater.SetupGet(item => item.IsSupported).Returns(false);
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        int opens = 0;
        connections.Setup(item => item.Open(saved, It.IsAny<CancellationToken>())).Returns(() => ++opens == 1 ? Task.FromResult(connection.Object) : Task.FromException<IServerConnection>(new RemoteException("down")));
        MainViewModel model = new(info.Object, connections.Object, store.Object, launcher.Object, feed.Object, updater.Object, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(80));
        await model.Start();
        UpdateViewModel update = ((VersionMismatchViewModel)model.Current!).Update;
        await eventually.Be(() => update.IsAvailable);
        update.BeginCommand.Execute(null);
        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.Phase == UpdatePhase.Waiting);

        connection.Raise(item => item.Lost += null, "The server closed the connection.");
        await eventually.Be(() => model.Current is ConnectViewModel);

        Assert.StartsWith("The server did not come back after the update.", ((ConnectViewModel)model.Current!).Error);
        Assert.Equal(UpdatePhase.Idle, update.Phase);
    }
}
