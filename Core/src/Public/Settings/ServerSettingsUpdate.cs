namespace Markwardt.PersonalAssistant;

/// <summary>
/// The settings the client sends to change.
/// </summary>
public sealed record ServerSettingsUpdate
{
    /// <summary>
    /// Gets where the user lives; blank clears it.
    /// </summary>
    public string? Location { get; init; }
}
