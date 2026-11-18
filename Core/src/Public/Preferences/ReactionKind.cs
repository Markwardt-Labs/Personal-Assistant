namespace Markwardt.PersonalAssistant;

/// <summary>
/// What the user thinks of an entry they were shown.
/// </summary>
public enum ReactionKind
{
    /// <summary>
    /// The user has not reacted, or took the reaction back.
    /// </summary>
    None,

    /// <summary>
    /// The user likes it: more like this.
    /// </summary>
    Like,

    /// <summary>
    /// The user dislikes it: less like this.
    /// </summary>
    Dislike,
}
