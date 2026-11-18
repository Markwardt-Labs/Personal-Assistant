namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class DailyScheduleTests
{
    private readonly TimeSpan interval = TimeSpan.FromMilliseconds(200);

    private readonly TimeSpan retry = TimeSpan.FromMilliseconds(60);

    private DailySchedule Create()
        => new(TimeProvider.System, interval, retry);

    [Fact]
    public async Task RefreshesAtOnceWhenNothingWasEverMade()
    {
        using CancellationTokenSource stop = new();
        DateTimeOffset? made = null;
        TaskCompletionSource refreshed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task running = Create().Run(() => made, () =>
        {
            made = DateTimeOffset.UtcNow;
            refreshed.TrySetResult();
            return Task.CompletedTask;
        }, stop.Token);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.CancelAsync();
        await running;

        Assert.NotNull(made);
    }

    [Fact]
    public async Task WaitsUntilAnIntervalAfterTheLastResult()
    {
        using CancellationTokenSource stop = new();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset? made = start;
        TaskCompletionSource<DateTimeOffset> refreshed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task running = Create().Run(() => made, () =>
        {
            made = DateTimeOffset.UtcNow;
            refreshed.TrySetResult(DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }, stop.Token);
        DateTimeOffset at = await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.CancelAsync();
        await running;

        Assert.True(at - start >= interval - TimeSpan.FromMilliseconds(30));
    }

    [Fact]
    public async Task TriesAgainAfterTheRetryWhenNothingNewWasMade()
    {
        using CancellationTokenSource stop = new();
        int attempts = 0;
        TaskCompletionSource third = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task running = Create().Run(() => null, () =>
        {
            if (Interlocked.Increment(ref attempts) == 3)
            {
                third.TrySetResult();
            }

            return Task.CompletedTask;
        }, stop.Token);
        await third.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.CancelAsync();
        await running;

        Assert.True(attempts >= 3);
    }

    [Fact]
    public async Task EndsWhenCancelledWhileWaiting()
    {
        using CancellationTokenSource stop = new();
        DailySchedule schedule = new(TimeProvider.System, TimeSpan.FromDays(1), TimeSpan.FromHours(1));

        Task running = schedule.Run(() => DateTimeOffset.UtcNow, () => Task.CompletedTask, stop.Token);
        await stop.CancelAsync();

        await running.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
