namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The Android head of the client application; hosts the root view as the single view.
/// </summary>
public sealed class App : ClientApp
{
    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = CreateMainView(typeof(App).Assembly, null, new AndroidUpdater(new IReleaseFeed.Factory().Create()));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
