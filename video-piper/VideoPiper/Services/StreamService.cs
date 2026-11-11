using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Contract for online playback: resolves a YouTube video's streams and relays them as a
/// local HLS playlist that the Windows media pipeline can play. Streaming is a fallback
/// for media that is not already downloaded.
/// </summary>
public interface IStreamService
{
    /// <summary>
    /// Starts (or restarts) a stream and returns the local playlist URI once the first
    /// segment is ready. Cancelling the token aborts the startup, not the returned stream.
    /// </summary>
    Task<Uri> StartAsync(string videoId, MediaKind kind, CancellationToken cancellationToken);

    /// <summary>Stops the active stream and removes its temp files.</summary>
    Task StopAsync();
}

/// <summary>
/// Streams YouTube through yt-dlp + ffmpeg into a temp HLS folder served over a loopback
/// HTTP server. This keeps full quality (the player gets a muxed stream) and avoids
/// depending on YouTube's progressive (360p) URLs. One stream at a time; starting a new
/// one stops the previous session. Registered as a singleton in DI.
/// </summary>
public sealed class StreamService : IStreamService
{
    private const int StartupTimeoutSeconds = 25;

    private readonly ISystemService _systemService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Session? _session;

    public StreamService(ISystemService systemService)
    {
        _systemService = systemService;
    }

