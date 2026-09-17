namespace VideoPiper.Models;

/// <summary>
/// Metadata for a single downloadable media item, as reported by yt-dlp.
/// Used both for playlist pre-fetching and for single downloads.
/// </summary>
public sealed record MediaEntry(
    string Id,
    string Title,
    string? Uploader,
    double? DurationSeconds,
    string Url);
