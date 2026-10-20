using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using VideoPiper.Models;
using VideoPiper.Services;

namespace VideoPiper.ViewModels;

/// <summary>
/// View model for the library tab: browsing the root, downloading media (audio or video)
/// into the library, and managing items (play local file, remove, delete).
/// </summary>
public enum LibrarySidebarSection
{
    AllMedia,
    AudioOnly,
    VideoOnly,
    FailedOnly,
    YouTubeSearch,
}

public sealed partial class LibraryViewModel : INotifyPropertyChanged
{
    private LibraryStore _store = new(string.Empty);
    private string _rootPath = string.Empty;
    private string _url = string.Empty;
    private string _searchQuery = string.Empty;
    private string _localSearchQuery = string.Empty;
    private MediaKind _kind = MediaKind.Audio;
    private LibrarySidebarSection _activeSection = LibrarySidebarSection.AllMedia;
    private bool _isSearching;
    private bool _hasSearchResults;
    private bool _isUrlInput;
    private int _filterIndex; // 0 = Alla, 1 = Ljud, 2 = Video
    private string? _error;
    private LibraryItem? _selectedItem;
    private CancellationTokenSource? _searchCts;
    private int _sortIndex; // 0 = Nyast, 1 = Titel, 2 = Längd
    private SearchResult? _selectedSearchResult;
    private bool _isSearchingDetail;
    private string? _selectedSearchDetailError;
    private bool _selectedSearchResultInLibrary;
    private CancellationTokenSource? _detailCts;
    private RelayCommand _searchResultAudioCommand;
    private RelayCommand _searchResultVideoCommand;

    public LibrarySidebarSection ActiveSection
    {
        get => _activeSection;
        set
        {
            if (Set(ref _activeSection, value))
            {
                FilterIndex = value switch
                {
                    LibrarySidebarSection.AudioOnly => 1,
                    LibrarySidebarSection.VideoOnly => 2,
                    LibrarySidebarSection.FailedOnly => 3,
                    _ => 0,
                };
                OnPropertyChanged(nameof(IsAllActive));
                OnPropertyChanged(nameof(IsAudioActive));
                OnPropertyChanged(nameof(IsVideoActive));
                OnPropertyChanged(nameof(IsFailedActive));
                OnPropertyChanged(nameof(IsSearchActive));
                OnPropertyChanged(nameof(IsCollectionActive));
                OnPropertyChanged(nameof(SectionTitle));
                OnPropertyChanged(nameof(EmptyStateTitle));
                OnPropertyChanged(nameof(EmptyStateHint));
                // A selected search result only makes sense in the search section —
                // leave its info page when switching sections.
                SelectedSearchResult = null;
            }
        }
    }

    public bool IsAllActive => _activeSection == LibrarySidebarSection.AllMedia;
    public bool IsAudioActive => _activeSection == LibrarySidebarSection.AudioOnly;
    public bool IsVideoActive => _activeSection == LibrarySidebarSection.VideoOnly;
    public bool IsFailedActive => _activeSection == LibrarySidebarSection.FailedOnly;
    public bool IsSearchActive => _activeSection == LibrarySidebarSection.YouTubeSearch;
    public bool IsCollectionActive => _activeSection != LibrarySidebarSection.YouTubeSearch;

    public string SectionTitle => _activeSection switch
    {
        LibrarySidebarSection.AudioOnly => $"Ljudfiler ({AudioCount})",
        LibrarySidebarSection.VideoOnly => $"Videofiler ({VideoCount})",
        LibrarySidebarSection.FailedOnly => $"Misslyckades ({FailedCount})",
        LibrarySidebarSection.YouTubeSearch => "YouTube-sökning",
        _ => $"Alla filer ({TotalCount})",
    };

    /// <summary>Unified search/URL box text. If a URL is entered, it enables download mode.
    /// A plain query only updates the box — the search itself runs when the user presses
    /// Sök or Enter (<see cref="RunSearchNowAsync"/>).</summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!Set(ref _searchQuery, value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (IsYouTubeUrl(trimmed))
            {
                _searchCts?.Cancel();
                IsSearching = false;
                HasSearchResults = false;
                SearchResults.Clear();
                SelectedSearchResult = null;
                Url = trimmed;
                IsUrlInput = true;
                return;
            }

