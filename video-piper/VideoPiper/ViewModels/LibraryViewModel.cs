using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    YouTubeSearch,
}

public sealed class LibraryViewModel : INotifyPropertyChanged
{
    private LibraryStore _store = new(string.Empty);
    private string _rootPath = string.Empty;
    private string _url = string.Empty;
    private string _searchQuery = string.Empty;
    private string _localSearchQuery = string.Empty;
    private MediaKind _kind = MediaKind.Audio;
    private LibrarySidebarSection _activeSection = LibrarySidebarSection.AllMedia;
    private bool _isDownloading;
    private bool _isFetching;
    private bool _isSearching;
    private bool _hasSearchResults;
    private bool _isUrlInput;
    private int _filterIndex; // 0 = Alla, 1 = Ljud, 2 = Video
    private string? _jobTitle;
    private double _jobPercent;
    private string? _jobPosition;
    private string? _error;
    private LibraryItem? _playingItem;
    private LibraryItem? _selectedItem;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<LibraryItem> Items { get; } = new();
    public ObservableCollection<LibraryItem> FilteredItems { get; } = new();
    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    private readonly RelayCommand _browseCommand;
    private readonly RelayCommand _downloadCommand;
    private readonly RelayCommand _stopDownloadCommand;
    private readonly RelayCommand _playSelectedCommand;
    private readonly RelayCommand _removeSelectedCommand;
    private readonly RelayCommand _deleteWithFileSelectedCommand;
    private readonly RelayCommand _deselectCommand;
    private readonly RelayCommand _resyncCommand;
    private readonly ParameterizedRelayCommand _playItemCommand;
    private readonly ParameterizedRelayCommand _openFolderItemCommand;
    private readonly ParameterizedRelayCommand _deleteItemCommand;
    private readonly ParameterizedRelayCommand _downloadSearchResultCommand;
    private readonly RelayCommand _pasteCommand;
    private readonly RelayCommand _closePlayerCommand;
    private readonly RelayCommand _openFolderSelectedCommand;
    private readonly RelayCommand _openRootFolderCommand;
    private readonly RelayCommand _clearSearchCommand;
    private readonly RelayCommand _clearErrorCommand;
    private readonly ParameterizedRelayCommand _selectSectionCommand;
    private readonly RelayCommand _executeSearchCommand;

    public ICommand BrowseCommand => _browseCommand;
    public ICommand DownloadCommand => _downloadCommand;
    public ICommand StopDownloadCommand => _stopDownloadCommand;
    public ICommand PlaySelectedCommand => _playSelectedCommand;
    public ICommand RemoveSelectedCommand => _removeSelectedCommand;
    public ICommand DeleteWithFileSelectedCommand => _deleteWithFileSelectedCommand;
    public ICommand DeselectCommand => _deselectCommand;
    public ICommand ResyncCommand => _resyncCommand;
    public ICommand PlayItemCommand => _playItemCommand;
    public ICommand OpenFolderItemCommand => _openFolderItemCommand;
    public ICommand DeleteItemCommand => _deleteItemCommand;
    public ICommand DownloadSearchResultCommand => _downloadSearchResultCommand;
    public ICommand PasteCommand => _pasteCommand;
    public ICommand ClosePlayerCommand => _closePlayerCommand;
    public ICommand OpenFolderSelectedCommand => _openFolderSelectedCommand;
    public ICommand OpenRootFolderCommand => _openRootFolderCommand;
    public ICommand ClearSearchCommand => _clearSearchCommand;
    public ICommand ClearErrorCommand => _clearErrorCommand;
    public ICommand SelectSectionCommand => _selectSectionCommand;
    public ICommand ExecuteSearchCommand => _executeSearchCommand;

