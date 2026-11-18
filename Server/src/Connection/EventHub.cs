namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Hands the events of the server to every connected client, in order, each client from a queue of its own so a slow one delays only itself.
/// </summary>
internal interface IEventHub
{
    /// <summary>
    /// Sends an event to every subscriber.
    /// </summary>
    /// <param name="remoteEvent">The event.</param>
    void Publish(RemoteEvent remoteEvent);

    /// <summary>
    /// Starts receiving the events published from now on.
    /// </summary>
    /// <returns>The subscription, which stops receiving when disposed.</returns>
    IEventSubscription Subscribe();
}

/// <inheritdoc />
internal sealed class EventHub : IEventHub
{
    private readonly List<EventSubscription> subscriptions = [];

    private readonly Lock gate = new();

    /// <inheritdoc />
    public void Publish(RemoteEvent remoteEvent)
    {
        lock (gate)
        {
            foreach (EventSubscription subscription in subscriptions)
            {
                subscription.Add(remoteEvent);
            }
        }
    }

    /// <inheritdoc />
    public IEventSubscription Subscribe()
    {
        EventSubscription subscription = new(this);

        lock (gate)
        {
            subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Removes a subscription that was disposed.
    /// </summary>
    /// <param name="subscription">The subscription.</param>
    internal void Remove(EventSubscription subscription)
    {
        lock (gate)
        {
            subscriptions.Remove(subscription);
        }
    }
}
