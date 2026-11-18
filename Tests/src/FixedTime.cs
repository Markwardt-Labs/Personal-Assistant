namespace Markwardt.PersonalAssistant.Tests.Unit;

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
        => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