            IsUrlInput = false;
            Url = string.Empty;

            // Editing the query invalidates any previous results — the search only runs
            // on Sök/Enter. Cancel anything in flight and drop the stale results.
            _searchCts?.Cancel();
            IsSearching = false;
            HasSearchResults = false;
            SearchResults.Clear();
            SelectedSearchResult = null;

            // The query box is a search box — as soon as there is a real query the
            // main area shows the search section, so Sök/Enter results never land
            // behind the collection list ("ghost search").
            if (trimmed.Length >= 2)
            {
                ActiveSection = LibrarySidebarSection.YouTubeSearch;
            }
        }
    }

    public static bool IsYouTubeUrl(string s) =>
        !string.IsNullOrWhiteSpace(s) &&
        (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         s.StartsWith("youtu.be/", StringComparison.OrdinalIgnoreCase) ||
         s.StartsWith("youtube.com/", StringComparison.OrdinalIgnoreCase) ||
         s.StartsWith("www.youtube.com/", StringComparison.OrdinalIgnoreCase));

    public bool IsUrlInput
    {
        get => _isUrlInput;
        private set
        {
            if (Set(ref _isUrlInput, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(ShowNoRootHint));
                _downloadCommand.RefreshCanExecute();
            }
        }
    }

    /// <summary>Optional filter text to search within already downloaded items in the local library.</summary>
    public string LocalSearchQuery
    {
        get => _localSearchQuery;
        set
        {
            if (Set(ref _localSearchQuery, value))
            {
                RefreshFilteredItems();
            }
        }
    }

    public int FilterIndex
    {
        get => _filterIndex;
        set
        {
            if (Set(ref _filterIndex, value))
            {
                RefreshFilteredItems();
                OnPropertyChanged(nameof(IsAllFilterSelected));
                OnPropertyChanged(nameof(IsAudioFilterSelected));
                OnPropertyChanged(nameof(IsVideoFilterSelected));
                OnPropertyChanged(nameof(IsFailedFilterSelected));
            }
        }
    }

    public bool IsAllFilterSelected => _filterIndex == 0;
    public bool IsAudioFilterSelected => _filterIndex == 1;
    public bool IsVideoFilterSelected => _filterIndex == 2;
    public bool IsFailedFilterSelected => _filterIndex == 3;

    public int TotalCount => Items.Count;
    public int AudioCount => Items.Count(i => i.Kind == MediaKind.Audio);
    public int VideoCount => Items.Count(i => i.Kind == MediaKind.Video);

    /// <summary>List sorting: 0 = Nyast, 1 = Titel, 2 = Längd.</summary>
    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            if (Set(ref _sortIndex, value))
            {
                RefreshFilteredItems();
            }
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (Set(ref _isSearching, value))
            {
                OnPropertyChanged(nameof(IsSearching));
                OnPropertyChanged(nameof(ShowNoResults));
            }
        }
    }

    public IReadOnlyList<SearchResult> SearchResultsView => SearchResults;

    /// <summary>True while a search is in flight or has produced results — the tab shows the search list.</summary>
    public bool HasSearchResults
    {
        get => _hasSearchResults;
        private set
        {
            if (Set(ref _hasSearchResults, value))
            {
                OnPropertyChanged(nameof(HasSearchResults));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ShowSearchList));
                OnPropertyChanged(nameof(ShowNoResults));
            }
        }
    }

    public async Task RunSearchNowAsync()
    {
        var trimmed = SearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        if (IsYouTubeUrl(trimmed))
        {
            Url = trimmed;
            IsUrlInput = true;
            await DownloadAsync();
            return;
        }

        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        OnUi(() =>
        {
            // Same guard as the SearchQuery setter: an explicit search must show
            // the search section, not leave results behind the collection list.
            ActiveSection = LibrarySidebarSection.YouTubeSearch;
            Error = null;
            IsSearching = true;
            HasSearchResults = true;
            SelectedSearchResult = null;
        });

        try
        {
            var results = await _searchService.SearchAsync(trimmed, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            OnUi(() =>
            {
                SearchResults.Clear();
                foreach (var result in results)
                {
                    SearchResults.Add(result);
                }
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ShowSearchList));
                OnPropertyChanged(nameof(ShowNoResults));
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                OnUi(() =>
                {
                    Error = $"Sökningen misslyckades: {UiErrors.FromException(ex, "okänt fel")}";
                    SearchResults.Clear();
                    HasSearchResults = false;
                    SelectedSearchResult = null;
                });
            }
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                OnUi(() => IsSearching = false);
            }
        }
    }

    /// <summary>The search result whose info page is open (or null). Selecting one never
    /// downloads — the info page's buttons do.</summary>
    public SearchResult? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (ReferenceEquals(_selectedSearchResult, value))
            {
                return;
            }

            Set(ref _selectedSearchResult, value);
            OnPropertyChanged(nameof(HasSelectedSearchResult));
            OnPropertyChanged(nameof(ShowSearchList));
            OnPropertyChanged(nameof(ShowNoResults));
            OnPropertyChanged(nameof(CanDownloadSearchResult));
            OnPropertyChanged(nameof(SearchDetailHint));
            RefreshSearchResultCommands();

            // Reset the per-result detail state, then enrich with the full metadata
            // (description) in the background. The download buttons work regardless —
            // they use the flat-search data already in the record.
            IsSearchingDetail = false;
            SelectedSearchDetailError = null;
            if (value is not null)
            {
                _ = FetchSearchDetailAsync(value);
            }
        }
    }

    /// <summary>True while the info page is open.</summary>
    public bool HasSelectedSearchResult => _selectedSearchResult is not null;

    /// <summary>Opens the info page for a search result (row click). Never downloads.</summary>
    public void SelectSearchResult(SearchResult result) => SelectedSearchResult = result;

    /// <summary>True when the current search has returned at least one result.</summary>
    public bool HasResults => HasSearchResults && SearchResults.Count > 0;

    /// <summary>The result list is visible when there are results and no info page is open.</summary>
    public bool ShowSearchList => HasResults && !HasSelectedSearchResult;

    /// <summary>The no-results state: a search finished with zero hits (not loading, no info page open).</summary>
    public bool ShowNoResults => HasSearchResults && !HasResults && !HasSelectedSearchResult && !IsSearching;

    /// <summary>True while the info page is fetching extra metadata (description).</summary>
    public bool IsSearchingDetail
    {
        get => _isSearchingDetail;
        private set => Set(ref _isSearchingDetail, value);
    }

    /// <summary>Error from the detail fetch, shown on the info page (never blocks download).</summary>
    public string? SelectedSearchDetailError
    {
        get => _selectedSearchDetailError;
        private set => Set(ref _selectedSearchDetailError, value);
    }

    /// <summary>True when the selected search result is already in the library.</summary>
    public bool SelectedSearchResultInLibrary => _selectedSearchResultInLibrary;

    /// <summary>Gate for the info page's download buttons.</summary>
    public bool CanDownloadSearchResult =>
        _selectedSearchResult is not null
        && !IsDownloading
        && !IsBusy
        && IsRootSet
        && !_selectedSearchResultInLibrary;

    /// <summary>"Ladda ner ljud" action on the search info page.</summary>
    public ICommand DownloadSearchResultAudioCommand => _searchResultAudioCommand;

    /// <summary>"Ladda ner video" action on the search info page.</summary>
    public ICommand DownloadSearchResultVideoCommand => _searchResultVideoCommand;

    /// <summary>Re-queries the two info-page download buttons so their enabled state
    /// tracks <see cref="CanDownloadSearchResult"/> (WinUI only re-queries on
    /// CanExecuteChanged). Call from every setter that can change the gate.</summary>
    private void RefreshSearchResultCommands()
    {
        _searchResultAudioCommand?.RefreshCanExecute();
        _searchResultVideoCommand?.RefreshCanExecute();
    }

    private async Task FetchSearchDetailAsync(SearchResult result)
    {
        _detailCts?.Cancel();
        var cts = new CancellationTokenSource();
        _detailCts = cts;
        OnUi(() => IsSearchingDetail = true);
        try
        {
            var json = await _searchService.FetchDetailJsonAsync(result.Id, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }
            OnUi(() =>
            {
                var description = YtDlpJson.GetDescription(json);
                // Audio-only uploads carry no video formats — the info page hides its
                // video option once this is known.
                var hasVideo = YtDlpJson.HasVideoFormat(json);
                if (!ReferenceEquals(_selectedSearchResult, result))
                {
                    return;
                }
                _selectedSearchResult = result with { Description = description, HasVideo = hasVideo };
                OnPropertyChanged(nameof(SelectedSearchResult));
                IsSearchingDetail = false;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            OnUi(() =>
            {
                if (ReferenceEquals(_selectedSearchResult, result))
                {
                    SelectedSearchDetailError = UiErrors.FromException(ex, "Kunde inte hämta videons detaljer.");
                }
                IsSearchingDetail = false;
            });
        }
    }

    /// <summary>Queues the selected search result (or the given one) for download in the
    /// chosen format. Does not switch sections or the global format toggle — the queue
    /// panel below the list shows progress.</summary>
    public Task DownloadSearchResultAsAsync(MediaKind kind, SearchResult? result = null)
    {
        var target = result ?? _selectedSearchResult;
        if (target is null)
        {
            return Task.CompletedTask;
        }

        var entry = new MediaEntry(
            Id: target.Id,
            Title: target.Title,
            Uploader: target.Uploader,
            DurationSeconds: target.DurationSeconds,
            Url: $"https://www.youtube.com/watch?v={target.Id}");

        // Skip items that are already in the library (complete, downloading, or queued).
        if (Items.Any(i => i.Id == entry.Id) || _downloadQueue.Any(q => q.Entry.Id == entry.Id))
        {
            return Task.CompletedTask;
        }

        OnUi(() =>
        {
            Error = null;
            EnqueueEntries(new[] { entry }, playlistTitle: null, kindOverride: kind);
        });
        return Task.CompletedTask;
    }

    /// <summary>The item currently selected in the list (two-way bound to the ListView). Selecting it
    /// loads it into the player without starting playback; the user presses play to begin.</summary>
    public LibraryItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value))
            {
                return;
            }

            Set(ref _selectedItem, value);
            OnPropertyChanged(nameof(IsItemSelected));
            // A new selection loads the item into the player (no auto-play); the
            // transport controls' play button (or "Spela") starts playback.
            PlayingItem = value;
            // If the file is gone, drop it from the player so the "download it"
            // prompt shows instead of the video.
            UpdateSelectedFileMissing();
            _playSelectedCommand.RefreshCanExecute();
            _openFolderSelectedCommand.RefreshCanExecute();
            _deleteSelectedCommand.RefreshCanExecute();
            _downloadSelectedCommand.RefreshCanExecute();
        }
    }

    private bool _selectedFileMissing;

    /// <summary>True when the selected item is complete but its file is missing from disk — the
    /// player area shows a "download it" prompt instead of the video.</summary>
    public bool SelectedFileMissing
    {
        get => _selectedFileMissing;
        private set
        {
            if (Set(ref _selectedFileMissing, value))
            {
                // A missing file can't play — clear the player so the "download
                // it" prompt shows. When the file comes back (re-download),
                // load the selected item into the player again (no auto-play).
                PlayingItem = value ? null : _selectedItem;
                OnPropertyChanged(nameof(IsPlayerVisible));
            }
        }
    }

    private void UpdateSelectedFileMissing()
    {
        var item = _selectedItem;
        var missing = item is { Status: ItemStatus.Complete }
                      && (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath));
        SelectedFileMissing = missing;
    }
    public bool IsItemSelected => _selectedItem is not null;
    public bool HasItems => Items.Count > 0;
    public bool HasFilteredItems => FilteredItems.Count > 0;
    public bool ShowEmptyState => FilteredItems.Count == 0 && !HasSearchResults && !IsBusy;

    /// <summary>Empty-state heading, adapted to the active section.</summary>
    public string EmptyStateTitle => _activeSection switch
    {
        LibrarySidebarSection.FailedOnly => "Inga misslyckade laddningar",
        _ => "Inga filer här ännu",
    };

    /// <summary>Empty-state hint line, adapted to the active section.</summary>
    public string EmptyStateHint => _activeSection switch
    {
        LibrarySidebarSection.FailedOnly => "Nedladdningar som misslyckas hamnar här — och kan startas om.",
        _ => "Använd YouTube-sökning för att hitta låtar och bygga ditt bibliotek.",
    };

    public string RootPath
    {
        get => _rootPath;
        private set
        {
            if (Set(ref _rootPath, value))
            {
                OnPropertyChanged(nameof(RootPathDisplay));
                OnPropertyChanged(nameof(IsRootSet));
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(ShowCollectionEmptyState));
                OnPropertyChanged(nameof(ShowNoRootHint));
                OnPropertyChanged(nameof(SearchDetailHint));
                _downloadCommand.RefreshCanExecute();
                _resyncCommand.RefreshCanExecute();
                RefreshSearchResultCommands();
            }
        }
    }

    public string RootPathDisplay => string.IsNullOrWhiteSpace(_rootPath) ? "Välj biblioteksmapp..." : _rootPath;
    public bool IsRootSet => !string.IsNullOrWhiteSpace(_rootPath);

    /// <summary>The collection's "no items yet" empty state — only when a folder is set.
    /// Without a folder, the first-launch "choose a folder" card takes over instead.</summary>
    public bool ShowCollectionEmptyState => IsRootSet && !HasFilteredItems;

    /// <summary>Hint under the search row: a URL is ready but no library folder is chosen yet.</summary>
    public bool ShowNoRootHint => IsUrlInput && !IsRootSet;

    /// <summary>Explanation line on the search info page — also explains why its download
    /// buttons stay disabled until a library folder is chosen.</summary>
    public string SearchDetailHint => _selectedSearchResult is null
        ? string.Empty
        : !IsRootSet
            ? "Välj en biblioteksmapp först — nedladdningar sparas i den mapp du väljer."
            : _selectedSearchResultInLibrary
                ? "Den här videon finns redan i biblioteket."
                : "Välj format för nedladdningen — den sparas i ditt bibliotek.";

    public string Url
    {
        get => _url;
        set
        {
            if (Set(ref _url, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                _downloadCommand.RefreshCanExecute();
            }
        }
    }

    public MediaKind Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                PreferencesService.SetFormat(value);
                OnPropertyChanged(nameof(IsAudioSelected));
                OnPropertyChanged(nameof(IsVideoSelected));
                OnPropertyChanged(nameof(FormatBadgeText));
            }
        }
    }

    public bool IsAudioSelected
    {
        get => _kind == MediaKind.Audio;
        set => Kind = value ? MediaKind.Audio : MediaKind.Video;
    }

    public bool IsVideoSelected => _kind == MediaKind.Video;
    public string FormatBadgeText => _kind == MediaKind.Audio ? "MP3 (Ljud)" : "MP4 (Video)";


    public Task PlayItemAsync(LibraryItem? item)
    {
        if (item is null)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
        {
            Error = $"Filen hittades inte på disk: {item.Title}";
            return Task.CompletedTask;
        }

        // Selecting an item only loads it into the player (no auto-play); the play
        // button here starts playback explicitly.
        PlayingItem = item;
        Error = null;
        UpdateSelectedFileMissing();
        return Task.CompletedTask;
    }

    private async Task PlaySelectedAsync()
    {
        await PlayItemAsync(SelectedItem);
    }

    /// <summary>
    /// Re-downloads the selected item into the library (used by the player's "file
    /// saknas" prompt) in the format it was originally saved as. Queues the item in
    /// the download worker like any other.
    /// </summary>
    private Task DownloadSelectedAsync()
    {
        return DownloadSelectedAsAsync(SelectedItem?.Kind ?? Kind);
    }

    /// <summary>
    /// Queues the selected item for download in a specific format (MP3 or MP4),
    /// overriding the global format toggle. The item's kind is updated so the list
    /// badge matches the new target file.
    /// </summary>
    public Task DownloadSelectedAsAsync(MediaKind kind)
    {
        var item = SelectedItem;
        if (item is null || string.IsNullOrEmpty(item.Id))
        {
            return Task.CompletedTask;
        }

        item.Kind = kind;
        _isRetryingFailed = true;
        EnqueueEntries(new[]
        {
            new MediaEntry(
                Id: item.Id,
                Title: item.Title,
                Uploader: item.Uploader,
                DurationSeconds: item.DurationSeconds,
                Url: $"https://www.youtube.com/watch?v={item.Id}")
        }, item.Playlist, kind);
        Error = null;
        return Task.CompletedTask;
    }

    /// <summary>Deletes the selected item. The index entry is always removed; the media on disk is
    /// deleted (plus any emptied folders) unless the delete-mode preference keeps files.</summary>
    private async Task DeleteSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        var removed = _store.Remove(item.Id);
        var target = removed ?? item;

        if (PreferencesService.GetDeleteMode() == DeleteMode.DeleteFiles)
        {
            _store.DeleteMediaFiles(target);
            _store.DeleteEmptyFolders(Path.GetDirectoryName(target.FilePath));
        }

        Log.For(typeof(LibraryViewModel)).LogInformation(
            "Tog bort {ItemId} {Title} (fil: {Path}, läge: {Mode})",
            item.Id, item.Title, item.FilePath, PreferencesService.GetDeleteMode());
        await _store.SaveAsync();

        if (ReferenceEquals(PlayingItem, item))
        {
            PlayingItem = null;
        }
        SelectedItem = null;
        RefreshItems();
        UpdateSelectedFileMissing();
    }


    private async Task ResyncAsync()
    {
        if (!IsRootSet)
        {
            return;
        }

        var changed = _store.SyncWithDisk();
        await _store.SaveAsync();
        RefreshItems();
        if (changed > 0)
        {
            Error = $"{changed} objekt saknade fil på disk och markerades som felaktiga.";
        }
    }

    /// <summary>
    /// Lands a library item in <see cref="Items"/>. The common path — a progress tick
    /// carrying the *same* instance the list already holds (the download service mutates
    /// its registered item) — makes zero collection changes: the row is already bound to
    /// that instance and its change notifications update it in place. Only real
    /// membership changes (a new item, or a re-download arriving as a fresh instance)
    /// touch the collections. This keeps the main list from being torn down and rebuilt
    /// on every progress tick, which made the whole view flicker during downloads.
    /// </summary>
    private void UpsertItem(LibraryItem item)
    {
        var index = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == item.Id)
            {
                index = i;
                break;
            }
        }

        var previous = index >= 0 ? Items[index] : null;

        if (previous is null)
        {
            Items.Insert(0, item);
        }
        else if (!ReferenceEquals(previous, item))
        {
            // A re-download of the same id arrives as a fresh instance (the service
            // always builds a new LibraryItem) — swap the reference once so the row
            // re-binds to it; every later tick is the in-place path above.
            Items[index] = item;
        }

        // Filter, sort and the counts only depend on these fields; a plain progress
        // tick on an unchanged instance must not rebuild the filtered list.
        var affectsList = previous is null
            || !ReferenceEquals(previous, item)
            || previous.Status != item.Status
            || previous.Kind != item.Kind
            || previous.Title != item.Title
            || previous.Uploader != item.Uploader
            || previous.Playlist != item.Playlist
            || previous.DurationSeconds != item.DurationSeconds;

        if (affectsList)
        {
            FailedCount = Items.Count(i => i.Status == ItemStatus.Failed);
            RefreshFilteredItems();
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(AudioCount));
            OnPropertyChanged(nameof(VideoCount));
            // The header title embeds the active section's count — it must track the counts
            // or it sits stale until the user switches sections.
            OnPropertyChanged(nameof(SectionTitle));
        }

        UpdateSelectedFileMissing();

        SyncSelectedSearchResultInLibrary(item.Id, item.Status);
    }

    /// <summary>If <paramref name="id"/> is the result on the open info page, update the
    /// "already in library" flag so the page's download buttons disable once the download
    /// has registered in the library. Call on the UI thread.</summary>
    private void SyncSelectedSearchResultInLibrary(string id, ItemStatus status)
    {
        if (_selectedSearchResult is null || _selectedSearchResult.Id != id)
        {
            return;
        }

        var inLibrary = status != ItemStatus.Queued && status != ItemStatus.Downloading;
        if (inLibrary != _selectedSearchResultInLibrary)
        {
            _selectedSearchResultInLibrary = inLibrary;
            OnPropertyChanged(nameof(SelectedSearchResultInLibrary));
            OnPropertyChanged(nameof(CanDownloadSearchResult));
            OnPropertyChanged(nameof(SearchDetailHint));
            OnPropertyChanged(nameof(SelectedSearchResult));
            RefreshSearchResultCommands();
        }
    }

    private async Task PasteAsync()
    {
        try
        {
            var text = await Windows.ApplicationModel.DataTransfer.Clipboard.GetContent().GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                SearchQuery = text.Trim();
            }
        }
        catch
        {
            // Clipboard unavailable — ignore.
        }
    }

    private void ClosePlayer()
    {
        // Clear the item first (stops the docked player's source) so the pop-out close
        // does not briefly resume playback that is about to be torn down.
        PlayingItem = null;
#if WINDOWS
        ClosePopOutWindow();
#endif
    }

    /// <summary>
    /// Opens the pop-out player window for the current item (created on the UI
    /// thread). While it is open, the docked player is hidden via
    /// <see cref="PlayerIsPopOut"/>; closing the window restores the dock.
    /// No-op on non-Windows (Skia) targets, where pop-out is not available.
    /// </summary>
    private void PopOutPlayer()
    {
#if WINDOWS
        var item = PlayingItem;
        if (item is null || _playerPopOutOpen)
        {
            return;
        }

        var window = new PlayerPopOutWindow(item);
        window.ClosedChanged += OnPopOutWindowClosed;
        _popOutWindow = window;
        _playerPopOutOpen = true;
        OnPropertyChanged(nameof(PlayerIsPopOut));
        OnPropertyChanged(nameof(IsDockedPlayerVisible));
        _popOutPlayerCommand.RefreshCanExecute();
        window.Activate();
        window.Configure(item.Kind == MediaKind.Video ? 860 : 520, item.Kind == MediaKind.Video ? 560 : 160);
#endif
    }
