namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI is asked when it looks for events: the task and the facts it needs, written to it as JSON.
/// </summary>
internal sealed record EventSuggestionRequest
{
    /// <summary>
    /// Gets what to do and how, which is the instruction of the request.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Gets where the user lives.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the feeds: descriptions of what to search for. Only events that relate to a feed may be listed.
    /// </summary>
    public required IReadOnlyList<EventFeed> Feeds { get; init; }

    /// <summary>
    /// Gets the first moment an event may start.
    /// </summary>
    public required DateTimeOffset From { get; init; }

    /// <summary>
    /// Gets the last moment an event may start.
    /// </summary>
    public required DateTimeOffset To { get; init; }

    /// <summary>
    /// Gets when the user is busy.
    /// </summary>
    public required IReadOnlyList<BusyTime> BusyTimes { get; init; }
}
