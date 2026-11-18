namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class AssistantServiceTests : IAsyncDisposable
{
    public AssistantServiceTests()
    {
        sessions.Setup(item => item.Open(It.IsAny<CancellationToken>())).ReturnsAsync(session);
        chat.Setup(item => item.Add(It.IsAny<ChatRole>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ChatRole role, string text, CancellationToken _) =>
        {
            ChatMessage message = new() { Index = stored.Count, Role = role, Text = text, Time = DateTimeOffset.UnixEpoch };
            stored.Add(message);
            return message;
        });
        chat.Setup(item => item.Read(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => stored);
        instructions.SetupGet(item => item.Text).Returns("Be helpful.");
        preferences.Setup(item => item.Background(true)).Returns("\nThe user likes jazz.");
        service = new(sessions.Object, instructions.Object, new FakeSetups(), preferences.Object, chat.Object, events, log.Object);
    }

    private readonly FakePromptSession session = new();

    private readonly Mock<IAssistantSessions> sessions = new();

    private readonly Mock<IChatStore> chat = new();

    private readonly Mock<ILog> log = new();

    private readonly Mock<IAssistantInstructions> instructions = new();

    private readonly Mock<IPreferenceService> preferences = new();

    private readonly EventHub events = new();

    private readonly AssistantService service;

    private readonly List<ChatMessage> stored = [];

    public async ValueTask DisposeAsync()
        => await service.Close();

    private async Task<List<RemoteEvent>> Collect(IEventSubscription subscription, Func<List<RemoteEvent>, bool> done)
    {
        List<RemoteEvent> collected = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        await foreach (RemoteEvent remoteEvent in subscription.Events.ReadAllAsync(timeout.Token))
        {
            collected.Add(remoteEvent);

            if (done(collected))
            {
                break;
            }
        }

        return collected;
    }

    [Fact]
    public async Task RefusesAnEmptyPrompt()
    {
        await Assert.ThrowsAsync<RequestException>(() => service.Prompt("  ", CancellationToken.None));
        await Assert.ThrowsAsync<RequestException>(() => service.Prompt(null, CancellationToken.None));
        sessions.Verify(item => item.Open(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordsThePromptAndTheReplyAndPublishesBoth()
    {
        session.Reply("It is done.");
        using IEventSubscription subscription = events.Subscribe();

        await service.Prompt("Book the dentist", CancellationToken.None);
        List<RemoteEvent> collected = await Collect(subscription, list => list.Count(item => item.Kind == RemoteEventKind.MessageAdded) == 2);

        ChatMessage[] messages = [.. collected.Where(item => item.Kind == RemoteEventKind.MessageAdded).Select(item => item.Message!)];
        Assert.Equal([ChatRole.User, ChatRole.Assistant], messages.Select(message => message.Role));
        Assert.Equal(["Book the dentist", "It is done."], messages.Select(message => message.Text));
        Assert.Equal([0, 1], messages.Select(message => message.Index));
    }

    [Fact]
    public async Task ReportsWhenTheAssistantStartsAndStopsWorking()
    {
        session.Reply("Done.");
        using IEventSubscription subscription = events.Subscribe();

        await service.Prompt("Hello", CancellationToken.None);
        List<RemoteEvent> collected = await Collect(subscription, list => list.Count(item => item.Kind == RemoteEventKind.WorkingChanged) == 2);

        Assert.Equal([true, false], collected.Where(item => item.Kind == RemoteEventKind.WorkingChanged).Select(item => item.Working));
    }

    [Fact]
    public async Task ReadsTheHistoryWithTheWorkingState()
    {
        stored.Add(new() { Index = 0, Role = ChatRole.User, Text = "Hi", Time = DateTimeOffset.UnixEpoch });

        ChatHistory history = await service.History(null, CancellationToken.None);

        Assert.Equal("Hi", Assert.Single(history.Messages).Text);
        Assert.False(history.Working);
        chat.Verify(item => item.Read(100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(40, 40)]
    [InlineData(9000, 500)]
    public async Task KeepsTheHistoryLimitReasonable(int requested, int expected)
    {
        await service.History(requested, CancellationToken.None);

        chat.Verify(item => item.Read(expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TellsTheUserWhenTheAiFails()
    {
        session.Fail("The tool is not signed in.");
        using IEventSubscription subscription = events.Subscribe();

        await service.Prompt("Hello", CancellationToken.None);
        List<RemoteEvent> collected = await Collect(subscription, list => list.Count(item => item.Kind == RemoteEventKind.MessageAdded) == 2);

        Assert.Contains("The tool is not signed in.", collected.Last(item => item.Kind == RemoteEventKind.MessageAdded).Message!.Text);
        Assert.Equal(ChatRole.Assistant, collected.Last(item => item.Kind == RemoteEventKind.MessageAdded).Message!.Role);
    }

    [Fact]
    public async Task ExplainsWhyTheAiCouldNotBeStarted()
    {
        sessions.Setup(item => item.Open(It.IsAny<CancellationToken>())).ThrowsAsync(new PromptSessionException("claude is not installed"));

        RequestException exception = await Assert.ThrowsAsync<RequestException>(() => service.Prompt("Hello", CancellationToken.None));

        Assert.Contains("claude is not installed", exception.Message);
    }

    [Fact]
    public async Task InterruptDoesNothingBeforeTheFirstPrompt()
    {
        Assert.False(await service.Interrupt(CancellationToken.None));
    }

    [Fact]
    public async Task GivesTheAiWhatItLearnedAboutTheUserWithEveryPrompt()
    {
        session.Reply("Done.");

        await service.Prompt("Hello", CancellationToken.None);

        Assert.Equal($"Be helpful.\nThe user likes jazz.\n{FakeSetups.Marker}", session.Configuration.Instructions);
    }

    [Theory]
    [InlineData("create_event")]
    [InlineData("update_event")]
    [InlineData("delete_event")]
    [InlineData("respond_to_event")]
    public async Task ReportsACalendarEditWhenTheWorkIsDone(string tool)
    {
        int edits = 0;
        service.CalendarEdited += () => edits++;
        session.Reply(new PromptToolCallEvent { Id = "1", Server = "claude_ai_Google_Calendar", Name = tool }, new PromptMessageEvent { Text = "Done." });
        using IEventSubscription subscription = events.Subscribe();

        await service.Prompt("Move the dentist", CancellationToken.None);
        await Collect(subscription, list => list.Count(item => item.Kind == RemoteEventKind.WorkingChanged) == 2);

        Assert.Equal(1, edits);
    }

    [Fact]
    public async Task ReportsNoCalendarEditForReadingTheCalendarOrOtherTools()
    {
        int edits = 0;
        service.CalendarEdited += () => edits++;
        session.Reply(
            new PromptToolCallEvent { Id = "1", Server = "claude_ai_Google_Calendar", Name = "list_events" },
            new PromptToolCallEvent { Id = "2", Server = "claude_ai_Gmail", Name = "create_event" },
            new PromptMessageEvent { Text = "Done." });
        using IEventSubscription subscription = events.Subscribe();

        await service.Prompt("What is on today", CancellationToken.None);
        await Collect(subscription, list => list.Count(item => item.Kind == RemoteEventKind.WorkingChanged) == 2);

        Assert.Equal(0, edits);
    }
}
