namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The suggested events as kept in the data folder.
/// </summary>
internal sealed record StoredSuggestions
{
    /// <summary>
    /// Gets when the suggestions were made.
    /// </summary>
    public required DateTimeOffset Generated { get; init; }

    /// <summary>
    /// Gets the suggestions, earliest first.
    /// </summary>
    public required IReadOnlyList<SuggestedEvent> Events { get; init; }
}
