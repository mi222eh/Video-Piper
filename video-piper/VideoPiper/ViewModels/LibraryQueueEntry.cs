using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoPiper.Models;

namespace VideoPiper.ViewModels;

/// <summary>
/// One row in the visible download queue: a job that is queued, downloading, or has finished.
/// <see cref="Logs"/> holds the live yt-dlp output for this item (capped) so the user can inspect
/// what happened while it ran. Entries live only for the session (not persisted to the index).
/// </summary>
public sealed class LibraryQueueEntry : INotifyPropertyChanged
{
    /// <summary>Maximum log lines kept per entry (memory cap for long playlists).</summary>
    public const int MaxLogLines = 400;

    private readonly object _logGate = new();
    private bool _logDirty;
    private ItemStatus _status = ItemStatus.Queued;
    private double _percent;
    private MediaKind _kind = MediaKind.Audio;

    /// <summary>Stable id: the yt-dlp video id of the job.</summary>
    public string Id { get; }

    /// <summary>Display title of the item.</summary>
    public string Title { get; }

    public MediaKind Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                OnPropertyChanged(nameof(KindLabel));
            }
        }
    }

    /// <summary>Channel/uploader, when known.</summary>
    public string? Uploader { get; }

    /// <summary>Playlist name, when the item is part of one.</summary>
    public string? Playlist { get; }

    /// <summary>1-based position in the queue at the time the entry was added (informational).</summary>
    public int QueuePosition { get; }

    public ItemStatus Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusGlyph));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsIndeterminate));
            }
        }
    }

    /// <summary>Download progress 0–100 while downloading.</summary>
    public double Percent
    {
        get => _percent;
        set
        {
            if (Set(ref _percent, value))
            {
                OnPropertyChanged(nameof(PercentFormatted));
                // The row's "Laddar ner N%" line is StatusText — without this it sits
                // at 0% for the whole download while the main window shows live percent.
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsIndeterminate));
            }
        }
    }

    /// <summary>Live yt-dlp output for this item (last <see cref="MaxLogLines"/> lines).</summary>
    public ObservableCollection<string> Logs { get; } = new();

    public bool IsDownloading => Status == ItemStatus.Downloading;

    /// <summary>True before the first percent tick lands — the row's bar then shows a
    /// marquee instead of a value (mirrors LibraryItem.IsIndeterminate).</summary>
    public bool IsIndeterminate => Status == ItemStatus.Downloading && Percent <= 0;

    public bool IsComplete => Status == ItemStatus.Complete;
    public bool IsFailed => Status == ItemStatus.Failed;
    public bool IsQueued => Status == ItemStatus.Queued;

    /// <summary>Segoe MDL2 glyph reflecting the current status, used by queue rows.</summary>
    public string StatusGlyph => Status switch
    {
        ItemStatus.Downloading => "\uE895", // sync/spinner
        ItemStatus.Complete => "\uE73E",    // check mark
        ItemStatus.Failed => "\uE783",      // warning
        _ => "\uE823",                       // clock (queued)
    };

    public string StatusText => Status switch
    {
        ItemStatus.Queued => "I kön",
        ItemStatus.Downloading => $"Laddar ner {Percent:0}%",
        ItemStatus.Complete => "Klart",
        ItemStatus.Failed => "Misslyckades",
        _ => string.Empty,
    };

    public string PercentFormatted => $"{Percent:0}%";

    /// <summary>True when the log buffer has at least one line.</summary>
    public bool HasLogs => Logs.Count > 0;

    /// <summary>All log lines joined into one string, for binding to a single read-only text surface.</summary>
    public string JoinedLogs
    {
        get
        {
            lock (_logGate)
            {
                return string.Join(Environment.NewLine, Logs);
            }
        }
    }

    public string KindLabel => Kind == MediaKind.Audio ? "MP3" : "MP4";

    public LibraryQueueEntry(MediaEntry entry, string? playlistTitle, int queuePosition, MediaKind kind)
    {
        Id = entry.Id;
        Title = entry.Title;
        Uploader = entry.Uploader;
        Playlist = playlistTitle;
        QueuePosition = queuePosition;
        Kind = kind;
    }

    /// <summary>
    /// Appends a log line (thread-safe — called from the download worker).
    /// The buffer is capped at <see cref="MaxLogLines"/> to keep memory bounded for long playlists.
    /// No change notifications are raised here: the worker appends several lines per second
    /// and each notification used to re-render the log panel (a full string join plus a
    /// layout pass per line, which flickered while downloading). The view model calls
    /// <see cref="FlushLog"/> on the UI thread at most once per dispatcher frame instead.
    /// </summary>
    public void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var stamp = $"[{DateTime.Now:HH:mm:ss}] {line}";
        lock (_logGate)
        {
            if (Logs.Count >= MaxLogLines)
            {
                Logs.RemoveAt(0);
            }
            Logs.Add(stamp);
            _logDirty = true;
        }
    }

    /// <summary>
    /// Raises the pending log change notifications (once, on the UI thread). Called by the
    /// view model from a coalesced <c>OnUi</c> — see <see cref="AppendLog"/>.
    /// </summary>
    public void FlushLog()
    {
        bool dirty;
        lock (_logGate)
        {
            dirty = _logDirty;
            _logDirty = false;
        }
        if (dirty)
        {
            OnPropertyChanged(nameof(HasLogs));
            OnPropertyChanged(nameof(JoinedLogs));
        }
    }

    /// <summary>Clears all log lines (e.g. when a failed item is retried) and notifies the UI.</summary>
    public void ClearLogs()
    {
        bool hadLogs;
        lock (_logGate)
        {
            hadLogs = Logs.Count > 0;
            Logs.Clear();
            _logDirty = false;
        }
        if (hadLogs)
        {
            OnPropertyChanged(nameof(HasLogs));
            OnPropertyChanged(nameof(JoinedLogs));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
