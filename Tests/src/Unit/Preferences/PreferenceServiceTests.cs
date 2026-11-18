namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class PreferenceServiceTests : IAsyncDisposable, IDisposable
{
    public PreferenceServiceTests()
    {
        workspace = new(folder.Path);
        store.Setup(item => item.Save(It.IsAny<StoredReactions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Callback<StoredReactions, CancellationToken>((value, _) => saved = value);
        service = new(ai.Object, store.Object, workspace, log.Object, TimeProvider.System);
    }

    private readonly TempFolder folder = new();

    private readonly Mock<IPreferenceAi> ai = new();

    private readonly Mock<IJsonFile<StoredReactions>> store = new();

    private readonly Mock<ILog> log = new();

    private readonly AgentWorkspace workspace;

    private readonly PreferenceService service;

    private StoredReactions? saved;

    public async ValueTask DisposeAsync()
        => await service.Close();

    public void Dispose()
        => folder.Dispose();

    private string Reactions()
        => File.ReadAllText(Path.Combine(workspace.Preferences, "Reactions.md"));

    [Fact]
    public void PointsAnAiAtThePreferenceFilesOfItsWorkspaceAndSaysWhetherItMayWrite()
    {
        string reading = service.Background(false);
        string writing = service.Background(true);

        Assert.Contains("Your workspace folder is your memory about this user", reading);
        Assert.Contains("Reactions.md lists the entries they liked and disliked", reading);
        Assert.Contains("Only read the workspace", reading);
        Assert.DoesNotContain("write it into the right file", reading);
        Assert.Contains("write it into the right file of the Preferences folder", writing);
        Assert.Contains("Never edit Reactions.md", writing);
        Assert.DoesNotContain("Only read the workspace", writing);
    }

    [Fact]
    public async Task RemembersAReactionAndWritesTheReactionsFileOfTheWorkspace()
    {
        await service.React(ReactionTarget.News, "https://example.com/a", "New chip", "Tech", ReactionKind.Like, CancellationToken.None);

        Assert.Equal(ReactionKind.Like, service.Of(ReactionTarget.News, "https://example.com/a"));
        Assert.Contains("liked news \"New chip\" (Tech)", Reactions());
        Assert.Contains("Never edit this file", Reactions());
        Assert.Single(saved!.Reactions);
    }

    [Fact]
    public async Task PreparesTheWorkspaceWithAReadmeThatTellsTheAgentHowToKeepItsNotes()
    {
        await service.React(ReactionTarget.News, "k", "Entry", null, ReactionKind.Like, CancellationToken.None);

        string readme = await File.ReadAllTextAsync(Path.Combine(workspace.Preferences, "README.md"));
        Assert.Contains("This folder is your memory about the user", readme);
        Assert.Contains("Reactions.md is written by the server", readme);
    }

    [Fact]
    public async Task ReplacesAnEarlierReactionToTheSameEntryAndTakesItBackWithNone()
    {
        await service.React(ReactionTarget.Event, "https://example.com/e", "Jazz Night", null, ReactionKind.Like, CancellationToken.None);
        await service.React(ReactionTarget.Event, "https://example.com/e", "Jazz Night", null, ReactionKind.Dislike, CancellationToken.None);

        Assert.Equal(ReactionKind.Dislike, service.Of(ReactionTarget.Event, "https://example.com/e"));
        Assert.Single(saved!.Reactions);
        Assert.Contains("disliked event \"Jazz Night\"", Reactions());

        await service.React(ReactionTarget.Event, "https://example.com/e", "Jazz Night", null, ReactionKind.None, CancellationToken.None);

        Assert.Equal(ReactionKind.None, service.Of(ReactionTarget.Event, "https://example.com/e"));
        Assert.Empty(saved.Reactions);
        Assert.DoesNotContain("Jazz Night", Reactions());
    }

    [Fact]
    public async Task KeepsReactionsToDifferentKindsOfEntriesApart()
    {
        await service.React(ReactionTarget.Mail, "same", "Mail topic", null, ReactionKind.Like, CancellationToken.None);
        await service.React(ReactionTarget.News, "same", "News entry", null, ReactionKind.Dislike, CancellationToken.None);

        Assert.Equal((ReactionKind.Like, ReactionKind.Dislike), (service.Of(ReactionTarget.Mail, "same"), service.Of(ReactionTarget.News, "same")));
    }

    [Theory]
    [InlineData(null, "title")]
    [InlineData("", "title")]
    [InlineData("key", null)]
    [InlineData("key", " ")]
    public async Task RefusesAReactionWithoutAKeyOrATitle(string? key, string? title)
    {
        await Assert.ThrowsAsync<RequestException>(() => service.React(ReactionTarget.News, key, title, null, ReactionKind.Like, CancellationToken.None));
    }

    [Fact]
    public async Task KeepsOnlyTheMostRecentReactionsAndKeepsTheTitlesOnOneLine()
    {
        for (int number = 0; number < 160; number++)
        {
            await service.React(ReactionTarget.News, $"k{number}", number == 159 ? "Entry\n159" : $"Entry {number}", null, ReactionKind.Like, CancellationToken.None);
        }

        Assert.Equal(150, saved!.Reactions.Count);
        Assert.Equal(150, Reactions().Split('\n').Count(line => line.StartsWith("- ", StringComparison.Ordinal)));
        Assert.Contains("\"Entry 159\"", Reactions());
        Assert.DoesNotContain("\"Entry 9\"", Reactions());
    }

    [Fact]
    public async Task HasTheAgentFoldTheNewReactionsIntoItsFilesOnceTenCameIn()
    {
        TaskCompletionSource<IReadOnlyList<StoredReaction>> given = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ai.Setup(item => item.Organize(It.IsAny<IReadOnlyList<StoredReaction>>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((IReadOnlyList<StoredReaction> reactions, string _, CancellationToken _) =>
        {
            given.TrySetResult(reactions);
            return Task.CompletedTask;
        });

        for (int number = 0; number < 10; number++)
        {
            await service.React(ReactionTarget.News, $"k{number}", $"Entry {number}", "Tech", ReactionKind.Like, CancellationToken.None);
        }

        Assert.Equal(10, (await given.Task.WaitAsync(TimeSpan.FromSeconds(5))).Count);
        await service.Close();
        Assert.NotNull(saved!.ProfiledThrough);
        ai.Verify(item => item.Organize(It.IsAny<IReadOnlyList<StoredReaction>>(), service.Background(true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoesNotFoldBeforeTenReactionsAndKeepsGoingWhenTheAiFails()
    {
        ai.Setup(item => item.Organize(It.IsAny<IReadOnlyList<StoredReaction>>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("offline"));

        for (int number = 0; number < 9; number++)
        {
            await service.React(ReactionTarget.News, $"k{number}", $"Entry {number}", null, ReactionKind.Like, CancellationToken.None);
        }

        ai.Verify(item => item.Organize(It.IsAny<IReadOnlyList<StoredReaction>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await service.React(ReactionTarget.News, "k9", "Entry 9", null, ReactionKind.Like, CancellationToken.None);
        await service.Close();

        log.Verify(item => item.Error(It.IsAny<string>(), It.IsAny<Exception?>()), Times.Once);
        Assert.Null(saved!.ProfiledThrough);
    }

    [Fact]
    public async Task OnlyHandsOverReactionsThatCameSinceTheLastFold()
    {
        DateTimeOffset through = DateTimeOffset.UtcNow.AddMinutes(-1);
        store.Setup(item => item.Load(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredReactions { ProfiledThrough = through, Reactions = [new() { Target = ReactionTarget.News, Key = "old", Title = "Old", Kind = ReactionKind.Like, Time = through.AddMinutes(-5) }] });
        List<int> sizes = [];
        ai.Setup(item => item.Organize(It.IsAny<IReadOnlyList<StoredReaction>>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((IReadOnlyList<StoredReaction> reactions, string _, CancellationToken _) =>
        {
            sizes.Add(reactions.Count);
            return Task.CompletedTask;
        });

        for (int number = 0; number < 10; number++)
        {
            await service.React(ReactionTarget.News, $"k{number}", $"Entry {number}", null, ReactionKind.Like, CancellationToken.None);
        }

        await service.Close();

        Assert.Equal([10], sizes);
    }
}
