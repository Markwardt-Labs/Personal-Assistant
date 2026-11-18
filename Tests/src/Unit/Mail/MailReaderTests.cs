namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class MailReaderTests
{
    private readonly FakePrompter prompter = new();

    private int asked;

    private MailReader Create()
        => new(prompter, new FakeSetups());

    private MailMessage Message(string id = "m1", string body = "See you at 3 pm.")
        => new() { Id = id, From = "a@example.com", To = "me@example.com", Subject = "Moved", Received = DateTimeOffset.UnixEpoch, Body = body };

    private void Answer(MailReadResult result)
        => prompter.OnCreate = session =>
        {
            asked++;
            session.Reply(result);
        };

    [Fact]
    public async Task ReadsTheMessageWithTheAiAndTheGmailTools()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            asked++;
            created.Reply(new MailReadResult { Message = Message() });
        };

        MailMessage message = await Create().Read("m1", CancellationToken.None);

        Assert.Equal("See you at 3 pm.", message.Body);
        Assert.EndsWith(FakeSetups.Marker, session!.Configuration.Instructions);
        Assert.Contains("m1", session.Prompts.Single().Text);
        Assert.True(session.IsDisposed);
        Assert.Empty(await prompter.List(CancellationToken.None));
    }

    [Fact]
    public async Task KeepsAMessageThatWasReadSoTheAiIsNotAskedAgain()
    {
        Answer(new MailReadResult { Message = Message() });
        MailReader reader = Create();

        await reader.Read("m1", CancellationToken.None);
        MailMessage again = await reader.Read("m1", CancellationToken.None);

        Assert.Equal(1, asked);
        Assert.Equal("m1", again.Id);
    }

    [Fact]
    public async Task CutsAVeryLongBody()
    {
        Answer(new MailReadResult { Message = Message(body: new string('x', 30000)) });

        MailMessage message = await Create().Read("m1", CancellationToken.None);

        Assert.Equal(20000, message.Body.Length);
    }

    [Fact]
    public async Task ForgetsTheOldestMessagesWhenTooManyAreKept()
    {
        Answer(new MailReadResult { Message = Message() });
        MailReader reader = Create();

        for (int number = 0; number < 101; number++)
        {
            await reader.Read($"id{number}", CancellationToken.None);
        }

        await reader.Read("id0", CancellationToken.None);

        Assert.Equal(102, asked);
    }

    [Fact]
    public async Task RefusesAMissingIdentifier()
    {
        await Assert.ThrowsAsync<RequestException>(() => Create().Read(" ", CancellationToken.None));
        await Assert.ThrowsAsync<RequestException>(() => Create().Read(null, CancellationToken.None));
        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task TellsWhyAMessageCouldNotBeRead()
    {
        Answer(new MailReadResult { Problem = " The message does not exist. " });

        RequestException exception = await Assert.ThrowsAsync<RequestException>(() => Create().Read("gone", CancellationToken.None));

        Assert.Equal("The message does not exist.", exception.Message);
    }

    [Fact]
    public async Task GivesAGeneralReasonWhenTheAiSaidNothing()
    {
        Answer(new MailReadResult());

        RequestException exception = await Assert.ThrowsAsync<RequestException>(() => Create().Read("gone", CancellationToken.None));

        Assert.Equal("The message could not be read.", exception.Message);
    }

    [Fact]
    public async Task ReportsAnAiThatFailed()
    {
        prompter.OnCreate = session => session.Fail("The tool is not signed in.");

        RequestException exception = await Assert.ThrowsAsync<RequestException>(() => Create().Read("m1", CancellationToken.None));

        Assert.Contains("The tool is not signed in.", exception.Message);
    }
}
