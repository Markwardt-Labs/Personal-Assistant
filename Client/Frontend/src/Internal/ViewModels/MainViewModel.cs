namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The root of the client: the connect screen until a connection is open, then the home screen, or the version mismatch screen when the server runs another version. A lost connection is reopened at once; while an update restarts the server the updating screen shows and the connection is retried until the server is back.
/// </summary>
internal sealed class MainViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel" /> class.
    /// </summary>
    /// <param name="info">The product and version of this client.</param>
    /// <param name="connections">Opens connections.</param>
    /// <param name="store">Keeps how to reach the server.</param>
    /// <param name="launcher">Opens articles in the browser.</param>
    /// <param name="feed">Finds the newest release, or <see langword="null" /> when the client does not look for one.</param>
    /// <param name="updater">Installs a release over this client, or <see langword="null" /> when this client cannot update itself.</param>
    /// <param name="retry">How long to wait between attempts to reach a server that restarts for an update, or <see langword="null" /> for three seconds.</param>
    /// <param name="patience">How long to wait for that server in all, or <see langword="null" /> for ten minutes.</param>
    /// <param name="time">The clock that measures the wait, or <see langword="null" /> for the system's.</param>
    public MainViewModel(IAppInfo info, IServerConnection.IFactory connections, ISettingsStore store, ILinkLauncher launcher, IReleaseFeed? feed = null, IClientUpdater? updater = null, TimeSpan? retry = null, TimeSpan? patience = null, TimeProvider? time = null)
    {
        this.info = info;
        this.connections = connections;
        this.store = store;
        this.launcher = launcher;
        this.retry = retry ?? TimeSpan.FromSeconds(3);
        this.patience = patience ?? TimeSpan.FromMinutes(10);
        this.time = time ?? TimeProvider.System;
        update = new UpdateViewModel(info, feed, updater);
        update.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UpdateViewModel.IsUpdating))
            {
                Raise(nameof(Current));
            }
        };
    }

    private readonly IAppInfo info;

    private readonly IServerConnection.IFactory connections;

    private readonly ISettingsStore store;

    private readonly ILinkLauncher launcher;

    private readonly UpdateViewModel update;

    private readonly TimeSpan retry;

    private readonly TimeSpan patience;

    private readonly TimeProvider time;

    private ViewModelBase? page;

    private IServerConnection? connection;

    private ConnectionSettings? saved;

    /// <summary>
    /// Gets the screen that is shown: the updating screen while an update runs, otherwise the screen of the connection.
    /// </summary>
    public ViewModelBase? Current => update.IsUpdating ? update : page;

    /// <summary>
    /// Gets the home screen when it is the one that shows, so that the back button can step back through it.
    /// </summary>
    public HomeViewModel? Home => page as HomeViewModel;

    /// <summary>
    /// Shows the connect screen and, when the client was connected before, connects again at once.
    /// </summary>
    /// <returns>A task that completes when the first attempt is over.</returns>
    public async Task Start()
    {
        saved = await store.Load(CancellationToken.None);
        _ = update.Watch();
        ConnectViewModel connect = ShowConnect(null);

        if (saved is not null)
        {
            await connect.Connect();
        }
    }

    /// <summary>
    /// Steps back through the screen that shows.
    /// </summary>
    /// <returns><see langword="true" /> if there was something to step back from.</returns>
    public bool GoBack()
        => !update.IsUpdating && Home?.GoBack() == true;

    private void Show(ViewModelBase next)
    {
        page = next;
        Raise(nameof(Current));
        Raise(nameof(Home));
    }

    private ConnectViewModel ShowConnect(string? error)
    {
        ConnectViewModel connect = new(info, connections, store, saved, Open);

        if (error is not null)
        {
            connect.Fail(error);
        }

        Show(connect);
        return connect;
    }

    private void Open(IServerConnection opened, ConnectionSettings settings)
    {
        connection = opened;
        saved = settings;
        update.Attach(opened);
        opened.Lost += reason => _ = Lost(opened, reason);

        if (opened.ServerVersion != info.Version)
        {
            Show(new VersionMismatchViewModel(info, opened, update, () => _ = Leave(null)));
            return;
        }

        ChatViewModel chat = new(opened);
        MailViewModel mail = new(opened);
        EventsViewModel events = new(opened, launcher);
        NewsViewModel news = new(opened, launcher);
        _ = chat.Load();
        Show(new HomeViewModel(mail, events, news, chat, back => Settings(opened, back), update));
    }

    private SettingsViewModel Settings(IServerConnection opened, Action back)
    {
        SettingsViewModel settings = new(opened, update, () => _ = Leave(null), back);
        _ = settings.Load();
        return settings;
    }

    private async Task Lost(IServerConnection lost, string reason)
    {
        if (connection != lost)
        {
            return;
        }

        if (update.IsUpdating)
        {
            await WaitForServer(reason);
        }
        else
        {
            await Leave(reason);
        }
    }

    private async Task Leave(string? reason)
    {
        if (connection is not { } closing)
        {
            return;
        }

        connection = null;
        await closing.DisposeAsync();
        ConnectViewModel connect = ShowConnect(reason);

        if (reason is not null)
        {
            await connect.Connect();
        }
    }

    private async Task WaitForServer(string reason)
    {
        if (connection is { } closing)
        {
            connection = null;
            await closing.DisposeAsync();
        }

        DateTimeOffset deadline = time.GetUtcNow() + patience;

        while (saved is { } settings && time.GetUtcNow() < deadline)
        {
            try
            {
                Open(await connections.Open(settings, CancellationToken.None), settings);
                update.ServerBack();
                return;
            }
            catch (Exception exception) when (exception is RemoteException or IOException or SocketException)
            {
                await Task.Delay(retry, time);
            }
        }

        update.Abandon();
        ShowConnect($"The server did not come back after the update. {reason}");
    }
}
