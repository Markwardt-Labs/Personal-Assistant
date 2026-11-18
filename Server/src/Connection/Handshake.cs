namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Lets a client in only when its first message carries the access key.
/// </summary>
internal interface IHandshake
{
    /// <summary>
    /// Reads the client's first message, compares its key in constant time and answers it.
    /// </summary>
    /// <param name="channel">The connection.</param>
    /// <param name="accessKey">The key the server accepts.</param>
    /// <param name="cancellation">Cancels the exchange.</param>
    /// <returns><see langword="true" /> if the client may go on.</returns>
    Task<bool> Admit(IMessageChannel channel, string accessKey, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class Handshake(IAppInfo info) : IHandshake
{
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public async Task<bool> Admit(IMessageChannel channel, string accessKey, CancellationToken cancellation = default)
    {
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeout);
        HandshakeRequest? request;

        try
        {
            request = await channel.Receive<HandshakeRequest>(1024, limit.Token);
        }
        catch (Exception exception) when (exception is InvalidDataException or OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            return false;
        }

        bool accepted = request?.AccessKey is { Length: > 0 } key && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(accessKey)));
        await channel.Send(new HandshakeResponse { Accepted = accepted, Version = accepted ? info.Version : null }, cancellation);
        return accepted;
    }
}
