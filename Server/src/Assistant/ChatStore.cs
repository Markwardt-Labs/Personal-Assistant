namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Keeps the conversation shown to the user: what was said, in order, in a file, which is the server's own record and not the AI tool's stored conversation.
/// </summary>
internal interface IChatStore
{
    /// <summary>
    /// Reads the newest messages.
    /// </summary>
    /// <param name="limit">The most messages to read.</param>
    /// <param name="cancellation">Cancels the read.</param>
    /// <returns>The messages in order, oldest first.</returns>
    Task<IReadOnlyList<ChatMessage>> Read(int limit, CancellationToken cancellation = default);

    /// <summary>
    /// Appends a message to the conversation.
    /// </summary>
    /// <param name="role">Who wrote it.</param>
    /// <param name="text">The text.</param>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>The stored message with its index.</returns>
    Task<ChatMessage> Add(ChatRole role, string text, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class ChatStore(string path, TimeProvider time) : IChatStore
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly List<ChatMessage> messages = [];

    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private bool loaded;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> Read(int limit, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);

        try
        {
            await Load(cancellation);
            return [.. messages.TakeLast(limit)];
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ChatMessage> Add(ChatRole role, string text, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);

        try
        {
            await Load(cancellation);
            ChatMessage message = new() { Index = messages.Count, Role = role, Text = text, Time = time.GetUtcNow() };
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(message, options) + "\n", cancellation);
            messages.Add(message);
            return message;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task Load(CancellationToken cancellation = default)
    {
        if (loaded)
        {
            return;
        }

        if (File.Exists(path))
        {
            foreach (string line in await File.ReadAllLinesAsync(path, cancellation))
            {
                if (line.Length > 0 && JsonSerializer.Deserialize<ChatMessage>(line, options) is { } message)
                {
                    messages.Add(message);
                }
            }
        }

        loaded = true;
    }
}