    public LibraryViewModel()
    {
        _browseCommand = new RelayCommand(BrowseAsync, () => !IsDownloading);
        _downloadCommand = new RelayCommand(DownloadAsync, () => CanDownload);
        _stopDownloadCommand = new RelayCommand(() => { _downloadCts?.Cancel(); return Task.CompletedTask; }, () => IsBusy);
        _playSelectedCommand = new RelayCommand(PlaySelectedAsync, () => SelectedItem is { Status: ItemStatus.Complete } && !string.IsNullOrEmpty(SelectedItem.FilePath));
        _removeSelectedCommand = new RelayCommand(RemoveSelectedAsync, () => SelectedItem is not null);
        _deleteWithFileSelectedCommand = new RelayCommand(DeleteWithFileSelectedAsync, () => SelectedItem is not null);
        _deselectCommand = new RelayCommand(() => { SelectedItem = null; return Task.CompletedTask; });
        _openFolderSelectedCommand = new RelayCommand(OpenFolderSelectedAsync, () => SelectedItem is not null);
        _openRootFolderCommand = new RelayCommand(OpenRootFolder);
        _resyncCommand = new RelayCommand(ResyncAsync, () => !IsDownloading);

        _playItemCommand = new ParameterizedRelayCommand(p => PlayItemAsync(p as LibraryItem));
        _openFolderItemCommand = new ParameterizedRelayCommand(p => OpenFolderItem(p as LibraryItem));
        _deleteItemCommand = new ParameterizedRelayCommand(p => DeleteItemAsync(p as LibraryItem));
        _downloadSearchResultCommand = new ParameterizedRelayCommand(DownloadSearchResultAsync, p => p is SearchResult && !IsBusy);

        _pasteCommand = new RelayCommand(PasteAsync, () => !IsBusy);
        _closePlayerCommand = new RelayCommand(ClosePlayer);
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
    }

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
                    _ => 0,
                };
                OnPropertyChanged(nameof(IsAllActive));
                OnPropertyChanged(nameof(IsAudioActive));
                OnPropertyChanged(nameof(IsVideoActive));
                OnPropertyChanged(nameof(IsSearchActive));
                OnPropertyChanged(nameof(IsCollectionActive));
                OnPropertyChanged(nameof(SectionTitle));
            }
        }
    }

    public bool IsAllActive => _activeSection == LibrarySidebarSection.AllMedia;
    public bool IsAudioActive => _activeSection == LibrarySidebarSection.AudioOnly;
    public bool IsVideoActive => _activeSection == LibrarySidebarSection.VideoOnly;
    public bool IsSearchActive => _activeSection == LibrarySidebarSection.YouTubeSearch;
    public bool IsCollectionActive => _activeSection != LibrarySidebarSection.YouTubeSearch;

    public string SectionTitle => _activeSection switch
    {
        LibrarySidebarSection.AudioOnly => $"Ljudfiler ({AudioCount})",
        LibrarySidebarSection.VideoOnly => $"Videofiler ({VideoCount})",
        LibrarySidebarSection.YouTubeSearch => "YouTube-sökning",
        _ => $"Alla filer ({TotalCount})",
    };

    /// <summary>Unified search/URL box text. If a URL is entered, it enables download mode; otherwise debounces YouTube search.</summary>
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
                Url = trimmed;
                IsUrlInput = true;
                return;
            }

            IsUrlInput = false;
            Url = string.Empty;

            // Debounce: cancel any in-flight/pending search and start a fresh one.
            _searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _searchCts = cts;
            _ = RunSearchAsync(value, cts.Token);
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
            }
        }
    }

    public bool IsAllFilterSelected => _filterIndex == 0;
    public bool IsAudioFilterSelected => _filterIndex == 1;
    public bool IsVideoFilterSelected => _filterIndex == 2;

    public int TotalCount => Items.Count;
    public int AudioCount => Items.Count(i => i.Kind == MediaKind.Audio);
    public int VideoCount => Items.Count(i => i.Kind == MediaKind.Video);

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (Set(ref _isSearching, value))
            {
                _downloadSearchResultCommand.RefreshCanExecute();
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
            Error = null;
            IsSearching = true;
            HasSearchResults = true;
        });

        try
        {
            var results = await SearchService.SearchAsync(trimmed, cts.Token);
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
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                OnUi(() => Error = $"Sökningen misslyckades: {ex.Message}");
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

    private async Task RunSearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 2)
        {
            OnUi(() =>
            {
                SearchResults.Clear();
                HasSearchResults = false;
            });
            return;
        }

        try
        {
            await Task.Delay(400, cancellationToken); // debounce window

            OnUi(() =>
            {
                IsSearching = true;
                HasSearchResults = true;
            });

            var results = await SearchService.SearchAsync(trimmed, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
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
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer query — nothing to do.
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                OnUi(() => Error = $"Sökningen misslyckades: {ex.Message}");
            }
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                OnUi(() => IsSearching = false);
            }
        }
    }

    private async Task DownloadSearchResultAsync(object? parameter)
    {
        if (parameter is not SearchResult result)
        {
            return;
        }

        var entry = new MediaEntry(
            Id: result.Id,
            Title: result.Title,
            Uploader: result.Uploader,
            DurationSeconds: result.DurationSeconds,
            Url: $"https://www.youtube.com/watch?v={result.Id}");

        // Switch to Collection view so user sees their new download appearing with progress
        ActiveSection = LibrarySidebarSection.AllMedia;

        await DownloadManyCoreAsync(new[] { entry }, playlistTitle: null);
    }

    /// <summary>The currently selected item in the list (two-way bound to the ListView).</summary>
    public LibraryItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!ReferenceEquals(_selectedItem, value))
            {
                Set(ref _selectedItem, value);
                OnPropertyChanged(nameof(IsItemSelected));
                _playSelectedCommand.RefreshCanExecute();
                _openFolderSelectedCommand.RefreshCanExecute();
                _removeSelectedCommand.RefreshCanExecute();
                _deleteWithFileSelectedCommand.RefreshCanExecute();
            }
        }
    }

    public bool IsItemSelected => _selectedItem is not null;
    public bool HasItems => Items.Count > 0;
    public bool HasFilteredItems => FilteredItems.Count > 0;
    public bool ShowEmptyState => FilteredItems.Count == 0 && !HasSearchResults && !IsBusy;

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
                _downloadCommand.RefreshCanExecute();
            }
        }
    }

    public string RootPathDisplay => string.IsNullOrWhiteSpace(_rootPath) ? "Välj biblioteksmapp..." : _rootPath;
    public bool IsRootSet => !string.IsNullOrWhiteSpace(_rootPath);

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

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (Set(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(IsBusy));
                _downloadCommand.RefreshCanExecute();
                _browseCommand.RefreshCanExecute();
                _stopDownloadCommand.RefreshCanExecute();
                _resyncCommand.RefreshCanExecute();
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
                _downloadCommand.RefreshCanExecute();
                _browseCommand.RefreshCanExecute();
                _stopDownloadCommand.RefreshCanExecute();
                _resyncCommand.RefreshCanExecute();
            }
        }
    }

    /// <summary>True while resolving metadata or downloading — the primary busy state.</summary>
    public bool IsBusy => IsFetching || IsDownloading;

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
            }
        }
    }

    public bool IsPlayerVisible => _playingItem is not null;

    public bool CanDownload =>
        !IsBusy &&
        IsRootSet &&
        ((!string.IsNullOrWhiteSpace(Url) && Url.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase)) ||
         IsYouTubeUrl(SearchQuery));

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
        catch
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
        _downloadCts = new CancellationTokenSource();
        IsFetching = true;
        JobTitle = "Hämtar metadata...";
        JobPercent = 0;
        JobPosition = null;

        try
        {
            var (entries, playlistTitle) = await LibraryDownloadService.FetchEntriesAsync(targetUrl, _downloadCts.Token);
            if (entries.Count == 0)
            {
                Error = "Inga objekt hittades i länken.";
                return;
            }

            // Clear search box so user sees library list with newly added downloading items
            ClearSearch();

            IsFetching = false;
            await DownloadManyCoreAsync(entries, playlistTitle);
        }
        catch (OperationCanceledException)
        {
            Error = "Nedladdningen avbröts.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsFetching = false;
        }
    }

    /// <summary>Downloads a resolved set of entries into the library, tracking per-item progress.</summary>
    private async Task DownloadManyCoreAsync(IReadOnlyList<MediaEntry> entries, string? playlistTitle)
    {
        Error = null;
        IsDownloading = true;
        _downloadCts = new CancellationTokenSource();

        try
        {
            await LibraryDownloadService.DownloadManyAsync(
                _store,
                entries,
                playlistTitle,
                Kind,
                item => OnUi(() =>
                {
                    UpsertItem(item);
                    JobTitle = item.Title;
                    JobPercent = item.Percent ?? 0;
                }),
                (current, total) => OnUi(() => JobPosition = $"{current}/{total}"),
                _downloadCts.Token);
        }
        catch (OperationCanceledException)
        {
            Error = "Nedladdningen avbröts.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsDownloading = false;
            JobTitle = null;
            JobPercent = 0;
            JobPosition = null;
        }
    }

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

        SelectedItem = item;
        PlayingItem = item;
        return Task.CompletedTask;
    }

    private async Task PlaySelectedAsync()
    {
        await PlayItemAsync(SelectedItem);
    }

    private async Task RemoveSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        _store.Remove(item.Id, deleteFile: false);
        await _store.SaveAsync();
        SelectedItem = null;
        RefreshItems();
    }

    public async Task DeleteItemAsync(LibraryItem? item)
    {
        if (item is null)
        {
            return;
        }

        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Ta bort ur biblioteket?",
            Content = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = $"Vill du radera \"{item.Title}\" från hårddisken eller bara ta bort posten ur biblioteket?",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            },
            PrimaryButtonText = "Radera fil från disk",
            SecondaryButtonText = "Bara ur biblioteket",
            CloseButtonText = "Avbryt",
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
            XamlRoot = App.MainWindowInstance?.Content?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.None)
        {
            return;
        }

        var deleteFile = result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary;
        _store.Remove(item.Id, deleteFile);
        await _store.SaveAsync();

        if (ReferenceEquals(SelectedItem, item))
        {
            SelectedItem = null;
        }
        if (ReferenceEquals(PlayingItem, item))
        {
            PlayingItem = null;
        }

        RefreshItems();
    }

    private async Task DeleteWithFileSelectedAsync()
    {
        await DeleteItemAsync(SelectedItem);
    }

    private async Task ResyncAsync()
    {
        var changed = _store.SyncWithDisk();
        await _store.SaveAsync();
        RefreshItems();
        if (changed > 0)
        {
            Error = $"{changed} objekt saknade fil på disk och markerades som felaktiga.";
        }
    }

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

        if (index >= 0)
        {
            Items[index] = item;
        }
        else
        {
            Items.Insert(0, item);
        }

        RefreshFilteredItems();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(AudioCount));
        OnPropertyChanged(nameof(VideoCount));
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
        PlayingItem = null;
    }

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
            RefreshFilteredItems();
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(AudioCount));
            OnPropertyChanged(nameof(VideoCount));
        });
    }

    public void RefreshFilteredItems()
    {
        OnUi(() =>
        {
            FilteredItems.Clear();
            var query = _localSearchQuery.Trim();
            var hasQuery = !string.IsNullOrEmpty(query);

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

                FilteredItems.Add(item);
            }

            OnPropertyChanged(nameof(HasFilteredItems));
            OnPropertyChanged(nameof(ShowEmptyState));
        });
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
