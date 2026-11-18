namespace Markwardt.PersonalAssistant.Tests.Integration;

public sealed class EndToEndTests : IAsyncLifetime
{
    public EndToEndTests()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
    }

    private readonly TempFolder folder = new();

    private readonly CancellationTokenSource stopping = new();

    private readonly FakePrompter prompter = new();

    private readonly int port;

    private readonly Mock<IMailDigestService> digest = new();

    private readonly Mock<IAgendaService> agenda = new();

    private readonly Mock<INewsService> news = new();

    private readonly Mock<IMailReader> mail = new();

    private readonly Mock<IPreferenceService> preferences = new();

    private readonly Mock<IServerUpdater> updater = new();

    private readonly Mock<ISettingsManager> settingsManager = new();

    private readonly EventHub eventsHub = new();

    private AssistantService assistant = null!;

    private Task server = Task.CompletedTask;

    public async Task InitializeAsync()
    {
        prompter.OnCreate = session => session.Reply("You have a dentist visit at 3 pm.");
        ServerConfig config = new() { Port = port, Key = "secret", DataFolder = folder.Path };
        TimeProvider time = TimeProvider.System;
        Mock<ILog> log = new();
        EventHub events = eventsHub;
        preferences.Setup(item => item.Background(true)).Returns(string.Empty);
        assistant = new(new AssistantSessions(prompter, new FakeSetups(), new JsonFile<StoredSession>(folder.Combine("session.json")), new AssistantInstructions()), new AssistantInstructions(), new FakeSetups(), preferences.Object, new ChatStore(folder.Combine("chat.jsonl"), time), events, log.Object);
        Mock<IAppInfo> info = new();
        info.SetupGet(item => item.Version).Returns("9.9.9");
        ConnectionServer connection = new(new Handshake(info.Object), new IMessageChannel.Factory(), new CertificateProvider(), new RequestHandler(assistant, digest.Object, agenda.Object, news.Object, mail.Object, preferences.Object, settingsManager.Object, updater.Object, log.Object), events, log.Object);
        TaskCompletionSource listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Listening += _ => listening.TrySetResult();
        server = Task.Run(() => connection.Run(config, stopping.Token));
        await Task.WhenAny(listening.Task, server).WaitAsync(TimeSpan.FromSeconds(20));
        await (server.IsFaulted ? server : Task.CompletedTask);
    }

    public async Task DisposeAsync()
    {
        await stopping.CancelAsync();
        await server;
        await assistant.Close();
        folder.Dispose();
    }

    private Task<IServerConnection> Open(string key = "secret")
        => new IServerConnection.Factory(new IMessageChannel.Factory()).Open(new() { Host = "localhost", Port = port, AccessKey = key }, CancellationToken.None);

    [Fact]
    public async Task ReportsTheServerVersionToAClientWithTheRightKey()
    {
        await using IServerConnection connection = await Open();

        Assert.Equal("9.9.9", connection.ServerVersion);
    }

    [Fact]
    public async Task RefusesAClientWithTheWrongKey()
    {
        RemoteException exception = await Assert.ThrowsAsync<RemoteException>(() => Open("wrong"));

        Assert.Equal("The server did not accept the access key.", exception.Message);
    }

    [Fact]
    public async Task AnswersAPlainBrowserRequestSoTheCertificateCanBeAccepted()
    {
        using HttpClient http = new(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });

        Assert.Equal("Personal Assistant server", await http.GetStringAsync($"https://localhost:{port}/"));
    }

    [Fact]
    public async Task CarriesAPromptToTheAssistantAndTheRepliesBack()
    {
        await using IServerConnection connection = await Open();
        Channel<RemoteEvent> received = Channel.CreateUnbounded<RemoteEvent>();
        connection.EventReceived += item => received.Writer.TryWrite(item);

        await connection.Prompt("What is on today?", CancellationToken.None);
        List<ChatMessage> messages = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));

        await foreach (RemoteEvent item in received.Reader.ReadAllAsync(timeout.Token))
        {
            if (item.Message is { } message)
            {
                messages.Add(message);
            }

            if (messages.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(["What is on today?", "You have a dentist visit at 3 pm."], messages.Select(message => message.Text));
        ChatHistory history = await connection.GetHistory(null, CancellationToken.None);
        Assert.Equal(2, history.Messages.Count);
    }

    [Fact]
    public async Task ShowsTheSameConversationToAClientThatJoinsLater()
    {
        await using (IServerConnection first = await Open())
        {
            await first.Prompt("Hello", CancellationToken.None);
        }

        await using IServerConnection second = await Open();
        ChatHistory history = await Wait(second);

        Assert.Equal("Hello", history.Messages[0].Text);
    }

    [Fact]
    public async Task ReportsAnErrorForAnEmptyPromptWithoutLosingTheConnection()
    {
        await using IServerConnection connection = await Open();

        RemoteException exception = await Assert.ThrowsAsync<RemoteException>(() => connection.Prompt(" ", CancellationToken.None));

        Assert.Equal("The prompt is empty.", exception.Message);
        Assert.NotNull(await connection.GetHistory(null, CancellationToken.None));
    }

    [Fact]
    public async Task TellsTheClientWhenTheServerStops()
    {
        await using IServerConnection connection = await Open();
        TaskCompletionSource<string> lost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Lost += reason => lost.TrySetResult(reason);

        await stopping.CancelAsync();

        Assert.False(string.IsNullOrEmpty(await lost.Task.WaitAsync(TimeSpan.FromSeconds(20))));
        await Assert.ThrowsAsync<RemoteException>(() => connection.GetHistory(null, CancellationToken.None));
    }

    [Fact]
    public async Task FailsToConnectToAServerThatIsNotThere()
    {
        stopping.Cancel();
        await server;

        await Assert.ThrowsAsync<RemoteException>(() => Open());
    }

    [Fact]
    public async Task CarriesTheMailDigestAndAMessageToTheClient()
    {
        MailDigestState state = new() { Generating = false, Digest = new() { Generated = DateTimeOffset.UnixEpoch, Topics = [new() { Id = "t1", Title = "Dentist", Summary = "Appointment moved.", Importance = MailImportance.High, Reaction = ReactionKind.Like, Messages = [new() { Id = "m1", From = "a@example.com", Subject = "Moved", Received = DateTimeOffset.UnixEpoch, Preview = "To 3 pm" }] }] } };
        digest.Setup(item => item.Get(true, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        mail.Setup(item => item.Read("m1", It.IsAny<CancellationToken>())).ReturnsAsync(new MailMessage { Id = "m1", From = "a@example.com", To = "me@example.com", Subject = "Moved", Received = DateTimeOffset.UnixEpoch, Body = "See you at 3 pm." });
        await using IServerConnection connection = await Open();

        MailDigestState received = await connection.GetMailDigest(true, CancellationToken.None);
        MailMessage message = await connection.ReadMail("m1", CancellationToken.None);

        MailTopic topic = Assert.Single(received.Digest!.Topics);
        Assert.Equal(("Dentist", MailImportance.High, ReactionKind.Like), (topic.Title, topic.Importance, topic.Reaction));
        Assert.Equal("See you at 3 pm.", message.Body);
    }

    [Fact]
    public async Task CarriesTheAgendaAndTheEventFeedsToTheClient()
    {
        AgendaState state = new() { From = DateTimeOffset.UnixEpoch, To = DateTimeOffset.UnixEpoch.AddDays(60), Events = [new() { Id = "e1", Title = "Dentist", Start = DateTimeOffset.UnixEpoch.AddDays(1), End = DateTimeOffset.UnixEpoch.AddDays(1).AddHours(1) }], Feeds = [new() { Id = "f1", Name = "Jazz", Description = "jazz" }], Suggestions = [new() { Id = "s1", FeedId = "f1", Title = "Jazz Night", Start = DateTimeOffset.UnixEpoch.AddDays(2), Description = "Live trio.", Url = "https://example.com/jazz" }], Generating = false };
        agenda.Setup(item => item.Get(false, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        agenda.Setup(item => item.AddFeed("Blues", "live blues", It.IsAny<CancellationToken>())).ReturnsAsync(state);
        agenda.Setup(item => item.RemoveFeed("f1", It.IsAny<CancellationToken>())).ReturnsAsync(state);
        await using IServerConnection connection = await Open();

        AgendaState received = await connection.GetAgenda(false, CancellationToken.None);
        await connection.AddEventFeed("Blues", "live blues", CancellationToken.None);
        await connection.RemoveEventFeed("f1", CancellationToken.None);

        Assert.Equal("Dentist", Assert.Single(received.Events).Title);
        Assert.Equal("Jazz Night", Assert.Single(received.Suggestions).Title);
        agenda.Verify(item => item.AddFeed("Blues", "live blues", It.IsAny<CancellationToken>()), Times.Once);
        agenda.Verify(item => item.RemoveFeed("f1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CarriesTheNewsAndTheNewsFeedsToTheClient()
    {
        NewsState state = new() { Generating = false, Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }], Items = [new() { Id = "n1", FeedId = "f1", Title = "New chip", Summary = "Faster.", Url = "https://example.com/chip" }] };
        news.Setup(item => item.Get(false, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        news.Setup(item => item.AddFeed("Sport", "football", It.IsAny<CancellationToken>())).ReturnsAsync(state);
        news.Setup(item => item.RemoveFeed("f1", It.IsAny<CancellationToken>())).ReturnsAsync(state);
        await using IServerConnection connection = await Open();

        NewsState received = await connection.GetNews(false, CancellationToken.None);
        await connection.AddNewsFeed("Sport", "football", CancellationToken.None);
        await connection.RemoveNewsFeed("f1", CancellationToken.None);

        Assert.Equal("New chip", Assert.Single(received.Items).Title);
        news.Verify(item => item.AddFeed("Sport", "football", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CarriesReactionsAndTheServerUpdateToTheServer()
    {
        await using IServerConnection connection = await Open();

        await connection.React(ReactionTarget.Event, "https://example.com/jazz", "Jazz Night", "jazz", ReactionKind.Dislike, CancellationToken.None);
        await connection.UpdateServer(CancellationToken.None);

        preferences.Verify(item => item.React(ReactionTarget.Event, "https://example.com/jazz", "Jazz Night", "jazz", ReactionKind.Dislike, It.IsAny<CancellationToken>()), Times.Once);
        updater.Verify(item => item.Update(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushesChangesOfTheMailDigestToTheClient()
    {
        await using IServerConnection connection = await Open();
        TaskCompletionSource<RemoteEvent> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.EventReceived += item => received.TrySetResult(item);
        await connection.GetHistory(null, CancellationToken.None);

        eventsHub.Publish(new() { Kind = RemoteEventKind.MailDigestChanged, MailDigest = new() { Generating = true } });

        RemoteEvent remoteEvent = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(remoteEvent.MailDigest!.Generating);
    }

    [Fact]
    public async Task CarriesTheSettingsToTheServer()
    {
        ServerSettings state = new() { Location = "Austin" };
        ServerSettingsUpdate update = new() { Location = "Austin" };
        settingsManager.Setup(item => item.Get(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        settingsManager.Setup(item => item.Update(It.IsAny<ServerSettingsUpdate>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);
        await using IServerConnection connection = await Open();

        ServerSettings read = await connection.GetSettings(CancellationToken.None);
        await connection.SetSettings(update, CancellationToken.None);

        Assert.Equal("Austin", read.Location);
        settingsManager.Verify(item => item.Update(It.Is<ServerSettingsUpdate>(given => given.Location == "Austin"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task<ChatHistory> Wait(IServerConnection connection)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));

        while (true)
        {
            ChatHistory history = await connection.GetHistory(null, timeout.Token);

            if (history.Messages.Count >= 2)
            {
                return history;
            }

            await Task.Delay(50, timeout.Token);
        }
    }
}
