namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI is asked when it reads the calendar: the task and the period, written to it as JSON.
/// </summary>
internal sealed record CalendarRequest
{
    /// <summary>
    /// Gets what to do and how, which is the instruction of the request.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Gets the start of the period.
    /// </summary>
    public required DateTimeOffset From { get; init; }

    /// <summary>
    /// Gets the end of the period.
    /// </summary>
    public required DateTimeOffset To { get; init; }
}
