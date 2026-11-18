namespace Markwardt.PersonalAssistant;

/// <summary>
/// An event in the user's local area that the assistant found on the web and suggests going to. It is not in the calendar.
/// </summary>
public sealed record SuggestedEvent
{
    /// <summary>
    /// Gets the identifier of the suggestion within the list it came in.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the identifier of the feed the event relates to.
    /// </summary>
    public required string FeedId { get; init; }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets when the event starts.
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Gets when the event ends, or <see langword="null" /> when that is not known.
    /// </summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>
    /// Gets the venue or address, or <see langword="null" /> when that is not known.
    /// </summary>
    public string? Venue { get; init; }

    /// <summary>
    /// Gets one or two sentences about the event and why it may suit the user.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the web address of the page the event was found on, where the user can read more or buy tickets.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the kind of event, such as music, food or outdoors, or <see langword="null" />.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets what the user thinks of the entry, which the server fills in when it sends it.
    /// </summary>
    public ReactionKind Reaction { get; init; }
}
