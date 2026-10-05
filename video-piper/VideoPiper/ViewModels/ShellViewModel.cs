using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoPiper.Services;

namespace VideoPiper.ViewModels;

public enum ShellMode
{
    Simple,
    Library,
}

/// <summary>
/// View model for the app-wide top bar: mode switcher (Snabbnedladdning/Bibliotek),
/// tool status and theme. Owns the navigation state shared by both feature view models.
/// </summary>
public sealed class ShellViewModel : INotifyPropertyChanged
{
    private ShellMode _mode = ShellMode.Simple;
    private bool _isDark = true;
    private string? _toolStatus;

    public ICommand ToggleModeCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public ShellViewModel()
    {
        _mode = PreferencesService.GetAppMode() == AppMode.Library ? ShellMode.Library : ShellMode.Simple;
        var savedTheme = PreferencesService.GetTheme();
        _isDark = savedTheme != "light";

        ToggleModeCommand = new RelayCommand(() =>
        {
            SetMode(IsLibrary ? ShellMode.Simple : ShellMode.Library);
        });
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
    }

    public bool IsSimple => _mode == ShellMode.Simple;
    public bool IsLibrary => _mode == ShellMode.Library;

    public ShellMode Mode
    {
        get => _mode;
        private set
        {
            if (Set(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsSimple));
                OnPropertyChanged(nameof(IsLibrary));
            }
        }
    }

    /// <summary>Switches the active mode (used by the app bar's mode switcher).</summary>
    public void SetMode(ShellMode mode)
    {
        Mode = mode;
        PreferencesService.SetAppMode(mode == ShellMode.Library ? AppMode.Library : AppMode.Simple);
    }

    /// <summary>Current tool state for the status pill: null = loading, "ready" = ok, else a Swedish warning.</summary>
    public string? ToolStatus
    {
        get => _toolStatus;
        set => Set(ref _toolStatus, value);
    }

    public bool IsDark
    {
        get => _isDark;
        private set => Set(ref _isDark, value);
    }

    private void ToggleTheme()
    {
        IsDark = !IsDark;
        PreferencesService.SetTheme(IsDark ? "dark" : "light");
        App.SetTheme(IsDark);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
