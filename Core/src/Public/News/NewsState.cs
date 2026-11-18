namespace Markwardt.PersonalAssistant;

/// <summary>
/// What the news tab shows: the feeds, the entries found for them, and what the server is doing about the next search.
/// </summary>
public sealed record NewsState
{
    /// <summary>
    /// Gets the feeds in the order the user added them.
    /// </summary>
    public required IReadOnlyList<NewsFeed> Feeds { get; init; }

    /// <summary>
    /// Gets the entries of the feeds that exist, newest first within each feed.
    /// </summary>
    public required IReadOnlyList<NewsItem> Items { get; init; }

    /// <summary>
    /// Gets when the news was last searched for, or <see langword="null" /> when it never was.
    /// </summary>
    public DateTimeOffset? Generated { get; init; }

    /// <summary>
    /// Gets a value indicating whether a search is running.
    /// </summary>
    public required bool Generating { get; init; }

    /// <summary>
    /// Gets why the last search failed, or <see langword="null" /> when it did not.
    /// </summary>
    public string? Error { get; init; }
}
