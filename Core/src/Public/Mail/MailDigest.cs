namespace Markwardt.PersonalAssistant;

/// <summary>
/// The mail of the last seven days grouped into topics, most important first.
/// </summary>
public sealed record MailDigest
{
    /// <summary>
    /// Gets when the digest was made.
    /// </summary>
    public required DateTimeOffset Generated { get; init; }

    /// <summary>
    /// Gets the topics, ordered by importance and then by the newest message.
    /// </summary>
    public required IReadOnlyList<MailTopic> Topics { get; init; }
}
