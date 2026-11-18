namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Makes the setup of every AI session the server starts: Claude Code with the workspace of the agent as its folder.
/// </summary>
internal interface IAiSetups
{
    /// <summary>
    /// Makes a setup.
    /// </summary>
    /// <param name="cancellation">Cancels the work.</param>
    /// <returns>The setup.</returns>
    Task<PromptSetup> Create(CancellationToken cancellation = default);

    /// <summary>
    /// Adds to the instructions of a session what it needs to be given the user's Gmail and Google Calendar tools, which a session only has when its instructions say so.
    /// </summary>
    /// <param name="instructions">The instructions of the session.</param>
    /// <returns>The instructions with the request for the connectors.</returns>
    string WithConnectors(string instructions);
}

/// <inheritdoc />
internal sealed class AiSetups(IAgentWorkspace workspace, IClaudeShim shim) : IAiSetups
{
    /// <inheritdoc />
    public async Task<PromptSetup> Create(CancellationToken cancellation = default)
    {
        await workspace.Prepare(cancellation);
        return new ClaudeSetup { WorkspacePath = workspace.Path };
    }

    /// <inheritdoc />
    public string WithConnectors(string instructions)
        => $"{instructions}\n\n{shim.Marker}";
}
