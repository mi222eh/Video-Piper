using System.Diagnostics;

namespace VideoPiper.Services;

public sealed record ToolInfo(bool Available, string? Version, string? Path);

public sealed record SystemStatus(ToolInfo YtDlp, ToolInfo Ffmpeg)
{
    public bool AllAvailable => YtDlp.Available && Ffmpeg.Available;
}

/// <summary>
/// Contract for resolving/ensuring the app-managed media tools (yt-dlp, ffmpeg).
/// View models depend on this interface so it can be faked in tests.
/// </summary>
public interface ISystemService
{
    Task<SystemStatus> CheckToolsAsync();

    Task<SystemStatus> EnsureToolsAsync(Action<string>? statusCallback = null);
}

/// <summary>
/// Resolves the media tools (yt-dlp, ffmpeg) from the app's own local install
/// (LocalFolder/Tools). System PATH is intentionally NOT used — the app owns its
/// binaries and keeps them updated on its own (see ToolUpdateService). Registered as a
/// singleton in DI. <see cref="ToolsDirectory"/> and <see cref="GetLocalVersionAsync"/>
/// stay static: they are used by <see cref="ToolUpdateService"/> (which would otherwise
/// create a circular dependency).
/// </summary>
public sealed class SystemService : ISystemService
{
    private readonly IToolUpdateService _toolUpdateService;

    public SystemService(IToolUpdateService toolUpdateService)
    {
        _toolUpdateService = toolUpdateService;
    }
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

    public async Task<SystemStatus> CheckToolsAsync()
    {
        var ytDlp = await GetLocalToolAsync("yt-dlp.exe", "--version");
        var ffmpeg = await GetLocalToolAsync("ffmpeg.exe", "-version");
        return new SystemStatus(ytDlp, ffmpeg);
    }

    /// <summary>Reads the installed version of a local tool, or null when not installed.</summary>
    public static async Task<string?> GetLocalVersionAsync(string fileName, string versionArg)
    {
        var info = await GetLocalToolAsync(fileName, versionArg);
        return info.Available ? info.Version : null;
    }

    /// <summary>
    /// Makes sure the local tools exist (installing any that are missing), then runs a
    /// background update check once per day. The daily gate is a timestamp saved in the
    /// app's local data folder (see ToolUpdatesPrefs).
    /// </summary>
    public async Task<SystemStatus> EnsureToolsAsync(Action<string>? statusCallback = null)
    {
        Directory.CreateDirectory(ToolsDirectory);

        var current = await CheckToolsAsync();
        var toInstall = new List<string>();
        if (!current.YtDlp.Available)
        {
            toInstall.Add("yt-dlp");
        }

        if (!current.Ffmpeg.Available)
        {
            toInstall.Add("ffmpeg");
        }

        if (toInstall.Count > 0)
        {
            statusCallback?.Invoke($"Hämtar verktyg: {string.Join(", ", toInstall)}...");
            foreach (var tool in toInstall)
            {
                var result = await _toolUpdateService.CheckToolAsync(tool, apply: true);
                statusCallback?.Invoke(result.Error is null
                    ? "Verktyg installerade."
                    : $"Några verktyg kunde inte hämtas ({result.Error})");
            }
        }

        // Daily background update check (both tools), once per day.
        if (ToolUpdatesPrefs.IsUpdateCheckDue())
        {
            ToolUpdatesPrefs.MarkChecked();
            _ = Task.Run(async () =>
            {
                try
                {
                    await _toolUpdateService.CheckToolAsync("yt-dlp", apply: true);
                    await _toolUpdateService.CheckToolAsync("ffmpeg", apply: true);
                }
                catch
                {
                    // Update check is best effort.
                }
            });
        }

        return await CheckToolsAsync();
    }

    private static async Task<ToolInfo> GetLocalToolAsync(string fileName, string versionArg)
    {
        var localPath = Path.Combine(ToolsDirectory, fileName);
        if (File.Exists(localPath))
        {
            var version = await RunAsync(localPath, versionArg);
            if (version is not null)
            {
                return new ToolInfo(true, version, localPath);
            }

            // Binary present but not runnable (e.g. still downloading) — treat as unavailable.
        }

        return new ToolInfo(false, null, null);
    }

    private static async Task<string?> RunAsync(string fileName, string argument)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, argument)
            {
                // The version probe has no natural target folder. Without a working
                // directory the child inherits the app's CWD, which may be an untrusted
                // reparse point (WinError 448); SafeWorkingDirectory falls back to a
                // verified temp directory.
                WorkingDirectory = SafeWorkingDirectory.Resolve(null),
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

/// <summary>
/// Persists the timestamp of the last background tool-update check (tools.json in
/// the app's local data folder) so at most one automatic check runs per day.
/// Manual checks from the settings page do NOT update this timestamp.
/// </summary>
public static class ToolUpdatesPrefs
{
    private const int CheckIntervalHours = 24;

    private static string FilePath
    {
        get
        {
#if WINDOWS || HAS_UNO
            try
            {
                return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "tools.json");
            }
            catch
            {
                // Fallback to LocalApplicationData
            }
#endif
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoPiper");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "tools.json");
        }
    }

    private sealed record ToolsPrefs(DateTimeOffset? LastUpdateCheckUtc);

    public static bool IsUpdateCheckDue()
    {
        var last = Load()?.LastUpdateCheckUtc;
        return last is null || DateTimeOffset.UtcNow - last.Value >= TimeSpan.FromHours(CheckIntervalHours);
    }

    public static void MarkChecked()
    {
        try
        {
            System.IO.File.WriteAllText(FilePath, System.Text.Json.JsonSerializer.Serialize(new ToolsPrefs(DateTimeOffset.UtcNow)));
        }
        catch
        {
            // Best effort: a lost timestamp only means an extra check.
        }
    }

    private static ToolsPrefs? Load()
    {
        try
        {
            if (!System.IO.File.Exists(FilePath))
            {
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<ToolsPrefs>(System.IO.File.ReadAllText(FilePath));
        }
        catch
        {
            return null;
        }
    }
}
