using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Contract for library-mode downloads (metadata fetch + per-item download/merge).
/// View models depend on this interface so it can be faked in tests.
/// </summary>
public interface ILibraryDownloadService
{
    Task<(List<MediaEntry> Entries, string? PlaylistTitle)> FetchEntriesAsync(string url, CancellationToken cancellationToken);

    Task<LibraryItem> DownloadEntryAsync(
        LibraryStore store,
        MediaEntry entry,
        string? playlistTitle,
        MediaKind kind,
        Action<LibraryItem>? onItemChanged,
        CancellationToken cancellationToken,
        Action<string>? onLine = null);
}

/// <summary>
/// Downloads media into a <see cref="LibraryStore"/>: pre-fetches metadata (single video or
/// playlist), then downloads entries sequentially so each item gets its own progress and status.
/// Audio is converted to MP3; video is merged to MP4 (h264/AAC, capped at 1080p). Registered as a
/// singleton in DI.
/// </summary>
public sealed class LibraryDownloadService : ILibraryDownloadService
{
    private readonly ISystemService _systemService;

    public LibraryDownloadService(ISystemService systemService)
    {
        _systemService = systemService;
    }
    private const string OutputTemplate = "%(title)s [%(id)s].%(ext)s";

    /// <summary>
    /// Fetches metadata for a URL: one entry for a single video, all entries for a playlist.
    /// Playlists use --flat-playlist (fast, no stream resolution); the playlist title is returned separately.
    /// </summary>
    public async Task<(List<MediaEntry> Entries, string? PlaylistTitle)> FetchEntriesAsync(
        string url, CancellationToken cancellationToken)
    {
        var ytDlpPath = await ResolveYtDlpAsync();

        // Fast probe: check if playlist or single video with flat-playlist
        var flatJson = await RunYtDlpAsync(ytDlpPath, $"--flat-playlist -J --no-warnings \"{url}\"", DefaultWorkingDirectory(), cancellationToken);
        if (YtDlpJson.TryParsePlaylist(flatJson) is { } playlist && playlist.Entries.Count > 0)
        {
            return playlist;
        }

        // Single video: parse the root element directly from the flatJson without a 2nd subprocess call
        try
        {
            return (new List<MediaEntry> { YtDlpJson.ParseSingle(flatJson) }, null);
        }
        catch
        {
            // Fallback: full metadata probe only if flat parsing failed
            var fullJson = await RunYtDlpAsync(ytDlpPath, $"-J --no-warnings \"{url}\"", DefaultWorkingDirectory(), cancellationToken);
            return (new List<MediaEntry> { YtDlpJson.ParseSingle(fullJson) }, null);
        }
    }

