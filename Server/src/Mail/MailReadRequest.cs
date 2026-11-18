namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI is asked when it reads one message: the task and the identifier of the message, written to it as JSON.
/// </summary>
internal sealed record MailReadRequest
{
    /// <summary>
    /// Gets what to do and how, which is the instruction of the request.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Gets the identifier of the message, as the Gmail tools gave it.
    /// </summary>
    public required string MessageId { get; init; }
}
