namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// A message of the conversation as the chat shows it.
/// </summary>
/// <param name="message">The message.</param>
internal sealed class MessageViewModel(ChatMessage message)
{
    /// <summary>
    /// Gets the position of the message in the conversation.
    /// </summary>
    public int Index { get; } = message.Index;

    /// <summary>
    /// Gets the text.
    /// </summary>
    public string Text { get; } = message.Text;

    /// <summary>
    /// Gets a value indicating whether the user wrote the message.
    /// </summary>
    public bool IsUser { get; } = message.Role == ChatRole.User;
}
