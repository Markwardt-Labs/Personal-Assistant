namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class AssistantSessionsTests : IDisposable
{
    public AssistantSessionsTests()
    {
        instructions.SetupGet(item => item.Text).Returns("Be helpful.");
    }

    private readonly TempFolder folder = new();

    private readonly FakePrompter prompter = new();

    private readonly Mock<IAssistantInstructions> instructions = new();

    public void Dispose()
        => folder.Dispose();

    private AssistantSessions Create(bool codex = false)
        => new(prompter, new FakeSetups(codex: codex), new JsonFile<StoredSession>(folder.Combine("session.json")), instructions.Object);

    [Fact]
    public async Task CreatesAClaudeSessionInTheWorkspaceAndRemembersIt()
    {
        await using IPromptSession session = await Create().Open(CancellationToken.None);

        ClaudeSetup setup = Assert.IsType<ClaudeSetup>(Assert.Single(prompter.Created));
        Assert.Equal("/workspace", setup.WorkspacePath);
        StoredSession? stored = await new JsonFile<StoredSession>(folder.Combine("session.json")).Load(CancellationToken.None);
        Assert.Equal(session.Id, stored!.Id);
    }

    [Fact]
    public async Task ConfiguresTheSessionAsTheAssistantWithTheConnectors()
    {
        await using IPromptSession session = await Create().Open(CancellationToken.None);

        Assert.Equal(PromptCapability.Balanced, session.Configuration.Capability);
        Assert.Equal(PromptEffort.Medium, session.Configuration.Effort);
        Assert.True(session.Configuration.IsWebAllowed);
        Assert.False(session.Configuration.IsShellAllowed);
        Assert.Equal($"Be helpful.\n{FakeSetups.Marker}", session.Configuration.Instructions);
        Assert.Empty(session.Transient.Tools);
    }

    [Fact]
    public async Task LoadsTheStoredSessionAgainAfterARestart()
    {
        string id;

        await using (IPromptSession first = await Create().Open(CancellationToken.None))
        {
            id = first.Id;
        }

        await using IPromptSession second = await Create().Open(CancellationToken.None);

        Assert.Equal(id, second.Id);
        Assert.Single(prompter.Created);
        Assert.Contains(FakeSetups.Marker, second.Configuration.Instructions);
    }

    [Fact]
    public async Task MakesANewSessionWhenTheStoredOneIsGone()
    {
        await new JsonFile<StoredSession>(folder.Combine("session.json")).Save(new() { Id = "gone" }, CancellationToken.None);

        await using IPromptSession session = await Create().Open(CancellationToken.None);

        Assert.NotEqual("gone", session.Id);
        Assert.Equal(session.Id, (await new JsonFile<StoredSession>(folder.Combine("session.json")).Load(CancellationToken.None))!.Id);
    }

    [Fact]
    public async Task MakesANewSessionWhenTheStoredOneRunsOnAnotherAiTool()
    {
        IPromptSession session = await Create(true).Open(CancellationToken.None);
        string first = session.Id;

        await using IPromptSession second = await Create().Open(CancellationToken.None);

        Assert.NotEqual(first, second.Id);
        Assert.Equal(2, prompter.Created.Count);
        Assert.IsType<ClaudeSetup>(prompter.Created[1]);
    }
}
