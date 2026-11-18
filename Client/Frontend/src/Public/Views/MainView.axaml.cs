namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The root view of the client; shows whichever screen the view model selects and steps back through it when the back button of the phone is pressed.
/// </summary>
public sealed partial class MainView : UserControl
{
    private TopLevel? top;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainView" /> class.
    /// </summary>
    public MainView()
        => InitializeComponent();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        top = TopLevel.GetTopLevel(this);

        if (top is not null)
        {
            top.BackRequested += Back;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (top is not null)
        {
            top.BackRequested -= Back;
            top = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void Back(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { } model && model.GoBack())
        {
            e.Handled = true;
        }
    }
}
