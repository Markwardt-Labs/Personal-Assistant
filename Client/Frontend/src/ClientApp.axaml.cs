namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The Avalonia application of the client: the theme plus the root view wired to the real services.
/// </summary>
public class ClientApp : Application
{
    /// <inheritdoc />
    public override void Initialize()
        => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Builds the root view wired to the real services, for the head that hosts it.
    /// </summary>
    /// <param name="head">The executable assembly of the head, whose version is shown and compared with the server's.</param>
    /// <param name="dataFolder">The folder holding the saved settings, or <see langword="null" /> for the <c>PersonalAssistant</c> folder of the user's application data.</param>
    /// <param name="updater">Installs a newer release over the app, or <see langword="null" /> when the app cannot update itself, in which case it still updates its server.</param>
    /// <returns>The root view, already connecting to the server it used last.</returns>
    public MainView CreateMainView(Assembly head, string? dataFolder = null, IClientUpdater? updater = null)
    {
        string folder = dataFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalAssistant");
        MainView view = new();
        MainViewModel model = new(new IAppInfo.Factory().Create(head), new IServerConnection.Factory(new IMessageChannel.Factory()), new SettingsStore(Path.Combine(folder, "settings.json")), new LinkLauncher(() => TopLevel.GetTopLevel(view)), new IReleaseFeed.Factory().Create(), updater);
        view.DataContext = model;
        _ = model.Start();
        return view;
    }
}
