namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// One event as the AI proposes it, before the server checks it.
/// </summary>
internal sealed record SuggestedEventDraft
{
    /// <summary>
    /// Gets the identifier of the feed the event relates to, exactly as it was given.
    /// </summary>
    public required string FeedId { get; init; }

    /// <summary>
    /// Gets the title of the event.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets when the event starts, in ISO 8601 with the UTC offset of the place.
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Gets when the event ends, or <see langword="null" /> when the page does not say.
    /// </summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>
    /// Gets the venue or address, or <see langword="null" /> when the page does not say.
    /// </summary>
    public string? Venue { get; init; }

    /// <summary>
    /// Gets one or two sentences about the event and why it may suit the user.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the address of the web page the event was found on.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the kind of event, such as music, food, outdoors, family or arts, or <see langword="null" />.
    /// </summary>
    public string? Category { get; init; }
}
