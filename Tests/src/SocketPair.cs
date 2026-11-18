namespace Markwardt.PersonalAssistant.Tests;

public sealed class SocketPair : IDisposable
{
    public SocketPair()
    {
        Pipe toServer = new();
        Pipe toClient = new();
        Client = WebSocket.CreateFromStream(new PipeStream(toClient.Reader, toServer.Writer), new WebSocketCreationOptions { IsServer = false });
        Server = WebSocket.CreateFromStream(new PipeStream(toServer.Reader, toClient.Writer), new WebSocketCreationOptions { IsServer = true });
    }

    public WebSocket Client { get; }

    public WebSocket Server { get; }

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }
}