    public async Task<Uri> StartAsync(string videoId, MediaKind kind, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
            var session = await Session.CreateAsync(_systemService, videoId, kind, cancellationToken).ConfigureAwait(false);
            _session = session;
            return session.PlaylistUri;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        if (_session is { } session)
        {
            _session = null;
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class Session : IAsyncDisposable
    {
        private readonly string _directory;
        private readonly Process _ffmpeg;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly Task _acceptTask;
        private readonly Task<string> _stderrTask;

        private Session(string directory, Process ffmpeg, TcpListener listener, Uri playlistUri, CancellationTokenSource cts, Task acceptTask, Task<string> stderrTask)
        {
            _directory = directory;
            _ffmpeg = ffmpeg;
            _listener = listener;
            PlaylistUri = playlistUri;
            _cts = cts;
            _acceptTask = acceptTask;
            _stderrTask = stderrTask;
        }

        public Uri PlaylistUri { get; }

        public static async Task<Session> CreateAsync(
            ISystemService systemService,
            string videoId,
            MediaKind kind,
            CancellationToken cancellationToken)
        {
            var tools = await systemService.CheckToolsAsync().ConfigureAwait(false);
            if (tools.YtDlp.Path is null || tools.Ffmpeg.Path is null || !File.Exists(tools.Ffmpeg.Path))
            {
                tools = await systemService.EnsureToolsAsync().ConfigureAwait(false);
            }

            var ytDlpPath = tools.YtDlp.Path ?? throw new FileNotFoundException("yt-dlp hittades inte.");
            var ffmpegPath = tools.Ffmpeg.Path ?? throw new FileNotFoundException("ffmpeg hittades inte.");
            if (!File.Exists(ffmpegPath))
            {
                throw new FileNotFoundException("ffmpeg hittades inte.");
            }

            var urls = await ResolveStreamUrlsAsync(ytDlpPath, videoId, kind, cancellationToken).ConfigureAwait(false);
            var directory = Path.Combine(Path.GetTempPath(), "videopiper-stream-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var playlistPath = Path.Combine(directory, "playlist.m3u8");
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Process? ffmpeg = null;
            Task<string>? stderrTask = null;
            try
            {
                ffmpeg = StartFfmpeg(ffmpegPath, directory, urls, kind, transcode: false, cts.Token);
                stderrTask = DrainAsync(ffmpeg.StandardError);

                var ready = await WaitForPlaylistAsync(ffmpeg, playlistPath, stderrTask, cts.Token).ConfigureAwait(false);
                if (!ready)
                {
                    var firstError = await ReadStderrAsync(stderrTask).ConfigureAwait(false);
                    await KillAsync(ffmpeg).ConfigureAwait(false);
                    ffmpeg = StartFfmpeg(ffmpegPath, directory, urls, kind, transcode: true, cts.Token);
                    stderrTask = DrainAsync(ffmpeg.StandardError);
                    ready = await WaitForPlaylistAsync(ffmpeg, playlistPath, stderrTask, cts.Token).ConfigureAwait(false);
                    if (!ready)
                    {
                        var secondError = await ReadStderrAsync(stderrTask).ConfigureAwait(false);
                        throw new InvalidOperationException(
                            "Kunde inte starta strömmen." + (string.IsNullOrWhiteSpace(secondError) ? firstError : secondError));
                    }
                }

                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var playlistUri = new Uri($"http://127.0.0.1:{port}/playlist.m3u8");
                var acceptTask = AcceptLoopAsync(listener, directory, cts.Token);

                return new Session(directory, ffmpeg, listener, playlistUri, cts, acceptTask, stderrTask);
            }
            catch
            {
                cts.Cancel();
                if (ffmpeg is not null)
                {
                    await KillAsync(ffmpeg).ConfigureAwait(false);
                }
                TryDeleteDirectory(directory);
                cts.Dispose();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
                // Already disposed.
            }

            await KillAsync(_ffmpeg).ConfigureAwait(false);
            try
            {
                _listener.Stop();
            }
            catch
            {
                // Already stopped.
            }

            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch
            {
                // Expected when the listener stops.
            }

            try
            {
                await _stderrTask.ConfigureAwait(false);
            }
            catch
            {
                // Best effort.
            }

            _ffmpeg.Dispose();
            TryDeleteDirectory(_directory);
            _cts.Dispose();
        }

        private static async Task<List<string>> ResolveStreamUrlsAsync(
            string ytDlpPath,
            string videoId,
            MediaKind kind,
            CancellationToken cancellationToken)
        {
            var maxHeight = PreferencesService.GetMaxHeight();
            var minFps = PreferencesService.GetMinFps();
            var format = kind == MediaKind.Video
                ? DownloadService.BuildStreamFormat(maxHeight, minFps)
                : "bestaudio/best";

            var psi = new ProcessStartInfo(ytDlpPath)
            {
                WorkingDirectory = SafeWorkingDirectory.Resolve(null),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--no-warnings");
            psi.ArgumentList.Add("--no-playlist");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(format);
            psi.ArgumentList.Add("-g");
            psi.ArgumentList.Add($"https://www.youtube.com/watch?v={videoId}");

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Kunde inte starta yt-dlp.");
            using var reg = cancellationToken.Register(() => TryKill(process));
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(stderr) ? $"yt-dlp avslutades med kod {process.ExitCode}." : stderr.Trim());
            }

            var urls = stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (urls.Count == 0)
            {
                throw new InvalidOperationException("yt-dlp returnerade ingen ström-URL.");
            }

            return urls;
        }

        private static Process StartFfmpeg(
            string ffmpegPath,
            string directory,
            IReadOnlyList<string> urls,
            MediaKind kind,
            bool transcode,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo(ffmpegPath)
            {
                WorkingDirectory = SafeWorkingDirectory.Resolve(directory),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            void Add(params string[] args)
            {
                foreach (var arg in args)
                {
                    psi.ArgumentList.Add(arg);
                }
            }

            Add("-y", "-hide_banner", "-loglevel", "error",
                "-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5",
                "-user_agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                "-referer", "https://www.youtube.com/");

            if (kind == MediaKind.Video)
            {
                Add("-i", urls[0]);
                if (urls.Count > 1)
                {
                    Add("-i", urls[1]);
                }

                Add("-map", "0:v:0");
                if (urls.Count > 1)
                {
                    Add("-map", "1:a:0?");
                }
                else
                {
                    Add("-map", "0:a:0?");
                }

                if (transcode)
                {
                    Add("-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-c:a", "aac", "-b:a", "192k");
                }
                else
                {
                    Add("-c:v", "copy", "-c:a", "aac", "-b:a", "192k");
                }
            }
            else
            {
                Add("-i", urls[0], "-vn", "-c:a", "aac", "-b:a", "192k");
            }

            Add("-f", "hls",
                "-hls_time", "2",
                "-hls_list_size", "0",
                "-hls_playlist_type", "event",
                "-hls_segment_filename", Path.Combine(directory, "seg%05d.ts"),
                Path.Combine(directory, "playlist.m3u8"));

            var process = Process.Start(psi) ?? throw new InvalidOperationException("Kunde inte starta ffmpeg.");
            // Do not dispose this registration: it must live as long as the process, and the
            // token's CTS disposes it when the session ends.
            _ = cancellationToken.Register(() => TryKill(process));
            return process;
        }

        private static async Task<bool> WaitForPlaylistAsync(
            Process ffmpeg,
            string playlistPath,
            Task<string> stderrTask,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddSeconds(StartupTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(playlistPath))
                {
                    try
                    {
                        var text = await File.ReadAllTextAsync(playlistPath, cancellationToken).ConfigureAwait(false);
                        if (text.Contains(".ts", StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    catch (IOException)
                    {
                        // ffmpeg is mid-write; retry.
                    }
                }

                if (ffmpeg.HasExited)
                {
                    await stderrTask.ConfigureAwait(false);
                    return false;
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            // Timed out while ffmpeg is still running — the caller kills it; awaiting the
            // stderr drain here would block on the live process.
            return false;
        }

        private static Task<string> DrainAsync(StreamReader reader) => Task.Run(async () =>
        {
            var lines = new List<string>();
            try
            {
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    // Keep draining so ffmpeg never blocks on a full stderr pipe; keep the
                    // tail for the failure message.
                    lines.Add(line);
                    if (lines.Count > 40)
                    {
                        lines.RemoveAt(0);
                    }
                }
            }
            catch
            {
                // Process killed.
            }

            return string.Join(Environment.NewLine, lines);
        });

        private static async Task<string> ReadStderrAsync(Task<string> stderrTask)
        {
            await Task.WhenAny(stderrTask, Task.Delay(500)).ConfigureAwait(false);
            return stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
        }

        private static async Task AcceptLoopAsync(TcpListener listener, string directory, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ServeAsync(client, directory, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Client disconnected or the session stopped.
                    }
                    finally
                    {
                        client.Dispose();
                    }
                }, CancellationToken.None);
            }
        }

        private static async Task ServeAsync(TcpClient client, string directory, CancellationToken cancellationToken)
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return;
            }

            var method = parts[0];
            var path = Uri.UnescapeDataString(parts[1].Split('?')[0].TrimStart('/'));

            long rangeStart = 0;
            var hasRange = false;
            string? header;
            while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)))
            {
                var match = RangeRegex.Match(header);
                if (match.Success)
                {
                    hasRange = long.TryParse(match.Groups[1].Value, out rangeStart);
                }
            }

            if (path != "playlist.m3u8" && !(path.StartsWith("seg", StringComparison.Ordinal) && path.EndsWith(".ts", StringComparison.Ordinal)))
            {
                await WriteStatusAsync(stream, 404, "Not Found", cancellationToken).ConfigureAwait(false);
                return;
            }

            var filePath = Path.Combine(directory, path);
            if (!File.Exists(filePath))
            {
                // A player may ask for a segment ffmpeg has not written yet.
                await WriteStatusAsync(stream, 404, "Not Found", cancellationToken).ConfigureAwait(false);
                return;
            }

            var length = new FileInfo(filePath).Length;
            if (hasRange && rangeStart >= 0 && rangeStart < length)
            {
                var end = length - 1;
                var status = 206;
                var rangeHeader = $"Content-Range: bytes {rangeStart}-{end}/{length}\r\n";
                var contentLength = end - rangeStart + 1;
                await WriteHeadersAsync(stream, status, "Partial Content", path, contentLength, rangeHeader, cancellationToken).ConfigureAwait(false);
                await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, useAsync: true);
                file.Seek(rangeStart, SeekOrigin.Begin);
                await CopyBytesAsync(file, stream, contentLength, cancellationToken).ConfigureAwait(false);
                return;
            }

