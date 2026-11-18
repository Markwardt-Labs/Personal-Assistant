namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The settings screen: where the user lives (kept by the server), the manual refresh of the tabs, which the server does by itself, the versions of the app and its server, the check for updates, and the way to another server.
/// </summary>
internal sealed class SettingsViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel" /> class.
    /// </summary>
    /// <param name="connection">The open connection, which reads and changes the settings of the server and whose settings events the screen follows.</param>
    /// <param name="update">The update of the app and its server.</param>
    /// <param name="changeServer">Called when the user chooses another server.</param>
    /// <param name="back">Called when the user leaves the screen.</param>
    public SettingsViewModel(IServerConnection connection, UpdateViewModel update, Action changeServer, Action back)
    {
        this.connection = connection;
        Update = update;
        ChangeServerCommand = new RelayCommand(changeServer);
        BackCommand = new RelayCommand(back);
        SaveCommand = new AsyncCommand(Save);
        RefreshMailCommand = new AsyncCommand(() => Attempt(() => connection.GetMailDigest(true)));
        RefreshEventsCommand = new AsyncCommand(() => Attempt(() => connection.GetAgenda(true)));
        RefreshNewsCommand = new AsyncCommand(() => Attempt(() => connection.GetNews(true)));
        connection.EventReceived += Receive;
    }

    private readonly IServerConnection connection;

    private string location = string.Empty;

    private string? saved;

    /// <summary>
    /// Gets the update of the app and its server.
    /// </summary>
    public UpdateViewModel Update { get; }

    /// <summary>
    /// Gets the command that goes to the screen for choosing a server.
    /// </summary>
    public ICommand ChangeServerCommand { get; }

    /// <summary>
    /// Gets the command that leaves the screen.
    /// </summary>
    public ICommand BackCommand { get; }

    /// <summary>
    /// Gets the command that saves the settings.
    /// </summary>
    public AsyncCommand SaveCommand { get; }

    /// <summary>
    /// Gets the command that asks the server to sort the mail again now. The tab shows the result when the server pushes it.
    /// </summary>
    public AsyncCommand RefreshMailCommand { get; }

    /// <summary>
    /// Gets the command that asks the server to read the calendar and look for suggested events again now.
    /// </summary>
    public AsyncCommand RefreshEventsCommand { get; }

    /// <summary>
    /// Gets the command that asks the server to search the news again now.
    /// </summary>
    public AsyncCommand RefreshNewsCommand { get; }

    /// <summary>
    /// Gets or sets where the user lives.
    /// </summary>
    public string Location
    {
        get => location;
        set
        {
            if (Set(ref location, value))
            {
                Saved = null;
            }
        }
    }

    /// <summary>
    /// Gets what the screen says after the settings were saved, or <see langword="null" />.
    /// </summary>
    public string? Saved
    {
        get => saved;
        private set
        {
            if (Set(ref saved, value))
            {
                Raise(nameof(HasSaved));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="Saved" /> is set.
    /// </summary>
    public bool HasSaved => saved is not null;

    /// <summary>
    /// Reads the settings from the server.
    /// </summary>
    /// <returns>A task that completes when they are shown.</returns>
    public async Task Load()
    {
        ServerSettings? read = null;

        if (await Attempt(async () => read = await connection.GetSettings()) && read is not null)
        {
            Location = read.Location ?? string.Empty;
        }
    }

    private async Task Save()
    {
        Saved = null;
        ServerSettings? read = null;
        ServerSettingsUpdate update = new() { Location = location };

        if (await Attempt(async () => read = await connection.SetSettings(update)) && read is not null)
        {
            Location = read.Location ?? string.Empty;
            Saved = "Saved.";
        }
    }

    private void Receive(RemoteEvent remoteEvent)
    {
        if (remoteEvent is { Kind: RemoteEventKind.SettingsChanged, Settings: { } changed })
        {
            Location = changed.Location ?? string.Empty;
        }
    }
}
