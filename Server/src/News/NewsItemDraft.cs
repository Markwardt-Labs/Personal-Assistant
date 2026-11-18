namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// One news entry as the AI proposes it, before the server checks it.
/// </summary>
internal sealed record NewsItemDraft
{
    /// <summary>
    /// Gets the identifier of the feed the entry was found for, exactly as it was given.
    /// </summary>
    public required string FeedId { get; init; }

    /// <summary>
    /// Gets the headline.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets one or two sentences that say what happened, in the AI's own words.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the name of the publisher, or <see langword="null" />.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the address of the article page the entry was found on.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets when the article was published, or <see langword="null" /> when the page does not say.
    /// </summary>
    public DateTimeOffset? Published { get; init; }
}
