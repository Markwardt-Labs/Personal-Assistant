namespace Markwardt.PersonalAssistant;

/// <summary>
/// What kind of entry a reaction is about.
/// </summary>
public enum ReactionTarget
{
    /// <summary>
    /// A topic of the mail tab.
    /// </summary>
    Mail,

    /// <summary>
    /// A suggested event of the events tab.
    /// </summary>
    Event,

    /// <summary>
    /// An entry of the news tab.
    /// </summary>
    News,
}
