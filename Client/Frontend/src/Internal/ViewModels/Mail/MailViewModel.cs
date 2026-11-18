namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The mail tab: the mail of the last seven days as summary entries about topics, most important first, with the spam, advertising and unimportant ones in a collapsed section, and the screens to open a topic and a message.
/// </summary>
internal sealed class MailViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailViewModel" /> class.
    /// </summary>
    /// <param name="connection">The open connection, whose mail events the tab follows.</param>
    /// <param name="culture">The culture the times are written in, or <see langword="null" /> for the current one.</param>
    public MailViewModel(IServerConnection connection, CultureInfo? culture = null)
    {
        this.connection = connection;
        this.culture = culture ?? CultureInfo.CurrentCulture;
        ToggleOtherCommand = new RelayCommand(() => IsOtherExpanded = !isOtherExpanded);
        connection.EventReceived += Receive;
    }

    private readonly IServerConnection connection;

    private readonly CultureInfo culture;

    private MailDigestState? state;

    private object? page;

    private MailTopicViewModel? topicPage;

    private bool isOtherExpanded;

    /// <summary>
    /// Gets the topics that matter, most important first.
    /// </summary>
    public ObservableCollection<MailTopicViewModel> Topics { get; } = [];

    /// <summary>
    /// Gets the topics of spam, advertising and unimportant mail, which are collapsed until the user expands them.
    /// </summary>
    public ObservableCollection<MailTopicViewModel> OtherTopics { get; } = [];

    /// <summary>
    /// Gets the command that expands or collapses the section of unimportant topics.
    /// </summary>
    public ICommand ToggleOtherCommand { get; }

    /// <summary>
    /// Gets the screen shown over the list, a topic or a message, or <see langword="null" /> while the list shows.
    /// </summary>
    public object? Page
    {
        get => page;
        private set
        {
            if (Set(ref page, value))
            {
                Raise(nameof(HasPage));
                Raise(nameof(IsList));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a topic or a message is open.
    /// </summary>
    public bool HasPage => page is not null;

    /// <summary>
    /// Gets a value indicating whether the list shows.
    /// </summary>
    public bool IsList => page is null;

    /// <summary>
    /// Gets a value indicating whether the section of unimportant topics is expanded.
    /// </summary>
    public bool IsOtherExpanded
    {
        get => isOtherExpanded;
        private set => Set(ref isOtherExpanded, value);
    }

    /// <summary>
    /// Gets a value indicating whether there are unimportant topics.
    /// </summary>
    public bool HasOther => OtherTopics.Count > 0;

    /// <summary>
    /// Gets the title of the collapsed section.
    /// </summary>
    public string OtherHeader => $"Spam, ads and unimportant ({OtherTopics.Count})";

    /// <summary>
    /// Gets a value indicating whether the server is sorting the mail.
    /// </summary>
    public bool IsGenerating => state?.Generating == true;

    /// <summary>
    /// Gets why the last sorting failed, or <see langword="null" />.
    /// </summary>
    public string? DigestError => state?.Error;

    /// <summary>
    /// Gets a value indicating whether <see cref="DigestError" /> is set.
    /// </summary>
    public bool HasDigestError => state?.Error is not null;

    /// <summary>
    /// Gets a value indicating whether the digest exists and there was no mail in the last seven days.
    /// </summary>
    public bool IsEmpty => state?.Digest is not null && Topics.Count == 0 && OtherTopics.Count == 0;

    /// <summary>
    /// Reads the digest when the tab is shown, unless there is one already or the server is making it; a tab that has nothing to show, because the first attempt failed or the mail was not connected yet, asks again each time.
    /// </summary>
    /// <returns>A task that completes when the digest is shown.</returns>
    public async Task Load()
    {
        if (state is { Digest: not null } or { Generating: true })
        {
            return;
        }

        MailDigestState? read = null;

        if (await Attempt(async () => read = await connection.GetMailDigest(false)) && read is not null)
        {
            Apply(read);
        }
    }

    /// <summary>
    /// Steps back from a message to its topic, or from a topic to the list.
    /// </summary>
    /// <returns><see langword="true" /> if there was something to step back from.</returns>
    public bool GoBack()
    {
        if (page is MailMessageViewModel && topicPage is not null && topicPage.Items.Count > 1)
        {
            Page = topicPage;
            return true;
        }

        if (page is not null)
        {
            topicPage = null;
            Page = null;
            return true;
        }

        return false;
    }

    private void Receive(RemoteEvent remoteEvent)
    {
        if (remoteEvent is { Kind: RemoteEventKind.MailDigestChanged, MailDigest: { } changed })
        {
            Apply(changed);
        }
    }

    private void Apply(MailDigestState current)
    {
        state = current;
        Topics.Clear();
        OtherTopics.Clear();

        foreach (MailTopic topic in current.Digest?.Topics ?? [])
        {
            MailTopicViewModel model = new(connection, topic, Open, OpenMessage, () => GoBack(), culture);
            (topic.Importance is MailImportance.Low or MailImportance.Junk ? OtherTopics : Topics).Add(model);
        }

        Raise(nameof(HasOther));
        Raise(nameof(OtherHeader));
        Raise(nameof(IsGenerating));
        Raise(nameof(DigestError));
        Raise(nameof(HasDigestError));
        Raise(nameof(IsEmpty));
    }

    private void Open(MailTopicViewModel topic)
    {
        if (topic.Items.Count == 1)
        {
            OpenMessage(topic, topic.Items[0]);
        }
        else
        {
            topicPage = topic;
            Page = topic;
        }
    }

    private void OpenMessage(MailTopicViewModel topic, MailItemViewModel item)
    {
        topicPage = topic;
        MailMessageViewModel message = new(connection, item, () => GoBack());
        Page = message;
        _ = message.Load();
    }
}
