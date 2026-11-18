namespace Markwardt.PersonalAssistant;

/// <summary>
/// An entry of the calendar.
/// </summary>
public sealed record CalendarEvent
{
    /// <summary>
    /// Gets the identifier the AI uses to refer to the event.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets when the event starts.
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Gets when the event ends.
    /// </summary>
    public required DateTimeOffset End { get; init; }

    /// <summary>
    /// Gets where the event takes place, or <see langword="null" /> when that is not known.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Gets free text about the event, or <see langword="null" /> for none.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Gets the email addresses of the people invited, or <see langword="null" /> for none.
    /// </summary>
    public IReadOnlyList<string>? Attendees { get; init; }

    /// <summary>
    /// Gets a value indicating whether the event lasts whole days. <see cref="Start" /> is then the first day at midnight and <see cref="End" /> the day after the last one.
    /// </summary>
    public bool IsAllDay { get; init; }
}
