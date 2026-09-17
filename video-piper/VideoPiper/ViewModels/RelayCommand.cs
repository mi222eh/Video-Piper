using System.Windows.Input;

namespace VideoPiper.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(() => { execute(); return Task.CompletedTask; }, canExecute)
    {
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            await _execute();
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RefreshCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A command that receives its parameter (e.g. the clicked list item).
/// </summary>
public sealed class ParameterizedRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;

    public ParameterizedRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            await _execute(parameter);
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RefreshCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

