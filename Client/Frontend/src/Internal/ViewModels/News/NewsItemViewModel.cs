namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// A summary entry of the news tab with the link to its article.
/// </summary>
internal sealed class NewsItemViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewsItemViewModel" /> class.
    /// </summary>
    /// <param name="connection">The connection that tells the server what the user thinks of the entry.</param>
    /// <param name="launcher">Opens the article in the browser.</param>
    /// <param name="item">The entry.</param>
    /// <param name="feedName">The name of the feed the entry is under.</param>
    /// <param name="culture">The culture the date is written in.</param>
    public NewsItemViewModel(IServerConnection connection, ILinkLauncher launcher, NewsItem item, string feedName, CultureInfo culture)
    {
        Item = item;
        string published = item.Published is { } time ? time.ToLocalTime().ToString("d", culture) : string.Empty;
        Meta = string.Join(" - ", new[] { item.Source, published }.Where(part => !string.IsNullOrWhiteSpace(part)));
        Reaction = new ReactionViewModel(connection, ReactionTarget.News, item.ReactionKey, item.Title, feedName, item.Reaction);
        OpenCommand = new AsyncCommand(() => launcher.Open(item.Url));
    }

    /// <summary>
    /// Gets the entry.
    /// </summary>
    public NewsItem Item { get; }

    /// <summary>
    /// Gets the like and dislike buttons.
    /// </summary>
    public ReactionViewModel Reaction { get; }

    /// <summary>
    /// Gets the command that opens the article.
    /// </summary>
    public AsyncCommand OpenCommand { get; }

    /// <summary>
    /// Gets the headline.
    /// </summary>
    public string Title => Item.Title;

    /// <summary>
    /// Gets the sentences that say what happened.
    /// </summary>
    public string Summary => Item.Summary;

    /// <summary>
    /// Gets the publisher and the date, as text.
    /// </summary>
    public string Meta { get; }
}
