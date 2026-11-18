namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI answers when it reads the calendar: the events, or why it could not.
/// </summary>
internal sealed record CalendarResult
{
    /// <summary>
    /// Gets the events of the period, each once.
    /// </summary>
    public required IReadOnlyList<CalendarEvent> Events { get; init; }

    /// <summary>
    /// Gets one sentence that says what is wrong when the Google Calendar tools are missing or fail, or <see langword="null" /> when the calendar was read.
    /// </summary>
    public string? Problem { get; init; }
}
