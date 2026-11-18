namespace Markwardt.PersonalAssistant;

/// <summary>
/// One request of a client, sent as one JSON message.
/// </summary>
public sealed record RemoteRequest
{
    /// <summary>
    /// Gets the number the client chose, echoed in the response.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets what the client asks for.
    /// </summary>
    public required RemoteCommand Command { get; init; }

    /// <summary>
    /// Gets the prompt, for <see cref="RemoteCommand.Prompt" />.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets the most messages to read, for <see cref="RemoteCommand.GetHistory" />; the server picks a default when it is left out.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Gets a value indicating whether to make a new digest even when one exists, for <see cref="RemoteCommand.GetMailDigest" />.
    /// </summary>
    public bool? Refresh { get; init; }

    /// <summary>
    /// Gets the identifier of the message, for <see cref="RemoteCommand.ReadMail" />.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Gets the identifier of the event feed, for <see cref="RemoteCommand.RemoveEventFeed" />.
    /// </summary>
    public string? FeedId { get; init; }

    /// <summary>
    /// Gets the name of the feed, for <see cref="RemoteCommand.AddNewsFeed" /> and <see cref="RemoteCommand.AddEventFeed" />; its description is <see cref="Text" />.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets what kind of entry is reacted to, for <see cref="RemoteCommand.React" />.
    /// </summary>
    public ReactionTarget? Target { get; init; }

    /// <summary>
    /// Gets what identifies the entry across searches (a news address, a title), for <see cref="RemoteCommand.React" />.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Gets the title of the entry, for <see cref="RemoteCommand.React" />.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets what else describes the entry (its feed, its sender, its category), for <see cref="RemoteCommand.React" />.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets what the user thinks of the entry, for <see cref="RemoteCommand.React" />.
    /// </summary>
    public ReactionKind? Reaction { get; init; }

    /// <summary>
    /// Gets the new settings, for <see cref="RemoteCommand.SetSettings" />.
    /// </summary>
    public ServerSettingsUpdate? Settings { get; init; }
}
