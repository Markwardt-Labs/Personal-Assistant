namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The conversation with the assistant: what was said, what the user is typing, and whether the assistant is working.
/// </summary>
internal sealed class ChatViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatViewModel" /> class.
    /// </summary>
    /// <param name="connection">The open connection, whose events the chat follows.</param>
    public ChatViewModel(IServerConnection connection)
    {
        this.connection = connection;
        connection.EventReceived += Receive;
        SendCommand = new AsyncCommand(Send, () => !string.IsNullOrWhiteSpace(input));
        StopCommand = new AsyncCommand(Stop);
    }

    private readonly IServerConnection connection;

    private string input = string.Empty;

    private bool isWorking;

    /// <summary>
    /// Gets the messages in order, oldest first.
    /// </summary>
    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    /// <summary>
    /// Gets the command that sends what was typed.
    /// </summary>
    public AsyncCommand SendCommand { get; }

    /// <summary>
    /// Gets the command that stops what the assistant is doing.
    /// </summary>
    public AsyncCommand StopCommand { get; }

    /// <summary>
    /// Gets or sets what the user is typing.
    /// </summary>
    public string Input
    {
        get => input;
        set
        {
            if (Set(ref input, value))
            {
                SendCommand.Refresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the assistant is working on a prompt.
    /// </summary>
    public bool IsWorking
    {
        get => isWorking;
        private set => Set(ref isWorking, value);
    }

    /// <summary>
    /// Reads the conversation from the server.
    /// </summary>
    /// <returns>A task that completes when the conversation is shown.</returns>
    public async Task Load()
    {
        ChatHistory? history = null;

        if (await Attempt(async () => history = await connection.GetHistory(null, CancellationToken.None)) && history is not null)
        {
            foreach (ChatMessage message in history.Messages)
            {
                Add(message);
            }

            IsWorking = history.Working;
        }
    }

    private async Task Send()
    {
        string text = input.Trim();

        if (text.Length == 0)
        {
            return;
        }

        Input = string.Empty;

        if (!await Attempt(() => connection.Prompt(text, CancellationToken.None)))
        {
            Input = text;
        }
    }

    private async Task Stop()
        => await Attempt(() => connection.Interrupt(CancellationToken.None));

    private void Receive(RemoteEvent remoteEvent)
    {
        if (remoteEvent is { Kind: RemoteEventKind.MessageAdded, Message: { } message })
        {
            Add(message);
        }
        else if (remoteEvent is { Kind: RemoteEventKind.WorkingChanged, Working: { } working })
        {
            IsWorking = working;
        }
    }

    private void Add(ChatMessage message)
    {
        if (Messages.Any(item => item.Index == message.Index))
        {
            return;
        }

        int position = Messages.Count;

        while (position > 0 && Messages[position - 1].Index > message.Index)
        {
            position--;
        }

        Messages.Insert(position, new MessageViewModel(message));
    }
}
