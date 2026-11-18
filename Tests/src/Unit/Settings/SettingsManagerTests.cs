namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class SettingsManagerTests
{
    public SettingsManagerTests()
    {
        settings.SetupGet(item => item.Location).Returns("Austin");
    }

    private readonly Mock<ISettingsService> settings = new();

    private readonly EventHub events = new();

    private SettingsManager Create()
        => new(settings.Object, events);

    [Fact]
    public async Task ReadsTheSettingsAfterTheyAreLoaded()
    {
        ServerSettings read = await Create().Get(CancellationToken.None);

        Assert.Equal("Austin", read.Location);
        settings.Verify(item => item.Ready(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangesTheSettingsAndPushesThemToEveryClient()
    {
        ServerSettingsUpdate update = new() { Location = "Dallas" };
        using IEventSubscription subscription = events.Subscribe();

        ServerSettings result = await Create().Update(update, CancellationToken.None);

        settings.Verify(item => item.Update(update, It.IsAny<CancellationToken>()), Times.Once);
        RemoteEvent pushed = await subscription.Events.ReadAsync();
        Assert.Equal(RemoteEventKind.SettingsChanged, pushed.Kind);
        Assert.Equal(result, pushed.Settings);
    }
}
