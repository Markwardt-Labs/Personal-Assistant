namespace Markwardt.PersonalAssistant.Tests.Unit;

[Collection("Headless")]
public sealed class ViewTests(HeadlessFixture fixture)
{
    private readonly Mock<IServerConnection> connection = new();

    private readonly Mock<ILinkLauncher> launcher = new();

    private readonly CultureInfo culture = CultureInfo.GetCultureInfo("en-US");

    private (IReadOnlyList<string> Texts, IReadOnlyList<string> Buttons) Render(Control content)
    {
        Window window = new() { Width = 420, Height = 800, Content = content };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        List<string> texts = [.. content.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Inlines is { Count: > 0 } ? string.Concat(block.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text)) : block.Text ?? string.Empty)];
        List<string> buttons = [.. content.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => Avalonia.Automation.AutomationProperties.GetName(button) is { Length: > 0 } name ? name : button.Content as string ?? button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty)];
        window.Close();
        return (texts, buttons);
    }

    private MailTopic Topic(string id, string title, MailImportance importance, params string[] subjects)
        => new() { Id = id, Title = title, Summary = $"Summary of {title}.", Importance = importance, Messages = [.. subjects.Select((subject, index) => new MailSummary { Id = $"{id}-{index}", From = "Anna <anna@example.com>", Subject = subject, Received = DateTimeOffset.UnixEpoch.AddDays(index), Preview = "Preview" })] };

    [Fact]
    public async Task ShowsTheConnectScreen()
    {
        Mock<IAppInfo> info = new();
        info.SetupGet(item => item.Name).Returns("Personal Assistant");
        info.SetupGet(item => item.Version).Returns("1.0.0");

        (IReadOnlyList<string> texts, IReadOnlyList<string> buttons) = await fixture.Run(() => Render(new MainView { DataContext = new HolderViewModel(new ConnectViewModel(info.Object, Mock.Of<IServerConnection.IFactory>(), Mock.Of<ISettingsStore>(), null, (_, _) => { })) }));

        Assert.Contains("Personal Assistant", texts);
        Assert.Contains("Version 1.0.0", texts);
        Assert.Contains("Connect", buttons);
    }

    [Fact]
    public async Task ShowsTheConversationAndItsButtons()
    {
        ChatViewModel chat = new(connection.Object);
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MessageAdded, Message = new() { Index = 0, Role = ChatRole.User, Text = "What is on today?", Time = DateTimeOffset.UnixEpoch } });
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MessageAdded, Message = new() { Index = 1, Role = ChatRole.Assistant, Text = "A dentist visit at 3 pm.", Time = DateTimeOffset.UnixEpoch } });

        (IReadOnlyList<string> texts, IReadOnlyList<string> buttons) = await fixture.Run(() => Render(new ChatView { DataContext = chat }));

        Assert.Contains("What is on today?", texts);
        Assert.Contains("A dentist visit at 3 pm.", texts);
        Assert.Contains("Send", buttons);
        Assert.DoesNotContain("Change Server", buttons);
    }

    [Fact]
    public async Task BuildsTheRootViewWhichStartsOnTheConnectScreen()
    {
        using TempFolder folder = new();

        MainViewModel model = await fixture.Run(() => (MainViewModel)((MainView)((ClientApp)Avalonia.Application.Current!).CreateMainView(typeof(ViewTests).Assembly, folder.Path)).DataContext!);
        await new Eventually().Be(() => model.Current is ConnectViewModel);

        Assert.IsType<ConnectViewModel>(model.Current);
    }

    [Fact]
    public async Task ShowsTheMailTabWithTheImportantTopicsAndTheCollapsedOnes()
    {
        MailViewModel mail = new(connection.Object, culture);
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MailDigestChanged, MailDigest = new() { Generating = false, Digest = new() { Generated = DateTimeOffset.UnixEpoch, Topics = [Topic("t1", "Tax deadline", MailImportance.Critical, "Reminder"), Topic("t2", "Store promotions", MailImportance.Junk, "Sale", "Offer", "Deal")] } } });

        (IReadOnlyList<string> collapsed, _) = await fixture.Run(() => Render(new MailView { DataContext = mail }));
        mail.ToggleOtherCommand.Execute(null);
        (IReadOnlyList<string> expanded, _) = await fixture.Run(() => Render(new MailView { DataContext = mail }));

        Assert.Contains("Tax deadline", collapsed);
        Assert.Contains("Critical", collapsed);
        Assert.DoesNotContain("Store promotions", collapsed);
        Assert.Contains(collapsed, text => text.StartsWith("Spam, ads and unimportant (1)", StringComparison.Ordinal));
        Assert.Contains("Store promotions", expanded);
        Assert.Contains(expanded, text => text.StartsWith("3 emails", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShowsTheEventsTabWithTheCalendarAndSuggestionsOfTheWeek()
    {
        EventsViewModel events = new(connection.Object, launcher.Object, culture, new FixedTime(new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero)));
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.AgendaChanged, Agenda = new() { From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), To = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), Events = [new() { Id = "e1", Title = "Dentist", Start = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero), End = new(2026, 11, 18, 16, 0, 0, TimeSpan.Zero) }], Feeds = [new() { Id = "f1", Name = "Jazz", Description = "live jazz" }], Suggestions = [new() { Id = "s1", FeedId = "f1", Title = "Jazz Night", Start = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero), Description = "Live trio.", Url = "https://example.com/jazz" }], Generating = false } });

        (IReadOnlyList<string> texts, IReadOnlyList<string> buttons) = await fixture.Run(() => Render(new EventsView { DataContext = events }));

        Assert.Contains("Dentist", texts);
        Assert.Contains("Jazz Night", texts);
        Assert.Equal(["Previous", "Today", "Next"], buttons.Where(text => text is "Previous" or "Today" or "Next"));
    }

    [Fact]
    public async Task ShowsTheNewsTabWithTheEntriesUnderTheirFeeds()
    {
        NewsViewModel news = new(connection.Object, launcher.Object, culture);
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.NewsChanged, News = new() { Generating = false, Feeds = [new() { Id = "f1", Name = "Tech", Description = "chips" }, new() { Id = "f2", Name = "Sport", Description = "football" }], Items = [new() { Id = "n1", FeedId = "f1", Title = "New chip", Summary = "It is faster.", Source = "Wire", Url = "https://example.com/chip" }] } });

        (IReadOnlyList<string> texts, _) = await fixture.Run(() => Render(new NewsView { DataContext = news }));

        Assert.Contains("Tech", texts);
        Assert.Contains("New chip", texts);
        Assert.Contains("Sport", texts);
        Assert.Contains("Nothing found yet.", texts);
    }

    [Fact]
    public async Task ShowsTheHomeScreenWithChatAsTheLastTab()
    {
        Mock<IAppInfo> info = new();
        info.SetupGet(item => item.Version).Returns("1.0.0");
        UpdateViewModel update = new(info.Object, null, null);
        HomeViewModel home = new(new MailViewModel(connection.Object, culture), new EventsViewModel(connection.Object, launcher.Object, culture), new NewsViewModel(connection.Object, launcher.Object, culture), new ChatViewModel(connection.Object), back => new SettingsViewModel(connection.Object, update, () => { }, back), update);

        (IReadOnlyList<string> texts, IReadOnlyList<string> buttons) = await fixture.Run(() => Render(new HomeView { DataContext = home }));

        Assert.Contains("Mail", texts);
        Assert.Equal(["Settings", "Mail", "Events", "News", "Chat"], buttons);
    }
}
