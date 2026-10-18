using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoPiper.Services;

/// <summary>
/// Contract for managing the app's own yt-dlp/ffmpeg binaries (check for updates,
/// install). View models depend on this interface so it can be faked in tests.
/// </summary>
public interface IToolUpdateService
{
    /// <summary>Download progress: (message, percent-or-null for indeterminate).</summary>
    event Action<string, double?>? Progress;

    /// <summary>
    /// Checks GitHub for the latest version of one tool and, when <paramref name="apply"/> is
    /// set and the local copy is older (or missing), downloads and installs the update.
    /// Never throws; failures are reported via <see cref="ToolUpdateResult.Error"/>.
    /// </summary>
    Task<ToolUpdateResult> CheckToolAsync(string tool, bool apply);
}
/// <summary>
/// Manages the app's own copies of yt-dlp and ffmpeg (installed into the app's
/// local Tools directory). Checks GitHub for the latest release, compares it with
/// the locally installed version, and downloads the update when one is available.
/// System binaries are never used. Registered as a singleton in DI.
/// </summary>
public sealed class ToolUpdateService : IToolUpdateService
{
    public const string YtDlpRepo = "yt-dlp/yt-dlp";
    public const string FfmpegRepo = "GyanD/codexffmpeg";

    /// <summary>GitHub API base (override in tests to point at a local server).</summary>
    public static string ApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>Shared HTTP client (replace in tests).</summary>
    public static HttpClient Http { get; set; } = CreateDefaultClient();

    /// <summary>Download progress: (message, percent-or-null for indeterminate).</summary>
    public event Action<string, double?>? Progress;

    private readonly SemaphoreSlim YtDlpGate = new(1, 1);
    private readonly SemaphoreSlim FfmpegGate = new(1, 1);

