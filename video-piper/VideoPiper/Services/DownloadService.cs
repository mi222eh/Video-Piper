using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Contract for simple-mode (one-off) downloads. View models depend on this interface
/// so it can be faked in tests.
/// </summary>
public interface IDownloadService
{
    Task RunAsync(string targetUrl, string? savePath, MediaKind kind, CancellationToken cancellationToken, Action<DownloadProgress> onProgress);
}

/// <summary>
/// Runs yt-dlp to download a video as MP3 (audio) or MP4 (video), reporting progress line by line.
/// Mirrors the SSE behavior of the previous Deno backend. Registered as a singleton in DI.
/// <see cref="BuildVideoFormat"/>, <see cref="BuildVideoOnlyFormat"/> and <see cref="ParseDestination"/>
/// stay static (pure helpers, used directly by tests and <see cref="LibraryDownloadService"/>).
/// </summary>
public sealed class DownloadService : IDownloadService
{
    private readonly ISystemService _systemService;

    public DownloadService(ISystemService systemService)
    {
        _systemService = systemService;
    }
    /// <summary>
    /// Builds the yt-dlp format string for video downloads from the user's quality preferences.
    /// Prefers the chosen framerate when available, then falls back to the best stream at or below
    /// the chosen height, so a 60fps preference never breaks a 24/30fps source.
    /// </summary>
    public static string BuildVideoFormat(int maxHeight, int minFps)
    {
        var baseBranch = $"bestvideo[ext=mp4][height<={maxHeight}]+bestaudio[ext=m4a]/best[ext=mp4][height<={maxHeight}]/best[ext=mp4]/best";
        if (minFps <= 0)
        {
            return baseBranch;
        }

        var preferred = $"bestvideo[ext=mp4][height<={maxHeight}][fps>={minFps}]+bestaudio[ext=m4a]/";
        return preferred + baseBranch;
    }

    /// <summary>
    /// Builds the yt-dlp format string for downloading **only the video stream** (no audio),
    /// using the same quality preferences as <see cref="BuildVideoFormat"/>. Used when a local
    /// MP3 for the item already exists and will be merged into the video with ffmpeg — this
    /// avoids re-downloading (and re-encoding) the audio. Like <see cref="BuildVideoFormat"/>,
    /// it prefers the chosen framerate when available and falls back to the best video stream
    /// at or below the chosen height (the closest available resolution).
    /// </summary>
    public static string BuildVideoOnlyFormat(int maxHeight, int minFps)
    {
        var baseBranch = $"bestvideo[ext=mp4][height<={maxHeight}]/bestvideo[height<={maxHeight}]/bestvideo";
        if (minFps <= 0)
        {
            return baseBranch;
        }

        var preferred = $"bestvideo[ext=mp4][height<={maxHeight}][fps>={minFps}]/";
        return preferred + baseBranch;
    }

    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+\w+)\s+at\s+([\d.]+\w+/s)\s+ETA\s+([\d:]+)",
        RegexOptions.Compiled);

    public async Task RunAsync(
        string targetUrl,
        string? savePath,
        MediaKind kind,
        CancellationToken cancellationToken,
        Action<DownloadProgress> onProgress)
    {
        var workingDir = !string.IsNullOrWhiteSpace(savePath) && Directory.Exists(savePath)
            ? savePath
            : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

        onProgress(new DownloadProgress
        {
            Status = "starting",
            Message = "Startar nedladdning...",
        });

        var tools = await _systemService.CheckToolsAsync();
        var ytDlpPath = tools.YtDlp.Path ?? "yt-dlp";
        var ffmpegLocation = tools.Ffmpeg.Path is not null && File.Exists(tools.Ffmpeg.Path)
            ? $"--ffmpeg-location \"{Path.GetDirectoryName(tools.Ffmpeg.Path)}\" "
            : string.Empty;

        try
        {
            var args = kind == MediaKind.Audio
                ? $"{ffmpegLocation}-x --audio-format mp3 --newline --progress \"{targetUrl}\""
                : $"{ffmpegLocation}-f \"{BuildVideoFormat(PreferencesService.GetMaxHeight(), PreferencesService.GetMinFps())}\" --merge-output-format mp4 --newline --progress \"{targetUrl}\"";

            var psi = new ProcessStartInfo(ytDlpPath, args)
            {
                // No -P is passed, so the child downloads into its CWD. Route it through
                // SafeWorkingDirectory: the CWD must be traversable (an untrusted reparse
                // point would make yt-dlp crash with WinError 448), falling back to a
                // verified temp directory when the save folder is not usable.
                WorkingDirectory = SafeWorkingDirectory.Resolve(workingDir),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                throw new InvalidOperationException("Kunde inte starta yt-dlp.");
            }

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

            var errorLines = new List<string>();
            var destLines = new List<string>();
            var stdoutTask = ReadLinesAsync(process.StandardOutput, onProgress, kind, isError: false, null, destLines);
            var stderrTask = ReadLinesAsync(process.StandardError, onProgress, kind, isError: true, errorLines, null);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);

            if (process.ExitCode == 0)
            {
                onProgress(new DownloadProgress
                {
                    Status = "finished",
                    Percent = 100,
                    Message = "Klar!",
                    Destination = ParseDestination(destLines),
                });
            }
            else
            {
                var lastErr = errorLines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
                var msg = !string.IsNullOrWhiteSpace(lastErr)
                    ? lastErr.Trim()
                    : $"yt-dlp avslutades med kod {process.ExitCode}";
                throw new InvalidOperationException(msg);
            }
        }
        catch (OperationCanceledException)
        {
            onProgress(new DownloadProgress
            {
                Status = "error",
                Message = "Nedladdningen avbröts.",
            });
        }
    }

    private static async Task ReadLinesAsync(
        System.IO.StreamReader reader,
        Action<DownloadProgress> onProgress,
        MediaKind kind,
        bool isError,
        List<string>? errorLines,
        List<string>? destLines)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (isError && errorLines is not null)
            {
                errorLines.Add(trimmed);
            }

            if (!isError && destLines is not null && trimmed.StartsWith("[destination]", StringComparison.OrdinalIgnoreCase))
            {
                destLines.Add(trimmed);
                continue;
            }

            var match = ProgressRegex.Match(trimmed);
            if (match.Success)
            {
                onProgress(new DownloadProgress
                {
                    Status = "downloading",
                    Percent = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                    Speed = match.Groups[3].Value,
                    Eta = match.Groups[4].Value,
                    Message = trimmed,
                });
            }
            else if (trimmed.Contains("[ExtractAudio]") || trimmed.Contains("[ffmpeg]"))
            {
                onProgress(new DownloadProgress
                {
                    Status = "converting",
                    Percent = 99,
                    Message = kind == MediaKind.Audio ? "Konverterar till MP3..." : "Sätter ihop video (MP4)...",
                });
            }
            else
            {
                onProgress(new DownloadProgress
                {
                    Status = isError ? "error" : "downloading",
                    Message = trimmed,
                });
            }
        }
    }

    /// <summary>
    /// Extracts the finished file path from yt-dlp's "[destination] &lt;path&gt;" lines
    /// (the last one wins, since merging/conversion can restate it). Lines without the
    /// prefix are ignored.
    /// </summary>
    internal static string? ParseDestination(IReadOnlyCollection<string> destLines)
    {
        string? path = null;
        foreach (var line in destLines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("[destination]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = trimmed["[destination]".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                path = candidate;
            }
        }
        return path;
    }
}
