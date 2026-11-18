namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The screen where the user says which server to use and gives its access key.
/// </summary>
internal sealed class ConnectViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectViewModel" /> class.
    /// </summary>
    /// <param name="info">The product and version, shown on the screen.</param>
    /// <param name="connections">Opens connections.</param>
    /// <param name="store">Keeps the settings once they worked.</param>
    /// <param name="saved">The settings the client last connected with, or <see langword="null" /> for none.</param>
    /// <param name="connected">Called with the open connection and the settings that worked after a successful connect.</param>
    public ConnectViewModel(IAppInfo info, IServerConnection.IFactory connections, ISettingsStore store, ConnectionSettings? saved, Action<IServerConnection, ConnectionSettings> connected)
    {
        this.connections = connections;
        this.store = store;
        this.connected = connected;
        host = saved?.Host ?? string.Empty;
        port = (saved?.Port ?? 52680).ToString(CultureInfo.InvariantCulture);
        accessKey = saved?.AccessKey ?? string.Empty;
        Title = info.Name;
        Version = $"Version {info.Version}";
        ConnectCommand = new AsyncCommand(Connect);
    }

    private readonly IServerConnection.IFactory connections;

    private readonly ISettingsStore store;

    private readonly Action<IServerConnection, ConnectionSettings> connected;

    private string host;

    private string port;

    private string accessKey;

    /// <summary>
    /// Gets the product name.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the version of this client, as text.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets or sets the server's host name or address.
    /// </summary>
    public string Host
    {
        get => host;
        set => Set(ref host, value);
    }

    /// <summary>
    /// Gets or sets the server's port, as typed.
    /// </summary>
    public string Port
    {
        get => port;
        set => Set(ref port, value);
    }

    /// <summary>
    /// Gets or sets the access key, as typed.
    /// </summary>
    public string AccessKey
    {
        get => accessKey;
        set => Set(ref accessKey, value);
    }

    /// <summary>
    /// Gets the command that connects to the server.
    /// </summary>
    public AsyncCommand ConnectCommand { get; }

    /// <summary>
    /// Shows why a connection that was open ended.
    /// </summary>
    /// <param name="message">The reason.</param>
    public void Fail(string message)
        => Error = message;

    /// <summary>
    /// Connects with what was typed, saving it when the server accepts it.
    /// </summary>
    /// <returns>A task that completes when the attempt is over.</returns>
    public async Task Connect()
    {
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number is < 1 or > 65535 || string.IsNullOrEmpty(accessKey))
        {
            Error = "Enter the server's host, a port from 1 to 65535 and the access key.";
            return;
        }

        ConnectionSettings settings = new() { Host = host.Trim(), Port = number, AccessKey = accessKey };

        await Attempt(async () =>
        {
            IServerConnection connection = await connections.Open(settings, CancellationToken.None);
            await store.Save(settings, CancellationToken.None);
            connected(connection, settings);
        });
    }
}
