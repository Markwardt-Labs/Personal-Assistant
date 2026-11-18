namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class MailViewModelTests
{
    private readonly Mock<IServerConnection> connection = new();

    private readonly CultureInfo culture = CultureInfo.GetCultureInfo("en-US");

    private readonly Eventually eventually = new();

    private MailViewModel Create()
        => new(connection.Object, culture);

    private MailTopic Topic(string title, MailImportance importance, params string[] ids)
        => new() { Id = title, Title = title, Summary = $"About {title}.", Importance = importance, Messages = [.. ids.Select((id, index) => new MailSummary { Id = id, From = "Anna <anna@example.com>", Subject = $"Subject {id}", Received = DateTimeOffset.UnixEpoch.AddHours(index), Preview = $"Preview {id}", IsUnread = id == "u" })] };

    private MailDigestState State(params MailTopic[] topics)
        => new() { Generating = false, Digest = new() { Generated = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero), Topics = topics } };

    private void Push(MailDigestState state)
        => connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MailDigestChanged, MailDigest = state });

    [Fact]
    public async Task ShowsTheImportantTopicsInOrderAndCollapsesSpamAdsAndUnimportantOnes()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(Topic("Urgent", MailImportance.Critical, "a"), Topic("Friends", MailImportance.High, "b"), Topic("Updates", MailImportance.Normal, "c"), Topic("Newsletter", MailImportance.Low, "d"), Topic("Store ads", MailImportance.Junk, "e", "f", "g")));
        MailViewModel mail = Create();

        await mail.Load();

        Assert.Equal(["Urgent", "Friends", "Updates"], mail.Topics.Select(topic => topic.Title));
        Assert.Equal(["Newsletter", "Store ads"], mail.OtherTopics.Select(topic => topic.Title));
        Assert.True(mail.HasOther);
        Assert.Equal("Spam, ads and unimportant (2)", mail.OtherHeader);
        Assert.False(mail.IsOtherExpanded);
        mail.ToggleOtherCommand.Execute(null);
        Assert.True(mail.IsOtherExpanded);
        mail.ToggleOtherCommand.Execute(null);
        Assert.False(mail.IsOtherExpanded);
    }

    [Fact]
    public async Task ShowsAMultiMessageTopicAsOneEntryWithItsCountAndLabels()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(Topic("Store ads", MailImportance.Junk, "e", "f", "g"), Topic("Urgent", MailImportance.Critical, "u")));
        MailViewModel mail = Create();

        await mail.Load();

        MailTopicViewModel ads = Assert.Single(mail.OtherTopics);
        Assert.Equal("3 emails", ads.CountText);
        Assert.False(ads.HasImportanceLabel);
        MailTopicViewModel urgent = Assert.Single(mail.Topics);
        Assert.Equal(("1 email", "Critical", true, true), (urgent.CountText, urgent.ImportanceLabel, urgent.IsCritical, urgent.HasUnread));
    }

    [Fact]
    public async Task ReadsTheDigestOnlyTheFirstTimeAndShowsWhenItWasMade()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State());
        MailViewModel mail = Create();

        await mail.Load();
        await mail.Load();

        connection.Verify(item => item.GetMailDigest(false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(mail.IsEmpty);
    }

    [Fact]
    public async Task AsksAgainWhenTheTabIsShownWhileThereIsNothingToShow()
    {
        connection.SetupSequence(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(new MailDigestState { Generating = false, Error = "The Gmail connector is not connected." }).ReturnsAsync(State(Topic("Dentist", MailImportance.High, "a")));
        MailViewModel mail = Create();

        await mail.Load();
        Assert.True(mail.HasDigestError);
        Assert.Empty(mail.Topics);

        await mail.Load();

        Assert.False(mail.HasDigestError);
        Assert.Single(mail.Topics);
        connection.Verify(item => item.GetMailDigest(false, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DoesNotAskAgainWhileTheServerIsMakingTheDigest()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(new MailDigestState { Generating = true });
        MailViewModel mail = Create();

        await mail.Load();
        await mail.Load();

        connection.Verify(item => item.GetMailDigest(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void FollowsTheDigestThePushedByTheServerAndItsErrors()
    {
        MailViewModel mail = Create();

        Push(new MailDigestState { Generating = false, Error = "The Gmail connector is not connected." });

        Assert.True(mail.HasDigestError);
        Assert.Equal("The Gmail connector is not connected.", mail.DigestError);

        Push(State(Topic("Urgent", MailImportance.Critical, "a")));

        Assert.False(mail.HasDigestError);
        Assert.Single(mail.Topics);
    }

    [Fact]
    public async Task OpensATopicWithSeveralMessagesAndThenOneOfItsMessages()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(Topic("Store ads", MailImportance.Junk, "e", "f")));
        connection.Setup(item => item.ReadMail("f", It.IsAny<CancellationToken>())).ReturnsAsync(new MailMessage { Id = "f", From = "Anna <anna@example.com>", To = "me@example.com", Cc = "sam@example.com", Subject = "Subject f", Received = DateTimeOffset.UnixEpoch, Body = "The full text." });
        MailViewModel mail = Create();
        await mail.Load();

        mail.OtherTopics[0].OpenCommand.Execute(null);

        MailTopicViewModel topic = Assert.IsType<MailTopicViewModel>(mail.Page);
        Assert.Equal(["Subject e", "Subject f"], topic.Items.Select(item => item.Subject));
        Assert.True(mail.HasPage);
        Assert.False(mail.IsList);

        topic.Items[1].OpenCommand.Execute(null);
        MailMessageViewModel message = Assert.IsType<MailMessageViewModel>(mail.Page);
        await eventually.Be(() => !message.IsLoading);

        Assert.Equal(("The full text.", "me@example.com", "sam@example.com", "Anna"), (message.Body, message.To, message.Cc, message.From));
        Assert.True(message.HasCc);
    }

    [Fact]
    public async Task OpensTheMessageDirectlyWhenTheTopicHasOnlyOne()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(Topic("Dentist", MailImportance.High, "a")));
        connection.Setup(item => item.ReadMail("a", It.IsAny<CancellationToken>())).ReturnsAsync(new MailMessage { Id = "a", From = "d@example.com", To = "me@example.com", Subject = "Subject a", Received = DateTimeOffset.UnixEpoch, Body = "Text" });
        MailViewModel mail = Create();
        await mail.Load();

        mail.Topics[0].OpenCommand.Execute(null);

        Assert.IsType<MailMessageViewModel>(mail.Page);
    }

    [Fact]
    public async Task StepsBackFromAMessageToItsTopicAndFromThereToTheList()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(Topic("Store ads", MailImportance.Junk, "e", "f")));
        MailViewModel mail = Create();
        await mail.Load();
        mail.OtherTopics[0].OpenCommand.Execute(null);
        MailTopicViewModel topic = (MailTopicViewModel)mail.Page!;
        topic.Items[0].OpenCommand.Execute(null);

        Assert.True(mail.GoBack());
        Assert.Same(topic, mail.Page);
        Assert.True(mail.GoBack());
        Assert.Null(mail.Page);
        Assert.False(mail.GoBack());
    }

    [Fact]
    public async Task StepsBackFromASingleMessageStraightToTheList()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(Topic("Dentist", MailImportance.High, "a")));
        MailViewModel mail = Create();
        await mail.Load();
        mail.Topics[0].OpenCommand.Execute(null);

        Assert.True(mail.GoBack());

        Assert.Null(mail.Page);
    }

    [Fact]
    public async Task ShowsWhyAMessageCouldNotBeRead()
    {
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(Topic("Dentist", MailImportance.High, "a")));
        connection.Setup(item => item.ReadMail("a", It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The Gmail connector is not connected."));
        MailViewModel mail = Create();
        await mail.Load();

        mail.Topics[0].OpenCommand.Execute(null);
        MailMessageViewModel message = (MailMessageViewModel)mail.Page!;
        await eventually.Be(() => message.HasError);

        Assert.Equal("The Gmail connector is not connected.", message.Error);
        Assert.False(message.IsLoading);
    }

    [Fact]
    public async Task GivesEachTopicTheLikeAndDislikeButtonsForItsKey()
    {
        MailTopic topic = Topic("Tax Deadline", MailImportance.High, "a") with { Reaction = ReactionKind.Like };
        connection.Setup(item => item.GetMailDigest(false, It.IsAny<CancellationToken>())).ReturnsAsync(State(topic));
        MailViewModel mail = Create();
        await mail.Load();

        mail.Topics[0].Reaction.DislikeCommand.Execute(null);
        await eventually.Be(() => mail.Topics[0].Reaction.IsDisliked);

        Assert.False(mail.Topics[0].Reaction.IsLiked);
        connection.Verify(item => item.React(ReactionTarget.Mail, "tax deadline", "Tax Deadline", "High", ReactionKind.Dislike, It.IsAny<CancellationToken>()), Times.Once);
    }
}
