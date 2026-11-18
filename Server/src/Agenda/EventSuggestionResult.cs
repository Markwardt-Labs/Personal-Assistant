namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the AI answers when it looks for events: the events it found.
/// </summary>
internal sealed record EventSuggestionResult
{
    /// <summary>
    /// Gets the events, which must come from pages the AI actually read.
    /// </summary>
    public required IReadOnlyList<SuggestedEventDraft> Events { get; init; }
}
