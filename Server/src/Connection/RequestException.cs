namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// A request cannot be done, for a reason the client should be told.
/// </summary>
/// <param name="message">The reason, written for the user.</param>
internal sealed class RequestException(string message) : Exception(message)
{
}
