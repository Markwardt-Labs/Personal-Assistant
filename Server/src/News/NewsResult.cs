namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI answers when it searches the news: the entries it found.
/// </summary>
internal sealed record NewsResult
{
    /// <summary>
    /// Gets the entries, which must come from pages the AI actually read.
    /// </summary>
    public required IReadOnlyList<NewsItemDraft> Items { get; init; }
}
