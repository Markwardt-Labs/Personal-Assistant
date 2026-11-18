namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The event feeds as kept in the data folder.
/// </summary>
internal sealed record StoredFeeds
{
    /// <summary>
    /// Gets the feeds in the order the user added them.
    /// </summary>
    public required IReadOnlyList<EventFeed> Feeds { get; init; }
}
