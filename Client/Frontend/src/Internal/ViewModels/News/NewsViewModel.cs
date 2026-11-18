namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The news tab: the news feeds the user follows, each with the summary entries the server found for it, refreshed by the server once a day and by the user whenever they like.
/// </summary>
internal sealed class NewsViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewsViewModel" /> class.
    /// </summary>
    /// <param name="connection">The open connection, whose news events the tab follows.</param>
    /// <param name="launcher">Opens articles in the browser.</param>
    /// <param name="culture">The culture the dates are written in, or <see langword="null" /> for the current one.</param>
    public NewsViewModel(IServerConnection connection, ILinkLauncher launcher, CultureInfo? culture = null)
    {
        this.connection = connection;
        this.launcher = launcher;
        this.culture = culture ?? CultureInfo.CurrentCulture;
        ManageFeedsCommand = new RelayCommand(() => IsManagingFeeds = true);
        CloseFeedsCommand = new RelayCommand(() => IsManagingFeeds = false);
        AddFeedCommand = new AsyncCommand(AddFeed, () => newName.Trim().Length > 0 && newDescription.Trim().Length >= 3);
        connection.EventReceived += Receive;
    }

    private readonly IServerConnection connection;

    private readonly ILinkLauncher launcher;

    private readonly CultureInfo culture;

    private NewsState? state;

    private bool isManagingFeeds;

    private string newName = string.Empty;

    private string newDescription = string.Empty;

    private bool loaded;

    /// <summary>
    /// Gets the feeds with their entries, in the order the user added the feeds.
    /// </summary>
    public ObservableCollection<NewsFeedSectionViewModel> Sections { get; } = [];

    /// <summary>
    /// Gets the command that opens the list of feeds.
    /// </summary>
    public ICommand ManageFeedsCommand { get; }

    /// <summary>
    /// Gets the command that closes the list of feeds.
    /// </summary>
    public ICommand CloseFeedsCommand { get; }

    /// <summary>
    /// Gets the command that adds the feed that was typed.
    /// </summary>
    public AsyncCommand AddFeedCommand { get; }

    /// <summary>
    /// Gets a value indicating whether no page (the feeds) is open over the news.
    /// </summary>
    public bool IsRoot => !isManagingFeeds;

    /// <summary>
    /// Gets a value indicating whether the list of feeds shows.
    /// </summary>
    public bool IsManagingFeeds
    {
        get => isManagingFeeds;
        private set
        {
            if (Set(ref isManagingFeeds, value))
            {
                Raise(nameof(IsRoot));
            }
        }
    }

    /// <summary>
    /// Gets or sets the name of the feed that is being typed.
    /// </summary>
    public string NewName
    {
        get => newName;
        set
        {
            if (Set(ref newName, value))
            {
                AddFeedCommand.Refresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets what the feed that is being typed searches for.
    /// </summary>
    public string NewDescription
    {
        get => newDescription;
        set
        {
            if (Set(ref newDescription, value))
            {
                AddFeedCommand.Refresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the state was read and there are no feeds, which is when the tab explains how to get news.
    /// </summary>
    public bool NeedsFeeds => state is not null && state.Feeds.Count == 0;

    /// <summary>
    /// Gets a value indicating whether there are feeds.
    /// </summary>
    public bool HasFeeds => state is { Feeds.Count: > 0 };

    /// <summary>
    /// Gets a value indicating whether the server is searching the news.
    /// </summary>
    public bool IsGenerating => state?.Generating == true;

    /// <summary>
    /// Gets why the last search failed, or <see langword="null" />.
    /// </summary>
    public string? SearchError => state?.Error;

    /// <summary>
    /// Gets a value indicating whether <see cref="SearchError" /> is set.
    /// </summary>
    public bool HasSearchError => state?.Error is not null;

    /// <summary>
    /// Reads the state the first time the tab is shown.
    /// </summary>
    /// <returns>A task that completes when the state is shown.</returns>
    public async Task Load()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        NewsState? read = null;

        if (await Attempt(async () => read = await connection.GetNews(false)) && read is not null)
        {
            Apply(read);
        }
    }

    /// <summary>
    /// Closes the list of feeds when it shows.
    /// </summary>
    /// <returns><see langword="true" /> if it was showing.</returns>
    public bool GoBack()
    {
        if (!isManagingFeeds)
        {
            return false;
        }

        IsManagingFeeds = false;
        return true;
    }

    private async Task AddFeed()
    {
        string name = newName.Trim();
        string description = newDescription.Trim();
        NewsState? read = null;

        if (await Attempt(async () => read = await connection.AddNewsFeed(name, description)) && read is not null)
        {
            NewName = string.Empty;
            NewDescription = string.Empty;
            IsManagingFeeds = false;
            Apply(read);
        }
    }

    private void Remove(NewsFeedSectionViewModel section)
    {
        _ = RemoveFeed(section);
    }

    private async Task RemoveFeed(NewsFeedSectionViewModel section)
    {
        NewsState? read = null;

        if (await Attempt(async () => read = await connection.RemoveNewsFeed(section.Feed.Id)) && read is not null)
        {
            Apply(read);
        }
    }

    private void Receive(RemoteEvent remoteEvent)
    {
        if (remoteEvent is { Kind: RemoteEventKind.NewsChanged, News: { } changed })
        {
            Apply(changed);
        }
    }

    private void Apply(NewsState current)
    {
        state = current;
        Sections.Clear();

        foreach (NewsFeed feed in current.Feeds)
        {
            IReadOnlyList<NewsItemViewModel> items = [.. current.Items.Where(item => item.FeedId == feed.Id).Select(item => new NewsItemViewModel(connection, launcher, item, feed.Name, culture))];
            Sections.Add(new NewsFeedSectionViewModel(feed, items, current.Generating ? "Searching..." : "Nothing found yet.", Remove));
        }

        Raise(nameof(NeedsFeeds));
        Raise(nameof(HasFeeds));
        Raise(nameof(IsGenerating));
        Raise(nameof(SearchError));
        Raise(nameof(HasSearchError));
    }
}
