namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class PreferenceAiTests
{
    private readonly FakePrompter prompter = new();

    private readonly StoredReaction reaction = new() { Target = ReactionTarget.News, Key = "k", Title = "New chip", Context = "Tech", Kind = ReactionKind.Like, Time = DateTimeOffset.UnixEpoch };

    private PreferenceAi Create()
        => new(prompter, new FakeSetups());

    [Fact]
    public async Task TellsTheAgentTheNewReactionsAndWhatToDoWithThem()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            created.Reply("Done.");
        };

        await Create().Organize([reaction], "\nbackground", CancellationToken.None);

        string prompt = session!.Prompts.Single().Text;
        Assert.Contains("- liked news \"New chip\" (Tech)", prompt);
        Assert.Contains("Do not edit Reactions.md", prompt);
        Assert.EndsWith("\nbackground", session.Configuration.Instructions);
    }

    [Fact]
    public async Task WorksInTheWorkspaceInASessionThatIsDeletedAfterwards()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            created.Reply("Done.");
        };

        await Create().Organize([reaction], string.Empty, CancellationToken.None);

        Assert.Equal("/workspace", Assert.IsType<ClaudeSetup>(Assert.Single(prompter.Created)).WorkspacePath);
        Assert.True(session!.IsDisposed);
        Assert.Empty(await prompter.List(CancellationToken.None));
    }

    [Fact]
    public async Task ReportsAnAiThatFailed()
    {
        prompter.OnCreate = session => session.Fail("offline");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Create().Organize([reaction], string.Empty, CancellationToken.None));

        Assert.Contains("offline", exception.Message);
    }
}
