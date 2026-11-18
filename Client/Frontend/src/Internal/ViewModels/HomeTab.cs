namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The tabs of the home screen, in the order they are shown.
/// </summary>
internal enum HomeTab
{
    /// <summary>
    /// The mail of the last seven days as topics.
    /// </summary>
    Mail,

    /// <summary>
    /// The calendar and the suggested events.
    /// </summary>
    Events,

    /// <summary>
    /// The news of the feeds.
    /// </summary>
    News,

    /// <summary>
    /// The conversation with the assistant, where the user gives direct instructions.
    /// </summary>
    Chat,
}
