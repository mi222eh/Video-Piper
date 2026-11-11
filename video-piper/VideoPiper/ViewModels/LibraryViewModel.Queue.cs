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
    private string? _playerTitle;
    private string? _playerSubtitle;
    private string? _playerVideoId;
    private MediaKind _playerKind = MediaKind.Audio;
    private string? _playerSourceUri;
    private bool _playerIsStream;
    private bool _playerStreamLoading;
    private string? _playerStreamStatus;
    private string? _playerStreamError;
    private CancellationTokenSource? _streamCts;
    private LibraryQueueEntry? _selectedQueueEntry;
    private readonly IStreamService _streamService;
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

    /// <summary>1 while a log-flush OnUi is already queued (coalesces bursts of log lines).</summary>
    private int _logFlushPending;

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
    private readonly RelayCommand _toggleQueuePanelCommand;
    private readonly ParameterizedRelayCommand _switchPlayerKindCommand;
    private readonly RelayCommand _streamSelectedCommand;
    private readonly RelayCommand _retryStreamCommand;

    private bool _queuePanelOpen;

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
    public ICommand ToggleQueuePanelCommand => _toggleQueuePanelCommand;
    public ICommand SwitchPlayerKindCommand => _switchPlayerKindCommand;
    public ICommand StreamSelectedCommand => _streamSelectedCommand;
    public ICommand RetryStreamCommand => _retryStreamCommand;

    /// <summary>Raised when the view should start playback (the player's transport Play).</summary>
    public event Action? PlaybackRequested;

    public LibraryViewModel(ILibraryDownloadService libraryDownloadService, ISearchService searchService, IStreamService streamService)
    {
        _libraryDownloadService = libraryDownloadService;
        _searchService = searchService;
        _streamService = streamService;
        _browseCommand = new RelayCommand(BrowseAsync, () => !IsDownloading);
        _downloadCommand = new RelayCommand(DownloadAsync, () => CanDownload);
        _stopDownloadCommand = new RelayCommand(StopQueueAsync, () => IsBusy);
        _playSelectedCommand = new RelayCommand(PlaySelectedAsync, () => SelectedItem is { Status: ItemStatus.Complete });
        _deleteSelectedCommand = new RelayCommand(DeleteSelectedAsync, () => SelectedItem is not null);
        _downloadSelectedCommand = new RelayCommand(DownloadSelectedAsync, () => SelectedFileMissing);
        _deselectCommand = new RelayCommand(() => { SelectedItem = null; return Task.CompletedTask; });
        _openFolderSelectedCommand = new RelayCommand(OpenFolderSelectedAsync, () => SelectedItem is not null);
        _openRootFolderCommand = new RelayCommand(OpenRootFolder);
        _resyncCommand = new RelayCommand(ResyncAsync, () => IsRootSet && !IsDownloading);

        _playItemCommand = new ParameterizedRelayCommand(p => PlayItemAsync(p as LibraryItem));

        _pasteCommand = new RelayCommand(PasteAsync, () => !IsBusy);
        _closePlayerCommand = new RelayCommand(ClosePlayer);
        _popOutPlayerCommand = new RelayCommand(PopOutPlayer, () => PlayerSourceUri is not null && !PlayerIsPopOut);
        _clearSearchCommand = new RelayCommand(ClearSearch);
        _clearErrorCommand = new RelayCommand(() => { Error = null; return Task.CompletedTask; });
        _streamSelectedCommand = new RelayCommand(StreamSelectedAsync, () => SelectedItem is not null && CanStream);
        _retryStreamCommand = new RelayCommand(RetryStreamAsync, () => PlayerVideoId is not null && CanStream);
        _switchPlayerKindCommand = new ParameterizedRelayCommand(p =>
        {
            if (p is MediaKind kind)
            {
                _ = SwitchPlayerKindAsync(kind);
            }
            return Task.CompletedTask;
        });

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
            () => DownloadSearchResultAsAsync(MediaKind.Audio), () => CanDownloadSearchAudio);
        _searchResultVideoCommand = new RelayCommand(
            () => DownloadSearchResultAsAsync(MediaKind.Video), () => CanDownloadSearchVideo);
        _playSearchResultCommand = new RelayCommand(PlaySelectedSearchResultAsync, () => CanPlaySearchResult);
        _streamSearchResultCommand = new RelayCommand(StreamSelectedSearchResultAsync, () => CanStreamSearchResult);

        _retryFailedCommand = new RelayCommand(RetryFailedAsync, () => !IsBusy && FailedCount > 0);
        _clearQueueCommand = new RelayCommand(ClearQueueAsync, () => QueueCount > 0 && !IsDownloading);
        _clearFinishedQueueEntriesCommand = new RelayCommand(ClearFinishedQueueEntriesAsync, () => QueueEntries.Count > 0);
        _toggleQueuePanelCommand = new RelayCommand(() =>
        {
            QueuePanelOpen = !QueuePanelOpen;
            return Task.CompletedTask;
        });
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

    /// <summary>The library item behind the current player source, when there is one.</summary>
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

    /// <summary>Title shown in the player header (library item or YouTube result).</summary>
    public string? PlayerTitle
    {
        get => _playerTitle;
        private set
        {
            if (Set(ref _playerTitle, value))
            {
                OnPropertyChanged(nameof(IsPlayerVisible));
                OnPropertyChanged(nameof(IsDockedPlayerVisible));
                OnPropertyChanged(nameof(ShowMissingPrompt));
                _popOutPlayerCommand.RefreshCanExecute();
            }
        }
    }

    public string? PlayerSubtitle
    {
        get => _playerSubtitle;
        private set => Set(ref _playerSubtitle, value);
    }

    /// <summary>The YouTube id behind the player, when known (needed for streaming/format switching).</summary>
    public string? PlayerVideoId
    {
        get => _playerVideoId;
        private set
        {
            if (Set(ref _playerVideoId, value))
            {
                OnPropertyChanged(nameof(PlayerCanSwitchKind));
                _retryStreamCommand.RefreshCanExecute();
            }
        }
    }

    public MediaKind PlayerKind
    {
        get => _playerKind;
        private set
        {
            if (Set(ref _playerKind, value))
            {
                OnPropertyChanged(nameof(PlayerIsAudio));
                OnPropertyChanged(nameof(PlayerIsVideo));
                OnPropertyChanged(nameof(PlayerKindLabel));
            }
        }
    }

    public bool PlayerIsAudio => _playerKind == MediaKind.Audio;
    public bool PlayerIsVideo => _playerKind == MediaKind.Video;
    public string PlayerKindLabel => _playerKind == MediaKind.Audio ? "MP3" : "MP4";

    /// <summary>Local file path or loopback HLS URL for the current source.</summary>
    public string? PlayerSourceUri
    {
        get => _playerSourceUri;
        private set
        {
            if (Set(ref _playerSourceUri, value))
            {
                _popOutPlayerCommand?.RefreshCanExecute();
            }
        }
    }

    /// <summary>True when the current source is the online HLS relay (not an offline file).</summary>
    public bool PlayerIsStream
    {
        get => _playerIsStream;
        private set
        {
            if (Set(ref _playerIsStream, value))
            {
                OnPropertyChanged(nameof(PlayerSourceLabel));
            }
        }
    }

    public string PlayerSourceLabel =>
        _playerStreamLoading ? "Buffrar" :
        _playerStreamError is not null ? "Fel" :
        _playerIsStream ? "Strömmar" : "Offline";

    public bool PlayerIsStreamLoading
    {
        get => _playerStreamLoading;
        private set
        {
            if (Set(ref _playerStreamLoading, value))
            {
                OnPropertyChanged(nameof(PlayerSourceLabel));
            }
        }
    }

    public string? PlayerStreamStatus
    {
        get => _playerStreamStatus;
        private set => Set(ref _playerStreamStatus, value);
    }

    public string? PlayerStreamError
    {
        get => _playerStreamError;
        private set
        {
            if (Set(ref _playerStreamError, value))
            {
                OnPropertyChanged(nameof(PlayerSourceLabel));
            }
        }
    }

    /// <summary>Streaming needs the Windows media stack (the same one local playback uses).</summary>
    public bool CanStream => OperatingSystem.IsWindows();

    /// <summary>The MP3/MP4 switch is available once the video id is known.</summary>
    public bool PlayerCanSwitchKind => _playerVideoId is not null;

    public bool IsPlayerVisible => _playerTitle is not null || _selectedFileMissing;

    /// <summary>The "file missing" prompt shows only while no source (local or stream) is loaded.</summary>
    public bool ShowMissingPrompt => _selectedFileMissing && _playerTitle is null;

    /// <summary>True while the pop-out player window is open (the docked player is hidden).</summary>
    public bool PlayerIsPopOut => _playerPopOutOpen;

    /// <summary>The docked player surface shows only when a source is loaded and the
    /// pop-out window is closed (a missing file or pop-out hides it).</summary>
    public bool IsDockedPlayerVisible => _playerTitle is not null && !_playerPopOutOpen;

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

    /// <summary>
    /// Whether the queue panel (Laddningskön card) is shown. Hidden by default — the app bar
    /// queue icon toggles it. It auto-closes when every job has finished, unless the user is
    /// reading a row's log (SelectedQueueEntry is set); the compact progress card covers the
    /// active-download feedback while the panel is closed.
    /// </summary>
    public bool QueuePanelOpen
    {
        get => _queuePanelOpen;
        set => Set(ref _queuePanelOpen, value);
    }

    /// <summary>Number of queue entries still in flight (queued or downloading) — the app bar badge.</summary>
    public int ActiveQueueCount => QueueEntries.Count(e => e.Status is ItemStatus.Queued or ItemStatus.Downloading);

    /// <summary>
    /// Re-raises the app bar badge count and applies the panel's auto-close rule. Call on the UI
    /// thread after any change to queue entry status or membership (enqueue, job start/finish,
    /// cancel, clears).
    /// </summary>
    private void RefreshQueuePanelState()
    {
        if (QueueEntries.Count == 0)
        {
            QueuePanelOpen = false;
        }
        else if (SelectedQueueEntry is null
                 && QueueEntries.All(e => e.Status is ItemStatus.Complete or ItemStatus.Failed))
        {
            // Everything is done and the user isn't reading a log — tuck the panel away.
            QueuePanelOpen = false;
        }

        OnPropertyChanged(nameof(ActiveQueueCount));
        RefreshSearchResultCommands();
    }

    public async Task InitializeAsync()
    {
        Kind = PreferencesService.GetFormat();
        var root = PreferencesService.GetLibraryRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            // First launch (or the saved folder is gone): never silently pick or
            // create a location. Keep the root unset — the library view shows a
            // "choose a folder" state and downloads stay disabled until the user
            // picks one (BrowseAsync persists the choice).
            return;
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

        if (!IsRootSet)
        {
            Error = "Välj en biblioteksmapp först — nedladdningar sparas i den mapp du väljer.";
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
                var jobKind = kindOverride ?? Kind;
                var key = QueueKey(entry.Id, jobKind);
                var job = new QueueJob(entry, playlistTitle, jobKind);

                if (_queueEntryById.TryGetValue(key, out var existing))
                {
                    // Already waiting or downloading in this format: don't add a second job.
                    if (existing.Status is ItemStatus.Queued or ItemStatus.Downloading)
                    {
                        continue;
                    }

                    // A finished/failed job for the same format: reset its row and re-queue.
                    existing.Status = ItemStatus.Queued;
                    existing.Percent = 0;
                    existing.Kind = jobKind;
                    existing.ClearLogs();
                    existing.AppendLog("Överför till kön igen.");
                    existing.FlushLog();
                }
                else
                {
                    var queueEntry = new LibraryQueueEntry(entry, playlistTitle, position + 1, jobKind);
                    _queueEntryById[key] = queueEntry;
                    newEntries.Add(queueEntry);
                }

                _downloadQueue.Enqueue(job);
                position++;
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
            // The queue-count-gated buttons stay disabled otherwise (CanExecute is
            // only re-raised on an explicit refresh).
            _clearQueueCommand.RefreshCanExecute();
            _clearFinishedQueueEntriesCommand.RefreshCanExecute();
            // The panel stays where the user left it — the progress card already
            // surfaces the active download (the app bar badge shows the job count).
            RefreshQueuePanelState();
        });

        EnsureWorkerStarted();
        if (_isRetryingFailed)
        {
            _isRetryingFailed = false;
            Error = null;
        }
    }

    private readonly record struct QueueJob(MediaEntry Entry, string? PlaylistTitle, MediaKind Kind)
    {
        public string QueueKey => LibraryViewModel.QueueKey(Entry.Id, Kind);
    }

    /// <summary>One queue slot per video *and* format, so MP3 and MP4 can be queued together.</summary>
    private static string QueueKey(string id, MediaKind kind) => $"{id}|{(kind == MediaKind.Audio ? "a" : "v")}";


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
                    _queueEntryById.TryGetValue(job.QueueKey, out queueEntry);
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
                            // Pass the exact value through: the queue row, the item row and the
                            // progress card all format with "{0}%", which rounds — feeding them
                            // pre-truncated ints made the queue row lag the main window by 1%.
                            var percent = item.Percent ?? 0;
                            JobPercent = percent;
                            if (queueEntry is not null && queueEntry.Percent != percent)
                            {
                                queueEntry.Percent = percent;
                            }
                        }),
                        _queueCts!.Token,
                        onLine: line =>
                        {
                            if (queueEntry is null)
                            {
                                return;
                            }
                            // Buffer only (no notification per line) — coalesce to at most one
                            // UI flush per queued frame so the log panel updates live without
                            // a layout pass per line.
                            queueEntry.AppendLog(line);
                            if (System.Threading.Interlocked.Exchange(ref _logFlushPending, 1) == 0)
                            {
                                var entry = queueEntry;
                                OnUi(() =>
                                {
                                    System.Threading.Interlocked.Exchange(ref _logFlushPending, 0);
                                    entry.FlushLog();
                                });
                            }
                        });

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
                            queueEntry.FlushLog();
                        }
                        // UpsertItem alone misses the final state when the item was
                        // registered as Queued and only progress was reported since —
                        // land the final LibraryItem so the info page (and collection)
                        // sees the result.
                        if (Items.FirstOrDefault(i => i.Id == result.Id) is not null)
                        {
                            UpsertItem(result);
                        }
                        SyncSelectedSearchResultInLibrary(result.Id);
                        RefreshQueuePanelState();
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
                            queueEntry.FlushLog();
                        }
                        while (_downloadQueue.TryDequeue(out var remaining))
                        {
                            QueueCount--;
                            lock (_queueGate)
                            {
                                if (_queueEntryById.TryGetValue(remaining.QueueKey, out var remainingEntry))
                                {
                                    remainingEntry.Status = ItemStatus.Failed;
                                    remainingEntry.AppendLog("Avbröts av användaren.");
                                    remainingEntry.FlushLog();
                                }
                            }
                        }
                        Error = "Nedladdningen avbröts.";
                        RefreshQueuePanelState();
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
                if (_queueEntryById.Remove(job.QueueKey, out var entry) && entry.Status == ItemStatus.Queued)
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
            RefreshQueuePanelState();
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
        }, entry.Playlist, entry.Kind);
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
            RefreshQueuePanelState();
        });
        return Task.CompletedTask;
    }
}
