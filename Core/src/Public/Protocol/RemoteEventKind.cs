namespace Markwardt.PersonalAssistant;

/// <summary>
/// What a <see cref="RemoteEvent" /> reports.
/// </summary>
public enum RemoteEventKind
{
    /// <summary>
    /// A message was added to the conversation.
    /// </summary>
    MessageAdded,

    /// <summary>
    /// The assistant started or stopped working.
    /// </summary>
    WorkingChanged,

    /// <summary>
    /// The mail digest or the work of making it changed.
    /// </summary>
    MailDigestChanged,

    /// <summary>
    /// The suggested events or the work of making them changed.
    /// </summary>
    AgendaChanged,

    /// <summary>
    /// The news or the work of searching for it changed.
    /// </summary>
    NewsChanged,

    /// <summary>
    /// The settings changed.
    /// </summary>
    SettingsChanged,
}
