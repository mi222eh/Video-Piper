using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoPiper.Services;

namespace VideoPiper.ViewModels;

/// <summary>
/// One tool row in the settings page: installed/latest version, update status and the
/// "check for updates" command for that tool.
/// </summary>
public sealed class ToolSettingsModel : INotifyPropertyChanged
{
    private string? _installedVersion;
    private string? _latestVersion;
    private string? _statusText;
    private bool _busy;
    private bool _busyChecking;
    private bool _hasUpdate;

    private readonly IToolUpdateService _toolUpdateService;

    public ToolSettingsModel(string tool, string title, string glyph, IToolUpdateService toolUpdateService)
    {
        _toolUpdateService = toolUpdateService;
        Tool = tool;
        Title = title;
        Glyph = glyph;
        CheckCommand = new RelayCommand(CheckAsync, () => !Busy);
        UpdateCommand = new RelayCommand(UpdateAsync, () => !Busy && HasUpdate);
    }

    public string Tool { get; }

    public string Title { get; }

    public string Glyph { get; }

    public RelayCommand CheckCommand { get; }

    public RelayCommand UpdateCommand { get; }

    /// <summary>Short installed version (e.g. "2026.08.19"), or null when not installed.</summary>
    public string? InstalledVersion
    {
        get => _installedVersion;
        internal set => Set(ref _installedVersion, value);
    }

    public string? LatestVersion
    {
        get => _latestVersion;
        internal set => Set(ref _latestVersion, value);
    }

    /// <summary>Swedish status line under the title.</summary>
    public string? StatusText
    {
        get => _statusText;
        internal set => Set(ref _statusText, value);
    }

    public bool Busy
    {
        get => _busy;
        internal set
        {
            if (Set(ref _busy, value))
            {
                OnPropertyChanged(nameof(BusyChecking));
                OnPropertyChanged(nameof(CanUpdate));
                CheckCommand.RefreshCanExecute();
                UpdateCommand.RefreshCanExecute();
            }
        }
    }

    /// <summary>True while a version check (not an update) is running.</summary>
    public bool BusyChecking
    {
        get => _busyChecking;
        internal set => Set(ref _busyChecking, value);
    }

    /// <summary>True when a check has found a newer version (or the tool is missing).</summary>
    public bool HasUpdate
    {
        get => _hasUpdate;
        internal set
        {
            if (Set(ref _hasUpdate, value))
            {
                OnPropertyChanged(nameof(LatestText));
                OnPropertyChanged(nameof(CanUpdate));
                UpdateCommand.RefreshCanExecute();
            }
        }
    }

    /// <summary>True when an update can be started (update available and not busy).</summary>
    public bool CanUpdate => HasUpdate && !Busy;


    public string InstalledText => InstalledVersion is null ? "Inte installerad" : InstalledVersion;

    public string LatestText => HasUpdate && LatestVersion is not null ? $"Ny version tillgänglig: {LatestVersion}" : "";

    internal async Task RefreshInstalledAsync()
    {
        var raw = await SystemService.GetLocalVersionAsync(Tool == "yt-dlp" ? "yt-dlp.exe" : "ffmpeg.exe", Tool == "yt-dlp" ? "--version" : "-version");
        InstalledVersion = ToolUpdateService.FormatVersion(raw);
        OnPropertyChanged(nameof(InstalledText));
    }

    /// <summary>Checks GitHub for a newer version (read-only) and reports the result.</summary>
    internal async Task CheckAsync()
    {
        if (Busy)
        {
            return;
        }

        OnUi(() =>
        {
            Busy = true;
            BusyChecking = true;
            StatusText = "Kontrollerar...";
        });

        var result = await _toolUpdateService.CheckToolAsync(Tool, apply: false);
        OnUi(() =>
        {
            Busy = false;
            BusyChecking = false;
            if (result.Error is not null)
            {
                StatusText = $"Kontrollen misslyckades: {result.Error}";
                return;
            }

            LatestVersion = result.LatestVersion;
            _ = RefreshInstalledAsync();

            if (result.Updated)
            {
                HasUpdate = false;
                StatusText = $"Installerad: {result.LatestVersion}";
            }
            else if (result.InstalledVersion is null)
            {
                HasUpdate = true;
                StatusText = "Inte installerad – hämta senaste versionen.";
            }
            else if (result.LatestVersion is not null && ToolUpdateService.IsNewerVersion(result.InstalledVersion, result.LatestVersion))
            {
                HasUpdate = true;
                StatusText = $"Ny version tillgänglig ({result.LatestVersion}).";
            }
            else
            {
                HasUpdate = false;
                StatusText = "Senaste versionen är installerad.";
            }

            OnPropertyChanged(nameof(LatestText));
        });
    }

