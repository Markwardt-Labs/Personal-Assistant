namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Finds and reads the config file of the server in its app data folder, making one with a fresh access key on the first start.
/// </summary>
internal interface IConfigLoader
{
    /// <summary>
    /// Loads the config.
    /// </summary>
    /// <param name="args">The command line arguments; <c>--data-folder</c> followed by a path uses that folder instead of the app data folder of the machine, which is for running a second server on one machine.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The config, with its app data folder created.</returns>
    /// <exception cref="InvalidDataException">The file is not a valid config.</exception>
    Task<ServerConfig> Load(IReadOnlyList<string> args, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class ConfigLoader(string defaultFolder) : IConfigLoader
{
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    /// <inheritdoc />
    public async Task<ServerConfig> Load(IReadOnlyList<string> args, CancellationToken cancellation = default)
    {
        int index = args.ToList().IndexOf("--data-folder");
        string folder = Path.GetFullPath(index >= 0 && index + 1 < args.Count ? args[index + 1] : defaultFolder);
        string path = Path.Combine(folder, "config.json");
        Directory.CreateDirectory(folder);
        ServerConfig config;

        if (File.Exists(path))
        {
            try
            {
                await using FileStream stream = File.OpenRead(path);
                config = await JsonSerializer.DeserializeAsync<ServerConfig>(stream, options, cancellation) ?? throw new InvalidDataException($"The config file {path} is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"The config file {path} is not valid: {exception.Message}", exception);
            }
        }
        else
        {
            config = new() { Key = RandomNumberGenerator.GetHexString(64, true) };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(config, options), cancellation);
        }

        return config with { DataFolder = folder };
    }
}
