namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The events of the calendar as the server last read them, kept so the events tab has something to show without asking the AI again.
/// </summary>
internal sealed record StoredCalendar
{
    /// <summary>
    /// Gets when the calendar was read.
    /// </summary>
    public required DateTimeOffset Read { get; init; }

    /// <summary>
    /// Gets the events, earliest first.
    /// </summary>
    public required IReadOnlyList<CalendarEvent> Events { get; init; }
}
