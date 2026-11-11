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
    private string? _audioFilePath;
    private string? _videoFilePath;
    private MediaKind? _lastPlayedKind;
    private string? _thumbnailUrl;
    private string? _localThumbnailPath;
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
                Set(ref _filePath, value == MediaKind.Audio ? _audioFilePath : _videoFilePath, nameof(FilePath));
                OnPropertyChanged(nameof(IconGlyph));
                OnPropertyChanged(nameof(KindLabel));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasAudio));
                OnPropertyChanged(nameof(HasVideo));
                OnPropertyChanged(nameof(PreferredKind));
                OnPropertyChanged(nameof(CanPlay));
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
                OnPropertyChanged("ProgressVisibility");
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

    /// <summary>
    /// Legacy primary file path (kept for index compatibility). New code uses
    /// <see cref="PathFor"/> / <see cref="AudioFilePath"/> / <see cref="VideoFilePath"/>
    /// because one video can have both an MP3 and an MP4 on disk.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set => Set(ref _filePath, value);
    }

    /// <summary>Absolute path to the downloaded MP3, when one exists.</summary>
    public string? AudioFilePath
    {
        get => _audioFilePath;
        set => SetPath(MediaKind.Audio, value, nameof(AudioFilePath));
    }

    /// <summary>Absolute path to the downloaded MP4, when one exists.</summary>
    public string? VideoFilePath
    {
        get => _videoFilePath;
        set => SetPath(MediaKind.Video, value, nameof(VideoFilePath));
    }

    /// <summary>The format the user last chose for this item; playback prefers it.</summary>
    public MediaKind? LastPlayedKind
    {
        get => _lastPlayedKind;
        set
        {
            if (Set(ref _lastPlayedKind, value))
            {
                OnPropertyChanged(nameof(PreferredKind));
            }
        }
    }

    /// <summary>Remote thumbnail URL (usually derived from the YouTube id).</summary>
    public string? ThumbnailUrl
    {
        get => _thumbnailUrl;
        set
        {
            if (Set(ref _thumbnailUrl, value))
            {
                OnPropertyChanged(nameof(ThumbnailUri));
            }
        }
    }

    /// <summary>Cached thumbnail path under the app data folder, when one has been saved.</summary>
    public string? LocalThumbnailPath
    {
        get => _localThumbnailPath;
        set
        {
            if (Set(ref _localThumbnailPath, value))
            {
                OnPropertyChanged(nameof(ThumbnailUri));
            }
        }
    }

    /// <summary>Image URI for the row: the cached file when present, else the remote URL.</summary>
    public string? ThumbnailUri =>
        !string.IsNullOrEmpty(_localThumbnailPath) && File.Exists(_localThumbnailPath)
            ? _localThumbnailPath
            : _thumbnailUrl;

    /// <summary>The file path for a specific format, or null when that format is not downloaded.</summary>
    public string? PathFor(MediaKind kind) => kind == MediaKind.Audio
        ? _audioFilePath ?? (Kind == MediaKind.Audio ? _filePath : null)
        : _videoFilePath ?? (Kind == MediaKind.Video ? _filePath : null);

    /// <summary>True when the MP3 is recorded and still exists on disk.</summary>
    public bool HasAudioFile => !string.IsNullOrEmpty(AudioFilePath) && File.Exists(AudioFilePath);

    /// <summary>True when the MP4 is recorded and still exists on disk.</summary>
    public bool HasVideoFile => !string.IsNullOrEmpty(VideoFilePath) && File.Exists(VideoFilePath);

    public bool HasAnyFile => HasAudioFile || HasVideoFile;

    /// <summary>Section membership: has audio (downloaded, or queued as audio).</summary>
    public bool HasAudio => HasAudioFile || (Kind == MediaKind.Audio && !HasVideoFile);

    /// <summary>Section membership: has video (downloaded, or queued as video).</summary>
    public bool HasVideo => HasVideoFile || (Kind == MediaKind.Video && !HasAudioFile);

    /// <summary>
    /// The format to play by default: the last one the user chose for this item when it is
    /// available; otherwise video when an MP4 exists; otherwise audio.
    /// </summary>
    public MediaKind PreferredKind
    {
        get
        {
            if (_lastPlayedKind == MediaKind.Audio && HasAudioFile)
            {
                return MediaKind.Audio;
            }
            if (_lastPlayedKind == MediaKind.Video && HasVideoFile)
            {
                return MediaKind.Video;
            }
            if (HasVideoFile)
            {
                return MediaKind.Video;
            }
            if (HasAudioFile)
            {
                return MediaKind.Audio;
            }
            return _lastPlayedKind ?? Kind;
        }
    }

    /// <summary>
    /// Fills the per-format paths for indexes written before this field existed:
    /// the legacy <see cref="FilePath"/> maps to its <see cref="Kind"/>, then the item's
    /// folder is scanned by the <c>[id]</c> marker for the other format (MP3 next to MP4 is
    /// normal after the MP3-reuse merge).
    /// </summary>
    public void MigratePaths()
    {
        if (!string.IsNullOrEmpty(_filePath))
        {
            if (Kind == MediaKind.Audio)
            {
                _audioFilePath ??= _filePath;
            }
            else
            {
                _videoFilePath ??= _filePath;
            }
        }

        var folder = Path.GetDirectoryName(_audioFilePath ?? _videoFilePath ?? _filePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var marker = $"[{Id}]";
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (!file.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Ignore ffmpeg's in-progress merge temp (a crash can leave one behind).
                if (file.Contains(".merging.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var extension = Path.GetExtension(file);
                if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    _audioFilePath ??= file;
                }
                else if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                         || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                         || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase))
                {
                    _videoFilePath ??= file;
                }
            }
        }
        catch
        {
            // Best effort: an unreadable folder just means no extra path.
        }

        _thumbnailUrl ??= string.IsNullOrEmpty(Id) ? null : $"https://i.ytimg.com/vi/{Id}/mqdefault.jpg";

        var primary = PathFor(Kind);
        if (!string.IsNullOrEmpty(primary))
        {
            _filePath = primary;
        }
    }

    /// <summary>
    /// Records the file for one format and keeps <see cref="FilePath"/> pointed at the
    /// primary format's file. Raises every computed property that depends on availability.
    /// </summary>
    public void SetPath(MediaKind kind, string? path)
        => SetPath(kind, path, kind == MediaKind.Audio ? nameof(AudioFilePath) : nameof(VideoFilePath));

    private void SetPath(MediaKind kind, string? path, string propertyName)
    {
        path = string.IsNullOrWhiteSpace(path) ? null : path;
        var changed = kind == MediaKind.Audio
            ? Set(ref _audioFilePath, path, propertyName)
            : Set(ref _videoFilePath, path, propertyName);

        if (kind == Kind)
        {
            Set(ref _filePath, path, nameof(FilePath));
        }

        if (!changed)
        {
            return;
        }

        OnPropertyChanged(nameof(HasAudioFile));
        OnPropertyChanged(nameof(HasVideoFile));
        OnPropertyChanged(nameof(HasAnyFile));
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(HasVideo));
        OnPropertyChanged(nameof(PreferredKind));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(KindLabel));
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
    public bool CanPlay => HasAnyFile;
    public bool IsIndeterminate => Status == ItemStatus.Downloading && (Percent == null || Percent <= 0);
