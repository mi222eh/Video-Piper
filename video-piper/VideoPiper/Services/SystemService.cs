using System.Diagnostics;

namespace VideoPiper.Services;

public sealed record ToolInfo(bool Available, string? Version, string? Path);

public sealed record SystemStatus(ToolInfo YtDlp, ToolInfo Ffmpeg)
{
    public bool AllAvailable => YtDlp.Available && Ffmpeg.Available;
}

/// <summary>
/// Detects whether the external media tools (yt-dlp, ffmpeg) are available.
/// Resolution order: binaries installed by the app (LocalFolder/Tools), then system PATH.
/// </summary>
public static class SystemService
{
    public static string ToolsDirectory
    {
        get
        {
#if WINDOWS || HAS_UNO
            try
            {
                return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "Tools");
            }
            catch
            {
                // Fallback to LocalApplicationData
            }
#endif
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoPiper", "Tools");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static async Task<SystemStatus> CheckToolsAsync()
    {
        var ytDlp = await ResolveAsync("yt-dlp", "--version");
        var ffmpeg = await ResolveAsync("ffmpeg", "-version");
        return new SystemStatus(ytDlp, ffmpeg);
    }

    /// <summary>
    /// Ensures that yt-dlp and ffmpeg binaries are present locally, automatically
    /// downloading any that are missing and scheduling a background update for yt-dlp.
    /// </summary>
    public static async Task<SystemStatus> EnsureToolsAsync(Action<string>? statusCallback = null)
    {
        Directory.CreateDirectory(ToolsDirectory);

        var current = await CheckToolsAsync();
        var toInstall = new List<string>();

        var localYtDlp = Path.Combine(ToolsDirectory, "yt-dlp.exe");
        if (!File.Exists(localYtDlp) && !current.YtDlp.Available)
        {
            toInstall.Add("yt-dlp");
        }

        var localFfmpeg = Path.Combine(ToolsDirectory, "ffmpeg.exe");
        if (!File.Exists(localFfmpeg) && !current.Ffmpeg.Available)
        {
            toInstall.Add("ffmpeg");
        }

        if (toInstall.Count > 0)
        {
            statusCallback?.Invoke($"Hämtar verktyg: {string.Join(", ", toInstall)}...");
            await ToolInstallerService.InstallMissingAsync(toInstall);
        }

        // Schedule background update for local yt-dlp binary
        if (File.Exists(localYtDlp))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await AutoUpdateYtDlpAsync(localYtDlp);
                }
                catch
                {
                    // Update check best effort
                }
            });
        }

        return await CheckToolsAsync();
    }

    public static async Task<bool> AutoUpdateYtDlpAsync(string localPath)
    {
        try
        {
            var psi = new ProcessStartInfo(localPath, "--update")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ToolInfo> ResolveAsync(string fileName, string versionArg)
    {
        // 1. Locally installed copy (from the in-app installer)
        var localPath = Path.Combine(ToolsDirectory, fileName + ".exe");
        if (File.Exists(localPath))
        {
            var localVersion = await RunAsync(localPath, versionArg);
            if (localVersion is not null)
            {
                return new ToolInfo(true, localVersion, localPath);
            }
        }

        // 2. System PATH
        var pathVersion = await RunAsync(fileName, versionArg);
        if (pathVersion is not null)
        {
            return new ToolInfo(true, pathVersion, fileName);
        }

        return new ToolInfo(false, null, null);
    }

    private static async Task<string?> RunAsync(string fileName, string argument)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, argument)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? stdout.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
