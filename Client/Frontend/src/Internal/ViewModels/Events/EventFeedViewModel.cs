namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// An event feed in the list of the feeds.
/// </summary>
internal sealed class EventFeedViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventFeedViewModel" /> class.
    /// </summary>
    /// <param name="feed">The feed.</param>
    /// <param name="remove">Called when the user removes the feed.</param>
    public EventFeedViewModel(EventFeed feed, Action<EventFeedViewModel> remove)
    {
        Feed = feed;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    /// <summary>
    /// Gets the feed.
    /// </summary>
    public EventFeed Feed { get; }

    /// <summary>
    /// Gets the name of the feed.
    /// </summary>
    public string Name => Feed.Name;

    /// <summary>
    /// Gets what to search for.
    /// </summary>
    public string Description => Feed.Description;

    /// <summary>
    /// Gets the command that removes the feed.
    /// </summary>
    public ICommand RemoveCommand { get; }
}
