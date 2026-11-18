namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The AI session of the assistant as the server remembers it: its identifier.
/// </summary>
internal sealed record StoredSession
{
    /// <summary>
    /// Gets the identifier of the session.
    /// </summary>
    public required string Id { get; init; }
}
