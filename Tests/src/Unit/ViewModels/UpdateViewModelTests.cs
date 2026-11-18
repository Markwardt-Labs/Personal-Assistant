namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class UpdateViewModelTests
{
    public UpdateViewModelTests()
    {
        info.SetupGet(item => item.Version).Returns("1.0.0");
        connection.SetupGet(item => item.ServerVersion).Returns("1.0.0");
        updater.SetupGet(item => item.IsSupported).Returns(true);
    }

    private readonly Mock<IAppInfo> info = new();

    private readonly Mock<IReleaseFeed> feed = new();

    private readonly Mock<IClientUpdater> updater = new();

    private readonly Mock<IServerConnection> connection = new();

    private readonly Eventually eventually = new();

    private ReleaseInfo Release(string version)
        => new() { Version = AppVersion.TryParse(version, out AppVersion? parsed) ? parsed : null!, Assets = [] };

    private UpdateViewModel Create(IClientUpdater? clientUpdater = null, TimeSpan? interval = null)
        => new(info.Object, feed.Object, clientUpdater ?? updater.Object, interval);

    private async Task<UpdateViewModel> Available(string version = "2.0.0", string? server = "1.0.0", bool clientSupported = true)
    {
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(Release(version));
        connection.SetupGet(item => item.ServerVersion).Returns(server);
        updater.SetupGet(item => item.IsSupported).Returns(clientSupported);
        UpdateViewModel update = Create();
        update.Attach(connection.Object);
        await eventually.Be(() => update.Latest is not null);
        return update;
    }

    [Fact]
    public async Task OffersAnUpdateWhenTheReleaseIsNewerThanTheClientAndTheServer()
    {
        UpdateViewModel update = await Available();

        Assert.True(update.IsAvailable);
        Assert.True(update.ClientNeedsUpdate);
        Assert.True(update.ServerNeedsUpdate);
        Assert.Equal("Version 2.0.0 replaces 1.0.0 on this phone and 1.0.0 on the server.", update.Summary);
    }

    [Fact]
    public async Task OffersNothingWhenBothAreUpToDate()
    {
        UpdateViewModel update = await Available("1.0.0");

        Assert.False(update.IsAvailable);
        Assert.Equal("This app and its server are up to date.", update.Summary);
        update.BeginCommand.Execute(null);
        Assert.Equal(UpdatePhase.Idle, update.Phase);
    }

    [Fact]
    public async Task OffersOnlyTheServerWhenTheClientCannotUpdateItself()
    {
        UpdateViewModel update = await Available(clientSupported: false);

        Assert.False(update.ClientNeedsUpdate);
        Assert.True(update.ServerNeedsUpdate);
        Assert.Equal("Version 2.0.0 replaces 1.0.0 on the server.", update.Summary);
    }

    [Fact]
    public async Task OffersTheUpdateForAServerThatDoesNotSayWhichVersionItIs()
    {
        UpdateViewModel update = await Available("1.0.0", null);

        Assert.True(update.ServerNeedsUpdate);
        Assert.False(update.ClientNeedsUpdate);
    }

    [Fact]
    public async Task ShowsTheVersionsOfTheAppAndItsServer()
    {
        UpdateViewModel update = await Available();

        Assert.Equal("This app 1.0.0, server 1.0.0.", update.Versions);
    }

    [Fact]
    public async Task AsksForConfirmationAndCancels()
    {
        UpdateViewModel update = await Available();

        update.BeginCommand.Execute(null);

        Assert.True(update.IsOpen);
        Assert.Equal("Cancel", update.CloseText);
        update.CloseCommand.Execute(null);
        Assert.Equal(UpdatePhase.Idle, update.Phase);
    }

    [Fact]
    public async Task UpdatesTheServerFirstThenTheClientAndEndsWithTheInstaller()
    {
        List<string> order = [];
        connection.Setup(item => item.UpdateServer(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Callback(() => order.Add("server"));
        updater.Setup(item => item.Install(It.IsAny<ReleaseInfo>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Callback<ReleaseInfo, IProgress<double>, CancellationToken>((_, progress, _) =>
        {
            order.Add("client");
            progress.Report(0.5);
        });
        UpdateViewModel update = await Available();
        update.BeginCommand.Execute(null);

        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.Phase == UpdatePhase.Finished);

        Assert.Equal(["server", "client"], order);
        Assert.True(update.IsUpdating);
        Assert.True(update.CanClose);
        Assert.Equal("The installer of the system finishes the update.", update.Status);
    }

    [Fact]
    public async Task OnlyUpdatesTheServerWhenTheClientIsCurrentAndWaitsForItToRestart()
    {
        info.SetupGet(item => item.Version).Returns("2.0.0");
        UpdateViewModel update = await Available();
        update.BeginCommand.Execute(null);

        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.Phase == UpdatePhase.Waiting);

        updater.Verify(item => item.Install(It.IsAny<ReleaseInfo>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(update.IsWorking);
        update.ServerBack();
        Assert.Equal(UpdatePhase.Idle, update.Phase);
    }

    [Fact]
    public async Task CannotBeClosedWhileItIsWorking()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Setup(item => item.UpdateServer(It.IsAny<CancellationToken>())).Returns(release.Task);
        UpdateViewModel update = await Available();
        update.BeginCommand.Execute(null);
        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.Phase == UpdatePhase.Working);

        update.CloseCommand.Execute(null);

        Assert.Equal(UpdatePhase.Working, update.Phase);
        Assert.False(update.CanClose);
        release.SetResult();
    }

    [Fact]
    public async Task ReturnsToTheConfirmationWithTheErrorWhenAStepFails()
    {
        updater.Setup(item => item.Install(It.IsAny<ReleaseInfo>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Release 2.0.0 has no PersonalAssistant-android.apk file."));
        UpdateViewModel update = await Available();
        update.BeginCommand.Execute(null);

        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.Phase == UpdatePhase.Confirming && update.HasError);

        Assert.Equal("Release 2.0.0 has no PersonalAssistant-android.apk file.", update.Error);
    }

    [Fact]
    public async Task ExplainsAServerThatIsTooOldToUpdateItself()
    {
        connection.SetupGet(item => item.ServerVersion).Returns((string?)null);
        connection.Setup(item => item.UpdateServer(It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("That command is not known."));
        UpdateViewModel update = await Available("1.0.0", null);
        update.BeginCommand.Execute(null);

        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.HasError);

        Assert.Contains("too old to update itself", update.Error);
        updater.Verify(item => item.Install(It.IsAny<ReleaseInfo>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChecksNowAndSaysWhatItFound()
    {
        UpdateViewModel update = await Available();

        update.CheckCommand.Execute(null);
        await eventually.Be(() => update.CheckResult == "Version 2.0.0 is available.");

        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(Release("1.0.0"));
        update.CheckCommand.Execute(null);
        await eventually.Be(() => update.CheckResult == "Up to date. The newest release is 1.0.0.");
        Assert.True(update.HasCheckResult);
    }

    [Fact]
    public async Task SaysWhenThereIsNoReleaseOrItCannotBeLookedUp()
    {
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync((ReleaseInfo?)null);
        UpdateViewModel update = Create();
        update.Attach(connection.Object);
        update.CheckCommand.Execute(null);
        await eventually.Be(() => update.CheckResult == "No release was found on GitHub.");

        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("GitHub did not answer in time."));
        update.CheckCommand.Execute(null);
        await eventually.Be(() => update.HasCheckError);

        Assert.Equal("The newest release could not be looked up: GitHub did not answer in time.", update.CheckResult);
    }

    [Fact]
    public async Task LooksAgainAtIntervals()
    {
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(Release("1.0.0"));
        using UpdateViewModel update = Create(interval: TimeSpan.FromMilliseconds(20));

        Task watching = update.Watch();
        await eventually.Be(() => feed.Invocations.Count >= 3);

        Assert.False(watching.IsCompleted);
    }

    [Fact]
    public async Task StopsLookingWhenDisposed()
    {
        feed.Setup(item => item.Latest(It.IsAny<CancellationToken>())).ReturnsAsync(Release("1.0.0"));
        UpdateViewModel update = Create(interval: TimeSpan.FromMilliseconds(20));
        Task watching = update.Watch();

        update.Dispose();

        await watching.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DoesNothingWithoutAFeed()
    {
        UpdateViewModel update = new(info.Object, null, null);
        update.Attach(connection.Object);

        await update.Check();

        Assert.False(update.IsAvailable);
        Assert.Null(update.Latest);
    }

    [Fact]
    public async Task AbandonsAnUpdateThatWaitsForAServerThatNeverComesBack()
    {
        info.SetupGet(item => item.Version).Returns("2.0.0");
        UpdateViewModel update = await Available();
        update.BeginCommand.Execute(null);
        update.ConfirmCommand.Execute(null);
        await eventually.Be(() => update.Phase == UpdatePhase.Waiting);

        update.Abandon();

        Assert.Equal(UpdatePhase.Idle, update.Phase);
    }
}
