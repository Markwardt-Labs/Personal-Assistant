namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The screen that replaces the app while the client and its server run different versions.
/// </summary>
public sealed partial class VersionMismatchView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionMismatchView" /> class.
    /// </summary>
    public VersionMismatchView()
        => InitializeComponent();
}
