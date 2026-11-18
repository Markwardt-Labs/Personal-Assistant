namespace Markwardt.PersonalAssistant;

/// <summary>
/// What identifies an entry across searches, which the server and the client both need to agree on so that a reaction finds its entry again: the address for news and events, the title for a mail topic.
/// </summary>
public static class ReactionKeys
{
    extension(MailTopic topic)
    {
        /// <summary>
        /// Gets the key of the topic, its title without regard to case; a new digest that words a topic differently starts without a reaction.
        /// </summary>
        public string ReactionKey => topic.Title.Trim().ToLowerInvariant();
    }

    extension(SuggestedEvent suggestion)
    {
        /// <summary>
        /// Gets the key of the suggestion, the address of its page.
        /// </summary>
        public string ReactionKey => suggestion.Url;
    }

    extension(NewsItem item)
    {
        /// <summary>
        /// Gets the key of the entry, the address of its article.
        /// </summary>
        public string ReactionKey => item.Url;
    }
}
