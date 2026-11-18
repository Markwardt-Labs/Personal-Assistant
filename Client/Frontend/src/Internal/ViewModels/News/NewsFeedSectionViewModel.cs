namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// A news feed with the entries found for it, which are shown under its name.
/// </summary>
internal sealed class NewsFeedSectionViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewsFeedSectionViewModel" /> class.
    /// </summary>
    /// <param name="feed">The feed.</param>
    /// <param name="items">The entries found for it, newest first.</param>
    /// <param name="emptyText">What to say when there are no entries.</param>
    /// <param name="remove">Called when the user removes the feed.</param>
    public NewsFeedSectionViewModel(NewsFeed feed, IReadOnlyList<NewsItemViewModel> items, string emptyText, Action<NewsFeedSectionViewModel> remove)
    {
        Feed = feed;
        Items = items;
        EmptyText = emptyText;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    /// <summary>
    /// Gets the feed.
    /// </summary>
    public NewsFeed Feed { get; }

    /// <summary>
    /// Gets the entries found for the feed.
    /// </summary>
    public IReadOnlyList<NewsItemViewModel> Items { get; }

    /// <summary>
    /// Gets what is said when there are no entries.
    /// </summary>
    public string EmptyText { get; }

    /// <summary>
    /// Gets the command that removes the feed.
    /// </summary>
    public ICommand RemoveCommand { get; }

    /// <summary>
    /// Gets the name of the feed.
    /// </summary>
    public string Name => Feed.Name;

    /// <summary>
    /// Gets what the feed searches for.
    /// </summary>
    public string Description => Feed.Description;

    /// <summary>
    /// Gets a value indicating whether there are entries.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets a value indicating whether there are no entries.
    /// </summary>
    public bool IsEmpty => Items.Count == 0;
}
