namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class RequestHandlerTests
{
    private readonly Mock<IAssistantService> assistant = new();

    private readonly Mock<IMailDigestService> digest = new();

    private readonly Mock<IAgendaService> agenda = new();

    private readonly Mock<INewsService> news = new();

    private readonly Mock<IMailReader> mail = new();

    private readonly Mock<IPreferenceService> preferences = new();

    private readonly Mock<ISettingsManager> settings = new();

    private readonly Mock<IServerUpdater> updater = new();

    private readonly Mock<ILog> log = new();

    private RequestHandler Create()
        => new(assistant.Object, digest.Object, agenda.Object, news.Object, mail.Object, preferences.Object, settings.Object, updater.Object, log.Object);

    [Fact]
    public async Task AnswersGetHistoryWithTheConversation()
    {
        ChatHistory history = new() { Messages = [], Working = true };
        assistant.Setup(item => item.History(25, It.IsAny<CancellationToken>())).ReturnsAsync(history);

        RemoteResponse response = await Create().Handle(new() { Id = 4, Command = RemoteCommand.GetHistory, Limit = 25 }, CancellationToken.None);

        Assert.Equal(new RemoteResponse { Id = 4, History = history }, response);
    }

    [Fact]
    public async Task GivesAPromptToTheAssistant()
    {
        RemoteResponse response = await Create().Handle(new() { Id = 2, Command = RemoteCommand.Prompt, Text = "Hi" }, CancellationToken.None);

        Assert.Equal(new RemoteResponse { Id = 2 }, response);
        assistant.Verify(item => item.Prompt("Hi", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InterruptsTheAssistant()
    {
        RemoteResponse response = await Create().Handle(new() { Id = 3, Command = RemoteCommand.Interrupt }, CancellationToken.None);

        Assert.Null(response.Error);
        assistant.Verify(item => item.Interrupt(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TurnsARequestExceptionIntoTheErrorOfTheAnswer()
    {
        assistant.Setup(item => item.Prompt(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestException("The prompt is empty."));

        RemoteResponse response = await Create().Handle(new() { Id = 9, Command = RemoteCommand.Prompt }, CancellationToken.None);

        Assert.Equal(new RemoteResponse { Id = 9, Error = "The prompt is empty." }, response);
    }

    [Fact]
    public async Task HidesUnexpectedFailuresFromTheClientAndLogsThem()
    {
        assistant.Setup(item => item.History(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArithmeticException("disk on fire"));

        RemoteResponse response = await Create().Handle(new() { Id = 5, Command = RemoteCommand.GetHistory }, CancellationToken.None);

        Assert.Equal("The server failed to do that.", response.Error);
        log.Verify(item => item.Error(It.IsAny<string>(), It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task RefusesACommandItDoesNotKnow()
    {
        RemoteResponse response = await Create().Handle(new() { Id = 1, Command = (RemoteCommand)99 }, CancellationToken.None);

        Assert.Equal("That command is not known.", response.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnswersTheMailDigestAndPassesTheRefreshFlag(bool refresh)
    {
        MailDigestState state = new() { Generating = true };
        digest.Setup(item => item.Get(refresh, It.IsAny<CancellationToken>())).ReturnsAsync(state);

        RemoteResponse response = await Create().Handle(new() { Id = 1, Command = RemoteCommand.GetMailDigest, Refresh = refresh }, CancellationToken.None);

        Assert.Same(state, response.MailDigest);
    }

    [Fact]
    public async Task ReadsAMessage()
    {
        MailMessage message = new() { Id = "m1", From = "a@example.com", To = "me@example.com", Subject = "Hi", Received = DateTimeOffset.UnixEpoch, Body = "Text" };
        mail.Setup(item => item.Read("m1", It.IsAny<CancellationToken>())).ReturnsAsync(message);

        RemoteResponse response = await Create().Handle(new() { Id = 2, Command = RemoteCommand.ReadMail, MessageId = "m1" }, CancellationToken.None);

        Assert.Same(message, response.Mail);
    }

    [Fact]
    public async Task TellsTheUserWhyAMessageCannotBeRead()
    {
        mail.Setup(item => item.Read(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestException("The Gmail connector is not connected."));

        RemoteResponse response = await Create().Handle(new() { Id = 2, Command = RemoteCommand.ReadMail }, CancellationToken.None);

        Assert.Equal("The Gmail connector is not connected.", response.Error);
    }

    [Fact]
    public async Task AnswersTheAgendaAndManagesEventFeeds()
    {
        AgendaState state = new() { From = DateTimeOffset.UnixEpoch, To = DateTimeOffset.UnixEpoch, Events = [], Feeds = [], Suggestions = [], Generating = false };
        agenda.Setup(item => item.Get(true, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        agenda.Setup(item => item.AddFeed("Jazz", "live jazz", It.IsAny<CancellationToken>())).ReturnsAsync(state);
        agenda.Setup(item => item.RemoveFeed("f1", It.IsAny<CancellationToken>())).ReturnsAsync(state);

        Assert.Same(state, (await Create().Handle(new() { Id = 1, Command = RemoteCommand.GetAgenda, Refresh = true }, CancellationToken.None)).Agenda);
        Assert.Same(state, (await Create().Handle(new() { Id = 2, Command = RemoteCommand.AddEventFeed, Name = "Jazz", Text = "live jazz" }, CancellationToken.None)).Agenda);
        Assert.Same(state, (await Create().Handle(new() { Id = 3, Command = RemoteCommand.RemoveEventFeed, FeedId = "f1" }, CancellationToken.None)).Agenda);
    }

    [Fact]
    public async Task AnswersTheNewsAndManagesNewsFeeds()
    {
        NewsState state = new() { Feeds = [], Items = [], Generating = false };
        news.Setup(item => item.Get(false, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        news.Setup(item => item.AddFeed("Tech", "chips", It.IsAny<CancellationToken>())).ReturnsAsync(state);
        news.Setup(item => item.RemoveFeed("f1", It.IsAny<CancellationToken>())).ReturnsAsync(state);

        Assert.Same(state, (await Create().Handle(new() { Id = 1, Command = RemoteCommand.GetNews }, CancellationToken.None)).News);
        Assert.Same(state, (await Create().Handle(new() { Id = 2, Command = RemoteCommand.AddNewsFeed, Name = "Tech", Text = "chips" }, CancellationToken.None)).News);
        Assert.Same(state, (await Create().Handle(new() { Id = 3, Command = RemoteCommand.RemoveNewsFeed, FeedId = "f1" }, CancellationToken.None)).News);
    }

    [Fact]
    public async Task PassesAReactionToTheServerThatLearnsFromIt()
    {
        RemoteResponse response = await Create().Handle(new() { Id = 7, Command = RemoteCommand.React, Target = ReactionTarget.News, Key = "https://example.com/a", Title = "Chips", Context = "Tech", Reaction = ReactionKind.Like }, CancellationToken.None);

        Assert.Null(response.Error);
        preferences.Verify(item => item.React(ReactionTarget.News, "https://example.com/a", "Chips", "Tech", ReactionKind.Like, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefusesAReactionWithoutAKindOfEntryOrAReaction()
    {
        Assert.Equal("The kind of entry is missing.", (await Create().Handle(new() { Id = 1, Command = RemoteCommand.React, Reaction = ReactionKind.Like }, CancellationToken.None)).Error);
        Assert.Equal("The reaction is missing.", (await Create().Handle(new() { Id = 2, Command = RemoteCommand.React, Target = ReactionTarget.Mail }, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task UpdatesTheServer()
    {
        RemoteResponse response = await Create().Handle(new() { Id = 1, Command = RemoteCommand.UpdateServer }, CancellationToken.None);

        Assert.Null(response.Error);
        updater.Verify(item => item.Update(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task ReportsWhyTheServerCouldNotUpdate(Type failure)
    {
        updater.Setup(item => item.Update(It.IsAny<CancellationToken>())).ThrowsAsync((Exception)Activator.CreateInstance(failure, "The folder is read only.")!);

        RemoteResponse response = await Create().Handle(new() { Id = 1, Command = RemoteCommand.UpdateServer }, CancellationToken.None);

        Assert.Equal("The folder is read only.", response.Error);
    }

    [Fact]
    public async Task ReadsAndChangesTheSettings()
    {
        ServerSettings state = new() { Location = "Austin" };
        ServerSettingsUpdate update = new() { Location = "Austin" };
        settings.Setup(item => item.Get(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        settings.Setup(item => item.Update(update, It.IsAny<CancellationToken>())).ReturnsAsync(state);

        Assert.Same(state, (await Create().Handle(new() { Id = 1, Command = RemoteCommand.GetSettings }, CancellationToken.None)).Settings);
        Assert.Same(state, (await Create().Handle(new() { Id = 2, Command = RemoteCommand.SetSettings, Settings = update }, CancellationToken.None)).Settings);
    }

    [Fact]
    public async Task RefusesToChangeSettingsWithoutNewSettings()
    {
        RemoteResponse response = await Create().Handle(new() { Id = 1, Command = RemoteCommand.SetSettings }, CancellationToken.None);

        Assert.Equal("The settings are missing.", response.Error);
    }
}
