using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoPiper.Models;
using VideoPiper.Services;

namespace VideoPiper.ViewModels;

/// <summary>
/// Queue side of the library view model: metadata fetch, the sequential download worker,
/// and the visible queue panel (<see cref="QueueEntries"/>) with per-item logs.
/// </summary>
public sealed partial class LibraryViewModel
{
    private bool _isFetching;
    private bool _isDownloading;
    private int _queueCount;
    private int _failedCount;
    private string? _jobTitle;
    private double _jobPercent;
    private string? _jobPosition;
    private bool _isRetryingFailed;
    private LibraryItem? _playingItem;
    private bool _playerPopOutOpen;
    private LibraryQueueEntry? _selectedQueueEntry;
#if WINDOWS
    private PlayerPopOutWindow? _popOutWindow;
#endif

    public ObservableCollection<LibraryItem> Items { get; } = new();
    public ObservableCollection<LibraryItem> FilteredItems { get; } = new();
    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    // Download queue state
    private readonly ConcurrentQueue<QueueJob> _downloadQueue = new();
    private readonly object _queueGate = new();
    private readonly Dictionary<string, LibraryQueueEntry> _queueEntryById = new(StringComparer.Ordinal);
    private Task? _workerTask;
    private CancellationTokenSource? _queueCts;
    private bool _queueRunning;

    private readonly RelayCommand _browseCommand;
    private readonly RelayCommand _downloadCommand;
    private readonly RelayCommand _stopDownloadCommand;
    private readonly RelayCommand _playSelectedCommand;
    private readonly RelayCommand _deleteSelectedCommand;
    private readonly RelayCommand _deselectCommand;
    private readonly RelayCommand _resyncCommand;
    private readonly RelayCommand _downloadSelectedCommand;
    private readonly ParameterizedRelayCommand _playItemCommand;
    private readonly RelayCommand _pasteCommand;
    private readonly RelayCommand _closePlayerCommand;
    private readonly RelayCommand _popOutPlayerCommand;
    private readonly RelayCommand _openFolderSelectedCommand;
    private readonly RelayCommand _openRootFolderCommand;
    private readonly RelayCommand _clearSearchCommand;
    private readonly RelayCommand _clearErrorCommand;
    private readonly ParameterizedRelayCommand _selectSectionCommand;
    private readonly RelayCommand _executeSearchCommand;
    private readonly RelayCommand _retryFailedCommand;
    private readonly RelayCommand _clearQueueCommand;
    private readonly RelayCommand _clearFinishedQueueEntriesCommand;
    private readonly ParameterizedRelayCommand _selectQueueEntryCommand;
    private readonly ParameterizedRelayCommand _retryQueueEntryCommand;

    private readonly ILibraryDownloadService _libraryDownloadService;
    private readonly ISearchService _searchService;

    public ICommand BrowseCommand => _browseCommand;
    public ICommand DownloadCommand => _downloadCommand;
    public ICommand StopDownloadCommand => _stopDownloadCommand;
    public ICommand PlaySelectedCommand => _playSelectedCommand;
    public ICommand DeleteSelectedCommand => _deleteSelectedCommand;
    public ICommand DeselectCommand => _deselectCommand;
    public ICommand ResyncCommand => _resyncCommand;
    public ICommand PlayItemCommand => _playItemCommand;
    public ICommand PasteCommand => _pasteCommand;
    public ICommand ClosePlayerCommand => _closePlayerCommand;
    public ICommand PopOutPlayerCommand => _popOutPlayerCommand;
    public ICommand OpenFolderSelectedCommand => _openFolderSelectedCommand;
    public ICommand OpenRootFolderCommand => _openRootFolderCommand;
    public ICommand DownloadSelectedCommand => _downloadSelectedCommand;
    public ICommand ClearSearchCommand => _clearSearchCommand;
    public ICommand ClearErrorCommand => _clearErrorCommand;
    public ICommand SelectSectionCommand => _selectSectionCommand;
    public ICommand ExecuteSearchCommand => _executeSearchCommand;
    public ICommand RetryFailedCommand => _retryFailedCommand;
    public ICommand ClearQueueCommand => _clearQueueCommand;
    public ICommand ClearFinishedQueueEntriesCommand => _clearFinishedQueueEntriesCommand;
    public ICommand SelectQueueEntryCommand => _selectQueueEntryCommand;
    public ICommand RetryQueueEntryCommand => _retryQueueEntryCommand;

