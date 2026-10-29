using System.Windows.Input;

namespace RomothequeManager.V2.MvvmFoundation;

// BCL-only temporary command for an unconnected UI foundation.
// No package or extra view dependencies before dependency/ZIP review.
internal sealed class InventoryCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?> _canExecute;

    public InventoryCommand(Action<object?> execute, Predicate<object?> canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute(parameter);

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter)) _execute(parameter);
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
