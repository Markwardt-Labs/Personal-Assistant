namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class MailDigestAiTests
{
    private readonly FakePrompter prompter = new();

    private readonly DateTimeOffset now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private MailSummary Message(string id)
        => new() { Id = id, From = "a@example.com", Subject = "Sale", Received = DateTimeOffset.UnixEpoch, Preview = "50% off" };

    private MailDigestAi Create()
        => new(prompter, new FakeSetups());

    private Task<MailDigestResult> Digest(string background = "")
        => Create().Digest("newer_than:7d", 200, now, background, CancellationToken.None);

    [Fact]
    public async Task ReadsTheTopicsAndTheirMessagesOfTheStructuredAnswer()
    {
        prompter.OnCreate = session => session.Reply(new MailDigestResult { Topics = [new() { Title = "Store ads", Summary = "One sale.", Importance = MailImportance.Junk, Messages = [Message("m1")] }] });

        MailDigestResult result = await Digest("\nbackground");

        MailTopicDraft topic = Assert.Single(result.Topics);
        Assert.Equal(("Store ads", MailImportance.Junk), (topic.Title, topic.Importance));
        Assert.Equal(["m1"], topic.Messages.Select(message => message.Id));
        Assert.Null(result.Problem);
    }

    [Fact]
    public async Task PassesTheProblemTheAiReported()
    {
        prompter.OnCreate = session => session.Reply(new MailDigestResult { Topics = [], Problem = "The Gmail connector is not connected." });

        MailDigestResult result = await Digest();

        Assert.Equal("The Gmail connector is not connected.", result.Problem);
        Assert.Empty(result.Topics);
    }

    [Fact]
    public async Task AsksInASessionOfItsOwnWithTheConnectorsAndTheBackgroundAndDeletesItAfterwards()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            created.Reply(new MailDigestResult { Topics = [] });
        };

        await Digest("\nlikes jazz");

        Assert.Contains("\nlikes jazz", session!.Configuration.Instructions);
        Assert.EndsWith(FakeSetups.Marker, session.Configuration.Instructions);
        Assert.False(session.Configuration.IsWebAllowed);
        Assert.True(session.IsDisposed);
        Assert.Empty(await prompter.List(CancellationToken.None));
    }

    [Fact]
    public async Task ReportsAnAiThatFailed()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            created.Fail("The tool is not signed in.");
        };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Digest());

        Assert.Contains("The tool is not signed in.", exception.Message);
        Assert.True(session!.IsDisposed);
    }

    [Fact]
    public async Task ReportsAnAnswerThatIsNotTopics()
    {
        prompter.OnCreate = session => session.Reply("I could not do that.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Digest());
    }
}
