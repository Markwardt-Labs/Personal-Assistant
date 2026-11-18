namespace Markwardt.PersonalAssistant;

/// <summary>
/// A named description of what to search for when suggesting events in the local area, written by the user, such as <c>Jazz</c>: <c>live jazz nights</c>. The server only suggests events that relate to a feed.
/// </summary>
public sealed record EventFeed
{
    /// <summary>
    /// Gets the identifier of the feed.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the name the user gave the feed, under which its suggestions are labeled.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets what to search for.
    /// </summary>
    public required string Description { get; init; }
}
