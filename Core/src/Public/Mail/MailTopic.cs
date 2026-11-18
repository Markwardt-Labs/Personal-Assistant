namespace Markwardt.PersonalAssistant;

/// <summary>
/// One topic of the mail of the last days: a summary entry that stands for every message about the same thing, so three advertisements of one store are one topic.
/// </summary>
public sealed record MailTopic
{
    /// <summary>
    /// Gets the identifier of the topic within its digest.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets a short title of what the topic is about.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets one or two sentences that say what the messages of the topic tell the user.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets how much the topic matters.
    /// </summary>
    public required MailImportance Importance { get; init; }

    /// <summary>
    /// Gets the messages of the topic, newest first.
    /// </summary>
    public required IReadOnlyList<MailSummary> Messages { get; init; }

    /// <summary>
    /// Gets what the user thinks of the entry, which the server fills in when it sends it.
    /// </summary>
    public ReactionKind Reaction { get; init; }
}
