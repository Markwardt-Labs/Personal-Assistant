namespace Markwardt.PersonalAssistant;

/// <summary>
/// The newest mail digest together with what the server is doing about the next one.
/// </summary>
public sealed record MailDigestState
{
    /// <summary>
    /// Gets the newest digest, or <see langword="null" /> when none was made yet.
    /// </summary>
    public MailDigest? Digest { get; init; }

    /// <summary>
    /// Gets a value indicating whether a new digest is being made.
    /// </summary>
    public required bool Generating { get; init; }

    /// <summary>
    /// Gets why the last attempt to make a digest failed, or <see langword="null" /> when it did not.
    /// </summary>
    public string? Error { get; init; }
}
