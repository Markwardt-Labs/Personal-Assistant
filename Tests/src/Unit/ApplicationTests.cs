namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class ApplicationTests
{
    public ApplicationTests()
    {
        info.SetupGet(item => item.Name).Returns("Personal Assistant");
        info.SetupGet(item => item.Version).Returns("1.2.3");
        loader.Setup(item => item.Load(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
    }

    private readonly Mock<IAppInfo> info = new();

    private readonly Mock<IConfigLoader> loader = new();

    private readonly Mock<IConnectionServer> server = new();

    private readonly StringWriter output = new();

    private readonly ServerConfig config = new() { Key = "the-key", Port = 4321, DataFolder = "/data" };

    private ServerConfig? served;

    private Application Create()
        => new(info.Object, loader.Object, given =>
        {
            served = given;
            return server.Object;
        }, output);

    [Fact]
    public async Task RunsTheServerWithTheLoadedConfigAndAnnouncesWhereItListens()
    {
        server.Setup(item => item.Run(config, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Callback(() =>
        {
            server.Raise(item => item.Certified += null, "ABCDEF");
            server.Raise(item => item.Listening += null, 4321);
        });

        int code = await Create().Run(["config.json"], CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Same(config, served);
        Assert.Contains("Certificate SHA-256: ABCDEF", output.ToString());
        Assert.Contains("Personal Assistant 1.2.3 is listening on port 4321. App folder: /data. Access key: the-key", output.ToString());
        loader.Verify(item => item.Load(It.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "config.json" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task ReportsAConfigThatCannotBeReadAndDoesNotStart(Type failure)
    {
        loader.Setup(item => item.Load(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ThrowsAsync((Exception)Activator.CreateInstance(failure, "The config is broken.")!);

        int code = await Create().Run([], CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Contains("The config is broken.", output.ToString());
        Assert.Null(served);
    }

    [Fact]
    public async Task ReportsAServerThatCannotStart()
    {
        server.Setup(item => item.Run(It.IsAny<ServerConfig>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Address already in use."));

        int code = await Create().Run([], CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Contains("The server could not start: Address already in use.", output.ToString());
    }
}
