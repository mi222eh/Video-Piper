using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Downloads media into a <see cref="LibraryStore"/>: pre-fetches metadata (single video or
/// playlist), then downloads entries sequentially so each item gets its own progress and status.
/// Audio is converted to MP3; video is merged to MP4 (h264/AAC, capped at 1080p).
/// </summary>
public static class LibraryDownloadService
{
    private const string OutputTemplate = "%(title)s [%(id)s].%(ext)s";
    private const string VideoFormat = "bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/best[ext=mp4]/best";

    /// <summary>
    /// Fetches metadata for a URL: one entry for a single video, all entries for a playlist.
    /// Playlists use --flat-playlist (fast, no stream resolution); the playlist title is returned separately.
    /// </summary>
    public static async Task<(List<MediaEntry> Entries, string? PlaylistTitle)> FetchEntriesAsync(
        string url, CancellationToken cancellationToken)
    {
        var ytDlpPath = await ResolveYtDlpAsync();

        // First probe: is this a playlist?
        var flatJson = await RunYtDlpAsync(ytDlpPath, $"--flat-playlist -J --no-warnings \"{url}\"", cancellationToken);
        using var doc = JsonDocument.Parse(flatJson);
        var root = doc.RootElement;

        JsonElement? entriesElement = null;
        if (root.TryGetProperty("entries", out var e) && e.ValueKind == JsonValueKind.Array)
        {
            entriesElement = e;
        }
        if (entriesElement is { } entries && entries.GetArrayLength() > 0)
        {
            var playlistTitle = GetString(root, "title");
            var entriesList = new List<MediaEntry>();
            foreach (var entry in entries.EnumerateArray())
            {
                var id = GetString(entry, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                // Flat-playlist entries often carry the bare video id in "url".
                var rawUrl = GetString(entry, "url");
                var urlValue = rawUrl is null || !rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? $"https://www.youtube.com/watch?v={id}"
                    : rawUrl;
                entriesList.Add(new MediaEntry(
                    Id: id,
                    Title: GetString(entry, "title") ?? id,
                    Uploader: GetString(entry, "channel") ?? GetString(entry, "uploader"),
                    DurationSeconds: GetDouble(entry, "duration"),
                    Url: urlValue));
            }

            return (entriesList, playlistTitle);
        }

        // Single video: full metadata for accurate uploader/duration.
        var fullJson = await RunYtDlpAsync(ytDlpPath, $"-J --no-warnings \"{url}\"", cancellationToken);
        using var fullDoc = JsonDocument.Parse(fullJson);
        var single = fullDoc.RootElement;
        return (new List<MediaEntry>
        {
            new(
                Id: GetString(single, "id") ?? url.GetHashCode().ToString("x"),
                Title: GetString(single, "title") ?? url,
                Uploader: GetString(single, "channel") ?? GetString(single, "uploader"),
                DurationSeconds: GetDouble(single, "duration"),
                Url: url),
        }, null);
    }

    /// <summary>
    /// Downloads entries sequentially into the library. Each entry is registered before its download
    /// starts (status Downloading) and finalized (Complete/Failed) afterwards; the index is saved per entry.
    /// </summary>
    public static async Task DownloadManyAsync(
        LibraryStore store,
        IEnumerable<MediaEntry> entries,
        string? playlistTitle,
        MediaKind kind,
        Action<LibraryItem>? onItemChanged,
        Action<int, int>? onItemPosition,
        CancellationToken cancellationToken)
    {
        var list = entries.ToList();
        var ytDlpPath = await ResolveYtDlpAsync();

        for (var index = 0; index < list.Count; index++)
        {
            var entry = list[index];
            cancellationToken.ThrowIfCancellationRequested();

            var item = new LibraryItem
            {
                Id = entry.Id,
                Title = entry.Title,
                Uploader = entry.Uploader,
                Playlist = playlistTitle,
                Kind = kind,
                Status = ItemStatus.Downloading,
                Percent = 0,
                DurationSeconds = entry.DurationSeconds,
            };

            store.AddOrUpdate(item);
            await store.SaveAsync();
            onItemChanged?.Invoke(item);

            // Report job position (e.g. "2/5") for multi-entry downloads.
            if (list.Count > 1)
            {
                onItemPosition?.Invoke(index + 1, list.Count);
            }

            try
            {
                var folder = store.ResolveTargetFolder(entry.Uploader, playlistTitle);
                Directory.CreateDirectory(folder);

                var args = kind == MediaKind.Audio
                    ? $"-x --audio-format mp3 -P \"{folder}\" -o \"{OutputTemplate}\" --newline --progress \"{entry.Url}\""
                    : $"-f \"{VideoFormat}\" --merge-output-format mp4 -P \"{folder}\" -o \"{OutputTemplate}\" --newline --progress \"{entry.Url}\"";

                await RunYtDlpAsync(ytDlpPath, args, cancellationToken, progress =>
                {
                    item.Percent = progress;
                    onItemChanged?.Invoke(item);
                });

                item.FilePath = FindDownloadedFile(folder, entry.Id, kind);
                item.Status = ItemStatus.Complete;
                item.CompletedUtc = DateTimeOffset.UtcNow;
                item.Error = null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.Status = ItemStatus.Failed;
                item.Error = ex.Message;
            }

            store.AddOrUpdate(item);
            await store.SaveAsync();
            onItemChanged?.Invoke(item);
        }
    }

    private static async Task<string> ResolveYtDlpAsync()
    {
        return (await SystemService.CheckToolsAsync()).YtDlp.Path ?? "yt-dlp";
    }

    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+([\d.]+)%", RegexOptions.Compiled);

    /// <summary>Runs yt-dlp, streams stdout lines (reporting download percent when recognized), and returns the full stdout.</summary>
    private static async Task<string> RunYtDlpAsync(
        string ytDlpPath,
        string arguments,
        CancellationToken cancellationToken,
        Action<double>? onPercent = null)
    {
        var psi = new ProcessStartInfo(ytDlpPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Kunde inte starta yt-dlp.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        var output = new StringBuilder();
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(cts.Token)) is not null)
        {
            output.AppendLine(line);

            var match = ProgressRegex.Match(line);
            if (match.Success && onPercent is not null)
            {
                onPercent(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            }
        }

        await process.WaitForExitAsync(cts.Token);
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(stderr) ? $"yt-dlp avslutades med kod {process.ExitCode}" : stderr.Trim());
        }

        return output.ToString();
    }

    /// <summary>Locates the finished file for an entry by its [id] marker in the output template.</summary>
    private static string? FindDownloadedFile(string folder, string entryId, MediaKind kind)
    {
        var extension = kind == MediaKind.Audio ? ".mp3" : ".mp4";
        var marker = $"[{entryId}]";

        try
        {
            return Directory.EnumerateFiles(folder)
                .FirstOrDefault(f => f.Contains(marker, StringComparison.OrdinalIgnoreCase) &&
                                     f.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
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
