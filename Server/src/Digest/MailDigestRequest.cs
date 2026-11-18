namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI is asked when it makes the digest: the task and what it needs to find the mail, written to it as JSON.
/// </summary>
internal sealed record MailDigestRequest
{
    /// <summary>
    /// Gets what to do and how, which is the instruction of the request.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Gets the Gmail search that finds the mail of the last seven days.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Gets the most messages the digest covers.
    /// </summary>
    public required int Limit { get; init; }

    /// <summary>
    /// Gets the current time.
    /// </summary>
    public required DateTimeOffset Now { get; init; }
}
