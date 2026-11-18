namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The screen of a connected client: the mail, events, news and chat tabs along the bottom, one bar on top with what the tab needs (its title or views, the feeds and the way to the settings), and the offer to update when there is a newer release.
/// </summary>
internal sealed class HomeViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HomeViewModel" /> class.
    /// </summary>
    /// <param name="mail">The mail tab.</param>
    /// <param name="events">The events tab.</param>
    /// <param name="news">The news tab.</param>
    /// <param name="chat">The chat tab.</param>
    /// <param name="settings">Makes the settings screen when the user opens it.</param>
    /// <param name="update">The update of the app and its server.</param>
    public HomeViewModel(MailViewModel mail, EventsViewModel events, NewsViewModel news, ChatViewModel chat, Func<Action, SettingsViewModel> settings, UpdateViewModel update)
    {
        Mail = mail;
        Events = events;
        News = news;
        Chat = chat;
        Update = update;
        this.settings = settings;
        MailCommand = new RelayCommand(() => Select(HomeTab.Mail));
        EventsCommand = new RelayCommand(() => Select(HomeTab.Events));
        NewsCommand = new RelayCommand(() => Select(HomeTab.News));
        ChatCommand = new RelayCommand(() => Select(HomeTab.Chat));
        SettingsCommand = new RelayCommand(() => SettingsPage = settings(() => SettingsPage = null));
        FeedsCommand = new RelayCommand(() => (selected == HomeTab.Events ? Events.ManageFeedsCommand : News.ManageFeedsCommand).Execute(null));
        mail.PropertyChanged += (_, _) => RaiseChrome();
        events.PropertyChanged += (_, _) => RaiseChrome();
        news.PropertyChanged += (_, _) => RaiseChrome();
        chat.PropertyChanged += (_, _) => RaiseChrome();
        update.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UpdateViewModel.IsAvailable))
            {
                Raise(nameof(ShowUpdateOffer));
            }
        };
        Select(HomeTab.Mail);
    }

    private readonly Func<Action, SettingsViewModel> settings;

    private HomeTab selected;

    private SettingsViewModel? settingsPage;

    /// <summary>
    /// Gets the name of the selected tab, which the bar shows for every tab but events.
    /// </summary>
    public string TabTitle => selected switch
    {
        HomeTab.Mail => "Mail",
        HomeTab.Events => "Events",
        HomeTab.News => "News",
        _ => "Chat",
    };

    /// <summary>
    /// Gets the mail tab.
    /// </summary>
    public MailViewModel Mail { get; }

    /// <summary>
    /// Gets the events tab.
    /// </summary>
    public EventsViewModel Events { get; }

    /// <summary>
    /// Gets the news tab.
    /// </summary>
    public NewsViewModel News { get; }

    /// <summary>
    /// Gets the chat tab.
    /// </summary>
    public ChatViewModel Chat { get; }

    /// <summary>
    /// Gets the update of the app and its server.
    /// </summary>
    public UpdateViewModel Update { get; }

    /// <summary>
    /// Gets the command that shows the mail tab.
    /// </summary>
    public ICommand MailCommand { get; }

    /// <summary>
    /// Gets the command that shows the events tab.
    /// </summary>
    public ICommand EventsCommand { get; }

    /// <summary>
    /// Gets the command that shows the news tab.
    /// </summary>
    public ICommand NewsCommand { get; }

    /// <summary>
    /// Gets the command that shows the chat tab.
    /// </summary>
    public ICommand ChatCommand { get; }

    /// <summary>
    /// Gets the command that opens the settings.
    /// </summary>
    public ICommand SettingsCommand { get; }

    /// <summary>
    /// Gets the command that opens the feeds of the selected tab, events or news.
    /// </summary>
    public ICommand FeedsCommand { get; }

    /// <summary>
    /// Gets the tab that shows.
    /// </summary>
    public HomeTab Selected
    {
        get => selected;
        private set
        {
            if (Set(ref selected, value))
            {
                Raise(nameof(IsMailSelected));
                Raise(nameof(IsEventsSelected));
                Raise(nameof(IsNewsSelected));
                Raise(nameof(IsChatSelected));
                RaiseChrome();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the mail tab shows.
    /// </summary>
    public bool IsMailSelected => selected == HomeTab.Mail;

    /// <summary>
    /// Gets a value indicating whether the events tab shows.
    /// </summary>
    public bool IsEventsSelected => selected == HomeTab.Events;

    /// <summary>
    /// Gets a value indicating whether the news tab shows.
    /// </summary>
    public bool IsNewsSelected => selected == HomeTab.News;

    /// <summary>
    /// Gets a value indicating whether the chat tab shows.
    /// </summary>
    public bool IsChatSelected => selected == HomeTab.Chat;

    /// <summary>
    /// Gets a value indicating whether the bar shows its title: every tab but events, whose bar shows the views.
    /// </summary>
    public bool ShowTitle => selected != HomeTab.Events;

    /// <summary>
    /// Gets a value indicating whether the selected tab has feeds to manage.
    /// </summary>
    public bool CanManageFeeds => selected is HomeTab.Events or HomeTab.News;

    /// <summary>
    /// Gets a value indicating whether the bar is shown: not over the settings and not over a page of the selected tab, which has a bar of its own.
    /// </summary>
    public bool ShowBar => settingsPage is null && selected switch
    {
        HomeTab.Mail => Mail.IsList,
        HomeTab.Events => Events.IsRoot,
        HomeTab.News => News.IsRoot,
        _ => true,
    };

    /// <summary>
    /// Gets a value indicating whether the server is working for the selected tab (or the assistant is answering), which the bar shows as a thin progress line.
    /// </summary>
    public bool ShowProgress => selected switch
    {
        HomeTab.Mail => Mail.IsGenerating,
        HomeTab.Events => Events.IsGenerating,
        HomeTab.News => News.IsGenerating,
        _ => Chat.IsWorking,
    };

    /// <summary>
    /// Gets the settings screen that shows over the tabs, or <see langword="null" /> when it does not.
    /// </summary>
    public SettingsViewModel? SettingsPage
    {
        get => settingsPage;
        private set
        {
            if (Set(ref settingsPage, value))
            {
                Raise(nameof(HasSettings));
                Raise(nameof(ShowUpdateOffer));
                RaiseChrome();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the settings show.
    /// </summary>
    public bool HasSettings => settingsPage is not null;

    /// <summary>
    /// Gets a value indicating whether the offer to update shows: there is a newer release and the settings, which hold the same offer, are not open.
    /// </summary>
    public bool ShowUpdateOffer => Update.IsAvailable && settingsPage is null;

    /// <summary>
    /// Steps back from whatever is open: the settings, a message, a day, a list of feeds.
    /// </summary>
    /// <returns><see langword="true" /> if there was something to step back from.</returns>
    public bool GoBack()
    {
        if (settingsPage is not null)
        {
            SettingsPage = null;
            return true;
        }

        return selected switch
        {
            HomeTab.Mail => Mail.GoBack(),
            HomeTab.Events => Events.GoBack(),
            HomeTab.News => News.GoBack(),
            _ => false,
        };
    }

    private void RaiseChrome()
    {
        Raise(nameof(TabTitle));
        Raise(nameof(ShowTitle));
        Raise(nameof(CanManageFeeds));
        Raise(nameof(ShowBar));
        Raise(nameof(ShowProgress));
    }

    private void Select(HomeTab tab)
    {
        Selected = tab;

        switch (tab)
        {
            case HomeTab.Mail:
                _ = Mail.Load();
                break;
            case HomeTab.Events:
                _ = Events.Load();
                break;
            case HomeTab.News:
                _ = News.Load();
                break;
        }
    }
}
