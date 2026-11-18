namespace Markwardt.PersonalAssistant;

/// <summary>
/// A named description of what type of news to search for, written by the user, such as <c>Tech</c>: <c>new AI models and chips</c>. News is only searched for feeds.
/// </summary>
public sealed record NewsFeed
{
    /// <summary>
    /// Gets the identifier of the feed.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the name the user gave the feed, under which its entries are shown.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets what type of news to search for.
    /// </summary>
    public required string Description { get; init; }
}
