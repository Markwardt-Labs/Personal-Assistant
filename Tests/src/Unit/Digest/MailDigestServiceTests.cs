namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class MailDigestServiceTests : IAsyncDisposable
{
    public MailDigestServiceTests()
    {
        Mock<TimeProvider> time = new();
        time.Setup(item => item.GetUtcNow()).Returns(now);
        preferences.Setup(item => item.Background(false)).Returns("\nlikes jazz");
        service = new(ai.Object, store.Object, preferences.Object, schedule.Object, events, log.Object, time.Object);
    }

    private readonly Mock<IMailDigestAi> ai = new();

    private readonly Mock<IJsonFile<MailDigest>> store = new();

    private readonly Mock<IPreferenceService> preferences = new();

    private readonly Mock<IDailySchedule> schedule = new();

    private readonly Mock<ILog> log = new();

    private readonly EventHub events = new();

    private readonly DateTimeOffset now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly MailDigestService service;

    public async ValueTask DisposeAsync()
        => await service.Close();

    private MailSummary Message(string id, int hoursAgo = 1)
        => new() { Id = id, From = "a@example.com", Subject = $"Subject {id}", Received = now.AddHours(-hoursAgo), Preview = $"Preview {id}" };

    private MailTopicDraft Draft(string title, MailImportance importance, params MailSummary[] messages)
        => new() { Title = title, Summary = $"About {title}.", Importance = importance, Messages = messages };

    private void Answer(IReadOnlyList<MailTopicDraft> drafts, string? problem = null)
        => ai.Setup(item => item.Digest(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new MailDigestResult { Topics = drafts, Problem = problem });

    private async Task<MailDigestState> Generate(IReadOnlyList<MailTopicDraft> drafts, string? problem = null)
    {
        Answer(drafts, problem);
        using IEventSubscription subscription = events.Subscribe();
        await service.Get(true, CancellationToken.None);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        MailDigestState finished = null!;

        await foreach (RemoteEvent remoteEvent in subscription.Events.ReadAllAsync(timeout.Token))
        {
            if (remoteEvent.MailDigest is { Generating: false } state)
            {
                finished = state;
                break;
            }
        }

        return finished;
    }

    [Fact]
    public async Task AsksForTheLastSevenDaysWithoutSentMailAndGivesTheAiWhatWasLearned()
    {
        MailSummary message = Message("a");

        await Generate([Draft("Topic", MailImportance.Normal, message)]);

        ai.Verify(item => item.Digest("newer_than:7d -in:sent -in:drafts", 200, now, "\nlikes jazz", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OrdersTopicsByImportanceAndThenByTheNewestMessage()
    {
        MailDigestState state = await Generate([Draft("Ads", MailImportance.Junk, Message("a", 5)), Draft("Old normal", MailImportance.Normal, Message("c", 3)), Draft("New normal", MailImportance.Normal, Message("b", 1)), Draft("Urgent", MailImportance.Critical, Message("d", 2))]);

        Assert.Equal(["Urgent", "New normal", "Old normal", "Ads"], state.Digest!.Topics.Select(topic => topic.Title));
        Assert.Equal(["t1", "t2", "t3", "t4"], state.Digest.Topics.Select(topic => topic.Id));
    }

    [Fact]
    public async Task OneTopicStandsForAllItsMessagesNewestFirst()
    {
        MailDigestState state = await Generate([Draft("Store ads", MailImportance.Junk, Message("a", 3), Message("b", 1), Message("c", 2))]);

        MailTopic topic = Assert.Single(state.Digest!.Topics);
        Assert.Equal(["b", "c", "a"], topic.Messages.Select(message => message.Id));
    }

    [Fact]
    public async Task DropsRepeatedAndBlankIdsAndTopicsLeftWithNothing()
    {
        MailDigestState state = await Generate([Draft("First", MailImportance.High, Message("a"), Message(" "), Message("a")), Draft("Repeats", MailImportance.High, Message("a")), Draft("Empty", MailImportance.High), Draft("Second", MailImportance.Normal, Message("b"))]);

        Assert.Equal(["First", "Second"], state.Digest!.Topics.Select(topic => topic.Title));
        Assert.Equal(["a"], state.Digest.Topics[0].Messages.Select(message => message.Id));
    }

    [Fact]
    public async Task FallsBackToTheSubjectAndPreviewForABlankTitleAndSummary()
    {
        MailDigestState state = await Generate([new MailTopicDraft { Title = " ", Summary = "", Importance = MailImportance.Normal, Messages = [Message("a")] }]);

        MailTopic topic = Assert.Single(state.Digest!.Topics);
        Assert.Equal(("Subject a", "Preview a"), (topic.Title, topic.Summary));
    }

    [Fact]
    public async Task MakesAnEmptyDigestWhenThereIsNoMail()
    {
        MailDigestState state = await Generate([]);

        Assert.Empty(state.Digest!.Topics);
        Assert.Null(state.Error);
    }

    [Fact]
    public async Task SavesTheDigestAndMarksTheReactionOfTheUserOnTopics()
    {
        preferences.Setup(item => item.Of(ReactionTarget.Mail, "tax deadline")).Returns(ReactionKind.Dislike);

        MailDigestState state = await Generate([Draft("Tax Deadline", MailImportance.High, Message("a"))]);

        Assert.Equal(ReactionKind.Dislike, state.Digest!.Topics.Single().Reaction);
        store.Verify(item => item.Save(It.Is<MailDigest>(saved => saved.Topics.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportsTheProblemTheAiFoundAndKeepsTheOldDigest()
    {
        MailDigest old = new() { Generated = now.AddDays(-1), Topics = [] };
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(old);

        MailDigestState failed = await Generate([], " The Gmail connector is not connected. ");

        Assert.Equal("The Gmail connector is not connected.", failed.Error);
        Assert.Equal(old.Generated, failed.Digest!.Generated);
        store.Verify(item => item.Save(It.IsAny<MailDigest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportsWhyTheDigestCouldNotBeMadeWhenTheAiFailed()
    {
        ai.Setup(item => item.Digest(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("The AI could not be started."));
        using IEventSubscription subscription = events.Subscribe();

        await service.Get(true, CancellationToken.None);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        MailDigestState failed = null!;

        await foreach (RemoteEvent remoteEvent in subscription.Events.ReadAllAsync(timeout.Token))
        {
            if (remoteEvent.MailDigest is { Generating: false } state)
            {
                failed = state;
                break;
            }
        }

        Assert.Equal("The AI could not be started.", failed.Error);
    }

    [Fact]
    public async Task StartsAGenerationWhenThereIsNoDigestAndOnlyThenWithoutARefresh()
    {
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new MailDigest { Generated = now, Topics = [] });

        MailDigestState state = await service.Get(false, CancellationToken.None);

        Assert.False(state.Generating);
        Assert.NotNull(state.Digest);
        ai.Verify(item => item.Digest(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportsThatItIsGeneratingWhenAGenerationWasJustStarted()
    {
        TaskCompletionSource<MailDigestResult> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ai.Setup(item => item.Digest(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(release.Task);

        MailDigestState state = await service.Get(true, CancellationToken.None);

        Assert.True(state.Generating);
        release.SetResult(new MailDigestResult { Topics = [] });
    }

    [Fact]
    public async Task RunsTheDailyScheduleWhenStarted()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        schedule.Setup(item => item.Run(It.IsAny<Func<DateTimeOffset?>>(), It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            started.TrySetResult();
            return Task.CompletedTask;
        });

        service.Start(CancellationToken.None);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
