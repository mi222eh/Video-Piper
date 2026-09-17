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
    private ItemStatus _status = ItemStatus.Queued;
    private double? _percent;
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
    public string? Uploader { get; set; }

    /// <summary>Playlist name, when the item is part of a playlist — nested subfolder.</summary>
    public string? Playlist { get; set; }

    public MediaKind Kind { get; set; } = MediaKind.Audio;

    public ItemStatus Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
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
            }
        }
    }

    /// <summary>Duration in seconds, when known from metadata.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Absolute path to the media file once complete (or partially downloaded).</summary>
    public string? FilePath
    {
        get => _filePath;
        set => Set(ref _filePath, value);
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
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Segoe MDL2 glyph for the item kind, used by list rows.</summary>
    public string IconGlyph => Kind == MediaKind.Audio ? "\uE8D6" : "\uE714";

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
                    var d = TimeSpan.FromSeconds(DurationSeconds.Value);
                    parts.Add($"{(int)d.TotalMinutes}:{d.Seconds.ToString("00")}");
                    break;
            }

            return string.Join(" · ", parts);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
