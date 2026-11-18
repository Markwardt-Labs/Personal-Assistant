namespace Markwardt.PersonalAssistant;

/// <summary>
/// A summary entry of the news: what one article, or a few about the same story, tell the user, with a link to read it.
/// </summary>
public sealed record NewsItem
{
    /// <summary>
    /// Gets the identifier of the entry within its list.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the identifier of the feed the entry was found for.
    /// </summary>
    public required string FeedId { get; init; }

    /// <summary>
    /// Gets the headline.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets one or two sentences that say what happened.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the name of the publisher, or <see langword="null" /> when it is not known.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the web address of the article.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets when the article was published, or <see langword="null" /> when the page does not say.
    /// </summary>
    public DateTimeOffset? Published { get; init; }

    /// <summary>
    /// Gets what the user thinks of the entry, which the server fills in when it sends it.
    /// </summary>
    public ReactionKind Reaction { get; init; }
}
