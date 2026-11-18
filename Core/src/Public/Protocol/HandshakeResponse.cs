namespace Markwardt.PersonalAssistant;

/// <summary>
/// The reply of the server to a <see cref="HandshakeRequest" />.
/// </summary>
public sealed record HandshakeResponse
{
    /// <summary>
    /// Gets a value indicating whether the key was correct.
    /// </summary>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Gets the version of the server, sent only with an acceptance.
    /// </summary>
    public string? Version { get; init; }
}
