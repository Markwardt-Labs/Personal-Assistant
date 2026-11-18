namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class VersionMismatchViewModelTests
{
    public VersionMismatchViewModelTests()
    {
        info.SetupGet(item => item.Version).Returns("1.0.0");
        connection.SetupGet(item => item.ServerVersion).Returns("2.0.0");
        updater.SetupGet(item => item.IsSupported).Returns(true);
    }

    private readonly Mock<IAppInfo> info = new();

    private readonly Mock<IServerConnection> connection = new();

    private readonly Mock<IReleaseFeed> feed = new();

    private readonly Mock<IClientUpdater> updater = new();

    private bool changed;

    private VersionMismatchViewModel Create(UpdateViewModel? update = null)
        => new(info.Object, connection.Object, update ?? new UpdateViewModel(info.Object, feed.Object, updater.Object), () => changed = true);

    [Fact]
    public void NamesBothVersions()
    {
        Assert.Equal("This app is version 1.0.0 and the server is version 2.0.0.", Create().Message);
    }

    [Fact]
    public void NamesAServerThatPredatesVersionChecks()
    {
        connection.SetupGet(item => item.ServerVersion).Returns((string?)null);

        Assert.Equal("This app is version 1.0.0 and the server is from before versions were checked.", Create().Message);
    }

    [Fact]
    public async Task OffersTheUpdateThatWouldMakeThemMatch()
    {
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(new ReleaseInfo { Version = AppVersion.TryParse("2.0.0", out AppVersion? latest) ? latest : null!, Assets = [] });
        UpdateViewModel update = new(info.Object, feed.Object, updater.Object);
        VersionMismatchViewModel mismatch = Create(update);
        update.Attach(connection.Object);
        await new Eventually().Be(() => update.IsAvailable);

        Assert.Equal("Version 2.0.0 replaces 1.0.0 on this phone.", mismatch.Hint);
    }

    [Fact]
    public async Task SaysSoWhenNoNewerReleaseWouldMakeThemMatch()
    {
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(new ReleaseInfo { Version = AppVersion.TryParse("1.0.0", out AppVersion? latest) ? latest : null!, Assets = [] });
        UpdateViewModel update = new(info.Object, feed.Object, updater.Object);
        VersionMismatchViewModel mismatch = Create(update);
        update.Attach(connection.Object);
        await update.Check();

        Assert.Equal("No newer release would make them match.", mismatch.Hint);
    }

    [Fact]
    public async Task SaysWhenTheNewestReleaseCouldNotBeLookedUp()
    {
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("GitHub did not answer in time."));
        UpdateViewModel update = new(info.Object, feed.Object, updater.Object);
        VersionMismatchViewModel mismatch = Create(update);
        update.Attach(connection.Object);
        await update.Check();

        Assert.Equal("The newest release could not be looked up: GitHub did not answer in time.", mismatch.Hint);
    }

    [Fact]
    public void GoesToAnotherServerOnRequest()
    {
        Create().ChangeServerCommand.Execute(null);

        Assert.True(changed);
    }
}
