namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Where the server writes what an operator should see.
/// </summary>
internal interface ILog
{
    /// <summary>
    /// Writes a line of information.
    /// </summary>
    /// <param name="message">The line.</param>
    void Info(string message);

    /// <summary>
    /// Writes a failure.
    /// </summary>
    /// <param name="message">What failed.</param>
    /// <param name="exception">The exception behind it, if any.</param>
    void Error(string message, Exception? exception = null);
}

/// <inheritdoc />
internal sealed class ConsoleLog(TextWriter output, TimeProvider time) : ILog
{
    /// <inheritdoc />
    public void Info(string message)
        => output.WriteLine($"{time.GetLocalNow():HH:mm:ss} {message}");

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null)
        => Info(exception is null ? $"Error: {message}" : $"Error: {message} ({exception.GetType().Name}: {exception.Message})");
}
