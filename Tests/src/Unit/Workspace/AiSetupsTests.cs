namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class AiSetupsTests : IDisposable
{
    private readonly TempFolder folder = new();

    private readonly Mock<IClaudeShim> shim = new();

    public void Dispose()
        => folder.Dispose();

    [Fact]
    public async Task MakesAClaudeSetupInTheWorkspaceAndPreparesIt()
    {
        AgentWorkspace workspace = new(folder.Path);
        AiSetups setups = new(workspace, shim.Object);

        PromptSetup setup = await setups.Create(CancellationToken.None);

        Assert.Equal((typeof(ClaudeSetup), workspace.Path), (setup.GetType(), setup.WorkspacePath));
        Assert.True(File.Exists(Path.Combine(workspace.Preferences, "README.md")));
    }

    [Fact]
    public void AsksForTheConnectorsWithTheMarkerOfTheLauncher()
    {
        shim.SetupGet(item => item.Marker).Returns("MARKER");
        AiSetups setups = new(new AgentWorkspace(folder.Path), shim.Object);

        Assert.Equal("Be brief.\n\nMARKER", setups.WithConnectors("Be brief."));
    }
}
