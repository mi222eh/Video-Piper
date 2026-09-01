using System.IO.Compression;

namespace VideoPiper.Services;

public sealed class ToolInstallException : Exception
{
    public ToolInstallException(string message) : base(message)
    {
    }
}

/// <summary>
/// Downloads missing media tools (yt-dlp, ffmpeg) into the app's local data folder
/// so the user never has to install anything manually.
/// </summary>
public static class ToolInstallerService
{
    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegZipUrl = "https://github.com/GyanD/codexffmpeg/releases/download/7.1/ffmpeg-7.1-full_build.zip";

    public static event Action<string, double?>? Progress;

    /// <summary>Installs whichever of the given tools is missing.</summary>
    public static async Task InstallMissingAsync(IReadOnlyCollection<string> toolNames)
    {
        Directory.CreateDirectory(SystemService.ToolsDirectory);

        foreach (var tool in toolNames)
        {
            switch (tool.ToLowerInvariant())
            {
                case "yt-dlp":
                    await DownloadFileAsync(YtDlpUrl, Path.Combine(SystemService.ToolsDirectory, "yt-dlp.exe"), "yt-dlp");
                    break;

                case "ffmpeg":
                    await DownloadAndExtractFfmpegAsync();
                    break;
            }
        }
    }

    private static async Task DownloadFileAsync(string url, string destination, string label)
    {
        var tempPath = destination + ".download";
        try
        {
            using var http = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
            });

            Progress?.Invoke($"Hämtar {label}...", null);

            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                throw new ToolInstallException($"Kunde inte hämta {label} (HTTP {(int)response.StatusCode}).");
            }

            var total = response.Content.Headers.ContentLength;
            using var content = await response.Content.ReadAsStreamAsync();
            using var file = File.Create(tempPath);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await content.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;
                if (total is > 0)
                {
                    Progress?.Invoke($"Hämtar {label}...", readTotal * 100.0 / total.Value);
                }
            }

            File.Move(tempPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }

    private static async Task DownloadAndExtractFfmpegAsync()
    {
        var zipPath = Path.Combine(SystemService.ToolsDirectory, "ffmpeg.zip");
        await DownloadFileAsync(FfmpegZipUrl, zipPath, "ffmpeg");

        Progress?.Invoke("Extraherar ffmpeg...", null);
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = Path.Combine(SystemService.ToolsDirectory, "ffmpeg.exe");
                await using var dest = File.Create(target);
                await using var src = entry.Open();
                await src.CopyToAsync(dest);
                return;
            }

            throw new ToolInstallException("ffmpeg.exe hittades inte i arkivet.");
        }
        finally
        {
            try { File.Delete(zipPath); } catch { /* best effort */ }
        }
    }
}
