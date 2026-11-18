namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI answers when it reads one message: the message, or why it could not.
/// </summary>
internal sealed record MailReadResult
{
    /// <summary>
    /// Gets the message, or <see langword="null" /> when it could not be read.
    /// </summary>
    public MailMessage? Message { get; init; }

    /// <summary>
    /// Gets one sentence that says why the message could not be read, or <see langword="null" /> when it was.
    /// </summary>
    public string? Problem { get; init; }
}
