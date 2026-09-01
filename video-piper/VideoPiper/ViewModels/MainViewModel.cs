using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using VideoPiper.Models;
using VideoPiper.Services;

namespace VideoPiper.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _link = string.Empty;
    private string _savePath = string.Empty;
    private bool _isBusy;
    private bool _showProgress;
    private string _progressLabel = string.Empty;
    private double _progressPercent;
    private string? _progressSpeed;
    private string? _progressMessage;
    private string? _ytDlpVersion;
    private bool _downloadFinished;
    private bool _toolsReady = true;
    private bool _ytDlpMissing;
    private bool _ffmpegMissing;
    private bool _installYtDlpBusy;
    private bool _installFfmpegBusy;
    private string? _installStatus;
    private bool _isDark = true;

    private readonly RelayCommand _downloadCommand;
    private readonly RelayCommand _installYtDlpCommand;
    private readonly RelayCommand _installFfmpegCommand;
    private CancellationTokenSource? _downloadCts;

    public ICommand PasteCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand DownloadCommand => _downloadCommand;
    public ICommand InstallYtDlpCommand => _installYtDlpCommand;
    public ICommand InstallFfmpegCommand => _installFfmpegCommand;
    public ICommand ToggleThemeCommand { get; }

    public MainViewModel()
    {
        PasteCommand = new RelayCommand(PasteAsync, () => !IsBusy);
        BrowseCommand = new RelayCommand(BrowseAsync, () => !IsBusy);
        _downloadCommand = new RelayCommand(DownloadAsync, () => CanDownload);
        _installYtDlpCommand = new RelayCommand(() => InstallAsync("yt-dlp"), () => !IsBusy && !InstallYtDlpBusy && !InstallFfmpegBusy);
        _installFfmpegCommand = new RelayCommand(() => InstallAsync("ffmpeg"), () => !IsBusy && !InstallYtDlpBusy && !InstallFfmpegBusy);
        ToggleThemeCommand = new RelayCommand(ToggleThemeAsync);
    }

    public string Link
    {
        get => _link;
        set
        {
            if (Set(ref _link, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                _downloadCommand.RefreshCanExecute();
            }
        }
    }

    public string SavePath
    {
        get => _savePath;
        set
        {
            if (Set(ref _savePath, value))
            {
                OnPropertyChanged(nameof(SavePathDisplay));
            }
        }
    }

    public string SavePathDisplay => string.IsNullOrWhiteSpace(_savePath) ? "Standard / Arbetskatalog (Musik)" : _savePath;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(IsFinishedNotBusy));
                OnPropertyChanged(nameof(IsNormalNotBusy));
                OnPropertyChanged(nameof(IsProgressIndeterminate));
                _downloadCommand.RefreshCanExecute();
                _installYtDlpCommand.RefreshCanExecute();
                _installFfmpegCommand.RefreshCanExecute();
                (PasteCommand as RelayCommand)?.RefreshCanExecute();
                (BrowseCommand as RelayCommand)?.RefreshCanExecute();
            }
        }
    }

    public bool ShowProgress
    {
        get => _showProgress;
        private set => Set(ref _showProgress, value);
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        private set => Set(ref _progressLabel, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (Set(ref _progressPercent, value))
            {
                OnPropertyChanged(nameof(IsProgressIndeterminate));
            }
        }
    }

    public bool IsProgressIndeterminate => IsBusy && ProgressPercent <= 0;

    public string? ProgressSpeed
    {
        get => _progressSpeed;
        private set => Set(ref _progressSpeed, value);
    }

    public string? ProgressMessage
    {
        get => _progressMessage;
        private set => Set(ref _progressMessage, value);
    }

    /// <summary>Version string shown in the header pill (e.g. "yt-dlp v2026.07.04").</summary>
    public string? YtDlpVersion
    {
        get => _ytDlpVersion;
        private set => Set(ref _ytDlpVersion, value);
    }

    public bool ToolsReady
    {
        get => _toolsReady;
        private set
        {
            if (Set(ref _toolsReady, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                _downloadCommand.RefreshCanExecute();
            }
        }
    }

    public bool YtDlpMissing
    {
        get => _ytDlpMissing;
        private set => Set(ref _ytDlpMissing, value);
    }

    public bool FfmpegMissing
    {
        get => _ffmpegMissing;
        private set => Set(ref _ffmpegMissing, value);
    }

    public bool InstallYtDlpBusy
    {
        get => _installYtDlpBusy;
        private set
        {
            if (Set(ref _installYtDlpBusy, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                _downloadCommand.RefreshCanExecute();
                _installYtDlpCommand.RefreshCanExecute();
            }
        }
    }

    public bool InstallFfmpegBusy
    {
        get => _installFfmpegBusy;
        private set
        {
            if (Set(ref _installFfmpegBusy, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                _downloadCommand.RefreshCanExecute();
                _installFfmpegCommand.RefreshCanExecute();
            }
        }
    }

    public string? InstallStatus
    {
        get => _installStatus;
        private set => Set(ref _installStatus, value);
    }

    public bool DownloadFinished
    {
        get => _downloadFinished;
        private set
        {
            if (Set(ref _downloadFinished, value))
            {
                OnPropertyChanged(nameof(IsFinishedNotBusy));
                OnPropertyChanged(nameof(IsNormalNotBusy));
            }
        }
    }

    public bool IsFinishedNotBusy => DownloadFinished && !IsBusy;

    public bool IsNormalNotBusy => !DownloadFinished && !IsBusy;

    public bool CanDownload => !IsBusy && !InstallYtDlpBusy && !InstallFfmpegBusy && !string.IsNullOrWhiteSpace(Link) && ToolsReady;

    public bool IsDark
    {
        get => _isDark;
        private set => Set(ref _isDark, value);
    }

    public async Task InitializeAsync()
    {
        SavePath = PreferencesService.GetSavePath() ?? string.Empty;
        var savedTheme = PreferencesService.GetTheme();
        IsDark = savedTheme != "light";
        App.SetTheme(IsDark);
        await RefreshToolsAsync();
    }

    private async Task RefreshToolsAsync()
    {
        var status = await SystemService.CheckToolsAsync();
        YtDlpVersion = status.YtDlp.Available ? $"yt-dlp v{StripVersionPrefix(status.YtDlp.Version)}" : null;
        ToolsReady = status.AllAvailable;
        YtDlpMissing = !status.YtDlp.Available;
        FfmpegMissing = !status.Ffmpeg.Available;
    }

    private static string? StripVersionPrefix(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var firstLine = version.Split('\n')[0].Trim();
        var match = Regex.Match(firstLine, @"version\s+([\w.\-]+)");
        return match.Success ? match.Groups[1].Value : firstLine;
    }

    private async Task PasteAsync()
    {
        try
        {
            var text = await Windows.ApplicationModel.DataTransfer.Clipboard.GetContent().GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                Link = text.Trim();
            }
        }
        catch
        {
            // Clipboard unavailable — ignore.
        }
    }

    private async Task BrowseAsync()
    {
        var folder = await FolderPickerService.PickFolderAsync();
        if (folder is not null)
        {
            SavePath = folder;
            PreferencesService.SetSavePath(folder);
        }
    }

    private async Task DownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(Link))
        {
            return;
        }

        if (!ToolsReady)
        {
            ProgressMessage = "Ett fel uppstod: yt-dlp eller ffmpeg saknas. Hämta verktygen först.";
            ShowProgress = true;
            ProgressLabel = "Ett fel uppstod";
            return;
        }

        IsBusy = true;
        DownloadFinished = false;
        ShowProgress = true;
        ProgressPercent = 0;
        ProgressSpeed = null;
        ProgressLabel = "Initierar...";
        ProgressMessage = "Startar nedladdning...";
        _downloadCts = new CancellationTokenSource();

        try
        {
            var url = CleanUrl(Link.Trim());
            await DownloadService.RunAsync(url, SavePath, _downloadCts.Token, OnProgress);
        }
        catch (Exception ex)
        {
            OnUi(() =>
            {
                ProgressLabel = "Kunde inte ladda ner";
                ProgressMessage = ex.Message;
                ShowProgress = true;
                DownloadFinished = false;
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnProgress(DownloadProgress progress)
    {
        OnUi(() =>
        {
            switch (progress.Status)
            {
                case "starting":
                    ProgressLabel = "Initierar...";
                    break;

                case "downloading":
                    ProgressLabel = progress.Percent is > 0
                        ? $"Laddar ner ({progress.Percent.Value:0}%)"
                        : "Laddar ner...";
                    ProgressSpeed = string.IsNullOrEmpty(progress.Speed) ? null : $"{progress.Speed} · ETA {progress.Eta}";
                    break;

                case "converting":
                    ProgressLabel = "Konverterar ljud...";
                    ProgressPercent = 99;
                    break;

                case "finished":
                    ProgressLabel = "Nedladdning slutförd!";
                    ProgressPercent = 100;
                    ProgressSpeed = null;
                    DownloadFinished = true;
                    ShowProgress = true;
                    Link = string.Empty;
                    return;

                case "error":
                    ProgressLabel = "Kunde inte ladda ner";
                    DownloadFinished = false;
                    break;
            }

            if (progress.Percent is > 0)
            {
                ProgressPercent = progress.Percent.Value;
            }

            ProgressMessage = progress.Message;
            ShowProgress = true;
        });
    }

    private async Task InstallAsync(string tool)
    {
        var isYtDlp = tool == "yt-dlp";
        if (isYtDlp)
        {
            InstallYtDlpBusy = true;
        }
        else
        {
            InstallFfmpegBusy = true;
        }

        ToolInstallerService.Progress += OnInstallProgress;
        try
        {
            await ToolInstallerService.InstallMissingAsync(new[] { tool });
            await RefreshToolsAsync();
            InstallStatus = null;
        }
        catch (Exception ex)
        {
            InstallStatus = $"Kunde inte hämta {tool}: {ex.Message}";
        }
        finally
        {
            ToolInstallerService.Progress -= OnInstallProgress;
            if (isYtDlp)
            {
                InstallYtDlpBusy = false;
            }
            else
            {
                InstallFfmpegBusy = false;
            }
        }
    }

    private void OnInstallProgress(string message, double? percent)
    {
        OnUi(() => InstallStatus = percent is > 0 ? $"{message} ({percent.Value:0}%)" : message);
    }

    private Task ToggleThemeAsync()
    {
        IsDark = !IsDark;
        PreferencesService.SetTheme(IsDark ? "dark" : "light");
        App.SetTheme(IsDark);
        return Task.CompletedTask;
    }

    /// <summary>Strips query parameters except "v" before passing the URL to yt-dlp.</summary>
    internal static string CleanUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        if (string.IsNullOrEmpty(uri.Query))
        {
            return url;
        }

        var kept = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.StartsWith("v=", StringComparison.OrdinalIgnoreCase));

        if (!kept.Any())
        {
            return url;
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", kept),
        };

        return builder.Uri.ToString();
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
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

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

