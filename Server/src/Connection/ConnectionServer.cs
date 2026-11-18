namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Accepts secure WebSocket connections and serves the clients that supply the access key.
/// </summary>
internal interface IConnectionServer
{
    /// <summary>
    /// Raised with the SHA-256 fingerprint of the certificate the server presents, before it starts listening.
    /// </summary>
    event Action<string>? Certified;

    /// <summary>
    /// Raised with the bound port once the server is listening.
    /// </summary>
    event Action<int>? Listening;

    /// <summary>
    /// Listens until cancelled.
    /// </summary>
    /// <param name="config">The server settings.</param>
    /// <param name="cancellation">Stops the server.</param>
    Task Run(ServerConfig config, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class ConnectionServer(IHandshake handshake, IMessageChannel.IFactory channels, ICertificateProvider certificates, IRequestHandler handler, IEventHub events, ILog log) : IConnectionServer
{
    private readonly int requestLimit = 1024 * 1024;

    /// <inheritdoc />
    public event Action<string>? Certified;

    /// <inheritdoc />
    public event Action<int>? Listening;

    /// <inheritdoc />
    public async Task Run(ServerConfig config, CancellationToken cancellation = default)
    {
        using X509Certificate2 certificate = await certificates.Load(config, cancellation);
        Certified?.Invoke(Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256)));
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenAnyIP(config.Port, listen => listen.UseHttps(certificate)));
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
        await using WebApplication app = builder.Build();
        using CancellationTokenSource serving = new();
        app.UseWebSockets(new() { KeepAliveInterval = TimeSpan.FromSeconds(25), KeepAliveTimeout = TimeSpan.FromSeconds(20) });
        app.Run(context => Serve(context, config.Key, serving.Token));
        await app.StartAsync(cancellation);
        Listening?.Invoke(new Uri(app.Urls.First()).Port);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellation);
        }
        catch (OperationCanceledException)
        {
        }

        await serving.CancelAsync();
        await app.StopAsync(CancellationToken.None);
    }

    private async Task Serve(HttpContext context, string accessKey, CancellationToken cancellation = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Personal Assistant server", context.RequestAborted);
            return;
        }

        using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
        IMessageChannel channel = channels.Create(socket);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, cancellation);

        try
        {
            if (await handshake.Admit(channel, accessKey, linked.Token))
            {
                await Converse(channel, linked.Token);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or InvalidDataException or IOException)
        {
            log.Info($"A client was disconnected: {exception.Message}");
        }

        await Close(socket);
    }

    private async Task Converse(IMessageChannel channel, CancellationToken cancellation = default)
    {
        using IEventSubscription subscription = events.Subscribe();
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        Task pump = Push(channel, subscription, stopping.Token);

        try
        {
            while (await channel.Receive<RemoteRequest>(requestLimit, stopping.Token) is { } request)
            {
                await channel.Send(await handler.Handle(request, stopping.Token), stopping.Token);
            }
        }
        finally
        {
            await stopping.CancelAsync();
            await pump;
        }
    }

    private async Task Push(IMessageChannel channel, IEventSubscription subscription, CancellationToken cancellation = default)
    {
        try
        {
            await foreach (RemoteEvent remoteEvent in subscription.Events.ReadAllAsync(cancellation))
            {
                await channel.Send(new RemoteResponse { Id = 0, Event = remoteEvent }, cancellation);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
        }
    }

    private async Task Close(WebSocket socket)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        }
    }
}