            await WriteHeadersAsync(stream, 200, "OK", path, length, string.Empty, cancellationToken).ConfigureAwait(false);
            if (method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, useAsync: true))
            {
                await CopyBytesAsync(file, stream, length, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task WriteHeadersAsync(
            Stream stream,
            int status,
            string reason,
            string path,
            long contentLength,
            string extraHeaders,
            CancellationToken cancellationToken)
        {
            var contentType = path.EndsWith(".m3u8", StringComparison.Ordinal)
                ? "application/vnd.apple.mpegurl"
                : "video/mp2t";
            var headers = $"HTTP/1.1 {status} {reason}\r\n" +
                          $"Content-Type: {contentType}\r\n" +
                          $"Content-Length: {contentLength}\r\n" +
                          "Accept-Ranges: bytes\r\n" +
                          (path.EndsWith(".m3u8", StringComparison.Ordinal)
                              ? "Cache-Control: no-cache\r\n"
                              : "Cache-Control: max-age=3600\r\n") +
                          extraHeaders +
                          "Connection: close\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        private static async Task WriteStatusAsync(Stream stream, int status, string reason, CancellationToken cancellationToken)
        {
            var bytes = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        private static async Task CopyBytesAsync(Stream source, Stream destination, long count, CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024];
            while (count > 0)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, count)), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                count -= read;
            }
        }

        private static readonly Regex RangeRegex = new(@"^Range:\s*bytes=(\d+)-", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static async Task KillAsync(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // Already exited.
            }
        }

        private static void TryKill(Process process)
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
                // Already exited.
            }
        }

        private static void TryDeleteDirectory(string directory)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
