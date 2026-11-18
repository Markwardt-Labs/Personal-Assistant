namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// One message of a topic as the list of the topic shows it.
/// </summary>
internal sealed class MailItemViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailItemViewModel" /> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="open">Called when the user opens the message.</param>
    /// <param name="culture">The culture the time is written in.</param>
    public MailItemViewModel(MailSummary message, Action<MailItemViewModel> open, CultureInfo culture)
    {
        Message = message;
        Subject = string.IsNullOrWhiteSpace(message.Subject) ? "(no subject)" : message.Subject;
        TimeText = message.Received.ToLocalTime().ToString("g", culture);
        OpenCommand = new RelayCommand(() => open(this));
    }

    /// <summary>
    /// Gets the message.
    /// </summary>
    public MailSummary Message { get; }

    /// <summary>
    /// Gets the name of the sender, or the address when the message carries no name.
    /// </summary>
    public string From => Message.From.DisplayName;

    /// <summary>
    /// Gets the subject.
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Gets the start of the text.
    /// </summary>
    public string Preview => Message.Preview;

    /// <summary>
    /// Gets when the message was received, in the local time zone.
    /// </summary>
    public string TimeText { get; }

    /// <summary>
    /// Gets a value indicating whether the user has not read the message yet.
    /// </summary>
    public bool IsUnread => Message.IsUnread;

    /// <summary>
    /// Gets the command that opens the message.
    /// </summary>
    public ICommand OpenCommand { get; }
}
