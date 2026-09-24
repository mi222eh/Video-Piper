using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Runs yt-dlp to download a video as MP3 (audio) or MP4 (video), reporting progress line by line.
/// Mirrors the SSE behavior of the previous Deno backend.
/// </summary>
public static class DownloadService
{
    private const string VideoFormat = "bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/best[ext=mp4]/best";

    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+\w+)\s+at\s+([\d.]+\w+/s)\s+ETA\s+([\d:]+)",
        RegexOptions.Compiled);

    public static async Task RunAsync(
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

        var tools = await SystemService.CheckToolsAsync();
        var ytDlpPath = tools.YtDlp.Path ?? "yt-dlp";
        var ffmpegLocation = tools.Ffmpeg.Path is not null && File.Exists(tools.Ffmpeg.Path)
            ? $"--ffmpeg-location \"{Path.GetDirectoryName(tools.Ffmpeg.Path)}\" "
            : string.Empty;

        try
        {
            var args = kind == MediaKind.Audio
                ? $"{ffmpegLocation}-x --audio-format mp3 --newline --progress \"{targetUrl}\""
                : $"{ffmpegLocation}-f \"{VideoFormat}\" --merge-output-format mp4 --newline --progress \"{targetUrl}\"";

            var psi = new ProcessStartInfo(ytDlpPath, args)
            {
                WorkingDirectory = workingDir,
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
            var stdoutTask = ReadLinesAsync(process.StandardOutput, onProgress, kind, isError: false, null);
            var stderrTask = ReadLinesAsync(process.StandardError, onProgress, kind, isError: true, errorLines);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);

            if (process.ExitCode == 0)
            {
                onProgress(new DownloadProgress
                {
                    Status = "finished",
                    Percent = 100,
                    Message = "Klar!",
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
        List<string>? errorLines)
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
}
