namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// One client's view of the event stream.
/// </summary>
internal interface IEventSubscription : IDisposable
{
    /// <summary>
    /// Gets the events published since the subscription started, which completes when it is disposed.
    /// </summary>
    ChannelReader<RemoteEvent> Events { get; }
}

/// <inheritdoc />
internal sealed class EventSubscription(EventHub hub) : IEventSubscription
{
    private readonly Channel<RemoteEvent> channel = Channel.CreateUnbounded<RemoteEvent>();

    /// <inheritdoc />
    public ChannelReader<RemoteEvent> Events => channel.Reader;

    /// <summary>
    /// Queues an event for the subscriber.
    /// </summary>
    /// <param name="remoteEvent">The event.</param>
    public void Add(RemoteEvent remoteEvent)
        => channel.Writer.TryWrite(remoteEvent);

    /// <inheritdoc />
    public void Dispose()
    {
        hub.Remove(this);
        channel.Writer.TryComplete();
    }
}
