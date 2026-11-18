namespace Markwardt.PersonalAssistant;

/// <summary>
/// The newest part of the conversation plus whether the assistant is working.
/// </summary>
public sealed record ChatHistory
{
    /// <summary>
    /// Gets the messages in order, oldest first.
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assistant is working on a prompt.
    /// </summary>
    public required bool Working { get; init; }
}
