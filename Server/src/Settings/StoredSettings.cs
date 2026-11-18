namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the user set from the client, as the server keeps it.
/// </summary>
internal sealed record StoredSettings
{
    /// <summary>
    /// Gets where the user lives, or <see langword="null" />.
    /// </summary>
    public string? Location { get; init; }
}
