namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class AgentWorkspaceTests : IDisposable
{
    private readonly TempFolder folder = new();

    public void Dispose()
        => folder.Dispose();

    [Fact]
    public void LivesInAWorkspaceFolderInsideTheAppFolder()
    {
        AgentWorkspace workspace = new(folder.Path);

        Assert.Equal(folder.Combine("Workspace"), workspace.Path);
        Assert.Equal(folder.Combine("Workspace/Preferences"), workspace.Preferences);
    }

    [Fact]
    public async Task MakesTheFoldersAndAReadmeThatExplainsHowToOrganizeThePreferences()
    {
        AgentWorkspace workspace = new(folder.Path);

        await workspace.Prepare(CancellationToken.None);

        string readme = await File.ReadAllTextAsync(folder.Combine("Workspace/Preferences/README.md"));
        Assert.Contains("This folder is your memory about the user", readme);
        Assert.Contains("one file per area", readme);
        Assert.Contains("Reactions.md is written by the server", readme);
    }

    [Fact]
    public async Task NeverOverwritesTheReadmeTheAgentMayHaveEdited()
    {
        AgentWorkspace workspace = new(folder.Path);
        await workspace.Prepare(CancellationToken.None);
        await File.WriteAllTextAsync(folder.Combine("Workspace/Preferences/README.md"), "edited by the agent");

        await workspace.Prepare(CancellationToken.None);

        Assert.Equal("edited by the agent", await File.ReadAllTextAsync(folder.Combine("Workspace/Preferences/README.md")));
    }
}