    /// <summary>Downloads and installs the latest version for this tool.</summary>
    internal async Task UpdateAsync()
    {
        if (Busy)
        {
            return;
        }

        OnUi(() =>
        {
            Busy = true;
            StatusText = "Hämtar...";
        });

        var result = await _toolUpdateService.CheckToolAsync(Tool, apply: true);
        OnUi(() =>
        {
            Busy = false;
            if (result.Error is not null)
            {
                StatusText = $"Uppdateringen misslyckades: {result.Error}";
                return;
            }

            LatestVersion = result.LatestVersion;
            _ = RefreshInstalledAsync();
            HasUpdate = false;
            StatusText = result.Updated
                ? $"Uppdaterad till {result.LatestVersion}."
                : "Senaste versionen är redan installerad.";
            OnPropertyChanged(nameof(LatestText));
        });
    }

    private static void OnUi(Action action)
    {
        if (App.MainWindowInstance?.DispatcherQueue is { } queue)
        {
            if (queue.HasThreadAccess)
            {
                action();
            }
            else
            {
                queue.TryEnqueue(() => action());
            }
        }
        else
        {
            action();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>
/// View model for the settings page (Inställningar): one card per managed tool
/// (yt-dlp, ffmpeg) with installed/latest version, update status and check buttons.
/// Tools are checked automatically once per day in the background; the page
/// offers manual checks per tool.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private bool _isBusy;
    private bool _deleteFiles;
    private int _resolutionIndex;
    private int _framerateIndex;

    public SettingsViewModel(IToolUpdateService toolUpdateService)
    {
        YtDlp = new ToolSettingsModel("yt-dlp", "yt-dlp", "\uE8D6", toolUpdateService);
        Ffmpeg = new ToolSettingsModel("ffmpeg", "ffmpeg", "\uE95D", toolUpdateService);
        Tools = new[] { YtDlp, Ffmpeg };
        CheckAllCommand = new RelayCommand(CheckAllAsync, () => !IsBusy);

        _deleteFiles = PreferencesService.GetDeleteMode() == DeleteMode.DeleteFiles;
        _resolutionIndex = Array.IndexOf(ResolutionHeights, PreferencesService.GetMaxHeight());
        if (_resolutionIndex < 0)
        {
            _resolutionIndex = 2;
        }
        _framerateIndex = Array.IndexOf(FramerateFps, PreferencesService.GetMinFps());
        if (_framerateIndex < 0)
        {
            _framerateIndex = FramerateFps.Length - 1;
        }
    }

    public IReadOnlyCollection<ToolSettingsModel> Tools { get; }

    public ToolSettingsModel YtDlp { get; }

    public ToolSettingsModel Ffmpeg { get; }

    /// <summary>Resolution choices offered in the settings (label + max video height).</summary>
    public static readonly string[] ResolutionLabels =
    {
        "480p", "720p", "1080p", "1440p", "2160p",
    };

    internal static readonly int[] ResolutionHeights =
    {
        480, 720, 1080, 1440, 2160,
    };

    /// <summary>Framerate choices offered in the settings (label + minimum fps).</summary>
    public static readonly string[] FramerateLabels =
    {
        "24 fps", "30 fps", "60 fps",
    };

    internal static readonly int[] FramerateFps =
    {
        24, 30, 60,
    };

    /// <summary>Whether removing a library item also deletes its files and emptied folders.</summary>
    public bool DeleteFiles
    {
        get => _deleteFiles;
        set
        {
            if (Set(ref _deleteFiles, value))
            {
                PreferencesService.SetDeleteMode(value ? DeleteMode.DeleteFiles : DeleteMode.KeepFiles);
            }
        }
    }

    /// <summary>Selected resolution index (0 = 480p … 4 = 2160p).</summary>
    public int ResolutionIndex
    {
        get => _resolutionIndex;
        set
        {
            if (Set(ref _resolutionIndex, value) && value >= 0 && value < ResolutionHeights.Length)
            {
                PreferencesService.SetMaxHeight(ResolutionHeights[value]);
            }
        }
    }

    /// <summary>Selected framerate index (0 = 24 … 2 = 60).</summary>
    public int FramerateIndex
    {
        get => _framerateIndex;
        set
        {
            if (Set(ref _framerateIndex, value) && value >= 0 && value < FramerateFps.Length)
            {
                PreferencesService.SetMinFps(FramerateFps[value]);
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CheckAllText));
                CheckAllCommand.RefreshCanExecute();
            }
        }
    }

    public string CheckAllText => IsBusy ? "Kontrollerar alla..." : "Kontrollera alla";

    public RelayCommand CheckAllCommand { get; }

    public async Task InitializeAsync()
    {
        foreach (var tool in Tools)
        {
            await tool.RefreshInstalledAsync();
        }
    }

    /// <summary>Checks both tools for updates (read-only).</summary>
    public async Task CheckAllAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await Task.WhenAll(YtDlp.CheckAsync(), Ffmpeg.CheckAsync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
