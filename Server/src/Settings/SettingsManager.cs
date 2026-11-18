namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// What the settings screen of the client works with: reads and changes the settings, and tells every client when they change.
/// </summary>
internal interface ISettingsManager
{
    /// <summary>
    /// Reads the settings.
    /// </summary>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The settings.</returns>
    Task<ServerSettings> Get(CancellationToken cancellation = default);

    /// <summary>
    /// Changes the settings.
    /// </summary>
    /// <param name="update">The new settings.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>The settings as they are now.</returns>
    Task<ServerSettings> Update(ServerSettingsUpdate update, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class SettingsManager(ISettingsService settings, IEventHub events) : ISettingsManager
{
    /// <inheritdoc />
    public async Task<ServerSettings> Get(CancellationToken cancellation = default)
    {
        await settings.Ready(cancellation);
        return new() { Location = settings.Location };
    }

    /// <inheritdoc />
    public async Task<ServerSettings> Update(ServerSettingsUpdate update, CancellationToken cancellation = default)
    {
        await settings.Update(update, cancellation);
        ServerSettings current = await Get(cancellation);
        events.Publish(new() { Kind = RemoteEventKind.SettingsChanged, Settings = current });
        return current;
    }
}
