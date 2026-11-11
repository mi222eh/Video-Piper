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
    private bool _searchAudioOffline;
    private bool _searchVideoOffline;
    private CancellationTokenSource? _detailCts;
    private RelayCommand _searchResultAudioCommand;
    private RelayCommand _searchResultVideoCommand;
    private readonly RelayCommand _playSearchResultCommand;
    private readonly RelayCommand _streamSearchResultCommand;

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
                OnPropertyChanged(nameof(ShowSearchBrowser));
                OnPropertyChanged(nameof(ShowLocalSearch));
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

    /// <summary>The search list/help area is visible unless a media page is open on top.</summary>
    public bool ShowSearchBrowser => IsSearchActive && !HasSelectedSearchResult;

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
                RefreshLocalSearch(string.Empty);
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
            RefreshLocalSearch(trimmed);

            // The query box is a search box — as soon as there is a real query the
            // main area shows the search section, so Sök/Enter results never land
            // behind the collection list ("ghost search").
            if (trimmed.Length >= 1)
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
    public int AudioCount => Items.Count(i => i.HasAudio);
    public int VideoCount => Items.Count(i => i.HasVideo);

    /// <summary>Raised whenever any sidebar count changes — the count badges re-evaluate on it.</summary>
    public int SidebarCountsVersion { get; private set; }

    private void RefreshCounts()
    {
        FailedCount = Items.Count(i => i.Status == ItemStatus.Failed);
        SidebarCountsVersion++;
        OnPropertyChanged(nameof(SidebarCountsVersion));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(AudioCount));
        OnPropertyChanged(nameof(VideoCount));
        OnPropertyChanged(nameof(SectionTitle));
    }

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

    /// <summary>Local (offline) matches for the current search box text — shown instantly,
    /// before any YouTube request runs.</summary>
    public ObservableCollection<LibraryItem> LocalSearchResults { get; } = new();

    public bool HasLocalMatches => LocalSearchResults.Count > 0;
    public string LocalMatchesHeader => $"I ditt bibliotek ({LocalSearchResults.Count})";
    public bool ShowLocalSearch => IsSearchActive && HasLocalMatches;

    /// <summary>Matching is offline: title, uploader, playlist and video id.</summary>
    private static bool MatchesLocalQuery(LibraryItem item, string query) =>
        item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (item.Uploader?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.Playlist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.Id?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    private void RefreshLocalSearch(string? query = null)
    {
        query ??= _searchQuery;
        OnUi(() =>
        {
            LocalSearchResults.Clear();
            var trimmed = query.Trim();
            if (trimmed.Length > 0 && !IsYouTubeUrl(trimmed))
            {
                foreach (var item in Items.Where(i => MatchesLocalQuery(i, trimmed)).Take(50))
                {
                    LocalSearchResults.Add(item);
                }
            }

            OnPropertyChanged(nameof(HasLocalMatches));
            OnPropertyChanged(nameof(LocalMatchesHeader));
            OnPropertyChanged(nameof(ShowLocalSearch));
        });
    }

    /// <summary>Opens a local search match in the collection list and loads it into the player.</summary>
    public void OpenLibraryItem(LibraryItem item)
    {
        ActiveSection = LibrarySidebarSection.AllMedia;
        RefreshFilteredItems();
        SelectedItem = item;
    }


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
            OnPropertyChanged(nameof(ShowSearchBrowser));
            OnPropertyChanged(nameof(ShowSearchList));
            OnPropertyChanged(nameof(ShowNoResults));
            OnPropertyChanged(nameof(CanDownloadSearchAudio));
            OnPropertyChanged(nameof(CanDownloadSearchVideo));
            OnPropertyChanged(nameof(CanPlaySearchResult));
            OnPropertyChanged(nameof(CanStreamSearchResult));
            OnPropertyChanged(nameof(SearchDetailHint));
            RefreshSearchResultCommands();
            SyncSearchOfflineState();

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

    /// <summary>True when an offline MP3 for the open search result exists.</summary>
    public bool SearchAudioOffline => _searchAudioOffline;

    /// <summary>True when an offline MP4 for the open search result exists.</summary>
    public bool SearchVideoOffline => _searchVideoOffline;

    /// <summary>Gate for the info page's two download buttons (either format still missing).</summary>
    public bool CanDownloadSearchAudio =>
        _selectedSearchResult is not null
        && !IsBusy
        && IsRootSet
        && !_searchAudioOffline
        && !QueueEntries.Any(e => e.Id == _selectedSearchResult.Id
                                  && e.Kind == MediaKind.Audio
                                  && e.Status is ItemStatus.Queued or ItemStatus.Downloading);

    public bool CanDownloadSearchVideo =>
        _selectedSearchResult is not null
        && !IsBusy
        && IsRootSet
        && !_searchVideoOffline
        && _selectedSearchResult.HasVideo != false
        && !QueueEntries.Any(e => e.Id == _selectedSearchResult.Id
                                  && e.Kind == MediaKind.Video
                                  && e.Status is ItemStatus.Queued or ItemStatus.Downloading);

    /// <summary>"Spela" works without a library folder — it streams when nothing is offline.</summary>
    public bool CanPlaySearchResult => _selectedSearchResult is not null;

    public bool CanStreamSearchResult => _selectedSearchResult is not null && CanStream;

    /// <summary>"Ladda ner ljud" action on the media page.</summary>
    public ICommand DownloadSearchResultAudioCommand => _searchResultAudioCommand;

    /// <summary>"Ladda ner video" action on the media page.</summary>
    public ICommand DownloadSearchResultVideoCommand => _searchResultVideoCommand;

    /// <summary>"Spela" action on the media page (offline-first, streams otherwise).</summary>
    public ICommand PlaySearchResultCommand => _playSearchResultCommand;

    /// <summary>"Strömma" action on the media page (forces the online relay).</summary>
    public ICommand StreamSearchResultCommand => _streamSearchResultCommand;

    /// <summary>
    /// Re-reads which formats of the open search result are already on disk. Called when
    /// the selection changes and whenever a library item changes state.
    /// </summary>
    private void SyncSearchOfflineState()
    {
        var result = _selectedSearchResult;
        var item = result is null ? null : Items.FirstOrDefault(i => i.Id == result.Id);
        var audio = item?.HasAudioFile == true;
        var video = item?.HasVideoFile == true;
        if (audio == _searchAudioOffline && video == _searchVideoOffline)
        {
            return;
        }

        _searchAudioOffline = audio;
        _searchVideoOffline = video;
        OnPropertyChanged(nameof(SearchAudioOffline));
        OnPropertyChanged(nameof(SearchVideoOffline));
        OnPropertyChanged(nameof(CanDownloadSearchAudio));
        OnPropertyChanged(nameof(CanDownloadSearchVideo));
        OnPropertyChanged(nameof(SearchDetailHint));
        RefreshSearchResultCommands();
    }

    /// <summary>Re-queries the info-page buttons so their enabled state tracks the gates
    /// (WinUI only re-queries on CanExecuteChanged). Call from every setter that can change
    /// a gate.</summary>
    private void RefreshSearchResultCommands()
    {
        _searchResultAudioCommand?.RefreshCanExecute();
        _searchResultVideoCommand?.RefreshCanExecute();
        _playSearchResultCommand?.RefreshCanExecute();
        _streamSearchResultCommand?.RefreshCanExecute();
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
                OnPropertyChanged(nameof(CanDownloadSearchVideo));
                RefreshSearchResultCommands();
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

        // Skip a format that is already offline or already queued for this video.
        if (Items.Any(i => i.Id == entry.Id && (kind == MediaKind.Audio ? i.HasAudioFile : i.HasVideoFile))
            || QueueEntries.Any(q => q.Id == entry.Id && q.Kind == kind
                                     && q.Status is ItemStatus.Queued or ItemStatus.Downloading))
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
            UpdateSelectedFileMissing();

            // A new selection loads its preferred offline format into the player (no
            // auto-play). Failed/queued rows don't replace the current player.
            if (value is { Status: ItemStatus.Complete } && value.HasAnyFile)
            {
                LoadItemIntoPlayer(value, value.PreferredKind, play: false);
            }

            _playSelectedCommand.RefreshCanExecute();
            _streamSelectedCommand.RefreshCanExecute();
            _openFolderSelectedCommand.RefreshCanExecute();
            _deleteSelectedCommand.RefreshCanExecute();
            _downloadSelectedCommand.RefreshCanExecute();
        }
    }

    private bool _selectedFileMissing;

    /// <summary>True when the selected item is complete but none of its files exist on disk —
    /// the player area shows a "download or stream it" prompt instead of the video.</summary>
    public bool SelectedFileMissing
    {
        get => _selectedFileMissing;
        private set
        {
            if (Set(ref _selectedFileMissing, value))
            {
                if (value)
                {
                    // A missing file can't play — clear the player so the prompt shows.
                    ClearPlayerCore();
                }
                else if (_selectedItem is { } item && item.HasAnyFile)
                {
                    // The file came back (re-download): reload it without auto-play.
                    LoadItemIntoPlayer(item, item.PreferredKind, play: false);
                }

                OnPropertyChanged(nameof(IsPlayerVisible));
                OnPropertyChanged(nameof(ShowMissingPrompt));
            }
        }
    }

    private void UpdateSelectedFileMissing()
    {
        var item = _selectedItem;
        SelectedFileMissing = item is { Status: ItemStatus.Complete } && !item.HasAnyFile;
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

    /// <summary>Explanation line on the media page: what is offline and what the buttons do.</summary>
    public string SearchDetailHint
    {
        get
        {
            if (_selectedSearchResult is null)
            {
                return string.Empty;
            }
            if (!IsRootSet)
            {
                return "Välj en biblioteksmapp för att kunna ladda ner. Du kan fortfarande strömma direkt.";
            }
            if (_searchAudioOffline && _searchVideoOffline)
            {
                return "Både MP3 och MP4 finns offline — Spela använder den lokala filen.";
            }
            if (_searchAudioOffline)
            {
                return "MP3 finns offline. Välj MP4 för att strömma eller ladda ner videon.";
            }
            if (_searchVideoOffline)
            {
                return "MP4 finns offline. Ladda ner ljudet om du vill ha MP3 också.";
            }
            return "Strömma direkt, eller ladda ner ljud/video till biblioteket.";
        }
    }

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

        if (item.Status != ItemStatus.Complete)
        {
            Error = "Objektet är inte nedladdat ännu.";
            return Task.CompletedTask;
        }

        if (!item.HasAnyFile)
        {
            // The prompt offers download and streaming; don't load an empty player.
            SelectedFileMissing = true;
            return Task.CompletedTask;
        }

        LoadItemIntoPlayer(item, item.PreferredKind, play: true);
        return Task.CompletedTask;
    }

    private Task PlaySelectedAsync() => PlayItemAsync(SelectedItem);

    /// <summary>Loads a library item's chosen format, streaming when that file is not on disk.</summary>
    private void LoadItemIntoPlayer(LibraryItem item, MediaKind kind, bool play)
    {
        SetPlayerCore(item.Title, BuildItemSubtitle(item), item.Id, kind);
        PlayingItem = item;
        item.LastPlayedKind = kind;
        _ = _store.SaveAsync();

        var path = item.PathFor(kind);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            SetPlayerSource(path, isStream: false);
            if (play)
            {
                PlaybackRequested?.Invoke();
            }
            return;
        }

        if (!CanStream)
        {
            PlayerStreamError = "Strömning stöds endast på Windows. Ladda ner filen för att spela den.";
            return;
        }

        _ = StartStreamAsync(item.Id, kind, play);
    }

    private static string? BuildItemSubtitle(LibraryItem item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(item.Uploader))
        {
            parts.Add(item.Uploader);
        }
        if (!string.IsNullOrEmpty(item.Playlist))
        {
            parts.Add(item.Playlist);
        }
        if (item.DurationSeconds is > 0)
        {
            parts.Add(item.DurationFormatted);
        }
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// Plays a YouTube result: its offline copy when one exists for the chosen format,
    /// otherwise the online HLS relay. Never requires a library folder.
    /// </summary>
    public Task PlaySearchResultAsync(SearchResult? result = null, MediaKind? kind = null)
    {
        var target = result ?? SelectedSearchResult;
        if (target is null)
        {
            return Task.CompletedTask;
        }

        var offline = Items.FirstOrDefault(i => i.Id == target.Id);
        var chosen = kind
                     ?? (offline is not null
                         ? offline.PreferredKind
                         : target.HasVideo == false ? MediaKind.Audio : MediaKind.Video);
        LoadSearchResultIntoPlayer(target, offline, chosen, play: true, forceStream: false);
        return Task.CompletedTask;
    }

    private Task PlaySelectedSearchResultAsync() => PlaySearchResultAsync(SelectedSearchResult);

    /// <summary>Forces online playback (e.g. the user has only the MP3 and wants the video).</summary>
    private Task StreamSelectedSearchResultAsync()
    {
        var result = SelectedSearchResult;
        if (result is null)
        {
            return Task.CompletedTask;
        }

        var offline = Items.FirstOrDefault(i => i.Id == result.Id);
        var kind = result.HasVideo == false ? MediaKind.Audio : MediaKind.Video;
        LoadSearchResultIntoPlayer(result, offline, kind, play: true, forceStream: true);
        return Task.CompletedTask;
    }

    private void LoadSearchResultIntoPlayer(
        SearchResult result,
        LibraryItem? offline,
        MediaKind kind,
        bool play,
        bool forceStream)
    {
        SetPlayerCore(result.Title, result.DetailText, result.Id, kind);
        PlayingItem = offline;
        if (offline is not null)
        {
            offline.LastPlayedKind = kind;
            _ = _store.SaveAsync();
        }

        var path = offline?.PathFor(kind);
        if (!forceStream && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            SetPlayerSource(path, isStream: false);
            if (play)
            {
                PlaybackRequested?.Invoke();
            }
            return;
        }

        if (!CanStream)
        {
            PlayerStreamError = "Strömning stöds endast på Windows.";
            return;
        }

        _ = StartStreamAsync(result.Id, kind, play);
    }

    /// <summary>Streams the selected library item (used by the missing-file prompt).</summary>
    private Task StreamSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return Task.CompletedTask;
        }

        var kind = item.Kind == MediaKind.Video && !item.HasAudioFile
            ? MediaKind.Video
            : item.HasVideoFile || item.Kind == MediaKind.Video ? MediaKind.Video : MediaKind.Audio;

        SetPlayerCore(item.Title, BuildItemSubtitle(item), item.Id, kind);
        PlayingItem = item;

        if (!CanStream)
        {
            PlayerStreamError = "Strömning stöds endast på Windows.";
            return Task.CompletedTask;
        }

        _ = StartStreamAsync(item.Id, kind, playWhenReady: true);
        return Task.CompletedTask;
    }

    private Task RetryStreamAsync()
    {
        if (PlayerVideoId is { } id)
        {
            _ = StartStreamAsync(id, PlayerKind, playWhenReady: true);
        }
        return Task.CompletedTask;
    }

    /// <summary>Switches the player between MP3 and MP4, streaming when that file is not offline.</summary>
    public Task SetPlayerKindAsync(MediaKind kind) => SwitchPlayerKindAsync(kind);

    /// <summary>Switches the player between MP3 and MP4, streaming when that file is not offline.</summary>
    private async Task SwitchPlayerKindAsync(MediaKind kind)
    {
        if (PlayerVideoId is not { } id)
        {
            return;
        }

        PlayerKind = kind;
        if (PlayingItem is { } item)
        {
            item.LastPlayedKind = kind;
            await _store.SaveAsync();

            var path = item.PathFor(kind);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                StopStreaming();
                SetPlayerSource(path, isStream: false);
                PlayerStreamError = null;
                PlaybackRequested?.Invoke();
                return;
            }
        }

        await StartStreamAsync(id, kind, playWhenReady: true);
    }

    private async Task StartStreamAsync(string videoId, MediaKind kind, bool playWhenReady)
    {
        if (!CanStream)
        {
            PlayerStreamError = "Strömning stöds endast på Windows.";
            return;
        }

        _streamCts?.Cancel();
        _streamCts?.Dispose();
        var cts = new CancellationTokenSource();
        _streamCts = cts;

        PlayerKind = kind;
        PlayerStreamError = null;
        PlayerSourceUri = null;
        PlayerIsStream = true;
        PlayerIsStreamLoading = true;
        PlayerStreamStatus = kind == MediaKind.Video ? "Buffrar video..." : "Buffrar ljud...";

        try
        {
            var uri = await _streamService.StartAsync(videoId, kind, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            // StartAsync resolves yt-dlp/ffmpeg on background continuations. Every bound
            // field and command refresh must run on the UI thread or WinUI throws
            // E_ACCESSDENIED (a stowed XAML exception kills the app).
            OnUi(() =>
            {
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                PlayerSourceUri = uri.ToString();
                PlayerIsStreamLoading = false;
                PlayerStreamStatus = null;
                if (playWhenReady)
                {
                    PlaybackRequested?.Invoke();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Replaced by another selection/format.
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                OnUi(() =>
                {
                    PlayerIsStreamLoading = false;
                    PlayerStreamStatus = null;
                    PlayerIsStream = false;
                    PlayerStreamError = UiErrors.FromException(ex, "Kunde inte strömma videon.");
                });
            }
        }
    }

    /// <summary>Resets the player header/source fields without touching the selection.</summary>
    private void SetPlayerCore(string title, string? subtitle, string? videoId, MediaKind kind)
    {
        StopStreaming();
        PlayerTitle = title;
        PlayerSubtitle = subtitle;
        PlayerVideoId = videoId;
        PlayerKind = kind;
        PlayerStreamError = null;
        PlayerStreamStatus = null;
        PlayerIsStreamLoading = false;
    }

    private void SetPlayerSource(string uri, bool isStream)
    {
        PlayerSourceUri = uri;
        PlayerIsStream = isStream;
    }

    /// <summary>Cancels the stream request and stops the relay; safe to call repeatedly.</summary>
    private void StopStreaming()
    {
        if (_streamCts is { } cts)
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
                // Already disposed.
            }
            cts.Dispose();
            _streamCts = null;
        }

        _ = _streamService.StopAsync();
        PlayerIsStreamLoading = false;
        PlayerStreamStatus = null;
        PlayerIsStream = false;
        PlayerSourceUri = null;
    }

    /// <summary>Clears the player (title, source and stream) but leaves the selection intact.</summary>
    private void ClearPlayerCore()
    {
        StopStreaming();
        PlayingItem = null;
        PlayerTitle = null;
        PlayerSubtitle = null;
        PlayerVideoId = null;
        PlayerStreamError = null;
    }

    /// <summary>
    /// Re-downloads the selected item into the library (used by the player's "file
    /// saknas" prompt) in its preferred format. Queues the item in the download worker.
    /// </summary>
    private Task DownloadSelectedAsync()
    {
        return DownloadSelectedAsAsync(SelectedItem?.PreferredKind ?? Kind);
    }

    /// <summary>
    /// Queues the selected item for download in a specific format (MP3 or MP4),
    /// overriding the global format toggle. The item keeps both files it already has;
    /// the download adds or replaces only the chosen format.
    /// </summary>
    public Task DownloadSelectedAsAsync(MediaKind kind)
    {
        var item = SelectedItem;
        if (item is null || string.IsNullOrEmpty(item.Id))
        {
            return Task.CompletedTask;
        }

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
            var folder = new[] { target.VideoFilePath, target.AudioFilePath, target.FilePath }
                .Select(Path.GetDirectoryName)
                .FirstOrDefault(dir => !string.IsNullOrEmpty(dir));
            _store.DeleteEmptyFolders(folder);
        }

        Log.For(typeof(LibraryViewModel)).LogInformation(
            "Tog bort {ItemId} {Title} (MP3: {Audio}, MP4: {Video}, läge: {Mode})",
            item.Id, item.Title, item.AudioFilePath, item.VideoFilePath, PreferencesService.GetDeleteMode());
        await _store.SaveAsync();

        if (ReferenceEquals(PlayingItem, item))
        {
            ClosePlayer();
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
            || previous.DurationSeconds != item.DurationSeconds
            || previous.AudioFilePath != item.AudioFilePath
            || previous.VideoFilePath != item.VideoFilePath
            || previous.ThumbnailUri != item.ThumbnailUri;

        if (affectsList)
        {
            RefreshCounts();
            RefreshFilteredItems();
            RefreshLocalSearch();
        }
        UpdateSelectedFileMissing();

        SyncSelectedSearchResultInLibrary(item.Id);
    }

    /// <summary>If <paramref name="id"/> is the result on the open media page, refresh which
    /// of its formats are already offline (the page updates its badges and buttons).
    /// Call on the UI thread.</summary>
    private void SyncSelectedSearchResultInLibrary(string id)
    {
        if (_selectedSearchResult is null || _selectedSearchResult.Id != id)
        {
            return;
        }

        SyncSearchOfflineState();
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
        // Clear the source first (stops the docked player) so closing the pop-out
        // cannot briefly resume playback that is about to be torn down.
        ClearPlayerCore();
#if WINDOWS
        ClosePopOutWindow();
#endif
    }

    /// <summary>
    /// Opens the pop-out player window for the current source (created on the UI
    /// thread). While it is open, the docked player is hidden via
    /// <see cref="PlayerIsPopOut"/>; closing the window restores the dock.
    /// No-op on non-Windows (Skia) targets, where pop-out is not available.
    /// </summary>
    private void PopOutPlayer()
    {
#if WINDOWS
        var title = PlayerTitle;
        var source = PlayerSourceUri;
        if (title is null || source is null || _playerPopOutOpen)
        {
            return;
        }

        var isVideo = PlayerKind == MediaKind.Video;
        var window = new PlayerPopOutWindow(title, source);
        window.ClosedChanged += OnPopOutWindowClosed;
        _popOutWindow = window;
        _playerPopOutOpen = true;
        OnPropertyChanged(nameof(PlayerIsPopOut));
        OnPropertyChanged(nameof(IsDockedPlayerVisible));
        _popOutPlayerCommand.RefreshCanExecute();
        window.Activate();
        window.Configure(isVideo ? 860 : 520, isVideo ? 560 : 160);
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
        var target = new[] { item?.VideoFilePath, item?.AudioFilePath, item?.FilePath }
            .FirstOrDefault(path => !string.IsNullOrEmpty(path) && File.Exists(path));
        if (!string.IsNullOrEmpty(target))
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
            RefreshCounts();
            RefreshFilteredItems();
            RefreshLocalSearch();
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
            if (FilterIndex == 1 && !item.HasAudio)
            {
                continue;
            }
            if (FilterIndex == 2 && !item.HasVideo)
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
