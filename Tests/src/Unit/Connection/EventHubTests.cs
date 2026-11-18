namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class EventHubTests
{
    private readonly EventHub hub = new();

    private RemoteEvent Working(bool value)
        => new() { Kind = RemoteEventKind.WorkingChanged, Working = value };

    [Fact]
    public async Task DeliversEveryEventToEverySubscriberInOrder()
    {
        using IEventSubscription first = hub.Subscribe();
        using IEventSubscription second = hub.Subscribe();

        hub.Publish(Working(true));
        hub.Publish(Working(false));

        foreach (IEventSubscription subscription in new[] { first, second })
        {
            Assert.Equal(Working(true), await subscription.Events.ReadAsync());
            Assert.Equal(Working(false), await subscription.Events.ReadAsync());
        }
    }

    [Fact]
    public async Task DoesNotDeliverEventsPublishedBeforeSubscribing()
    {
        hub.Publish(Working(true));
        using IEventSubscription subscription = hub.Subscribe();
        hub.Publish(Working(false));

        Assert.Equal(Working(false), await subscription.Events.ReadAsync());
    }

    [Fact]
    public async Task CompletesTheEventsOfADisposedSubscriptionAndStopsDelivering()
    {
        IEventSubscription subscription = hub.Subscribe();
        subscription.Dispose();

        hub.Publish(Working(true));

        Assert.False(await subscription.Events.WaitToReadAsync());
    }
}
