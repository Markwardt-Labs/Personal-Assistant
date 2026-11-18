namespace Markwardt.PersonalAssistant.Tests;

internal sealed class FakeSetups(string workspace = "/workspace", bool codex = false) : IAiSetups
{
    public static readonly string Marker = "[connectors]";

    public Task<PromptSetup> Create(CancellationToken cancellation = default)
        => Task.FromResult<PromptSetup>(codex ? new CodexSetup { WorkspacePath = workspace } : new ClaudeSetup { WorkspacePath = workspace });

    public string WithConnectors(string instructions)
        => $"{instructions}\n{Marker}";
}
