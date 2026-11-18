namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI is asked when it searches the news: the task and the feeds, written to it as JSON.
/// </summary>
internal sealed record NewsRequest
{
    /// <summary>
    /// Gets what to do and how, which is the instruction of the request.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Gets the feeds to search for.
    /// </summary>
    public required IReadOnlyList<NewsFeed> Feeds { get; init; }

    /// <summary>
    /// Gets the earliest publication time an article may have.
    /// </summary>
    public required DateTimeOffset Since { get; init; }
}
