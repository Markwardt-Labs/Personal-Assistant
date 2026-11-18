namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The likes and dislikes of the user as the server keeps them, to mark entries and to know which reactions the agent has not folded into its preference files yet.
/// </summary>
internal sealed record StoredReactions
{
    /// <summary>
    /// Gets the time of the newest reaction the agent has folded into its preference files, or <see langword="null" /> before the first time.
    /// </summary>
    public DateTimeOffset? ProfiledThrough { get; init; }

    /// <summary>
    /// Gets the most recent reactions, oldest first.
    /// </summary>
    public required IReadOnlyList<StoredReaction> Reactions { get; init; }
}
