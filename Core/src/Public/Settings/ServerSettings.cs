namespace Markwardt.PersonalAssistant;

/// <summary>
/// What the user sets about the server from the client; the server keeps it, and only how to reach the server lives in its own config.
/// </summary>
public sealed record ServerSettings
{
    /// <summary>
    /// Gets where the user lives, such as <c>Austin, Texas</c>, which the suggested events are searched near, or <see langword="null" /> when it is not set.
    /// </summary>
    public string? Location { get; init; }
}
