namespace Markwardt.PersonalAssistant;

/// <summary>
/// The server refused or failed a request, or the connection to it failed.
/// </summary>
/// <param name="message">What went wrong, written for the user.</param>
public sealed class RemoteException(string message) : Exception(message)
{
}
