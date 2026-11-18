namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The news feeds as kept in the data folder.
/// </summary>
internal sealed record StoredNewsFeeds
{
    /// <summary>
    /// Gets the feeds in the order the user added them.
    /// </summary>
    public required IReadOnlyList<NewsFeed> Feeds { get; init; }
}
