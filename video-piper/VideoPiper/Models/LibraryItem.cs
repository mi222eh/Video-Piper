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
/// </summary>
public sealed class LibraryItem
{
    /// <summary>Stable id: yt-dlp video id, or a hash of the URL when no id is available.</summary>
    public string Id { get; init; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Channel/uploader name — determines the top-level subfolder.</summary>
    public string? Uploader { get; set; }

    /// <summary>Playlist name, when the item is part of a playlist — nested subfolder.</summary>
    public string? Playlist { get; set; }

    public MediaKind Kind { get; set; } = MediaKind.Audio;

    public ItemStatus Status { get; set; } = ItemStatus.Queued;

    /// <summary>Download progress 0–100 while downloading.</summary>
    public double? Percent { get; set; }

    /// <summary>Duration in seconds, when known from metadata.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Absolute path to the media file once complete (or partially downloaded).</summary>
    public string? FilePath { get; set; }

    public DateTimeOffset AddedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="ItemStatus.Failed"/>.</summary>
    public string? Error { get; set; }
}
