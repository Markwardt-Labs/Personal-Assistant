namespace Markwardt.PersonalAssistant;

/// <summary>
/// One message of the conversation between the user and the assistant.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>
    /// Gets the position of the message in the conversation, counting from zero, which lets a client that joins mid-stream tell what it has already seen.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets who wrote the message.
    /// </summary>
    public required ChatRole Role { get; init; }

    /// <summary>
    /// Gets the text of the message.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets when the message was written.
    /// </summary>
    public required DateTimeOffset Time { get; init; }
}
