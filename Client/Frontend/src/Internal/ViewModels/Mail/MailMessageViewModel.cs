namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// One message read in full.
/// </summary>
internal sealed class MailMessageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailMessageViewModel" /> class.
    /// </summary>
    /// <param name="connection">The connection that reads the message.</param>
    /// <param name="item">The message as the list showed it, which fills the screen until the text arrives.</param>
    /// <param name="back">Called when the user leaves the message.</param>
    public MailMessageViewModel(IServerConnection connection, MailItemViewModel item, Action back)
    {
        this.connection = connection;
        id = item.Message.Id;
        Subject = item.Subject;
        From = item.From;
        TimeText = item.TimeText;
        BackCommand = new RelayCommand(back);
    }

    private readonly IServerConnection connection;

    private readonly string id;

    private string body = string.Empty;

    private string to = string.Empty;

    private string? cc;

    private bool isLoading = true;

    /// <summary>
    /// Gets the command that leaves the message.
    /// </summary>
    public ICommand BackCommand { get; }

    /// <summary>
    /// Gets the subject.
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Gets the sender.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets when it was received.
    /// </summary>
    public string TimeText { get; }

    /// <summary>
    /// Gets the recipients, once the message was read.
    /// </summary>
    public string To
    {
        get => to;
        private set => Set(ref to, value);
    }

    /// <summary>
    /// Gets the copied recipients, or <see langword="null" />.
    /// </summary>
    public string? Cc
    {
        get => cc;
        private set
        {
            if (Set(ref cc, value))
            {
                Raise(nameof(HasCc));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the message has copied recipients.
    /// </summary>
    public bool HasCc => cc is not null;

    /// <summary>
    /// Gets the text of the message.
    /// </summary>
    public string Body
    {
        get => body;
        private set => Set(ref body, value);
    }

    /// <summary>
    /// Gets a value indicating whether the text is still being read.
    /// </summary>
    public bool IsLoading
    {
        get => isLoading;
        private set => Set(ref isLoading, value);
    }

    /// <summary>
    /// Reads the message from the server.
    /// </summary>
    /// <returns>A task that completes when the text is shown.</returns>
    public async Task Load()
    {
        MailMessage? message = null;
        await Attempt(async () => message = await connection.ReadMail(id));
        IsLoading = false;

        if (message is not null)
        {
            To = message.To;
            Cc = message.Cc;
            Body = message.Body;
        }
    }
}