#if WINDOWS
    /// <summary>Raised on the UI thread when the pop-out window closes; restores the dock.</summary>
    private void OnPopOutWindowClosed(object? sender, EventArgs e)
    {
        OnUi(() =>
        {
            if (_popOutWindow is not null)
            {
                _popOutWindow.ClosedChanged -= OnPopOutWindowClosed;
                _popOutWindow = null;
            }
            _playerPopOutOpen = false;
            OnPropertyChanged(nameof(PlayerIsPopOut));
            OnPropertyChanged(nameof(IsDockedPlayerVisible));
            _popOutPlayerCommand.RefreshCanExecute();
        });
    }

    /// <summary>Closes the pop-out window (if any) without clearing <see cref="PlayingItem"/>.</summary>
    private void ClosePopOutWindow()
    {
        if (_popOutWindow is { } window)
        {
            window.ClosedChanged -= OnPopOutWindowClosed;
            _popOutWindow = null;
            window.Close();
        }
        if (_playerPopOutOpen)
        {
            _playerPopOutOpen = false;
            OnPropertyChanged(nameof(PlayerIsPopOut));
            OnPropertyChanged(nameof(IsDockedPlayerVisible));
            _popOutPlayerCommand.RefreshCanExecute();
        }
    }
#endif

    public Task OpenFolderItem(LibraryItem? item)
    {
        var target = item?.FilePath;
        if (!string.IsNullOrEmpty(target) && File.Exists(target))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{target}\"") { UseShellExecute = true });
                return Task.CompletedTask;
            }
            catch { }
        }

        return OpenRootFolder();
    }

    private Task OpenFolderSelectedAsync()
    {
        return OpenFolderItem(SelectedItem);
    }

    public Task OpenRootFolder()
    {
        if (!string.IsNullOrEmpty(RootPath) && Directory.Exists(RootPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{RootPath}\"") { UseShellExecute = true });
            }
            catch { }
        }
        return Task.CompletedTask;
    }

    public void ClearSearch()
    {
        _searchCts?.Cancel();
        SearchQuery = string.Empty;
        SearchResults.Clear();
        HasSearchResults = false;
        IsSearching = false;
        IsUrlInput = false;
        Url = string.Empty;
        // Back to the library list, where newly queued entries become visible.
        ActiveSection = LibrarySidebarSection.AllMedia;
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void RefreshItems()
    {
        OnUi(() =>
        {
            Items.Clear();
            foreach (var item in _store.Items.OrderByDescending(i => i.AddedUtc))
            {
                Items.Add(item);
            }
            FailedCount = Items.Count(i => i.Status == ItemStatus.Failed);
            RefreshFilteredItems();
            UpdateSelectedFileMissing();
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(AudioCount));
            OnPropertyChanged(nameof(VideoCount));
            OnPropertyChanged(nameof(SectionTitle));
        });
    }

    /// <summary>
    /// Re-syncs <see cref="FilteredItems"/> with the current filter/sort — minimally.
    /// When the list already holds exactly the right items in the right order, only
    /// swapped references are replaced in place (usually nothing fires at all); the
    /// full Clear+Add only happens when membership or order actually changes (a filter
    /// or sort change, or items added/removed) — never on plain progress ticks.
    /// </summary>
    public void RefreshFilteredItems()
    {
        OnUi(() =>
        {
            var candidates = BuildFilteredCandidates();

            var sameMembershipAndOrder = FilteredItems.Count == candidates.Count
                && System.Linq.Enumerable.Range(0, candidates.Count).All(i => FilteredItems[i].Id == candidates[i].Id);

            if (sameMembershipAndOrder)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (!ReferenceEquals(FilteredItems[i], candidates[i]))
                    {
                        FilteredItems[i] = candidates[i];
                    }
                }
            }
            else
            {
                FilteredItems.Clear();
                foreach (var item in candidates)
                {
                    FilteredItems.Add(item);
                }
            }

            OnPropertyChanged(nameof(HasFilteredItems));
            OnPropertyChanged(nameof(ShowCollectionEmptyState));
            OnPropertyChanged(nameof(ShowEmptyState));
        });
    }

    private List<LibraryItem> BuildFilteredCandidates()
    {
        var query = _localSearchQuery.Trim();
        var hasQuery = !string.IsNullOrEmpty(query);

        var candidates = new List<LibraryItem>();
        foreach (var item in Items)
        {
            if (FilterIndex == 1 && item.Kind != MediaKind.Audio)
            {
                continue;
            }
            if (FilterIndex == 2 && item.Kind != MediaKind.Video)
            {
                continue;
            }
            if (FilterIndex == 3 && item.Status != ItemStatus.Failed)
            {
                continue;
            }
            if (hasQuery)
            {
                var inTitle = item.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
                var inUploader = item.Uploader?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
                var inPlaylist = item.Playlist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
                if (!inTitle && !inUploader && !inPlaylist)
                {
                    continue;
                }
            }

            candidates.Add(item);
        }

        return _sortIndex switch
        {
            1 => candidates.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            2 => candidates.OrderByDescending(i => i.DurationSeconds ?? 0).ToList(),
            _ => candidates,
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static void OnUi(Action action)
    {
        if (App.MainWindowInstance?.DispatcherQueue is { } queue)
        {
            if (queue.HasThreadAccess)
            {
                action();
            }
            else
            {
                queue.TryEnqueue(() => action());
            }
        }
        else
        {
            action();
        }
    }
}
