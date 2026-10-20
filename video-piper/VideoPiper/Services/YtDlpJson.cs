using System.Text.Json;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Parses yt-dlp JSON output (-J) into domain entries. Shared by the download and search services.
/// </summary>
internal static class YtDlpJson
{
    /// <summary>
    /// Parses a document as a playlist when it carries an entries array.
    /// Returns null when the document is not a playlist (e.g. a single video).
    /// </summary>
    public static (List<MediaEntry> Entries, string? PlaylistTitle)? TryParsePlaylist(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("entries", out var entriesElement) || entriesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var playlistTitle = GetString(root, "title");
        var entries = new List<MediaEntry>();
        foreach (var entry in entriesElement.EnumerateArray())
        {
            var mediaEntry = ParseEntry(entry);
            if (mediaEntry is not null)
            {
                entries.Add(mediaEntry);
            }
        }

        return (entries, playlistTitle);
    }

    /// <summary>Parses a single-video document (full metadata).</summary>
    public static MediaEntry ParseSingle(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseEntry(doc.RootElement) ?? throw new InvalidOperationException("Oväntat yt-dlp-svar.");
    }

    /// <summary>
    /// Extracts the "description" field from a single-video -J document (used by the
    /// video info page). Returns null when the field is absent or not a string.
    /// </summary>
    public static string? GetDescription(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return GetString(doc.RootElement, "description");
    }

    /// <summary>
    /// True when a single-video -J document contains at least one real video stream
    /// (a format whose vcodec is set). Audio-only uploads (podcasts, audio-only
    /// videos) carry "none" for every format. Returns true when no format data is
    /// available, so the video option is never hidden by accident.
    /// </summary>
    public static bool HasVideoFormat(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var format in formats.EnumerateArray())
            {
                var vcodec = GetString(format, "vcodec");
                if (!string.IsNullOrEmpty(vcodec) && !vcodec.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        var topVcodec = GetString(root, "vcodec");
        return topVcodec is null || !topVcodec.Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    private static MediaEntry? ParseEntry(JsonElement entry)
    {
        var id = GetString(entry, "id");
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        // Flat-playlist entries often carry the bare video id in "url".
        var rawUrl = GetString(entry, "url");
        var url = rawUrl is null || !rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? $"https://www.youtube.com/watch?v={id}"
            : rawUrl;

        return new MediaEntry(
            Id: id,
            Title: GetString(entry, "title") ?? id,
            Uploader: GetString(entry, "channel") ?? GetString(entry, "uploader"),
            DurationSeconds: GetDouble(entry, "duration"),
            Url: url);
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static double? GetDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }
}