    public LibraryViewModel(ILibraryDownloadService libraryDownloadService, ISearchService searchService)
    {
        _libraryDownloadService = libraryDownloadService;
        _searchService = searchService;
        _browseCommand = new RelayCommand(BrowseAsync, () => !IsDownloading);
        _downloadCommand = new RelayCommand(DownloadAsync, () => CanDownload);
        _stopDownloadCommand = new RelayCommand(StopQueueAsync, () => IsBusy);
        _playSelectedCommand = new RelayCommand(PlaySelectedAsync, () => SelectedItem is { Status: ItemStatus.Complete } && !string.IsNullOrEmpty(SelectedItem.FilePath));
        _deleteSelectedCommand = new RelayCommand(DeleteSelectedAsync, () => SelectedItem is not null);
        _downloadSelectedCommand = new RelayCommand(DownloadSelectedAsync, () => SelectedFileMissing);
        _deselectCommand = new RelayCommand(() => { SelectedItem = null; return Task.CompletedTask; });
        _openFolderSelectedCommand = new RelayCommand(OpenFolderSelectedAsync, () => SelectedItem is not null);
        _openRootFolderCommand = new RelayCommand(OpenRootFolder);
        _resyncCommand = new RelayCommand(ResyncAsync, () => !IsDownloading);

        _playItemCommand = new ParameterizedRelayCommand(p => PlayItemAsync(p as LibraryItem));

        _pasteCommand = new RelayCommand(PasteAsync, () => !IsBusy);
        _closePlayerCommand = new RelayCommand(ClosePlayer);
        _popOutPlayerCommand = new RelayCommand(PopOutPlayer, () => PlayingItem is not null && !PlayerIsPopOut);
        _clearSearchCommand = new RelayCommand(ClearSearch);
        _clearErrorCommand = new RelayCommand(() => { Error = null; return Task.CompletedTask; });

        _selectSectionCommand = new ParameterizedRelayCommand(p =>
        {
            if (p is string s)
            {
                ActiveSection = s.ToLowerInvariant() switch
                {
                    "audio" => LibrarySidebarSection.AudioOnly,
                    "video" => LibrarySidebarSection.VideoOnly,
                    "failed" => LibrarySidebarSection.FailedOnly,
                    "search" => LibrarySidebarSection.YouTubeSearch,
                    _ => LibrarySidebarSection.AllMedia,
                };
            }
            else if (p is LibrarySidebarSection sec)
            {
                ActiveSection = sec;
            }
            return Task.CompletedTask;
        });

        _executeSearchCommand = new RelayCommand(RunSearchNowAsync, () => !IsSearching);
        _searchResultAudioCommand = new RelayCommand(
            () => DownloadSearchResultAsAsync(MediaKind.Audio), () => CanDownloadSearchResult);
        _searchResultVideoCommand = new RelayCommand(
            () => DownloadSearchResultAsAsync(MediaKind.Video), () => CanDownloadSearchResult);

        _retryFailedCommand = new RelayCommand(RetryFailedAsync, () => !IsBusy && FailedCount > 0);
        _clearQueueCommand = new RelayCommand(ClearQueueAsync, () => QueueCount > 0 && !IsDownloading);
        _clearFinishedQueueEntriesCommand = new RelayCommand(ClearFinishedQueueEntriesAsync, () => QueueEntries.Count > 0);
        _selectQueueEntryCommand = new ParameterizedRelayCommand(p =>
        {
            if (p is LibraryQueueEntry entry)
            {
                SelectedQueueEntry = entry;
            }
            return Task.CompletedTask;
        });

        _retryQueueEntryCommand = new ParameterizedRelayCommand(p =>
        {
            if (p is LibraryQueueEntry entry)
            {
                RetryQueueEntryAsync(entry);
            }
            return Task.CompletedTask;
        });
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (Set(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsQueueActive));
                _downloadCommand.RefreshCanExecute();
                _stopDownloadCommand.RefreshCanExecute();
                _clearQueueCommand.RefreshCanExecute();
                _retryFailedCommand.RefreshCanExecute();
                RefreshSearchResultCommands();
            }
        }
    }

    public bool IsFetching
    {
        get => _isFetching;
        private set
        {
            if (Set(ref _isFetching, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsQueueActive));
                _downloadCommand.RefreshCanExecute();
                _retryFailedCommand.RefreshCanExecute();
                RefreshSearchResultCommands();
            }
        }
    }

    /// <summary>True while resolving metadata or downloading — the primary busy state.</summary>
    public bool IsBusy => IsFetching || IsDownloading;

    /// <summary>True while resolving metadata, downloading, or when jobs are still queued — drives the queue card.</summary>
    public bool IsQueueActive => IsBusy || QueueCount > 0;

    /// <summary>Number of entries waiting in the download queue (excluding the active one).</summary>
    public int QueueCount
    {
        get { lock (_queueGate) { return _queueCount; } }
        private set
        {
            lock (_queueGate)
            {
                if (Set(ref _queueCount, value))
                {
                    OnPropertyChanged(nameof(IsQueueActive));
                    OnPropertyChanged(nameof(QueueCountText));
                    _clearQueueCommand.RefreshCanExecute();
                }
            }
        }
    }

    /// <summary>Number of library items currently in the failed state.</summary>
    public int FailedCount
    {
        get
        {
            lock (_queueGate)
            {
                return _failedCount;
            }
        }
        private set
        {
            lock (_queueGate)
            {
                if (Set(ref _failedCount, value))
                {
                    OnPropertyChanged(nameof(HasFailedItems));
                    _retryFailedCommand.RefreshCanExecute();
                }
            }
        }
    }

    public bool HasFailedItems => FailedCount > 0;

    /// <summary>Label for the queue card: "N i kön" or empty.</summary>
    public string QueueCountText => QueueCount > 0 ? $"{QueueCount} i kön" : string.Empty;

    public string? JobTitle
    {
        get => _jobTitle;
        private set => Set(ref _jobTitle, value);
    }

    public double JobPercent
    {
        get => _jobPercent;
        private set => Set(ref _jobPercent, value);
    }

    public string? JobPosition
    {
        get => _jobPosition;
        private set => Set(ref _jobPosition, value);
    }

    public string? Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    /// <summary>The item currently loaded in the player (local playback).</summary>
    public LibraryItem? PlayingItem
    {
        get => _playingItem;
        private set
        {
            if (!ReferenceEquals(_playingItem, value))
            {
                Set(ref _playingItem, value);
                OnPropertyChanged(nameof(IsPlayerVisible));
                OnPropertyChanged(nameof(IsDockedPlayerVisible));
                _popOutPlayerCommand.RefreshCanExecute();
            }
        }
    }

    public bool IsPlayerVisible => _playingItem is not null || _selectedFileMissing;

    /// <summary>True while the pop-out player window is open (the docked player is hidden).</summary>
    public bool PlayerIsPopOut => _playerPopOutOpen;

    /// <summary>The docked player surface shows only when an item is loaded and the
    /// pop-out window is closed (a missing file or pop-out hides it).</summary>
    public bool IsDockedPlayerVisible => _playingItem is not null && !_playerPopOutOpen;

    public bool CanDownload =>
        !IsBusy &&
        IsRootSet &&
        ((!string.IsNullOrWhiteSpace(Url) && Url.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase)) ||
         IsYouTubeUrl(SearchQuery));

    // ------------------------------------------------------------------
    // Queue panel: visible list of jobs (queued / downloading / finished)
    // ------------------------------------------------------------------

    /// <summary>
    /// Jobs in the download queue, newest first. Includes the active download and finished jobs
    /// (kept so their logs stay inspectable). Not persisted to the index.
    /// </summary>
    public ObservableCollection<LibraryQueueEntry> QueueEntries { get; } = new();

    /// <summary>The queue entry whose log panel is open (clicking a row sets this).</summary>
    public LibraryQueueEntry? SelectedQueueEntry
    {
        get => _selectedQueueEntry;
        set
        {
            if (!ReferenceEquals(_selectedQueueEntry, value))
            {
                Set(ref _selectedQueueEntry, value);
                OnPropertyChanged(nameof(IsQueuePanelExpanded));
            }
        }
    }

    /// <summary>True when a log panel should be visible (a queue row is selected).</summary>
    public bool IsQueuePanelExpanded => _selectedQueueEntry is not null;

    /// <summary>True while the queue panel has anything to show (jobs queued, active, or finished).</summary>
    public bool HasQueueEntries => QueueEntries.Count > 0;

    public async Task InitializeAsync()
    {
        Kind = PreferencesService.GetFormat();
        var root = PreferencesService.GetLibraryRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            var myMusic = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            if (!string.IsNullOrWhiteSpace(myMusic) && Directory.Exists(myMusic))
            {
                root = Path.Combine(myMusic, "VideoPiper");
            }
            else
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                root = Path.Combine(userProfile, "Music", "VideoPiper");
            }
        }

        try
        {
            Directory.CreateDirectory(root);
            PreferencesService.SetLibraryRoot(root);
        }
        catch (Exception)
        {
            // Best effort
        }

        await OpenStoreAsync(root);
    }

    private async Task BrowseAsync()
    {
        var folder = await FolderPickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        PreferencesService.SetLibraryRoot(folder);
        await OpenStoreAsync(folder);
    }

    private async Task OpenStoreAsync(string root)
    {
        _store = new LibraryStore(root);
        await _store.LoadAsync();
        RootPath = _store.RootPath;
        RefreshItems();
    }

    private async Task DownloadAsync()
    {
        var targetUrl = !string.IsNullOrWhiteSpace(Url) ? Url.Trim() : SearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return;
        }

        Error = null;
        if (_queueCts is null || _queueCts.IsCancellationRequested)
        {
            _queueCts?.Dispose();
            _queueCts = new CancellationTokenSource();
        }
        IsFetching = true;
        JobTitle = "Hämtar metadata...";
        JobPercent = 0;
        JobPosition = null;

        try
        {
            var (entries, playlistTitle) = await _libraryDownloadService.FetchEntriesAsync(targetUrl, _queueCts.Token);
            if (entries.Count == 0)
            {
                Error = "Inga objekt hittades i länken.";
                return;
            }

            // Clear search box so user sees library list with newly queued entries
            ClearSearch();

            EnqueueEntries(entries, playlistTitle);
        }
        catch (OperationCanceledException)
        {
            Error = "Nedladdningen avbröts.";
        }
        catch (Exception ex)
        {
            Error = UiErrors.FromException(ex, "Något gick fel när länken hämtades.");
        }
        finally
        {
            IsFetching = false;
        }
    }

    /// <summary>Queues entries for the download worker, registers them in the visible queue, and starts the worker if needed.</summary>
    private void EnqueueEntries(IEnumerable<MediaEntry> entries, string? playlistTitle, MediaKind? kindOverride = null)
    {
        var list = entries.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var position = 0;
        var newEntries = new List<LibraryQueueEntry>();
        lock (_queueGate)
        {
            position = _downloadQueue.Count;
            foreach (var entry in list)
            {
                position++;
                _downloadQueue.Enqueue(new QueueJob(entry, playlistTitle, kindOverride ?? Kind));

                if (_queueEntryById.TryGetValue(entry.Id, out var existing))
                {
                    // Retry of an existing job: reset status and logs.
                    existing.Status = ItemStatus.Queued;
                    existing.Percent = 0;
                    existing.ClearLogs();
                    existing.AppendLog("Överför till kön igen.");
                    continue;
                }

                var queueEntry = new LibraryQueueEntry(entry, playlistTitle, position, kindOverride ?? Kind);
                _queueEntryById[entry.Id] = queueEntry;
                newEntries.Add(queueEntry);
            }
            QueueCount = _downloadQueue.Count;
        }

        OnUi(() =>
        {
            for (var i = newEntries.Count - 1; i >= 0; i--)
            {
                QueueEntries.Insert(0, newEntries[i]);
            }
            OnPropertyChanged(nameof(HasQueueEntries));
        });

        EnsureWorkerStarted();
        if (_isRetryingFailed)
        {
            _isRetryingFailed = false;
            Error = null;
        }
    }

    private readonly record struct QueueJob(MediaEntry Entry, string? PlaylistTitle, MediaKind Kind);


    private void EnsureWorkerStarted()
    {
        lock (_queueGate)
        {
            if (_queueRunning || _downloadQueue.IsEmpty)
            {
                return;
            }
            // Guarantee a live (non-cancelled) token for every worker, no matter which
            // enqueue path started it (search result, URL, retry). A CTS cancelled by
            // StopQueueAsync must be replaced — a cancelled token stays cancelled.
            if (_queueCts is null || _queueCts.IsCancellationRequested)
            {
                _queueCts?.Dispose();
                _queueCts = new CancellationTokenSource();
            }
            _queueRunning = true;
            _workerTask = Task.Run(QueueWorkerAsync);
        }
    }

    /// <summary>
    /// Downloads queued jobs one at a time until the queue is empty.
    /// Runs on a worker thread: every observable-state mutation MUST go through
    /// <see cref="OnUi"/> — WinRT raises COMException when PropertyChanged fires
    /// on a non-owning thread, and a faulted worker task dies silently.
    /// </summary>
    private async Task QueueWorkerAsync()
    {
        try
        {
            while (_downloadQueue.TryDequeue(out var job))
            {
                // Active-job state: observable properties -> UI thread.
                OnUi(() =>
                {
                    QueueCount--;
                    IsDownloading = true;
                    JobTitle = job.Entry.Title;
                    JobPercent = 0;
                    JobPosition = QueueCount > 0 ? $"{QueueCount} i kön" : null;
                });

                LibraryQueueEntry? queueEntry = null;
                lock (_queueGate)
                {
                    _queueEntryById.TryGetValue(job.Entry.Id, out queueEntry);
                }
                // A retry may have changed the format since the entry was first created.
                // Kind raises PropertyChanged synchronously (LibraryQueueEntry has no
                // PostToUi marshaling), so it MUST be set on the UI thread — a COMException
                // here would kill the worker task before the download even starts.
                OnUi(() =>
                {
                    if (queueEntry is not null)
                    {
                        queueEntry.Kind = job.Kind;
                        queueEntry.Status = ItemStatus.Downloading;
                    }
                });

                try
                {
                    // onItemChanged/onLine are already marshaled by the caller (OnUi).
                    var result = await _libraryDownloadService.DownloadEntryAsync(
                        _store,
                        job.Entry,
                        job.PlaylistTitle,
                        job.Kind,
                        item => OnUi(() =>
                        {
                            UpsertItem(item);
                            JobTitle = item.Title;
                            JobPercent = item.Percent ?? 0;
                            if (queueEntry is not null)
                            {
                                queueEntry.Percent = item.Percent ?? 0;
                            }
                        }),
                        _queueCts!.Token,
                        onLine: line => OnUi(() => queueEntry?.AppendLog(line)));

                    // DownloadEntryAsync marks the library item failed (but returns) on individual errors.
                    OnUi(() =>
                    {
                        if (queueEntry is not null)
                        {
                            queueEntry.Status = result.Status;
                            if (result.Status == ItemStatus.Failed)
                            {
                                queueEntry.AppendLog($"Misslyckades: {result.Error}");
                            }
                            else
                            {
                                queueEntry.AppendLog("Klart. Filen är sparad i biblioteket.");
                            }
                        }
                        // UpsertItem alone misses the final state when the item was
                        // registered as Queued and only progress was reported since —
                        // land the final LibraryItem so the info page (and collection)
                        // sees the result.
                        if (Items.FirstOrDefault(i => i.Id == result.Id) is not null)
                        {
                            UpsertItem(result);
                        }
                        SyncSelectedSearchResultInLibrary(result.Id, result.Status);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Mark the active job, then drain the rest — the user asked to stop.
                    OnUi(() =>
                    {
                        if (queueEntry is not null)
                        {
                            queueEntry.Status = ItemStatus.Failed;
                            queueEntry.AppendLog("Avbröts av användaren.");
                        }
                        while (_downloadQueue.TryDequeue(out var remaining))
                        {
                            QueueCount--;
                            lock (_queueGate)
                            {
                                if (_queueEntryById.TryGetValue(remaining.Entry.Id, out var remainingEntry))
                                {
                                    remainingEntry.Status = ItemStatus.Failed;
                                    remainingEntry.AppendLog("Avbröts av användaren.");
                                }
                            }
                        }
                        Error = "Nedladdningen avbröts.";
                    });
                    break;
                }
            }
        }
        finally
        {
            lock (_queueGate)
            {
                _queueRunning = false;
                _workerTask = null;
            }
            OnUi(() =>
            {
                IsDownloading = false;
                JobTitle = null;
                JobPercent = 0;
                JobPosition = null;

                // If new jobs were enqueued while we drained/cancelled, start a fresh worker.
                // EnsureWorkerStarted replaces the (now cancelled) CTS and checks IsEmpty.
                EnsureWorkerStarted();
            });
        }
    }

    private Task StopQueueAsync()
    {
        _queueCts?.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>Removes all not-yet-started jobs from the queue (the active download keeps running).</summary>
    private Task ClearQueueAsync()
    {
        var removed = new List<LibraryQueueEntry>();
        lock (_queueGate)
        {
            while (_downloadQueue.TryDequeue(out var job))
            {
                QueueCount--;
                if (_queueEntryById.Remove(job.Entry.Id, out var entry) && entry.Status == ItemStatus.Queued)
                {
                    entry.AppendLog("Tog bort ur kön.");
                    removed.Add(entry);
                }
            }
        }

        OnUi(() =>
        {
            foreach (var entry in removed)
            {
                QueueEntries.Remove(entry);
            }
            if (ReferenceEquals(_selectedQueueEntry, null) || removed.Contains(_selectedQueueEntry))
            {
                SelectedQueueEntry = null;
            }
            OnPropertyChanged(nameof(HasQueueEntries));
            _clearFinishedQueueEntriesCommand.RefreshCanExecute();
        });
        return Task.CompletedTask;
    }

    /// <summary>Re-queues every failed item (skipping ids already queued) so they download again.</summary>
    private Task RetryFailedAsync()
    {
        var failed = Items.Where(i => i.Status == ItemStatus.Failed).ToList();
        if (failed.Count == 0)
        {
            return Task.CompletedTask;
        }

        _isRetryingFailed = true;
        // Group by (playlist, kind) so each failed item re-downloads in *its own*
        // format (its persisted Kind) rather than the current global toggle.
        foreach (var group in failed
            .Where(i => i.Id is not null)
            .GroupBy(i => (i.Playlist, i.Kind)))
        {
            var entries = group
                .Select(i => new MediaEntry(
                    Id: i.Id,
                    Title: i.Title,
                    Uploader: i.Uploader,
                    DurationSeconds: i.DurationSeconds,
                    Url: $"https://www.youtube.com/watch?v={i.Id}"))
                .ToList();

            EnqueueEntries(entries, group.Key.Playlist, group.Key.Kind);
        }
        return Task.CompletedTask;
    }

    /// <summary>Re-queues a single failed queue entry (row click / log-panel retry) so it downloads again.</summary>
    private void RetryQueueEntryAsync(LibraryQueueEntry entry)
    {
        if (entry is null || !entry.IsFailed)
        {
            return;
        }

        var item = Items.FirstOrDefault(i => i.Id == entry.Id);
        if (item is null)
        {
            return;
        }

        _isRetryingFailed = true;
        EnqueueEntries(new[]
        {
            new MediaEntry(
                Id: entry.Id,
                Title: entry.Title,
                Uploader: item.Uploader,
                DurationSeconds: item.DurationSeconds,
                Url: $"https://www.youtube.com/watch?v={entry.Id}")
        }, entry.Playlist, item.Kind);
    }

    /// <summary>Removes finished (complete/failed) jobs from the queue panel; active and queued jobs stay.</summary>
    private Task ClearFinishedQueueEntriesAsync()
    {
        OnUi(() =>
        {
            var finished = QueueEntries
                .Where(e => e.Status == ItemStatus.Complete || e.Status == ItemStatus.Failed)
                .ToList();
            foreach (var entry in finished)
            {
                QueueEntries.Remove(entry);
            }
            if (!ReferenceEquals(_selectedQueueEntry, null) && !QueueEntries.Contains(_selectedQueueEntry))
            {
                SelectedQueueEntry = null;
            }
            OnPropertyChanged(nameof(HasQueueEntries));
            _clearFinishedQueueEntriesCommand.RefreshCanExecute();
        });
        return Task.CompletedTask;
    }
}
