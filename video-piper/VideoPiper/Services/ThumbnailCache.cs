using System.Net.Http;

namespace VideoPiper.Services;

/// <summary>
/// Downloads and caches YouTube thumbnails in the app data folder so library rows keep
/// showing artwork with no connection. Best effort: every failure returns null and the
/// row falls back to the remote URL (or a blank surface).
/// </summary>
public static class ThumbnailCache
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    /// <summary>The standard YouTube thumbnail for a video id (never 404s for real videos).</summary>
    public static string UrlFor(string? videoId) =>
        string.IsNullOrWhiteSpace(videoId) ? string.Empty : $"https://i.ytimg.com/vi/{videoId}/mqdefault.jpg";

    public static string DirectoryPath => Path.Combine(PreferencesService.AppDataDirectory, "thumbs");

    public static string PathFor(string videoId) =>
        Path.Combine(DirectoryPath, $"{Sanitize(videoId)}.jpg");

    /// <summary>The cached file path when it exists, else null. Safe to call from any thread.</summary>
    public static string? TryGet(string? videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        var path = PathFor(videoId);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Ensures a thumbnail exists on disk, downloading it if needed. Returns the cached path
    /// or null when the download failed.
    /// </summary>
    public static async Task<string?> EnsureAsync(string? videoId, string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        var existing = TryGet(videoId);
        if (existing is not null)
        {
            return existing;
        }

        var target = PathFor(videoId);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var bytes = await Http.GetByteArrayAsync(
                string.IsNullOrWhiteSpace(url) ? UrlFor(videoId) : url,
                cancellationToken);
            if (bytes.Length == 0)
            {
                return null;
            }

            await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
            File.Move(temp, target, overwrite: true);
            return target;
        }
        catch
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch
            {
                // Best effort.
            }

            return null;
        }
    }

    private static string Sanitize(string videoId)
    {
        var cleaned = new string(videoId.Where(char.IsLetterOrDigit).ToArray());
        return cleaned.Length == 0 ? "thumb" : cleaned;
    }
}
