namespace Markwardt.PersonalAssistant;

/// <summary>
/// JSON messages over a connected WebSocket, one message per WebSocket text message, so the server and the clients cannot drift apart on framing.
/// </summary>
public interface IMessageChannel
{
    /// <summary>
    /// Writes a message as one JSON text message; writes from several tasks are sent one after the other.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="message">The message to write.</param>
    /// <param name="cancellation">Cancels the write.</param>
    Task Send<T>(T message, CancellationToken cancellation = default);

    /// <summary>
    /// Reads one JSON text message.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="limit">The most bytes the message may have; a peer that has not been let in yet should not make the reader buffer much.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The message, or <see langword="null" /> when the peer closed the connection.</returns>
    /// <exception cref="InvalidDataException">The message is too long, is not text or is not a valid message.</exception>
    Task<T?> Receive<T>(int limit, CancellationToken cancellation = default)
        where T : class;

    /// <summary>
    /// Creates <see cref="IMessageChannel" /> instances.
    /// </summary>
    public interface IFactory
    {
        /// <summary>
        /// Creates a channel over a connected WebSocket.
        /// </summary>
        /// <param name="socket">The connected socket; the channel does not close it.</param>
        /// <returns>The channel.</returns>
        IMessageChannel Create(WebSocket socket);
    }

    /// <inheritdoc />
    public sealed class Factory : IFactory
    {
        /// <inheritdoc />
        public IMessageChannel Create(WebSocket socket)
            => new MessageChannel(socket);
    }
}
