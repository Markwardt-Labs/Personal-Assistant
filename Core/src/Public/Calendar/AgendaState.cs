namespace Markwardt.PersonalAssistant;

/// <summary>
/// What the events tab shows: the user's calendar over a window of months, the events suggested for the local area, and what the server is doing about the next suggestions.
/// </summary>
public sealed record AgendaState
{
    /// <summary>
    /// Gets the start of the window the calendar events cover.
    /// </summary>
    public required DateTimeOffset From { get; init; }

    /// <summary>
    /// Gets the end of the window the calendar events cover.
    /// </summary>
    public required DateTimeOffset To { get; init; }

    /// <summary>
    /// Gets the events of the calendar in the window, as the server last read them, earliest first.
    /// </summary>
    public required IReadOnlyList<CalendarEvent> Events { get; init; }

    /// <summary>
    /// Gets the event feeds the suggestions are searched for.
    /// </summary>
    public required IReadOnlyList<EventFeed> Feeds { get; init; }

    /// <summary>
    /// Gets the suggested events that have not ended and whose feed still exists, earliest first.
    /// </summary>
    public required IReadOnlyList<SuggestedEvent> Suggestions { get; init; }

    /// <summary>
    /// Gets when the suggestions were made, or <see langword="null" /> when none were made yet.
    /// </summary>
    public DateTimeOffset? SuggestionsGenerated { get; init; }

    /// <summary>
    /// Gets a value indicating whether new suggestions are being made.
    /// </summary>
    public required bool Generating { get; init; }

    /// <summary>
    /// Gets why the last attempt to make suggestions failed, or <see langword="null" /> when it did not.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets why the calendar could not be read the last time, or <see langword="null" /> when it could.
    /// </summary>
    public string? CalendarError { get; init; }
}
