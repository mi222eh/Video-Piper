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
    public static string ToolsDirectory => Path.Combine(ApplicationData.Current.LocalFolder.Path, "Tools");

    public static async Task<SystemStatus> CheckToolsAsync()
    {
        var ytDlp = await ResolveAsync("yt-dlp", "--version");
        var ffmpeg = await ResolveAsync("ffmpeg", "-version");
        return new SystemStatus(ytDlp, ffmpeg);
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
