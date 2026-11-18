namespace Markwardt.PersonalAssistant;

/// <summary>
/// What a client asks the server to do.
/// </summary>
public enum RemoteCommand
{
    /// <summary>
    /// Reads the newest messages of the conversation and whether the assistant is working.
    /// </summary>
    GetHistory,

    /// <summary>
    /// Gives the assistant a prompt; the replies arrive as events.
    /// </summary>
    Prompt,

    /// <summary>
    /// Stops what the assistant is doing right now without ending the conversation.
    /// </summary>
    Interrupt,

    /// <summary>
    /// Reads the mail digest and, when it is missing or a refresh is asked for, starts making a new one.
    /// </summary>
    GetMailDigest,

    /// <summary>
    /// Reads one message in full.
    /// </summary>
    ReadMail,

    /// <summary>
    /// Updates the server to the newest release; it restarts when the new version is in place.
    /// </summary>
    UpdateServer,

    /// <summary>
    /// Reads the calendar events and the suggested events of the local area and, when there are no suggestions yet or a refresh is asked for, starts making new suggestions.
    /// </summary>
    GetAgenda,

    /// <summary>
    /// Adds an event feed, which starts a search for events that relate to it.
    /// </summary>
    AddEventFeed,

    /// <summary>
    /// Removes an event feed and the suggestions that came from it.
    /// </summary>
    RemoveEventFeed,

    /// <summary>
    /// Reads the news feeds and their entries and, when there are no entries yet or a refresh is asked for, starts a search.
    /// </summary>
    GetNews,

    /// <summary>
    /// Adds a news feed, which starts a search for it.
    /// </summary>
    AddNewsFeed,

    /// <summary>
    /// Removes a news feed and its entries.
    /// </summary>
    RemoveNewsFeed,

    /// <summary>
    /// Likes or dislikes an entry of the mail, events or news tab, or takes the reaction back; the server learns from it.
    /// </summary>
    React,

    /// <summary>
    /// Reads the settings of the server.
    /// </summary>
    GetSettings,

    /// <summary>
    /// Changes the settings of the server.
    /// </summary>
    SetSettings,
}
