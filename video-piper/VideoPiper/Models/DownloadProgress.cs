namespace VideoPiper.Models;

/// <summary>
/// Progress state for a yt-dlp download, mirroring the SSE payloads of the previous backend.
/// </summary>
public sealed class DownloadProgress
{
    public string Status { get; init; } = "starting";

    public double? Percent { get; init; }

    public string? Message { get; init; }

    public string? Speed { get; init; }

    public string? Eta { get; init; }
}
