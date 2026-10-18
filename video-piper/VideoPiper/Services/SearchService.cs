using System.Diagnostics;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>A single YouTube search result.</summary>
public sealed partial record SearchResult(string Id, string Title, string? Uploader, double? DurationSeconds)
{
    /// <summary>"Uploader · 3:45" style meta line for list rows.</summary>
    public string MetaText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Uploader))
            {
                parts.Add(Uploader);
            }

            if (DurationSeconds is > 0)
            {
                var d = TimeSpan.FromSeconds(DurationSeconds.Value);
                parts.Add($"{(int)d.TotalMinutes}:{d.Seconds.ToString("00")}");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>Full metadata line for the video info page, e.g. "Av jawed · 0:19".</summary>
    public string? DetailText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Uploader))
            {
                parts.Add("Av " + Uploader);
            }
            if (DurationSeconds is > 0)
            {
                var d = TimeSpan.FromSeconds(DurationSeconds.Value);
                parts.Add(d.TotalHours >= 1
                    ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}"
                    : $"{(int)d.TotalMinutes}:{d.Seconds:00}");
            }
            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }
    }

    /// <summary>Video description from the full metadata fetch; empty in flat search results.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Contract for YouTube search. View models depend on this interface so it can be
/// faked in tests.
/// </summary>
public interface ISearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>Full JSON metadata for one video (no download) — used by the video info page.</summary>
    Task<string> FetchDetailJsonAsync(string videoId, CancellationToken cancellationToken);
}

/// <summary>
/// Searches YouTube via yt-dlp's youtubesearch extractor. One flat JSON call per query —
/// no downloads, so results stream back quickly. Registered as a singleton in DI.
/// </summary>
public sealed class SearchService : ISearchService
{
    private const int MaxResults = 20;

    private readonly ISystemService _systemService;

    public SearchService(ISystemService systemService)
    {
        _systemService = systemService;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var ytDlpPath = await ResolveYtDlpAsync();
        var sanitized = query.Replace("\"", "").Trim();
        var searchUrl = $"ytsearch{MaxResults}:{sanitized}";
        var json = await RunYtDlpAsync(ytDlpPath, $"--flat-playlist -J --no-warnings \"{searchUrl}\"", cancellationToken);

        // Search results share the playlist entries shape.
        if (YtDlpJson.TryParsePlaylist(json) is not { Entries.Count: > 0 } parsed)
        {
            return Array.Empty<SearchResult>();
        }

        return parsed.Entries
            .Select(e => new SearchResult(e.Id, e.Title, e.Uploader, e.DurationSeconds))
            .ToList();
    }

    public async Task<string> FetchDetailJsonAsync(string videoId, CancellationToken cancellationToken)
    {
        var ytDlpPath = await ResolveYtDlpAsync();
        // Metadata only: no download, no writes, safe to run from the app's tools.
        return await RunYtDlpAsync(
            ytDlpPath,
            $"-J --no-warnings --no-playlist \"https://www.youtube.com/watch?v={videoId}\"",
            cancellationToken);
    }

    private async Task<string> ResolveYtDlpAsync()
    {
        var status = await _systemService.CheckToolsAsync();
        if (status.YtDlp.Available && !string.IsNullOrEmpty(status.YtDlp.Path))
        {
            return status.YtDlp.Path;
        }

        status = await _systemService.EnsureToolsAsync();
        if (status.YtDlp.Available && !string.IsNullOrEmpty(status.YtDlp.Path))
        {
            return status.YtDlp.Path;
        }

        throw new FileNotFoundException("yt-dlp hittades inte. Installera yt-dlp och försök igen.");
    }

    private static async Task<string> RunYtDlpAsync(string ytDlpPath, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(ytDlpPath, arguments)
        {
            // Run from a trusted directory: inheriting the app's CWD can put the child on
            // an untrusted reparse point and crash the Python process (WinError 448).
            // SafeWorkingDirectory proves the temp path is reachable (TEMP/TMP may be
            // empty, in which case GetTempPath returns the process CWD).
            WorkingDirectory = SafeWorkingDirectory.Resolve(null),
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

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(stderr) ? $"yt-dlp avslutades med kod {process.ExitCode}" : stderr.Trim());
        }

        return stdout;
    }
}
