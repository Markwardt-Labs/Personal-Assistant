namespace Markwardt.PersonalAssistant;

/// <inheritdoc />
internal sealed class MessageChannel(WebSocket socket) : IMessageChannel
{
    private readonly SemaphoreSlim sending = new(1, 1);

    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc />
    public async Task Send<T>(T message, CancellationToken cancellation = default)
    {
        ReadOnlyMemory<byte> bytes = JsonSerializer.SerializeToUtf8Bytes(message, options);

        await sending.WaitAsync(cancellation);

        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellation);
        }
        finally
        {
            sending.Release();
        }
    }

    /// <inheritdoc />
    public async Task<T?> Receive<T>(int limit, CancellationToken cancellation = default)
        where T : class
    {
        using MemoryStream message = new();
        byte[] buffer = new byte[8192];
        ValueWebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), cancellation);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidDataException("Received a message that is not text.");
            }

            if (message.Length + result.Count > limit)
            {
                throw new InvalidDataException("Received a message that is too long.");
            }

            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        try
        {
            return JsonSerializer.Deserialize<T>(message.ToArray(), options);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Received an invalid message: {exception.Message}", exception);
        }
    }
}
