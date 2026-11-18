namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// A time when the user already has something in the calendar, given to the AI so that it can prefer suggestions that do not clash. It carries no title or people.
/// </summary>
internal sealed record BusyTime
{
    /// <summary>
    /// Gets when the busy time starts.
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Gets when the busy time ends.
    /// </summary>
    public required DateTimeOffset End { get; init; }
}
