namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Keeps what the user sets from the client: where they live. Everything that needs a setting reads it here when it needs it, so a change takes effect without a restart.
/// </summary>
internal interface ISettingsService
{
    /// <summary>
    /// Gets where the user lives, or <see langword="null" />, once <see cref="Ready" /> has completed.
    /// </summary>
    string? Location { get; }

    /// <summary>
    /// Loads what was saved, which has to be done before the properties say anything.
    /// </summary>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>A task that completes once the settings are loaded.</returns>
    Task Ready(CancellationToken cancellation = default);

    /// <summary>
    /// Changes the settings and saves them.
    /// </summary>
    /// <param name="update">The new settings; a blank location clears it.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>The settings as they are now.</returns>
    Task<StoredSettings> Update(ServerSettingsUpdate update, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class SettingsService(IJsonFile<StoredSettings> store) : ISettingsService
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private StoredSettings current = new();

    private bool loaded;

    /// <inheritdoc />
    public string? Location => current.Location;

    /// <inheritdoc />
    public async Task Ready(CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);

        try
        {
            await Load(cancellation);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<StoredSettings> Update(ServerSettingsUpdate update, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);

        try
        {
            await Load(cancellation);
            current = new() { Location = Clean(update.Location) };
            await store.Save(current, cancellation);
            return current;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task Load(CancellationToken cancellation = default)
    {
        if (!loaded)
        {
            StoredSettings? stored = await store.Load(cancellation);
            current = stored ?? current;
            loaded = true;

            if (stored is not null)
            {
                await store.Save(current, cancellation);
            }
        }
    }

    private string? Clean(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