#if WINDOWS || HAS_UNO
    public Microsoft.UI.Xaml.Visibility ProgressVisibility => Status == ItemStatus.Downloading ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
#endif
    public string PercentFormatted => $"{(Percent ?? 0):0}%";

    /// <summary>Which formats this item currently has (or is downloading): "MP3", "MP4" or "MP3 · MP4".</summary>
    public string KindLabel
    {
        get
        {
            var audio = HasAudioFile || (Kind == MediaKind.Audio && !HasVideoFile);
            var video = HasVideoFile || (Kind == MediaKind.Video && !HasAudioFile);
            if (audio && video)
            {
                return "MP3 · MP4";
            }
            return audio ? "MP3" : "MP4";
        }
    }

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

    /// <summary>
    /// Posts <see cref="PropertyChanged"/> to the UI thread. The model is compiled standalone
    /// by the test project (no App reference), so the app wires this to its dispatcher.
    /// Backing fields are assigned synchronously before this is called, so state is always
    /// current; only the notification is marshaled. When unset, events fire on the calling thread.
    /// </summary>
    public static Action<Action>? PostToUi;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        var handler = PropertyChanged;
        if (handler is null)
        {
            return;
        }

        if (PostToUi is not null)
        {
            PostToUi(() => handler(this, new PropertyChangedEventArgs(name)));
            return;
        }

        handler(this, new PropertyChangedEventArgs(name));
    }

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
