using System.Diagnostics;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>A single YouTube search result.</summary>
public sealed record SearchResult(string Id, string Title, string? Uploader, double? DurationSeconds)
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
}

/// <summary>
/// Searches YouTube via yt-dlp's youtubesearch extractor. One flat JSON call per query —
/// no downloads, so results stream back quickly.
/// </summary>
public static class SearchService
{
    private const int MaxResults = 20;

    public static async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var ytDlpPath = await ResolveYtDlpAsync();
        var searchUrl = $"ytsearch{MaxResults}:{Uri.EscapeDataString(query)}";
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

    private static async Task<string> ResolveYtDlpAsync()
    {
        var status = await SystemService.CheckToolsAsync();
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
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Kunde inte starta yt-dlp.");
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