    /// <summary>
    /// Downloads a single entry into the library with per-item progress. The item is added to the store
    /// before the download starts and finalized afterwards. Failures mark the item <see cref="ItemStatus.Failed"/>
    /// (kept in the index for retry); cancellation marks it <c>Failed</c> and rethrows.
    /// <paramref name="onLine"/> receives every stdout line from yt-dlp (for per-item log views).
    /// Returns the finalized item.
    /// </summary>
    public async Task<LibraryItem> DownloadEntryAsync(
        LibraryStore store,
        MediaEntry entry,
        string? playlistTitle,
        MediaKind kind,
        Action<LibraryItem>? onItemChanged,
        CancellationToken cancellationToken,
        Action<string>? onLine = null)
    {
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

        var ytDlpPath = await ResolveYtDlpAsync();

        try
        {
            var folder = store.ResolveTargetFolder(entry.Uploader, playlistTitle);
            Directory.CreateDirectory(folder);

            var tools = await _systemService.CheckToolsAsync();
            var ffmpegLocation = tools.Ffmpeg.Path is not null && File.Exists(tools.Ffmpeg.Path)
                ? $"--ffmpeg-location \"{Path.GetDirectoryName(tools.Ffmpeg.Path)}\" "
                : string.Empty;

            var maxHeight = PreferencesService.GetMaxHeight();
            var minFps = PreferencesService.GetMinFps();
            Action<double> onPercent = progress =>
            {
                item.Percent = progress;
                onItemChanged?.Invoke(item);
            };

            // When the user asks for MP4 and an MP3 for this item is already on disk,
            // download only the video stream and merge the existing MP3 into it with
            // ffmpeg — this skips re-downloading (and re-encoding) the audio. Requires
            // the app-managed ffmpeg. If no separate video stream exists, yt-dlp fails
            // and we fall back to the normal full-video download (no merge).
            var ffmpegPath = tools.Ffmpeg.Path;
            var canReuseMp3 = ffmpegPath is not null && File.Exists(ffmpegPath);
            string? reuseMp3 = kind == MediaKind.Video && canReuseMp3 ? FindExistingMp3(folder, entry.Id) : null;

            bool videoOnlySucceeded = false;
            if (reuseMp3 is not null)
            {
                onLine?.Invoke("MP3-fil finns redan — hämtar endast videodelen...");
                try
                {
                    var videoOnlyArgs = $"{ffmpegLocation}-f \"{DownloadService.BuildVideoOnlyFormat(maxHeight, minFps)}\" -P \"{folder}\" -o \"{OutputTemplate}\" --newline --progress \"{entry.Url}\"";
                    await RunYtDlpAsync(ytDlpPath, videoOnlyArgs, workingDirectory: folder, cancellationToken, onPercent, onLine);
                    videoOnlySucceeded = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    onLine?.Invoke($"Endast-videodel finns inte ({ex.Message}) — hämtar istället hela videon...");
                    reuseMp3 = null;
                }
            }

            if (!videoOnlySucceeded)
            {
                var args = kind == MediaKind.Audio
                    ? $"{ffmpegLocation}-x --audio-format mp3 -P \"{folder}\" -o \"{OutputTemplate}\" --newline --progress \"{entry.Url}\""
                    : $"{ffmpegLocation}-f \"{DownloadService.BuildVideoFormat(maxHeight, minFps)}\" --merge-output-format mp4 -P \"{folder}\" -o \"{OutputTemplate}\" --newline --progress \"{entry.Url}\"";
                await RunYtDlpAsync(ytDlpPath, args, workingDirectory: folder, cancellationToken, onPercent, onLine);
            }

            if (videoOnlySucceeded && reuseMp3 is not null)
            {
                onLine?.Invoke("Kopplar ihop videodelen med den sparade MP3-filen...");
                await MergeVideoWithMp3Async(ffmpegPath!, folder, entry.Id, reuseMp3, cancellationToken, onLine);
            }

            item.FilePath = FindDownloadedFile(folder, entry.Id, kind);
            item.Status = ItemStatus.Complete;
            item.CompletedUtc = DateTimeOffset.UtcNow;
            item.Error = null;
        }
        catch (OperationCanceledException)
        {
            item.Status = ItemStatus.Failed;
            item.Error = "Nedladdningen avbröts.";
            store.AddOrUpdate(item);
            await store.SaveAsync();
            onItemChanged?.Invoke(item);
            throw;
        }
        catch (Exception ex)
        {
            item.Status = ItemStatus.Failed;
            // Row status text is user-facing: never a raw (English) exception message.
            item.Error = UiErrors.FromException(ex, "Nedladdningen misslyckades.");
            Log.For(typeof(LibraryDownloadService)).LogError(ex, "Nedladdning misslyckades: {ItemId} {Title}", item.Id, item.Title);
        }

        store.AddOrUpdate(item);
        await store.SaveAsync();
        onItemChanged?.Invoke(item);
        return item;
    }

    private async Task<string> ResolveYtDlpAsync()
    {
        return (await _systemService.CheckToolsAsync()).YtDlp.Path ?? "yt-dlp";
    }

    /// <summary>
    /// A working directory for yt-dlp probes that have no per-item target folder yet. Routed
    /// through <see cref="SafeWorkingDirectory"/> so the child never inherits an untrusted
    /// reparse point (WinError 448) as its CWD — including the case where TEMP/TMP are empty
    /// and <c>Path.GetTempPath()</c> returns the process CWD.
    /// </summary>
    private static string DefaultWorkingDirectory() => SafeWorkingDirectory.Resolve(null);

    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+([\d.]+)%", RegexOptions.Compiled);

