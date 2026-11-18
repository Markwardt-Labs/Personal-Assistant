namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The news entries as kept in the data folder.
/// </summary>
internal sealed record StoredNews
{
    /// <summary>
    /// Gets when the news was last searched for.
    /// </summary>
    public required DateTimeOffset Generated { get; init; }

    /// <summary>
    /// Gets the entries.
    /// </summary>
    public required IReadOnlyList<NewsItem> Items { get; init; }
}
