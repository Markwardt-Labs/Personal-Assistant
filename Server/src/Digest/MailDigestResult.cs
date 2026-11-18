namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI answers when it makes the digest: the topics, or why it could not read the mail.
/// </summary>
internal sealed record MailDigestResult
{
    /// <summary>
    /// Gets the topics, which together hold every message once.
    /// </summary>
    public required IReadOnlyList<MailTopicDraft> Topics { get; init; }

    /// <summary>
    /// Gets one sentence that says what is wrong when the Gmail tools are missing or fail, or <see langword="null" /> when the mail was read.
    /// </summary>
    public string? Problem { get; init; }
}
