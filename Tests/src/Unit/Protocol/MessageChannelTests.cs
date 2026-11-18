namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class MessageChannelTests
{
    private readonly SocketPair sockets = new();

    [Fact]
    public async Task SendsAndReceivesAMessage()
    {
        IMessageChannel client = new IMessageChannel.Factory().Create(sockets.Client);
        IMessageChannel server = new IMessageChannel.Factory().Create(sockets.Server);

        await client.Send(new RemoteRequest { Id = 7, Command = RemoteCommand.Prompt, Text = "Hello" }, CancellationToken.None);
        RemoteRequest? received = await server.Receive<RemoteRequest>(1024, CancellationToken.None);

        Assert.Equal(new RemoteRequest { Id = 7, Command = RemoteCommand.Prompt, Text = "Hello" }, received);
    }

    [Fact]
    public async Task WritesCamelCaseNamesAndOmitsNulls()
    {
        IMessageChannel client = new IMessageChannel.Factory().Create(sockets.Client);

        await client.Send(new RemoteRequest { Id = 1, Command = RemoteCommand.GetHistory }, CancellationToken.None);
        byte[] buffer = new byte[256];
        ValueWebSocketReceiveResult result = await sockets.Server.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);

        Assert.Equal("""{"id":1,"command":"GetHistory"}""", Encoding.UTF8.GetString(buffer, 0, result.Count));
    }

    [Fact]
    public async Task ReadsAMessageSplitOverSeveralFrames()
    {
        IMessageChannel server = new IMessageChannel.Factory().Create(sockets.Server);
        string json = """{"id":3,"command":"Prompt","text":"split"}""";

        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes(json[..10]), WebSocketMessageType.Text, false, CancellationToken.None);
        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes(json[10..]), WebSocketMessageType.Text, true, CancellationToken.None);

        Assert.Equal("split", (await server.Receive<RemoteRequest>(1024, CancellationToken.None))?.Text);
    }

    [Fact]
    public async Task RejectsAMessageOverTheLimit()
    {
        IMessageChannel server = new IMessageChannel.Factory().Create(sockets.Server);
        await sockets.Client.SendAsync(new byte[2000].Select(_ => (byte)'a').ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidDataException>(() => server.Receive<RemoteRequest>(1024, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsInvalidJson()
    {
        IMessageChannel server = new IMessageChannel.Factory().Create(sockets.Server);
        await sockets.Client.SendAsync(Encoding.UTF8.GetBytes("not json"), WebSocketMessageType.Text, true, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidDataException>(() => server.Receive<RemoteRequest>(1024, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsBinaryMessages()
    {
        IMessageChannel server = new IMessageChannel.Factory().Create(sockets.Server);
        await sockets.Client.SendAsync(new byte[] { 1, 2, 3 }, WebSocketMessageType.Binary, true, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidDataException>(() => server.Receive<RemoteRequest>(1024, CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsNullWhenThePeerCloses()
    {
        IMessageChannel server = new IMessageChannel.Factory().Create(sockets.Server);
        await sockets.Client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

        Assert.Null(await server.Receive<RemoteRequest>(1024, CancellationToken.None));
    }

    [Fact]
    public async Task SendsConcurrentMessagesWithoutMixingThem()
    {
        IMessageChannel client = new IMessageChannel.Factory().Create(sockets.Client);
        IMessageChannel server = new IMessageChannel.Factory().Create(sockets.Server);

        Task sending = Task.WhenAll(Enumerable.Range(1, 20).Select(id => client.Send(new RemoteRequest { Id = id, Command = RemoteCommand.Prompt, Text = new string('x', 5000) }, CancellationToken.None)));
        List<int> ids = [];

        for (int count = 0; count < 20; count++)
        {
            ids.Add((await server.Receive<RemoteRequest>(10_000, CancellationToken.None))!.Id);
        }

        await sending;
        Assert.Equal(Enumerable.Range(1, 20), ids.Order());
    }
}
