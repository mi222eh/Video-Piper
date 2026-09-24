using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoPiper.Models;

/// <summary>
/// What kind of media a library item holds.
/// </summary>
public enum MediaKind
{
    Audio,
    Video,
}

/// <summary>
/// Lifecycle state of a library item's download.
/// </summary>
public enum ItemStatus
{
    Queued,
    Downloading,
    Complete,
    Failed,
}

/// <summary>
/// A single media file (or planned file) in the music/video library.
/// One item per track or video; playlist membership is recorded via <see cref="Playlist"/>.
/// Mutable fields raise change notifications so list rows update in place while downloading.
/// </summary>
public sealed class LibraryItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string? _uploader;
    private string? _playlist;
    private MediaKind _kind = MediaKind.Audio;
    private ItemStatus _status = ItemStatus.Queued;
    private double? _percent;
    private double? _durationSeconds;
    private string? _filePath;
    private string? _error;

    /// <summary>Stable id: yt-dlp video id, or a hash of the URL when no id is available.</summary>
    public string Id { get; init; } = string.Empty;

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    /// <summary>Channel/uploader name — determines the top-level subfolder.</summary>
    public string? Uploader
    {
        get => _uploader;
        set
        {
            if (Set(ref _uploader, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>Playlist name, when the item is part of a playlist — nested subfolder.</summary>
    public string? Playlist
    {
        get => _playlist;
        set
        {
            if (Set(ref _playlist, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public MediaKind Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                OnPropertyChanged(nameof(IconGlyph));
                OnPropertyChanged(nameof(KindLabel));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public ItemStatus Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(IsComplete));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(ProgressVisibility));
                OnPropertyChanged(nameof(IsIndeterminate));
            }
        }
    }

    /// <summary>Download progress 0–100 while downloading.</summary>
    public double? Percent
    {
        get => _percent;
        set
        {
            if (Set(ref _percent, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsIndeterminate));
                OnPropertyChanged(nameof(PercentFormatted));
            }
        }
    }

    /// <summary>Duration in seconds, when known from metadata.</summary>
    public double? DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            if (Set(ref _durationSeconds, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(DurationFormatted));
            }
        }
    }

    /// <summary>Absolute path to the media file once complete (or partially downloaded).</summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (Set(ref _filePath, value))
            {
                OnPropertyChanged(nameof(CanPlay));
            }
        }
    }

    public DateTimeOffset AddedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="ItemStatus.Failed"/>.</summary>
    public string? Error
    {
        get => _error;
        set
        {
            if (Set(ref _error, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsFailed));
            }
        }
    }

    public bool IsDownloading => Status == ItemStatus.Downloading;
    public bool IsComplete => Status == ItemStatus.Complete;
    public bool IsFailed => Status == ItemStatus.Failed;
    public bool CanPlay => Status == ItemStatus.Complete && !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);
    public Microsoft.UI.Xaml.Visibility ProgressVisibility => Status == ItemStatus.Downloading ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public bool IsIndeterminate => Status == ItemStatus.Downloading && (Percent == null || Percent <= 0);
    public string PercentFormatted => $"{(Percent ?? 0):0}%";

    public string KindLabel => Kind == MediaKind.Audio ? "MP3" : "MP4";

    /// <summary>Segoe MDL2 glyph for the item kind, used by list rows.</summary>
    public string IconGlyph => Kind == MediaKind.Audio ? "\uE8D6" : "\uE714";

    public string DurationFormatted
    {
        get
        {
            if (DurationSeconds is > 0)
            {
                var d = TimeSpan.FromSeconds(DurationSeconds.Value);
                return d.TotalHours >= 1
                    ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}"
                    : $"{(int)d.TotalMinutes}:{d.Seconds:00}";
            }
            return string.Empty;
        }
    }

    /// <summary>Display text combining uploader/playlist and current status, for list rows.</summary>
    public string StatusText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Uploader))
            {
                parts.Add(Uploader);
            }
            if (!string.IsNullOrEmpty(Playlist))
            {
                parts.Add(Playlist);
            }

            switch (Status)
            {
                case ItemStatus.Downloading:
                    parts.Add($"Laddar ner {(Percent ?? 0):0}%");
                    break;
                case ItemStatus.Failed:
                    parts.Add(string.IsNullOrEmpty(Error) ? "Misslyckades" : $"Misslyckades: {Error}");
                    break;
                case ItemStatus.Complete when DurationSeconds is > 0:
                    parts.Add(DurationFormatted);
                    break;
            }

            return string.Join(" · ", parts);
        }
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
