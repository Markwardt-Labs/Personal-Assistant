namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Runs the server: reads the config and serves clients until stopped.
/// </summary>
internal interface IApplication
{
    /// <summary>
    /// Runs until cancelled.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="cancellation">Stops the server.</param>
    /// <returns>The process exit code.</returns>
    Task<int> Run(IReadOnlyList<string> args, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class Application(IAppInfo info, IConfigLoader loader, Func<ServerConfig, IConnectionServer> createServer, TextWriter output) : IApplication
{
    /// <inheritdoc />
    public async Task<int> Run(IReadOnlyList<string> args, CancellationToken cancellation = default)
    {
        ServerConfig config;

        try
        {
            config = await loader.Load(args, cancellation);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            await output.WriteLineAsync(exception.Message);
            return 1;
        }

        IConnectionServer server = createServer(config);
        server.Certified += fingerprint => output.WriteLine($"Certificate SHA-256: {fingerprint}");
        server.Listening += port => output.WriteLine($"{info.Name} {info.Version} is listening on port {port}. App folder: {config.DataFolder}. Access key: {config.Key}");

        try
        {
            await server.Run(config, cancellation);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or SocketException or CryptographicException)
        {
            await output.WriteLineAsync($"The server could not start: {exception.Message}");
            return 1;
        }
    }
}
