namespace Markwardt.PersonalAssistant;

/// <summary>
/// Everything the server sends after the handshake: the answer to a request (its <see cref="Id" /> is the one of the request), or an event, which has id zero.
/// </summary>
public sealed record RemoteResponse
{
    /// <summary>
    /// Gets the identifier of the request this answers, or zero for an event.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets why the request failed, or <see langword="null" /> when it succeeded.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the conversation, for a successful <see cref="RemoteCommand.GetHistory" />.
    /// </summary>
    public ChatHistory? History { get; init; }

    /// <summary>
    /// Gets the event, when this message is one.
    /// </summary>
    public RemoteEvent? Event { get; init; }

    /// <summary>
    /// Gets the mail digest, for a successful <see cref="RemoteCommand.GetMailDigest" />.
    /// </summary>
    public MailDigestState? MailDigest { get; init; }

    /// <summary>
    /// Gets the message, for a successful <see cref="RemoteCommand.ReadMail" />.
    /// </summary>
    public MailMessage? Mail { get; init; }

    /// <summary>
    /// Gets the events, for a successful <see cref="RemoteCommand.GetAgenda" />.
    /// </summary>
    public AgendaState? Agenda { get; init; }

    /// <summary>
    /// Gets the news, for a successful <see cref="RemoteCommand.GetNews" />, <see cref="RemoteCommand.AddNewsFeed" /> or <see cref="RemoteCommand.RemoveNewsFeed" />.
    /// </summary>
    public NewsState? News { get; init; }

    /// <summary>
    /// Gets the settings, for a successful <see cref="RemoteCommand.GetSettings" /> or <see cref="RemoteCommand.SetSettings" />.
    /// </summary>
    public ServerSettings? Settings { get; init; }
}
