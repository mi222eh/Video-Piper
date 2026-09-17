using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoPiper.Models;
using VideoPiper.Services;

namespace VideoPiper.ViewModels;

/// <summary>
/// View model for the library tab: browsing the root, downloading media (audio or video)
/// into the library, and managing items (play local file, remove, delete).
/// </summary>
public sealed class LibraryViewModel : INotifyPropertyChanged
{
    private LibraryStore _store = new(string.Empty);
    private string _rootPath = string.Empty;
    private string _url = string.Empty;
    private MediaKind _kind = MediaKind.Audio;
    private bool _isDownloading;
    private bool _isFetching;
    private string? _jobTitle;
    private double _jobPercent;
    private string? _jobPosition;
    private string? _error;
    private LibraryItem? _playingItem;
    private LibraryItem? _selectedItem;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<LibraryItem> Items { get; } = new();

    /// <summary>Search results (empty when no search has run or the query was cleared).</summary>
    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    private readonly RelayCommand _browseCommand;
    private readonly RelayCommand _downloadCommand;
    private readonly RelayCommand _stopDownloadCommand;
    private readonly RelayCommand _playSelectedCommand;
    private readonly RelayCommand _removeSelectedCommand;
    private readonly RelayCommand _deleteWithFileSelectedCommand;
    private readonly RelayCommand _resyncCommand;
    private readonly ParameterizedRelayCommand _downloadSearchResultCommand;

    public ICommand BrowseCommand => _browseCommand;
    public ICommand DownloadCommand => _downloadCommand;
    public ICommand StopDownloadCommand => _stopDownloadCommand;
    public ICommand PlaySelectedCommand => _playSelectedCommand;
    public ICommand RemoveSelectedCommand => _removeSelectedCommand;
    public ICommand DeleteWithFileSelectedCommand => _deleteWithFileSelectedCommand;
    public ICommand ResyncCommand => _resyncCommand;
    public ICommand DownloadSearchResultCommand => _downloadSearchResultCommand;

    public LibraryViewModel()
    {
        _browseCommand = new RelayCommand(BrowseAsync, () => !IsDownloading);
        _downloadCommand = new RelayCommand(DownloadAsync, () => CanDownload);
        _stopDownloadCommand = new RelayCommand(() => { _downloadCts?.Cancel(); return Task.CompletedTask; }, () => IsDownloading);
        _playSelectedCommand = new RelayCommand(PlaySelectedAsync, () => SelectedItem is { Status: ItemStatus.Complete } && !string.IsNullOrEmpty(SelectedItem.FilePath));
        _removeSelectedCommand = new RelayCommand(RemoveSelectedAsync, () => SelectedItem is not null);
        _deleteWithFileSelectedCommand = new RelayCommand(DeleteWithFileSelectedAsync, () => SelectedItem is not null);
        _resyncCommand = new RelayCommand(ResyncAsync, () => !IsDownloading);
        _downloadSearchResultCommand = new ParameterizedRelayCommand(DownloadSearchResultAsync, p => p is SearchResult && !IsBusy);
    }

    private string _searchQuery = string.Empty;
    private bool _isSearching;

    /// <summary>Debounced search box text — typing triggers a YouTube search after a short pause.</summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!Set(ref _searchQuery, value))
            {
                return;
            }

            // Debounce: cancel any in-flight/pending search and start a fresh one.
            _searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _searchCts = cts;
            _ = RunSearchAsync(value, cts.Token);
        }
    }

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
            }
        }
    }

    private bool _hasSearchResults;

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

            IsSearching = true;
            HasSearchResults = true;
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
                IsSearching = false;
            }
        }
    }

    private async Task DownloadSearchResultAsync(object? parameter)
    {
        if (parameter is not SearchResult result)
        {
            return;
        }

        // Route through the normal download pipeline with a single synthesized entry.
        var entry = new MediaEntry(
            Id: result.Id,
            Title: result.Title,
            Uploader: result.Uploader,
            DurationSeconds: result.DurationSeconds,
            Url: $"https://www.youtube.com/watch?v={result.Id}");

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
                _removeSelectedCommand.RefreshCanExecute();
                _deleteWithFileSelectedCommand.RefreshCanExecute();
            }
        }
    }

    public bool IsItemSelected => _selectedItem is not null;

    public string RootPath
    {
        get => _rootPath;
        private set => Set(ref _rootPath, value);
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
                OnPropertyChanged(nameof(IsAudioSelected));
            }
        }
    }

    public bool IsAudioSelected => _kind == MediaKind.Audio;

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

    public bool CanDownload => !IsBusy && IsRootSet && !string.IsNullOrWhiteSpace(Url) && Url.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        var root = PreferencesService.GetLibraryRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
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
        Error = null;
        IsFetching = true;
        JobTitle = "Hämtar metadata...";
        JobPercent = 0;
        JobPosition = null;

        try
        {
            var url = Url.Trim();
            var (entries, playlistTitle) = await LibraryDownloadService.FetchEntriesAsync(url, CancellationToken.None);
            if (entries.Count == 0)
            {
                Error = "Inga objekt hittades i länken.";
                return;
            }

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

    private async Task PlaySelectedAsync()
    {
        var item = SelectedItem;
        if (item is null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
        {
            return;
        }

        PlayingItem = item;
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

    private async Task DeleteWithFileSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Ta bort ur biblioteket?",
            Content = new Microsoft.UI.Xaml.Controls.TextBlock { Text = $"\"{item.Title}\" tas bort och filen raderas." },
            CloseButtonText = "Avbryt",
            PrimaryButtonText = "Radera",
        };

        var result = await dialog.ShowAsync();
        if (result is not Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            return;
        }

        _store.Remove(item.Id, deleteFile: true);
        await _store.SaveAsync();
        SelectedItem = null;
        if (ReferenceEquals(PlayingItem, item))
        {
            PlayingItem = null;
        }

        RefreshItems();
    }

    private async Task ResyncAsync()
    {
        var changed = _store.SyncWithDisk();
        await _store.SaveAsync();
        RefreshItems();
        if (changed > 0)
        {
            Error = $"{changed} objekt(e) saknar fil på disk.";
        }
    }

    private void UpsertItem(LibraryItem item)
    {
        var index = Items.IndexOf(item);
        if (index >= 0)
        {
            Items[index] = item;
        }
        else
        {
            Items.Add(item);
        }
    }

    private void RefreshItems()
    {
        OnUi(() =>
        {
            Items.Clear();
            foreach (var item in _store.Items.OrderBy(i => i.AddedUtc))
            {
                Items.Add(item);
            }
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
