namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class ConnectViewModelTests
{
    public ConnectViewModelTests()
    {
        info.SetupGet(item => item.Name).Returns("Personal Assistant");
        info.SetupGet(item => item.Version).Returns("1.2.3");
    }

    private readonly Mock<IServerConnection.IFactory> connections = new();

    private readonly Mock<ISettingsStore> store = new();

    private readonly Mock<IAppInfo> info = new();

    private readonly Mock<IServerConnection> connection = new();

    private IServerConnection? opened;

    private ConnectionSettings? openedWith;

    private ConnectViewModel Create(ConnectionSettings? saved = null)
        => new(info.Object, connections.Object, store.Object, saved, (connected, settings) =>
    {
        opened = connected;
        openedWith = settings;
    });

    [Fact]
    public void StartsWithTheSavedSettings()
    {
        ConnectViewModel model = Create(new() { Host = "pc", Port = 1234, AccessKey = "key" });

        Assert.Equal(("pc", "1234", "key"), (model.Host, model.Port, model.AccessKey));
        Assert.Equal("Version 1.2.3", model.Version);
        Assert.Equal("Personal Assistant", model.Title);
    }

    [Fact]
    public void StartsWithTheDefaultPortAndNothingElse()
    {
        ConnectViewModel model = Create();

        Assert.Equal(("", "52680", ""), (model.Host, model.Port, model.AccessKey));
    }

    [Theory]
    [InlineData("", "52680", "key")]
    [InlineData("pc", "abc", "key")]
    [InlineData("pc", "0", "key")]
    [InlineData("pc", "70000", "key")]
    [InlineData("pc", "52680", "")]
    public async Task RefusesIncompleteSettingsWithoutConnecting(string host, string port, string key)
    {
        ConnectViewModel model = Create();
        (model.Host, model.Port, model.AccessKey) = (host, port, key);

        await model.Connect();

        Assert.True(model.HasError);
        Assert.Null(opened);
        connections.Verify(item => item.Open(It.IsAny<ConnectionSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConnectsSavesTheSettingsAndHandsOverTheConnection()
    {
        ConnectionSettings expected = new() { Host = "pc", Port = 52680, AccessKey = "key" };
        connections.Setup(item => item.Open(expected, It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        ConnectViewModel model = Create();
        (model.Host, model.AccessKey) = (" pc ", "key");

        await model.Connect();

        Assert.Same(connection.Object, opened);
        Assert.Equal(expected, openedWith);
        store.Verify(item => item.Save(expected, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(model.HasError);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task ShowsWhyTheServerCouldNotBeReachedAndSavesNothing()
    {
        connections.Setup(item => item.Open(It.IsAny<ConnectionSettings>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The server did not accept the access key."));
        ConnectViewModel model = Create();
        (model.Host, model.AccessKey) = ("pc", "wrong");

        await model.Connect();

        Assert.Equal("The server did not accept the access key.", model.Error);
        Assert.Null(opened);
        store.Verify(item => item.Save(It.IsAny<ConnectionSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ShowsWhyAnOpenConnectionEnded()
    {
        ConnectViewModel model = Create();

        model.Fail("The server closed the connection.");

        Assert.True(model.HasError);
        Assert.Equal("The server closed the connection.", model.Error);
    }
}
