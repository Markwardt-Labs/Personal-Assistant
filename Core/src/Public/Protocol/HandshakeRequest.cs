namespace Markwardt.PersonalAssistant;

/// <summary>
/// The first message of a client, which carries the access key.
/// </summary>
public sealed record HandshakeRequest
{
    /// <summary>
    /// Gets the access key the client was configured with.
    /// </summary>
    public string? AccessKey { get; init; }
}