    /// <summary>Runs yt-dlp, streams stdout lines (reporting download percent when recognized), and returns the full stdout.</summary>
    private static async Task<string> RunYtDlpAsync(
        string ytDlpPath,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        Action<double>? onPercent = null,
        Action<string>? onLine = null)
    {
        var psi = new ProcessStartInfo(ytDlpPath, arguments)
        {
            // Pin the working directory to the item's target folder. Without this the child
            // inherits the app's CWD; routing through SafeWorkingDirectory also guards the
            // case where the target folder itself descends through an untrusted reparse point
            // (WinError 448), falling back to a verified temp directory.
            WorkingDirectory = SafeWorkingDirectory.Resolve(workingDirectory),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Kunde inte starta yt-dlp.");

        using var reg = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have already exited.
            }
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        var output = new StringBuilder();
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(cts.Token)) is not null)
        {
            output.AppendLine(line);

            onLine?.Invoke(line);

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
            var files = Directory.EnumerateFiles(folder).ToList();
            var match = files.FirstOrDefault(f => f.Contains(marker, StringComparison.OrdinalIgnoreCase) &&
                                                  f.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            return files.FirstOrDefault(f => f.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Finds an already-downloaded MP3 for an entry (matched by the [id] marker), or null.</summary>
    private static string? FindExistingMp3(string folder, string entryId)
    {
        var marker = $"[{entryId}]";
        try
        {
            return Directory.EnumerateFiles(folder)
                .FirstOrDefault(f => f.Contains(marker, StringComparison.OrdinalIgnoreCase)
                                     && f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Merges a downloaded video-only file with an existing MP3 into a single MP4,
    /// producing the item's normal final file name (sanitized title + [id]). The video
    /// stream is copied when the container accepts it (h264/hevc); otherwise it is
    /// re-encoded to h264 so the result is a playable MP4. The MP3 audio is re-encoded
    /// to AAC (MP3 streams are not valid inside MP4).
    /// </summary>
    private static async Task MergeVideoWithMp3Async(
        string ffmpegPath,
        string folder,
        string entryId,
        string mp3Path,
        CancellationToken cancellationToken,
        Action<string>? onLine = null)
    {
        var marker = $"[{entryId}]";
        var videoFile = Directory.EnumerateFiles(folder)
            .FirstOrDefault(f => f.Contains(marker, StringComparison.OrdinalIgnoreCase)
                                 && (f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                                     || f.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
                                     || f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)));
        if (videoFile is null)
        {
            throw new InvalidOperationException("Hittade inte den nedladdade videodelen.");
        }

        // The merged MP4 keeps the same base name as the existing MP3 so the pair stays consistent.
        var finalPath = Path.ChangeExtension(mp3Path, ".mp4");
        var tempPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(mp3Path) + ".merging.mp4");

        async Task RunMergeAsync(string videoCodecArgs)
        {
            var args = $"-y -i \"{videoFile}\" -i \"{mp3Path}\" {videoCodecArgs} -c:a aac -b:a 192k -shortest -movflags +faststart \"{tempPath}\"";
            var psi = new ProcessStartInfo(ffmpegPath, args)
            {
                WorkingDirectory = SafeWorkingDirectory.Resolve(folder),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Kunde inte starta ffmpeg.");
            using var reg = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Process may have already exited.
                }
            });
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr)
                    ? $"ffmpeg avslutades med kod {process.ExitCode}"
                    : stderr.Trim().Split('\n').Last().Trim();
                throw new InvalidOperationException($"ffmpeg misslyckades: {message}");
            }
        }

        // The video stream from yt-dlp is usually h264, which MP4 accepts; try a stream copy
        // first (fast, lossless) and only re-encode when the codec is container-incompatible
        // (e.g. VP9/AV1 from a .webm source).
        try
        {
            await RunMergeAsync("-c:v copy");
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
        catch (Exception)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            onLine?.Invoke("Omkodar videon för MP4-kompatibilitet...");
            await RunMergeAsync("-c:v libx264 -preset veryfast -crf 21");
        }

        // Replace the merged file into place. The video-only file is removed only if it is
        // not already the final file (which the replace just overwrote).
        var videoIsFinal = string.Equals(Path.GetFullPath(videoFile), Path.GetFullPath(finalPath), StringComparison.OrdinalIgnoreCase);
        if (File.Exists(finalPath))
        {
            File.Replace(tempPath, finalPath, null);
        }
        else
        {
            File.Move(tempPath, finalPath);
        }
        if (!videoIsFinal)
        {
            try { File.Delete(videoFile); } catch { }
        }
    }

}
