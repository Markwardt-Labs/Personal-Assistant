namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Runs a refresh once a day: at once when nothing was ever made, then a day after the last success, and an hour after an attempt that did not make anything new.
/// </summary>
internal interface IDailySchedule
{
    /// <summary>
    /// Keeps refreshing until cancelled.
    /// </summary>
    /// <param name="made">Says when the newest result was made, or <see langword="null" /> when there is none.</param>
    /// <param name="refresh">Makes a new result.</param>
    /// <param name="cancellation">Ends the schedule.</param>
    /// <returns>A task that completes when the schedule ends.</returns>
    Task Run(Func<DateTimeOffset?> made, Func<Task> refresh, CancellationToken cancellation = default);
}

/// <inheritdoc />
/// <param name="time">The clock.</param>
/// <param name="interval">How long after a result the next one is made.</param>
/// <param name="retry">How long to wait after an attempt that made nothing new.</param>
internal sealed class DailySchedule(TimeProvider time, TimeSpan interval, TimeSpan retry) : IDailySchedule
{
    /// <inheritdoc />
    public async Task Run(Func<DateTimeOffset?> made, Func<Task> refresh, CancellationToken cancellation = default)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                TimeSpan wait = made() is { } last ? last + interval - time.GetUtcNow() : TimeSpan.Zero;

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, time, cancellation);
                }

                await refresh();

                if (made() is not { } latest || latest + interval <= time.GetUtcNow())
                {
                    await Task.Delay(retry, time, cancellation);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
