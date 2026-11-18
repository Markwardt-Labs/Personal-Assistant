namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// One topic as the AI proposes it, before the server checks it.
/// </summary>
internal sealed record MailTopicDraft
{
    /// <summary>
    /// Gets a short title of what the topic is about, at most eight words.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets one or two plain sentences that say what the messages of the topic tell the user; for bulk mail it says how many messages there are and what they offer.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets how much the topic matters to the user.
    /// </summary>
    public required MailImportance Importance { get; init; }

    /// <summary>
    /// Gets the messages of the topic as the Gmail tools list them, each with the identifier the tools gave it; every message belongs to exactly one topic.
    /// </summary>
    public required IReadOnlyList<MailSummary> Messages { get; init; }
}
