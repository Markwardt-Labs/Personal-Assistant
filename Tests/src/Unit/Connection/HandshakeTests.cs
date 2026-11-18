namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class HandshakeTests
{
    public HandshakeTests()
    {
        info.SetupGet(item => item.Version).Returns("1.2.3");
        channel.Setup(item => item.Send(It.IsAny<HandshakeResponse>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Callback<HandshakeResponse, CancellationToken>((response, _) => Sent = response);
    }

    private readonly Mock<IMessageChannel> channel = new();

    private readonly Mock<IAppInfo> info = new();

    private HandshakeResponse? Sent { get; set; }

    private Handshake Create()
        => new(info.Object);

    [Fact]
    public async Task AdmitsTheRightKeyAndReportsTheVersion()
    {
        channel.Setup(item => item.Receive<HandshakeRequest>(1024, It.IsAny<CancellationToken>())).ReturnsAsync(new HandshakeRequest { AccessKey = "secret" });

        Assert.True(await Create().Admit(channel.Object, "secret", CancellationToken.None));
        Assert.Equal(new HandshakeResponse { Accepted = true, Version = "1.2.3" }, Sent);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("")]
    [InlineData(null)]
    public async Task RefusesAWrongOrMissingKeyWithoutTheVersion(string? key)
    {
        channel.Setup(item => item.Receive<HandshakeRequest>(1024, It.IsAny<CancellationToken>())).ReturnsAsync(new HandshakeRequest { AccessKey = key });

        Assert.False(await Create().Admit(channel.Object, "secret", CancellationToken.None));
        Assert.Equal(new HandshakeResponse { Accepted = false }, Sent);
    }

    [Fact]
    public async Task RefusesAClientThatClosedBeforeSendingTheKey()
    {
        channel.Setup(item => item.Receive<HandshakeRequest>(1024, It.IsAny<CancellationToken>())).ReturnsAsync((HandshakeRequest?)null);

        Assert.False(await Create().Admit(channel.Object, "secret", CancellationToken.None));
    }

    [Fact]
    public async Task RefusesAMalformedFirstMessageWithoutAnswering()
    {
        channel.Setup(item => item.Receive<HandshakeRequest>(1024, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidDataException("bad"));

        Assert.False(await Create().Admit(channel.Object, "secret", CancellationToken.None));
        channel.Verify(item => item.Send(It.IsAny<HandshakeResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