    private static HttpClient CreateDefaultClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip,
    })
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    /// <summary>The yt-dlp release asset name for this platform/architecture.</summary>
    public static string YtDlpAssetName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (RuntimeInformation.OSArchitecture is Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe")
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "yt-dlp_macos"
                : "yt-dlp_linux";

    /// <summary>
    /// Checks GitHub for the latest version of one tool and, when <paramref name="apply"/> is
    /// set and the local copy is older (or missing), downloads and installs the update.
    /// Never throws; failures are reported via <see cref="ToolUpdateResult.Error"/>.
    /// </summary>
    public async Task<ToolUpdateResult> CheckToolAsync(string tool, bool apply)
    {
        var gate = tool == "yt-dlp" ? YtDlpGate : FfmpegGate;
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(SystemService.ToolsDirectory);
            var fileName = tool == "yt-dlp" ? "yt-dlp.exe" : "ffmpeg.exe";
            var current = await SystemService.GetLocalVersionAsync(fileName, tool == "yt-dlp" ? "--version" : "-version");

            string latest, downloadUrl;
            if (tool == "yt-dlp")
            {
                (latest, downloadUrl) = await GetLatestReleaseAsync(YtDlpRepo, _ => YtDlpAssetName);
            }
            else
            {
                (latest, downloadUrl) = await GetLatestReleaseAsync(FfmpegRepo, tag => $"ffmpeg-{tag}-full_build.zip");
            }

            if (!IsNewerVersion(current, latest))
            {
                return new ToolUpdateResult(tool, FormatVersion(current), latest, false, null);
            }
            if (!apply)
            {
                return new ToolUpdateResult(tool, FormatVersion(current), latest, false, null);
            }

            if (tool == "yt-dlp")
            {
                await DownloadFileAsync(downloadUrl, Path.Combine(SystemService.ToolsDirectory, "yt-dlp.exe"), "yt-dlp");
            }
            else
            {
                await DownloadAndExtractFfmpegAsync(downloadUrl);
            }

            Log.For(typeof(ToolUpdateService)).LogInformation("Uppdaterade {Tool}: {Current} -> {Latest}", tool, FormatVersion(current), latest);
            return new ToolUpdateResult(tool, FormatVersion(current), latest, true, null);
        }
        catch (Exception ex)
        {
            Log.For(typeof(ToolUpdateService)).LogError(ex, "Kunde inte kontrollera uppdatering för {Tool}", tool);
            return new ToolUpdateResult(tool, null, null, false, UiErrors.FromException(ex, "okänt fel"));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(string Tag, string Url)> GetLatestReleaseAsync(string repo, Func<string, string> assetForTag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/repos/{repo}/releases/latest");
        request.Headers.UserAgent.ParseAdd("VideoPiper");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Kunde inte hämta versionsinformation (HTTP {(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new InvalidOperationException("Oväntat svar från GitHub.");
        }

        var wanted = assetForTag(tag);
        string? url;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            url = FindAssetUrl(assets, name => name == wanted)
                ?? FindAssetUrl(assets, name => name.Contains(tag, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            throw new InvalidOperationException("Oväntat svar från GitHub.");
        }

        if (url is null)
        {
            throw new InvalidOperationException("Hittade ingen nedladdningslänk i releases.");
        }

        return (tag, url);
    }

    private static string? FindAssetUrl(JsonElement assets, Func<string, bool> predicate)
    {
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (predicate(name) && asset.TryGetProperty("browser_download_url", out var dl))
            {
                return dl.GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is newer than <paramref name="current"/>.
    /// A missing or unparseable current version counts as "newer available";
    /// an unparseable candidate never triggers an update.
    /// </summary>
    public static bool IsNewerVersion(string? current, string candidate)
    {
        if (!TryParseVersion(candidate, out var newer))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(current) || !TryParseVersion(current, out var installed))
        {
            return true;
        }

        var len = Math.Max(installed.Length, newer.Length);
        for (var i = 0; i < len; i++)
        {
            var a = i < installed.Length ? installed[i] : 0;
            var b = i < newer.Length ? newer[i] : 0;
            if (a != b)
            {
                return b > a;
            }
        }

        return false;
    }

    /// <summary>Extracts the first dotted number sequence from a version string (e.g. "ffmpeg version 7.1-full_build" → "7.1").</summary>
    internal static bool TryParseVersion(string? version, out int[] parts)
    {
        parts = Array.Empty<int>();
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var match = Regex.Match(version, @"(\d+(?:\.\d+)*)");
        if (!match.Success)
        {
            return false;
        }

        parts = match.Groups[1].Value
            .Split('.')
            .Select(p => int.TryParse(p, out var v) ? v : -1)
            .ToArray();
        return !parts.Contains(-1);
    }

    /// <summary>Short, displayable version from raw tool output (first dotted number, or null).</summary>
    public static string? FormatVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = Regex.Match(raw, @"(\d+(?:\.\d+)*)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task DownloadFileAsync(string url, string destination, string label)
    {
        var tempPath = destination + ".download";
        try
        {
            Progress?.Invoke($"Hämtar {label}...", null);
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Nedladdning misslyckades (HTTP {(int)response.StatusCode}).");
            }

            var total = response.Content.Headers.ContentLength;
            await using var content = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(tempPath);

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

            // Close both streams BEFORE the move: a still-open source handle makes
            // File.Move fail with a sharing violation on Windows.
            await content.DisposeAsync();
            await file.DisposeAsync();

            File.Move(tempPath, destination, overwrite: true);
            Progress?.Invoke($"{label} installerad.", 100);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }

    private async Task DownloadAndExtractFfmpegAsync(string url)
    {
        var zipPath = Path.Combine(SystemService.ToolsDirectory, "ffmpeg.zip");
        await DownloadFileAsync(url, zipPath, "ffmpeg");

        Progress?.Invoke("Extraherar ffmpeg...", null);
        try
        {
            var foundFfmpeg = false;
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    await ExtractEntry(archive, entry, "ffmpeg.exe");
                    foundFfmpeg = true;
                }
                else if (entry.FullName.EndsWith("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                {
                    await ExtractEntry(archive, entry, "ffprobe.exe");
                }
            }

            if (!foundFfmpeg)
            {
                throw new InvalidOperationException("ffmpeg.exe hittades inte i arkivet.");
            }
        }
        finally
        {
            try { File.Delete(zipPath); } catch { /* best effort */ }
        }
    }

    private async Task ExtractEntry(ZipArchive archive, ZipArchiveEntry entry, string fileName)
    {
        var target = Path.Combine(SystemService.ToolsDirectory, fileName);
        await using var dest = File.Create(target);
        await using var src = entry.Open();
        await src.CopyToAsync(dest);
    }
}

/// <summary>Outcome of a single tool update check.</summary>
public sealed record ToolUpdateResult(string Tool, string? InstalledVersion, string? LatestVersion, bool Updated, string? Error);
