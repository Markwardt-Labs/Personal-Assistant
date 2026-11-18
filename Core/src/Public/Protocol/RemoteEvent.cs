namespace Markwardt.PersonalAssistant;

/// <summary>
/// Something the server pushes to every connected client without being asked.
/// </summary>
public sealed record RemoteEvent
{
    /// <summary>
    /// Gets what happened.
    /// </summary>
    public required RemoteEventKind Kind { get; init; }

    /// <summary>
    /// Gets the added message, for <see cref="RemoteEventKind.MessageAdded" />.
    /// </summary>
    public ChatMessage? Message { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assistant is working, for <see cref="RemoteEventKind.WorkingChanged" />.
    /// </summary>
    public bool? Working { get; init; }

    /// <summary>
    /// Gets the state of the mail digest, for <see cref="RemoteEventKind.MailDigestChanged" />.
    /// </summary>
    public MailDigestState? MailDigest { get; init; }

    /// <summary>
    /// Gets the events, for <see cref="RemoteEventKind.AgendaChanged" />.
    /// </summary>
    public AgendaState? Agenda { get; init; }

    /// <summary>
    /// Gets the news, for <see cref="RemoteEventKind.NewsChanged" />.
    /// </summary>
    public NewsState? News { get; init; }

    /// <summary>
    /// Gets the settings, for <see cref="RemoteEventKind.SettingsChanged" />.
    /// </summary>
    public ServerSettings? Settings { get; init; }
}
