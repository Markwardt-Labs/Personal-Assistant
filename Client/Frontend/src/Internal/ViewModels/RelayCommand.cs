namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// A command that runs a synchronous action and is always allowed.
/// </summary>
/// <param name="execute">The action to run.</param>
internal sealed class RelayCommand(Action execute) : ICommand
{
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
        => true;

    /// <inheritdoc />
    public void Execute(object? parameter)
        => execute();
}
