namespace Markwardt.PersonalAssistant;

/// <summary>
/// A message as listed by a search.
/// </summary>
public sealed record MailSummary
{
    /// <summary>
    /// Gets the identifier of the message in the mailbox.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the sender, as the message names them.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Gets the subject.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Gets when it was received.
    /// </summary>
    public required DateTimeOffset Received { get; init; }

    /// <summary>
    /// Gets the start of the body.
    /// </summary>
    public required string Preview { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user has not read it yet.
    /// </summary>
    public bool IsUnread { get; init; }
}
