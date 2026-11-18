namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class CalendarAiTests
{
    private readonly FakePrompter prompter = new();

    private readonly DateTimeOffset from = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly DateTimeOffset to = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private Task<CalendarResult> Read()
        => new CalendarAi(prompter, new FakeSetups()).Read(from, to, CancellationToken.None);

    [Fact]
    public async Task ReadsTheEventsOfTheStructuredAnswer()
    {
        prompter.OnCreate = session => session.Reply(new CalendarResult { Events = [new() { Id = "e1", Title = "Dentist", Start = from.AddDays(3), End = from.AddDays(3).AddHours(1), Location = "Main Street" }] });

        CalendarResult result = await Read();

        CalendarEvent calendarEvent = Assert.Single(result.Events);
        Assert.Equal(("e1", "Dentist", "Main Street"), (calendarEvent.Id, calendarEvent.Title, calendarEvent.Location));
        Assert.Null(result.Problem);
    }

    [Fact]
    public async Task PassesTheProblemTheAiReported()
    {
        prompter.OnCreate = session => session.Reply(new CalendarResult { Events = [], Problem = "The Google Calendar connector is not connected." });

        Assert.Equal("The Google Calendar connector is not connected.", (await Read()).Problem);
    }

    [Fact]
    public async Task AsksInASessionOfItsOwnThatHasTheConnectorsAndIsDeletedAfterwards()
    {
        FakePromptSession? session = null;
        prompter.OnCreate = created =>
        {
            session = created;
            created.Reply(new CalendarResult { Events = [] });
        };

        await Read();

        Assert.EndsWith(FakeSetups.Marker, session!.Configuration.Instructions);
        Assert.False(session.Configuration.IsWebAllowed);
        Assert.True(session.IsDisposed);
        Assert.Empty(await prompter.List(CancellationToken.None));
        Assert.Contains("\"from\"", session.Prompts.Single().Text);
    }

    [Fact]
    public async Task ReportsAnAiThatFailed()
    {
        prompter.OnCreate = session => session.Fail("The tool is not signed in.");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(Read);

        Assert.Contains("The tool is not signed in.", exception.Message);
    }

    [Fact]
    public async Task ReportsAnAnswerThatIsNotEvents()
    {
        prompter.OnCreate = session => session.Reply("No idea.");

        await Assert.ThrowsAsync<InvalidOperationException>(Read);
    }
}
