namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class NewsAiTests
{
    private readonly FakePrompter prompter = new();

    private readonly NewsFeed feed = new() { Id = "f1", Name = "Tech", Description = "new AI models and chips" };

    private Task<IReadOnlyList<NewsItemDraft>> Search(string background = "")
        => new NewsAi(prompter, new FakeSetups()).Search([feed], DateTimeOffset.UnixEpoch, background, CancellationToken.None);

    [Fact]
    public async Task ReadsTheEntriesOfTheStructuredAnswerWithTheirFeedAndAddress()
    {
        prompter.OnCreate = session => session.Reply(new NewsResult { Items = [new() { FeedId = "f1", Title = "New chip", Summary = "It is faster.", Url = "https://example.com/chip", Source = "Wire" }] });

        IReadOnlyList<NewsItemDraft> items = await Search();

        NewsItemDraft item = Assert.Single(items);
        Assert.Equal(("f1", "https://example.com/chip"), (item.FeedId, item.Url));
        Assert.IsType<ClaudeSetup>(Assert.Single(prompter.Created));
    }

    [Fact]
    public async Task SearchesTheWebWithTheBackgroundInASessionThatIsDeletedAfterwards()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            created.Reply(new NewsResult { Items = [] });
        };

        await Search("\nno sports");

        Assert.True(session!.Configuration.IsWebAllowed);
        Assert.EndsWith("\nno sports", session.Configuration.Instructions);
        Assert.True(session.IsDisposed);
        Assert.Empty(await prompter.List(CancellationToken.None));
    }

    [Fact]
    public async Task ReportsAnAiThatFailed()
    {
        prompter.OnCreate = session => session.Fail("offline");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Search());

        Assert.Contains("offline", exception.Message);
    }
}
