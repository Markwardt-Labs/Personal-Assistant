namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// One like or dislike of the user as the server keeps it.
/// </summary>
internal sealed record StoredReaction
{
    /// <summary>
    /// Gets what kind of entry it was about.
    /// </summary>
    public required ReactionTarget Target { get; init; }

    /// <summary>
    /// Gets what identifies the entry across searches.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the title of the entry.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets what else describes the entry, such as its feed, its sender or its category.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets what the user thought of it.
    /// </summary>
    public required ReactionKind Kind { get; init; }

    /// <summary>
    /// Gets when the user reacted.
    /// </summary>
    public required DateTimeOffset Time { get; init; }
}
