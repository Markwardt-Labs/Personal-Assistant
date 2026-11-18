namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class EventSuggestionsAiTests
{
    private readonly FakePrompter prompter = new();

    private readonly EventFeed feed = new() { Id = "f1", Name = "Jazz", Description = "live jazz" };

    private EventSuggestionsAi Create()
        => new(prompter, new FakeSetups());

    private Task<IReadOnlyList<SuggestedEventDraft>> Suggest(string background = "")
        => Create().Suggest("Austin, Texas", [feed], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), background, CancellationToken.None);

    [Fact]
    public async Task ReadsTheEventsOfTheStructuredAnswerWithTheFeedTheyRelateTo()
    {
        prompter.OnCreate = session => session.Reply(new EventSuggestionResult { Events = [new() { FeedId = "f1", Title = "Jazz Night", Start = DateTimeOffset.UnixEpoch.AddDays(2), Description = "Live trio.", Url = "https://example.com/jazz" }] });

        IReadOnlyList<SuggestedEventDraft> events = await Suggest();

        SuggestedEventDraft draft = Assert.Single(events);
        Assert.Equal(("f1", "Jazz Night"), (draft.FeedId, draft.Title));
    }

    [Fact]
    public async Task SearchesTheWebInASessionOfItsOwnThatIsDeletedAfterwards()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            created.Reply(new EventSuggestionResult { Events = [] });
        };

        await Suggest("\nno sports");

        Assert.True(session!.Configuration.IsWebAllowed);
        Assert.EndsWith("\nno sports", session.Configuration.Instructions);
        Assert.True(session.IsDisposed);
        Assert.Empty(await prompter.List(CancellationToken.None));
    }

    [Fact]
    public async Task ReportsAnAiThatFailed()
    {
        prompter.OnCreate = session => session.Fail("offline");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Suggest());

        Assert.Contains("offline", exception.Message);
    }
}
