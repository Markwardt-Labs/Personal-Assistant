namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The conversation screen: the messages, the box to type in and the buttons around it.
/// </summary>
public sealed partial class ChatView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatView" /> class.
    /// </summary>
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow();
    }

    private ObservableCollection<MessageViewModel>? followed;

    private void Follow()
    {
        if (followed is not null)
        {
            followed.CollectionChanged -= Scroll;
        }

        followed = (DataContext as ChatViewModel)?.Messages;

        if (followed is not null)
        {
            followed.CollectionChanged += Scroll;
        }
    }

    private void Scroll(object? sender, NotifyCollectionChangedEventArgs e)
        => Dispatcher.UIThread.Post(() => this.FindControl<ScrollViewer>("Scroller")!.ScrollToEnd(), DispatcherPriority.Background);
}
