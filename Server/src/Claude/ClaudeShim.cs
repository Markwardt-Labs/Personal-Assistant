namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The launcher that stands between the AI library and Claude Code so that selected sessions can use the user's Gmail and Google Calendar connectors of their claude.ai account. The library always starts Claude Code with <c>--strict-mcp-config</c>, which leaves out every account connector, so the server hands the library its own executable as the command and this launcher starts the real <c>claude</c> with the arguments changed for the sessions that ask for connectors.
/// </summary>
internal interface IClaudeShim
{
    /// <summary>
    /// Gets the text a session puts in its instructions to be given the connectors; every other session keeps the library's arguments as they are and so has no connectors.
    /// </summary>
    string Marker { get; }

    /// <summary>
    /// Gets the name of the environment variable, set to <c>1</c> by the server for the processes it starts, that tells a launcher from the server.
    /// </summary>
    string Variable { get; }

    /// <summary>
    /// Gets the command the AI library is told to start in place of <c>claude</c>, which is this executable.
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Tells whether this process was started by the AI library as the stand-in for Claude Code instead of being the server.
    /// </summary>
    /// <param name="arguments">The command line arguments of the process.</param>
    /// <returns><see langword="true" /> if the process is the launcher.</returns>
    bool IsShim(IReadOnlyList<string> arguments);

    /// <summary>
    /// Changes the arguments of a session that asked for the connectors so that the connectors are loaded and allowed.
    /// </summary>
    /// <param name="arguments">The arguments the library gave.</param>
    /// <returns>The arguments to start Claude Code with, which are the given ones when the session did not ask.</returns>
    IReadOnlyList<string> Rewrite(IReadOnlyList<string> arguments);

    /// <summary>
    /// Starts the real Claude Code with the rewritten arguments, which shares the input, output and error streams of this process, and waits for it to end.
    /// </summary>
    /// <param name="arguments">The arguments the library gave.</param>
    /// <param name="cancellation">Ends Claude Code and everything it started when cancelled.</param>
    /// <returns>The exit code of Claude Code.</returns>
    Task<int> Run(IReadOnlyList<string> arguments, CancellationToken cancellation = default);
}

/// <inheritdoc />
/// <param name="processPath">The path of this executable, or <see langword="null" /> when it is not known.</param>
/// <param name="baseFolder">The folder of the application files, where the executable is looked for when the process is run by the <c>dotnet</c> command.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="run">Starts a process and gives its exit code.</param>
internal sealed class ClaudeShim(string? processPath, string baseFolder, Func<string, string?> environment, Func<ProcessStartInfo, CancellationToken, Task<int>> run) : IClaudeShim
{
    private readonly string connectors = "mcp__claude_ai_Gmail,mcp__claude_ai_Google_Calendar";

    /// <inheritdoc />
    public string Variable { get; } = "PERSONAL_ASSISTANT_CLAUDE_SHIM";

    /// <inheritdoc />
    public string Marker { get; } = "Connectors: the user's Gmail and Google Calendar tools are available in this session.";

    /// <inheritdoc />
    public string Command => Executable();

    /// <inheritdoc />
    public bool IsShim(IReadOnlyList<string> arguments)
        => arguments.Count > 0 && arguments[0] == "-p" && environment(Variable) == "1";

    /// <inheritdoc />
    public IReadOnlyList<string> Rewrite(IReadOnlyList<string> arguments)
    {
        int instructions = arguments.ToList().IndexOf("--append-system-prompt");

        if (instructions < 0 || instructions + 1 >= arguments.Count || !arguments[instructions + 1].Contains(Marker, StringComparison.Ordinal))
        {
            return arguments;
        }

        List<string> changed = [.. arguments.Where(argument => argument != "--strict-mcp-config")];
        int allowed = changed.IndexOf("--allowedTools");

        if (allowed >= 0 && allowed + 1 < changed.Count)
        {
            changed[allowed + 1] = $"{changed[allowed + 1]},{connectors}";
        }
        else
        {
            changed.AddRange(["--allowedTools", connectors]);
        }

        return changed;
    }

    /// <inheritdoc />
    public Task<int> Run(IReadOnlyList<string> arguments, CancellationToken cancellation = default)
    {
        ProcessStartInfo start = new("claude") { UseShellExecute = false };

        foreach (string argument in Rewrite(arguments))
        {
            start.ArgumentList.Add(argument);
        }

        return run(start, cancellation);
    }

    private string Executable()
    {
        string name = Path.GetFileNameWithoutExtension(processPath ?? string.Empty);

        if (processPath is not null && !string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        return Path.Combine(baseFolder, OperatingSystem.IsWindows() ? "Markwardt.PersonalAssistant.Server.exe" : "Markwardt.PersonalAssistant.Server");
    }
}
