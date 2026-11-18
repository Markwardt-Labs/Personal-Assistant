namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// A summary entry of the mail tab: one topic with the messages it stands for.
/// </summary>
internal sealed class MailTopicViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailTopicViewModel" /> class.
    /// </summary>
    /// <param name="connection">The connection that tells the server what the user thinks of the topic.</param>
    /// <param name="topic">The topic.</param>
    /// <param name="open">Called when the user opens the topic.</param>
    /// <param name="openMessage">Called when the user opens one message of the topic.</param>
    /// <param name="back">Called when the user leaves the topic.</param>
    /// <param name="culture">The culture the times are written in.</param>
    public MailTopicViewModel(IServerConnection connection, MailTopic topic, Action<MailTopicViewModel> open, Action<MailTopicViewModel, MailItemViewModel> openMessage, Action back, CultureInfo culture)
    {
        Topic = topic;
        Items = [.. topic.Messages.Select(message => new MailItemViewModel(message, item => openMessage(this, item), culture))];
        CountText = topic.Messages.Count == 1 ? "1 email" : $"{topic.Messages.Count} emails";
        LatestText = topic.Messages.Count == 0 ? string.Empty : topic.Messages.Max(message => message.Received).ToLocalTime().ToString("g", culture);
        Reaction = new ReactionViewModel(connection, ReactionTarget.Mail, topic.ReactionKey, topic.Title, topic.Importance.ToString(), topic.Reaction);
        OpenCommand = new RelayCommand(() => open(this));
        BackCommand = new RelayCommand(back);
    }

    /// <summary>
    /// Gets the topic.
    /// </summary>
    public MailTopic Topic { get; }

    /// <summary>
    /// Gets the messages of the topic, newest first.
    /// </summary>
    public IReadOnlyList<MailItemViewModel> Items { get; }

    /// <summary>
    /// Gets the like and dislike buttons.
    /// </summary>
    public ReactionViewModel Reaction { get; }

    /// <summary>
    /// Gets the command that opens the topic.
    /// </summary>
    public ICommand OpenCommand { get; }

    /// <summary>
    /// Gets the command that leaves the topic.
    /// </summary>
    public ICommand BackCommand { get; }

    /// <summary>
    /// Gets the short title.
    /// </summary>
    public string Title => Topic.Title;

    /// <summary>
    /// Gets the sentences that say what the messages tell the user.
    /// </summary>
    public string Summary => Topic.Summary;

    /// <summary>
    /// Gets how many messages the topic stands for.
    /// </summary>
    public string CountText { get; }

    /// <summary>
    /// Gets when the newest message arrived.
    /// </summary>
    public string LatestText { get; }

    /// <summary>
    /// Gets the label of the importance for the topics that need one, or an empty text.
    /// </summary>
    public string ImportanceLabel => Topic.Importance switch
    {
        MailImportance.Critical => "Critical",
        MailImportance.High => "High",
        _ => string.Empty,
    };

    /// <summary>
    /// Gets a value indicating whether the topic is critical, which colors its label.
    /// </summary>
    public bool IsCritical => Topic.Importance == MailImportance.Critical;

    /// <summary>
    /// Gets a value indicating whether <see cref="ImportanceLabel" /> says something.
    /// </summary>
    public bool HasImportanceLabel => ImportanceLabel.Length > 0;

    /// <summary>
    /// Gets a value indicating whether the user has not read one of the messages yet.
    /// </summary>
    public bool HasUnread => Topic.Messages.Any(message => message.IsUnread);
}
