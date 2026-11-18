namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Keeps one value in a JSON file of the data folder, written through a temporary file so that a crash cannot leave a half-written file.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
internal interface IJsonFile<T>
    where T : class
{
    /// <summary>
    /// Reads the saved value.
    /// </summary>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The value, or <see langword="null" /> when none was saved or the file cannot be read.</returns>
    Task<T?> Load(CancellationToken cancellation = default);

    /// <summary>
    /// Saves the value, replacing the earlier one.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellation">Cancels the write.</param>
    Task Save(T value, CancellationToken cancellation = default);
}

/// <inheritdoc />
/// <param name="path">The file.</param>
internal sealed class JsonFile<T>(string path) : IJsonFile<T>
    where T : class
{
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc />
    public async Task<T?> Load(CancellationToken cancellation = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellation);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task Save(T value, CancellationToken cancellation = default)
    {
        string temporary = path + ".tmp";

        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, options), cancellation);
        File.Move(temporary, path, true);
    }
}
