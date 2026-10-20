using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VideoPiper.Models;
using VideoPiper.Services;
using VideoPiper.ViewModels;

namespace VideoPiper;

public sealed partial class MainPage : Page
{
    private readonly Grid _simpleView;
    private readonly Grid _libraryView;
    private readonly Grid _settingsView;
    private readonly ShellViewModel _shell;
    private readonly MainViewModel _simpleVm;
    private readonly LibraryViewModel _libraryVm;
    private readonly SettingsViewModel _settingsVm;

    public MainPage()
    {
        _shell = new ShellViewModel();
        // View models are registered as singletons in App.OnLaunched (Uno Hosting) and
        // resolved here from the shared provider (the app uses frame navigation, not
        // Uno.Extensions Navigation, so the page pulls them from the container).
        _simpleVm = App.Host.Services.GetRequiredService<MainViewModel>();
        _libraryVm = App.Host.Services.GetRequiredService<LibraryViewModel>();
        _settingsVm = App.Host.Services.GetRequiredService<SettingsViewModel>();

        _simpleView = BuildSimpleView(_simpleVm);
        _libraryView = BuildLibraryView(_libraryVm);
        _settingsView = BuildSettingsView(_settingsVm);

        var viewHost = new Grid()
            .Children(
                _simpleView,
                _libraryView,
                _settingsView
            );

        var root = new Grid()
            .RowDefinitions(
                new RowDefinition { Height = GridLength.Auto }, // App bar
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // Mode views
            )
            .Children(
                BuildAppBar(_shell, _simpleVm, _libraryVm).Grid(row: 0),
                viewHost.Grid(row: 1)
            );

        this
            .Background(ThemeResource.Get<Brush>("VpPageBackgroundBrush"))
            .Content(root);

        SyncModeViews();
        _shell.PropertyChanged += OnShellPropertyChanged;

        _ = _simpleVm.InitializeAsync();
        _ = _libraryVm.InitializeAsync();
        _ = _settingsVm.InitializeAsync();
    }

    private void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsSimple) or nameof(ShellViewModel.IsLibrary) or nameof(ShellViewModel.SettingsOpen))
        {
            SyncModeViews();
        }
    }

    private void SyncModeViews()
    {
        if (_shell.SettingsOpen)
        {
            _simpleView.Visibility = Visibility.Collapsed;
            _libraryView.Visibility = Visibility.Collapsed;
            _settingsView.Visibility = Visibility.Visible;
            return;
        }

        var showSimple = _shell.IsSimple;
        _simpleView.Visibility = showSimple ? Visibility.Visible : Visibility.Collapsed;
        _libraryView.Visibility = showSimple ? Visibility.Collapsed : Visibility.Visible;
        _settingsView.Visibility = Visibility.Collapsed;
    }

    /// <summary>Attaches a tooltip. Icon-only controls must explain themselves on hover.</summary>
    private static T WithTip<T>(T element, string text) where T : DependencyObject
    {
        ToolTipService.SetToolTip(element, new ToolTip { Content = text });
        return element;
    }

    /// <summary>
    /// Confirms an irreversible delete (delete-mode "Ta bort filer" removes the media on
    /// disk). Windows shows a ContentDialog with the concrete consequence; other targets
    /// proceed without a dialog. Keep-files mode never asks — the media survives the delete.
    /// </summary>
    private static async Task<bool> ConfirmDeleteAsync(string title)
    {
#if WINDOWS
        var dialog = new ContentDialog
        {
            Title = "Ta bort från biblioteket?",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock
                    {
                        Text = "Filen raderas från datorn och posten tas bort ur biblioteket. Det går inte att ångra.",
                        FontSize = 12,
                        Foreground = BrushFromResource("TextFillColorSecondaryBrush", new SolidColorBrush(Colors.Gray)),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            PrimaryButtonText = "Ta bort",
            SecondaryButtonText = "Avbryt"
        };
        dialog.XamlRoot = (App.MainWindowInstance?.Content as UIElement)?.XamlRoot;
        return (await dialog.ShowAsync()) == ContentDialogResult.Primary;
#else
        return true;
#endif
    }

    private static Grid BuildSimpleView(MainViewModel vm)
    {
        return new Grid()
            .DataContext(vm)
            .Padding(new Thickness(24, 8, 24, 16))
            .RowDefinitions(
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // Content
            )
            .Children(
                new ScrollViewer()
                    .Grid(row: 0)
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                    .Content(
                        new Grid()
                            .MaxWidth(520)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Top)
                            .Padding(new Thickness(0, 24, 0, 24))
                            .RowSpacing(14)
                            .RowDefinitions(
                                new RowDefinition { Height = GridLength.Auto }, // Missing Tools Card
                                new RowDefinition { Height = GridLength.Auto }, // Error Card
                                new RowDefinition { Height = GridLength.Auto }, // Link Field
                                new RowDefinition { Height = GridLength.Auto }, // Save Path Field
                                new RowDefinition { Height = GridLength.Auto }, // Format Toggle (MP3/MP4)
                                new RowDefinition { Height = GridLength.Auto }, // Progress Card
                                new RowDefinition { Height = GridLength.Auto }  // Download Action Button
                            )
                            .Children(
                                BuildMissingToolsCard(vm).Grid(row: 0),
                                BuildErrorCard(vm).Grid(row: 1),
                                BuildLinkField(vm).Grid(row: 2),
                                BuildSavePathField(vm).Grid(row: 3),
                                BuildSimpleFormatToggle(vm).Grid(row: 4),
                                BuildProgressCard(vm).Grid(row: 5),
                                BuildDownloadButton(vm).Grid(row: 6)
                            )
                    )
            );
    }

    private static Grid BuildLibraryView(LibraryViewModel vm)
    {
        var rootGrid = new Grid()
            .DataContext(vm)
            .ColumnDefinitions(
                new ColumnDefinition { Width = new GridLength(230) }, // Left Sidebar Menu Bar
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } // Main Content Area
            );

        rootGrid.Children(
            BuildLibrarySidebar(vm).Grid(column: 0),
            BuildLibraryMainContent(vm).Grid(column: 1)
        );

        return rootGrid;
    }

    private static Border BuildLibrarySidebar(LibraryViewModel vm)
    {
        return new Border()
            .Background(ThemeResource.Get<Brush>("LayerFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(0, 0, 1, 0))
            .Child(
                new Grid()
                    .Padding(new Thickness(12, 16, 12, 16))
                    .RowDefinitions(
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Nav Menu
                        new RowDefinition { Height = GridLength.Auto }  // Folder & status
                    )
                    .Children(
                        // Menu items
                        new StackPanel()
                            .Grid(row: 0)
                            .Spacing(4)
                            .Children(
                                // Section header: UTFORSKA
                                new TextBlock()
                                    .Text("UTFORSKA")
                                    .FontSize(11)
                                    .FontWeight(FontWeights.SemiBold)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush"))
                                    .Margin(new Thickness(8, 6, 0, 4)),

                                // Item: YouTube-sökning
                                BuildSidebarItem(vm, "YouTube-sökning", "\uE721", LibrarySidebarSection.YouTubeSearch, null),

                                // Section header: DITT BIBLIOTEK
                                new TextBlock()
                                    .Text("DITT BIBLIOTEK")
                                    .FontSize(11)
                                    .FontWeight(FontWeights.SemiBold)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush"))
                                    .Margin(new Thickness(8, 16, 0, 4)),

                                // Item: Alla filer
                                BuildSidebarItem(vm, "Alla filer", "\uE8B7", LibrarySidebarSection.AllMedia, () => vm.TotalCount.ToString()),

                                // Item: Ljud (MP3)
                                BuildSidebarItem(vm, "Ljudfiler", "\uE8D6", LibrarySidebarSection.AudioOnly, () => vm.AudioCount.ToString()),

                                // Item: Video (MP4)
                                BuildSidebarItem(vm, "Videofiler", "\uE714", LibrarySidebarSection.VideoOnly, () => vm.VideoCount.ToString()),

                                // Item: Misslyckades (retry failed)
                                BuildSidebarItem(vm, "Misslyckades", "\uE783", LibrarySidebarSection.FailedOnly, () => vm.FailedCount.ToString())
                            ),

                        // Folder & Sync Box (Bottom of sidebar)
                        BuildSidebarFolderBox(vm).Grid(row: 1)
                    )
            );
    }

    private static Button BuildSidebarItem(LibraryViewModel vm, string label, string glyph, LibrarySidebarSection section, Func<string>? countFunc)
    {
        var icon = new FontIcon().Glyph(glyph).FontSize(14).VerticalAlignment(VerticalAlignment.Center);
        var text = new TextBlock().Text(label).FontSize(13).VerticalAlignment(VerticalAlignment.Center);

        var contentGrid = new Grid()
            .ColumnDefinitions(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            );

        var left = new StackPanel().Orientation(Orientation.Horizontal).Spacing(10).VerticalAlignment(VerticalAlignment.Center)
            .Children(icon, text);
        contentGrid.Children(left.Grid(column: 0));

        if (countFunc is not null)
        {
            var badge = new Border()
                .Grid(column: 1)
                .Padding(new Thickness(6, 2, 6, 2))
                .CornerRadius(new CornerRadius(6))
                .Background(ThemeResource.Get<Brush>("SubtleFillColorSecondaryBrush"))
                .VerticalAlignment(VerticalAlignment.Center)
                .Child(
                    new TextBlock()
                        .Text(x => x.Binding(() => vm.TotalCount).Convert(_ => countFunc()))
                        .FontSize(11)
                        .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                );
            contentGrid.Children(badge);
        }

        var btn = new Button()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .Padding(new Thickness(10, 8, 10, 8))
            .CornerRadius(new CornerRadius(8))
            .Content(contentGrid);

        btn.Click += (_, _) => vm.ActiveSection = section;

        return btn;
    }

    private static Border BuildSidebarFolderBox(LibraryViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(10, 8, 10, 8))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(6)
                    .Children(
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                            .Children(
                                new FontIcon().Glyph("\uED25").FontSize(12).Foreground(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush")),
                                new TextBlock().Text("Biblioteksmapp").FontSize(11).FontWeight(FontWeights.SemiBold)
                            ),
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.RootPathDisplay))
                            .FontSize(10)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                            .TextTrimming(TextTrimming.CharacterEllipsis),
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(4)
                            .Children(
                                new Button()
                                    .Padding(new Thickness(6, 3, 6, 3))
                                    .CornerRadius(new CornerRadius(4))
                                    .Command(x => x.Binding(() => vm.BrowseCommand))
                                    .Content(new TextBlock().Text("Byt").FontSize(10)),
                                new Button()
                                    .Padding(new Thickness(6, 3, 6, 3))
                                    .CornerRadius(new CornerRadius(4))
                                    .Command(x => x.Binding(() => vm.OpenRootFolderCommand))
                                    .Content(new TextBlock().Text("Öppna").FontSize(10)),
                                WithTip(
                                    new Button()
                                        .Padding(new Thickness(6, 3, 6, 3))
                                        .CornerRadius(new CornerRadius(4))
                                        .Command(x => x.Binding(() => vm.ResyncCommand))
                                        .Content(new FontIcon().Glyph("\uE895").FontSize(10)),
                                    "Synchronisera biblioteket")
                            )
                    )
            );
    }

    private static Grid BuildLibraryMainContent(LibraryViewModel vm)
    {
        return new Grid()
            .Padding(new Thickness(16, 16, 16, 16))
            .RowDefinitions(
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Main content (hero)
                new RowDefinition { Height = GridLength.Auto }                       // Band: player + queue
            )
            .RowSpacing(12)
            .Children(
                // Main content: the "browse" area — search box on top, then the section
                // header and the list (collection items or search results), or the video
                // info page when a search result is selected. Full width.
                new Grid()
                    .Grid(row: 0)
                    .RowDefinitions(
                        new RowDefinition { Height = GridLength.Auto },                       // Search box
                        new RowDefinition { Height = GridLength.Auto },                       // Section header
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }   // List / detail
                    )
                    .RowSpacing(12)
                    .Children(
                        BuildSearchBoxRow(vm).Grid(row: 0),
                        new Grid()
                            .Grid(row: 1)
                            .Children(
                                BuildSearchHeader(vm),
                                BuildCollectionHeader(vm)
                            ),
                        new Grid()
                            .Grid(row: 2)
                            .Children(
                                BuildSearchResultsArea(vm),
                                BuildCollectionListArea(vm),
                                BuildSearchDetailPage(vm)
                            )
                    ),

                // Band beneath the main content: the player dock (only while an item is
                // loaded/playing or its file is missing), the selected-item action bar,
                // the status cards and the download queue.
                new Grid()
                    .Grid(row: 1)
                    .RowDefinitions(
                        new RowDefinition { Height = GridLength.Auto }, // Player
                        new RowDefinition { Height = GridLength.Auto }, // Selected item action bar
                        new RowDefinition { Height = GridLength.Auto }, // Status cards
                        new RowDefinition { Height = GridLength.Auto }  // Download queue
                    )
                    .RowSpacing(12)
                    .Children(
                        BuildPlayerDock(vm).Grid(row: 0),
                        BuildSelectedItemBar(vm).Grid(row: 1),
                        new StackPanel()
                            .Grid(row: 2)
                            .Spacing(8)
                            .Children(
                                BuildLibraryErrorCard(vm),
                                BuildLibraryProgressCard(vm)
                            ),
                        BuildQueuePanel(vm).Grid(row: 3)
                    )
            );
    }

    private static UIElement BuildSearchBoxRow(LibraryViewModel vm)
    {
        var searchBox = new TextBox()
            .PlaceholderText("Sök på YouTube...")
            .FontSize(13)
            .Padding(new Thickness(12, 9, 12, 9))
            .CornerRadius(new CornerRadius(8))
            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b));

        // Update query as user types
        searchBox.TextChanged += (s, _) =>
        {
            if (s is TextBox tb && tb.Text != vm.SearchQuery)
            {
                vm.SearchQuery = tb.Text;
            }
        };

        // Immediate search on Enter key
        searchBox.KeyUp += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ = vm.RunSearchNowAsync();
            }
        };

        var searchRow = new Grid()
            .ColumnDefinitions(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // Input
                new ColumnDefinition { Width = GridLength.Auto },                     // Sök / Ladda ner
                new ColumnDefinition { Width = GridLength.Auto },                     // Format (MP3/MP4)
                new ColumnDefinition { Width = GridLength.Auto }                      // Klistra in
            )
            .ColumnSpacing(6)
            .Children(
                searchBox.Grid(column: 0),

                // Search / download button — label reflects whether the box holds a URL or a query
                new Button()
                    .Grid(column: 1)
                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                    .Padding(new Thickness(12, 9, 12, 9))
                    .CornerRadius(new CornerRadius(8))
                    .Command(x => x.Binding(() => vm.ExecuteSearchCommand))
                    .Content(
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                            .Children(
                                new FontIcon().Glyph(x => x.Binding(() => vm.IsUrlInput).Convert(url => url ? "\uE896" : "\uE721")).FontSize(13),
                                new TextBlock().Text(x => x.Binding(() => vm.IsUrlInput).Convert(url => url ? "Ladda ner" : "Sök")).FontSize(13).FontWeight(FontWeights.SemiBold)
                            )
                    ),

                // Global format toggle — the *default* format for new downloads. Labeled so
                // it is not confused with the per-item MP3/MP4 buttons on the selected-item bar.
                new StackPanel()
                    .Grid(column: 2)
                    .Spacing(2)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Children(
                        new TextBlock()
                            .Text("Format")
                            .FontSize(10)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush"))
                            .HorizontalAlignment(HorizontalAlignment.Center),
                        WithTip(
                            new Button()
                                .Padding(new Thickness(8, 5, 8, 5))
                                .CornerRadius(new CornerRadius(8))
                                .Command(new RelayCommand(() => { vm.Kind = vm.Kind == MediaKind.Audio ? MediaKind.Video : MediaKind.Audio; return Task.CompletedTask; }))
                                .Content(
                                    new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                                        .Children(
                                            new FontIcon().Glyph(x => x.Binding(() => vm.Kind).Convert(k => k == MediaKind.Audio ? "\uE8D6" : "\uE714")).FontSize(13),
                                            new TextBlock().Text(x => x.Binding(() => vm.Kind).Convert(k => k == MediaKind.Audio ? "MP3" : "MP4")).FontSize(12).FontWeight(FontWeights.SemiBold)
                                        )
                                ),
                            "Standardformat för nya nedladdningar")
                    ),

                // Paste button
                new Button()
                    .Grid(column: 3)
                    .Padding(new Thickness(8, 9, 8, 9))
                    .CornerRadius(new CornerRadius(8))
                    .Command(x => x.Binding(() => vm.PasteCommand))
                    .Content(
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                            .Children(
                                new FontIcon().Glyph("\uE77F").FontSize(13),
                                new TextBlock().Text("Klistra in").FontSize(12).FontWeight(FontWeights.Medium)
                            )
                    )
            );

        // While a URL sits in the box but no library folder is chosen, "Ladda ner"
        // is disabled — explain why instead of leaving a silent dead button.
        return new StackPanel()
            .Spacing(4)
            .Children(
                searchRow,
                new TextBlock()
                    .Text("Välj en biblioteksmapp först för att kunna ladda ner.")
                    .FontSize(11)
                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                    .Visibility(x => x.Binding(() => vm.ShowNoRootHint).Convert(show => show ? Visibility.Visible : Visibility.Collapsed))
            );
    }

    private static Grid BuildSearchHeader(LibraryViewModel vm)
    {
        return new Grid()
            .Visibility(x => x.Binding(() => vm.IsSearchActive).Convert(a => a ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                new StackPanel()
                    .Spacing(2)
                    .Children(
                        new TextBlock().Text("YouTube-sökning").FontSize(16).FontWeight(FontWeights.Bold),
                        new TextBlock().Text("Sök och spara direkt till ditt bibliotek").FontSize(11).Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                    )
            );
    }

    private static Grid BuildSearchResultsArea(LibraryViewModel vm)
    {
        var searchList = new ListView()
            .ItemsSource(x => x.Binding(() => vm.SearchResults))
            .IsItemClickEnabled(true)
            .Visibility(x => x.Binding(() => vm.ShowSearchList).Convert(h => h ? Visibility.Visible : Visibility.Collapsed));
        searchList.ItemTemplate = Application.Current.Resources["SearchResultTemplate"] as DataTemplate;
        searchList.ItemClick += (_, e) =>
        {
            // Clicking a result opens its video info page (no download) — the
            // download buttons on that page choose the format.
            if (e.ClickedItem is SearchResult result)
            {
                vm.SelectSearchResult(result);
            }
        };

        var emptyHelp = new StackPanel()
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Spacing(12)
            .Visibility(x => x.Binding(() => vm.HasSearchResults).Convert(h => h ? Visibility.Collapsed : Visibility.Visible))
            .Children(
                new FontIcon().Glyph("\uE721").FontSize(44).Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Sök efter musik på YouTube").FontSize(16).FontWeight(FontWeights.SemiBold).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Skriv in en artist, låt eller YouTube-länk ovan och tryck på Sök eller Enter.")
                    .FontSize(12).Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")).HorizontalAlignment(HorizontalAlignment.Center)
            );

        var loadingRing = new StackPanel()
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Spacing(10)
            .Visibility(x => x.Binding(() => vm.IsSearching).Convert(s => s ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                new ProgressRing().Width(32).Height(32).IsActive(true),
                new TextBlock().Text("Söker på YouTube...").FontSize(13).FontWeight(FontWeights.Medium)
            );

        var noResults = new StackPanel()
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Spacing(12)
            .Visibility(x => x.Binding(() => vm.ShowNoResults).Convert(s => s ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                new FontIcon().Glyph("\uE916").FontSize(44).Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Inga träffar").FontSize(16).FontWeight(FontWeights.SemiBold).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Hittade inga resultat. Pröv en annan sökterm eller ett annat sökord.")
                    .FontSize(12).Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")).HorizontalAlignment(HorizontalAlignment.Center)
            );
        return new Grid()
            .Visibility(x => x.Binding(() => vm.IsSearchActive).Convert(a => a ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                emptyHelp,
                searchList,
                loadingRing,
                noResults
            );
    }

    /// <summary>
    /// The video info page shown when a search result is clicked: the video's title,
    /// metadata and description, plus the two download actions ("Ladda ner ljud" /
    /// "Ladda ner video"). Clicking a search result never starts a download on its own.
    /// </summary>
    private static Border BuildSearchDetailPage(LibraryViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(10))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Padding(new Thickness(20, 18, 20, 18))
            .Visibility(x => x.Binding(() => vm.HasSelectedSearchResult).Convert(v => v ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new ScrollViewer()
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                    .Content(
                        new StackPanel()
                            .Spacing(8)
                            .Children(
                        new TextBlock()
                            .Text("Video")
                            .FontSize(11)
                            .FontWeight(FontWeights.SemiBold)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")),
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.SelectedSearchResult).Convert(r => r?.Title ?? string.Empty))
                            .FontSize(20)
                            .FontWeight(FontWeights.SemiBold)
                            .TextWrapping(TextWrapping.Wrap),
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.SelectedSearchResult).Convert(r => r?.DetailText ?? string.Empty))
                            .FontSize(12)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                            .Visibility(x => x.Binding(() => vm.SelectedSearchResult).Convert(r => !string.IsNullOrEmpty(r?.DetailText) ? Visibility.Visible : Visibility.Collapsed)),
                        // Download options above the description, so the primary
                        // actions are visible without scrolling past a long text.
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.SearchDetailHint))
                            .FontSize(12)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")),
                        new Grid()
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Margin(new Thickness(0, 6, 0, 0))
                            .ColumnDefinitions(
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = GridLength.Auto }
                            )
                            .ColumnSpacing(8)
                            .Children(
                                new Button()
                                    .Grid(column: 0)
                                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                                    .Padding(new Thickness(14, 9, 14, 9))
                                    .CornerRadius(new CornerRadius(8))
                                    .Command(x => x.Binding(() => vm.DownloadSearchResultAudioCommand))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                                            .Children(
                                                new FontIcon().Glyph("\uE8D6").FontSize(13),
                                                new TextBlock().Text("Ladda ner ljud").FontSize(13).FontWeight(FontWeights.SemiBold)
                                            )
                                    ),
                                // Neutral (not accent): only one primary action per view —
                                // audio is the app's default format, so it keeps the accent.
                                // Hidden for audio-only uploads (HasVideo == false); while
                                // the detail fetch is pending (HasVideo == null) it stays
                                // visible so a normal video is never blocked.
                                new Button()
                                    .Grid(column: 1)
                                    .Padding(new Thickness(14, 9, 14, 9))
                                    .CornerRadius(new CornerRadius(8))
                                    .Command(x => x.Binding(() => vm.DownloadSearchResultVideoCommand))
                                    .Visibility(x => x.Binding(() => vm.SelectedSearchResult)
                                        .Convert(r => (r?.HasVideo ?? true) ? Visibility.Visible : Visibility.Collapsed))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                                            .Children(
                                                new FontIcon().Glyph("\uE714").FontSize(13),
                                                new TextBlock().Text("Ladda ner video").FontSize(13).FontWeight(FontWeights.SemiBold)
                                            )
                                    )
                            ),
                        new StackPanel()
                            .Spacing(6)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Visibility(x => x.Binding(() => vm.IsSearchingDetail).Convert(s => s ? Visibility.Visible : Visibility.Collapsed))
                            .Children(
                                new ProgressRing().Width(20).Height(20).IsActive(true),
                                new TextBlock().Text("Hämtar detaljer...").FontSize(12)
                            ),
                        new Border()
                            .CornerRadius(new CornerRadius(8))
                            .Padding(new Thickness(12, 6, 12, 6))
                            .Background(ThemeResource.Get<Brush>("VpErrorBgBrush"))
                            .BorderBrush(ThemeResource.Get<Brush>("VpErrorBorderBrush"))
                            .BorderThickness(new Thickness(1))
                            .Visibility(x => x.Binding(() => vm.SelectedSearchDetailError).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
                            .Child(
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.SelectedSearchDetailError))
                                    .FontSize(11)
                                    .TextWrapping(TextWrapping.Wrap)
                            ),
                        // Description last: the long, scrollable content.
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.SelectedSearchResult).Convert(r => r?.Description ?? string.Empty))
                            .FontSize(13)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                            .TextWrapping(TextWrapping.Wrap)
                            .Visibility(x => x.Binding(() => vm.SelectedSearchResult).Convert(r => !string.IsNullOrEmpty(r?.Description) ? Visibility.Visible : Visibility.Collapsed))
                    )
                    )
            );
    }

    private static Grid BuildCollectionHeader(LibraryViewModel vm)
    {
        var localFilterBox = new TextBox()
            .PlaceholderText("Filtrera...")
            .FontSize(12)
            .Padding(new Thickness(10, 7, 10, 7))
            .CornerRadius(new CornerRadius(6))
            .Width(140);

        localFilterBox.TextChanged += (s, _) =>
        {
            if (s is TextBox tb)
            {
                vm.LocalSearchQuery = tb.Text;
            }
        };

        return new Grid()
            .Visibility(x => x.Binding(() => vm.IsCollectionActive).Convert(a => a ? Visibility.Visible : Visibility.Collapsed))
            .ColumnDefinitions(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            )
            .ColumnSpacing(8)
            .Children(
                new TextBlock()
                    .Grid(column: 0)
                    .Text(x => x.Binding(() => vm.SectionTitle))
                    .FontSize(16)
                    .FontWeight(FontWeights.Bold)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .TextTrimming(TextTrimming.CharacterEllipsis),

                // Retry failed (failed section only)
                new Button()
                    .Grid(column: 1)
                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                    .Padding(new Thickness(10, 6, 10, 6))
                    .CornerRadius(new CornerRadius(6))
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Visibility(x => x.Binding(() => vm.IsFailedActive).Convert(f => f ? Visibility.Visible : Visibility.Collapsed))
                    .Command(x => x.Binding(() => vm.RetryFailedCommand))
                    .Content(
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                            .Children(
                                new FontIcon().Glyph("\uE72C").FontSize(12),
                                new TextBlock().Text("Försök igen").FontSize(12).FontWeight(FontWeights.SemiBold)
                            )
                    ),

                // Sort dropdown
                new ComboBox()
                    .Grid(column: 2)
                    .Width(130)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .SelectedIndex(x => x.Binding(() => vm.SortIndex).TwoWay())
                    .ItemsSource(new[] { "Nyast först", "Titel A–Ö", "Längst först" }),

                localFilterBox.Grid(column: 3)
            );
    }

    private static Grid BuildCollectionListArea(LibraryViewModel vm)
    {
        var libraryList = new ListView()
            .ItemsSource(x => x.Binding(() => vm.FilteredItems))
            .SelectedItem(x => x.Binding(() => vm.SelectedItem).TwoWay())
            .IsItemClickEnabled(true);
        libraryList.ItemTemplate = Application.Current.Resources["LibraryItemTemplate"] as DataTemplate;
        libraryList.ItemClick += (_, e) =>
        {
            // Selecting an item only loads it into the player (no auto-play);
            // the play button in the player / action bar starts playback.
            if (e.ClickedItem is LibraryItem item)
            {
                vm.SelectedItem = item;
            }
        };

        // Search CTA (hidden in the failed section, which offers a retry instead)
        var emptySearchCta = new Button()
            .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
            .Padding(new Thickness(14, 8, 14, 8))
            .CornerRadius(new CornerRadius(6))
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Visibility(x => x.Binding(() => vm.ActiveSection).Convert(sec => sec == LibrarySidebarSection.FailedOnly ? Visibility.Collapsed : Visibility.Visible))
            .Content(
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                    .Children(
                        new FontIcon().Glyph("\uE721").FontSize(13),
                        new TextBlock().Text("Öppna YouTube-sökning").FontSize(12).FontWeight(FontWeights.SemiBold)
                    )
            )
            .Command(new RelayCommand(() => { vm.ActiveSection = LibrarySidebarSection.YouTubeSearch; return Task.CompletedTask; }));

        // Retry CTA (failed section only)
        var emptyRetryCta = new Button()
            .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
            .Padding(new Thickness(14, 8, 14, 8))
            .CornerRadius(new CornerRadius(6))
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Visibility(x => x.Binding(() => vm.ActiveSection).Convert(sec => sec == LibrarySidebarSection.FailedOnly ? Visibility.Visible : Visibility.Collapsed))
            .Command(x => x.Binding(() => vm.RetryFailedCommand))
            .Content(
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                    .Children(
                        new FontIcon().Glyph("\uE72C").FontSize(13),
                        new TextBlock().Text("Försök igen").FontSize(12).FontWeight(FontWeights.SemiBold)
                    )
            );

        var emptyCollection = new StackPanel()
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Spacing(12)
            .Visibility(x => x.Binding(() => vm.ShowCollectionEmptyState).Convert(show => show ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                new FontIcon().Glyph(x => x.Binding(() => vm.ActiveSection).Convert(sec => sec == LibrarySidebarSection.FailedOnly ? "\uE783" : "\uE8B7")).FontSize(44).Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text(x => x.Binding(() => vm.EmptyStateTitle)).FontSize(16).FontWeight(FontWeights.SemiBold).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text(x => x.Binding(() => vm.EmptyStateHint))
                    .FontSize(12).Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                emptySearchCta,
                emptyRetryCta
            );

        // First-launch state: no library folder chosen yet — ask the user to pick one.
        // This is the view's single accent action until a folder exists.
        var noRootCard = new StackPanel()
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Spacing(12)
            .Visibility(x => x.Binding(() => vm.IsRootSet).Convert(set => set ? Visibility.Collapsed : Visibility.Visible))
            .Children(
                new FontIcon().Glyph("\uED25").FontSize(44).Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Välj var ditt bibliotek ska sparas").FontSize(16).FontWeight(FontWeights.SemiBold).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Dina nedladdningar lagras i den mapp du väljer. Du kan ändra mappen när som helst under Biblioteksmapp i sidopanelen.")
                    .FontSize(12)
                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new Button()
                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                    .Padding(new Thickness(14, 8, 14, 8))
                    .CornerRadius(new CornerRadius(6))
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Command(x => x.Binding(() => vm.BrowseCommand))
                    .Content(
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                            .Children(
                                new FontIcon().Glyph("\uED25").FontSize(13),
                                new TextBlock().Text("Välj mapp").FontSize(12).FontWeight(FontWeights.SemiBold)
                            )
                    )
            );

        return new Grid()
            .Visibility(x => x.Binding(() => vm.IsCollectionActive).Convert(a => a ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                noRootCard,
                emptyCollection,
                libraryList
            );
    }

    private static Border BuildErrorCard(MainViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(14, 10, 14, 10))
            .Background(ThemeResource.Get<Brush>("VpErrorBgBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("VpErrorBorderBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.Error).Convert(err => string.IsNullOrEmpty(err) ? Visibility.Collapsed : Visibility.Visible))
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(10)
                    .Children(
                        new FontIcon()
                            .Glyph("\uE783")
                            .FontSize(16)
                            .Foreground(ThemeResource.Get<Brush>("VpErrorBrush"))
                            .VerticalAlignment(VerticalAlignment.Center),
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.Error))
                            .FontSize(12)
                            .TextWrapping(TextWrapping.Wrap)
                            .VerticalAlignment(VerticalAlignment.Center)
                    )
            );
    }


    private static Border BuildLibraryErrorCard(LibraryViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(12, 6, 12, 6))
            .Background(ThemeResource.Get<Brush>("VpErrorBgBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("VpErrorBorderBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.Error).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
            .Child(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .Children(
                        new StackPanel()
                            .Grid(column: 0)
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new FontIcon().Glyph("\uE783").FontSize(13).Foreground(ThemeResource.Get<Brush>("VpErrorBrush")),
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.Error))
                                    .FontSize(11)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),
                        new Button()
                            .Grid(column: 1)
                            .Padding(new Thickness(10, 3, 10, 3))
                            .CornerRadius(new CornerRadius(4))
                            .Margin(new Thickness(8, 0, 0, 0))
                            .Command(x => x.Binding(() => vm.RetryFailedCommand))
                            .Content("Försök igen")
                            .FontSize(11)
                            .Visibility(x => x.Binding(() => vm.HasFailedItems)
                                .Convert(b => b ? Visibility.Visible : Visibility.Collapsed)),
                        WithTip(
                            new Button()
                                .Grid(column: 2)
                                .Padding(new Thickness(6, 2, 6, 2))
                                .CornerRadius(new CornerRadius(4))
                                .Margin(new Thickness(6, 0, 0, 0))
                                .Command(x => x.Binding(() => vm.ClearErrorCommand))
                                .Content(new FontIcon().Glyph("\uE711").FontSize(10)),
                            "Dölj meddelandet")
                    )
            );
    }

    private static Border BuildLibraryProgressCard(LibraryViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(12, 8, 12, 8))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.IsQueueActive).Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new Grid()
                    .RowDefinitions(
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    )
                    .RowSpacing(6)
                    .Children(
                        new Grid()
                            .Grid(row: 0)
                            .ColumnDefinitions(
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                                new ColumnDefinition { Width = GridLength.Auto }
                            )
                            .Children(
                                new StackPanel()
                                    .Grid(column: 0)
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(8)
                                    .Children(
                                        new ProgressRing()
                                            .Width(14)
                                            .Height(14)
                                            .IsActive(true)
                                            .VerticalAlignment(VerticalAlignment.Center),
                                        new TextBlock()
                                            .Text(x => x.Binding(() => vm.JobTitle))
                                            .FontSize(12)
                                            .FontWeight(FontWeights.SemiBold)
                                            .TextTrimming(TextTrimming.CharacterEllipsis)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),
                                new StackPanel()
                                    .Grid(column: 1)
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        // Queue badge (Kö N) — visible when more jobs are queued
                                        new Border()
                                            .Padding(new Thickness(8, 3, 8, 3))
                                            .CornerRadius(new CornerRadius(6))
                                            .Background(ThemeResource.Get<Brush>("SubtleFillColorSecondaryBrush"))
                                            .Visibility(x => x.Binding(() => vm.QueueCount).Convert(n => n > 0 ? Visibility.Visible : Visibility.Collapsed))
                                            .Child(
                                                new TextBlock()
                                                    .Text(x => x.Binding(() => vm.QueueCount).Convert(n => $"{n} i kön"))
                                                    .FontSize(11)
                                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                            ),
                                        new Button()
                                            .Padding(new Thickness(8, 2, 8, 2))
                                            .CornerRadius(new CornerRadius(4))
                                            .Command(x => x.Binding(() => vm.StopDownloadCommand))
                                            .Content(
                                                new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                                    .Children(
                                                        new FontIcon().Glyph("\uE711").FontSize(10),
                                                        new TextBlock().Text("Avbryt").FontSize(11)
                                                    )
                                            )
                                    )
                            ),
                        new ProgressBar()
                            .Grid(row: 1)
                            .Height(4)
                            .CornerRadius(new CornerRadius(2))
                            .Maximum(100)
                            .Value(x => x.Binding(() => vm.JobPercent))
                            .IsIndeterminate(x => x.Binding(() => vm.IsFetching))
                    )
            );
    }

    /// <summary>
    /// The download queue card: one row per job (queued, downloading, finished) with live per-item
    /// progress, plus a per-item log panel that opens when a row is clicked.
    /// Collapses entirely when the queue is empty.
    /// </summary>
    private static Border BuildQueuePanel(LibraryViewModel vm)
    {
        var queueList = new ListView()
            .Height(190)
            .Padding(new Thickness(4, 4, 4, 4))
            .IsItemClickEnabled(true)
            .ItemsSource(x => x.Binding(() => vm.QueueEntries))
            .SelectedItem(x => x.Binding(() => vm.SelectedQueueEntry).TwoWay());
        queueList.ItemTemplate = Application.Current.Resources["QueueEntryTemplate"] as DataTemplate;
        queueList.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is LibraryQueueEntry entry)
            {
                // Select the row so its log shows in the log panel. A failed row's
                // log panel exposes a "Försök igen" button that re-queues it
                // (vm.RetryQueueEntryCommand) — auto-retrying here instead would
                // flip the entry to Queued and hide that button.
                vm.SelectedQueueEntry = entry;
            }
        };

        return new Border()
            .CornerRadius(new CornerRadius(10))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Padding(new Thickness(12, 10, 12, 10))
            // Hidden by default: the app bar queue icon toggles the panel (vm.QueuePanelOpen;
            // the VM auto-closes it when every job has finished).
            .Visibility(x => x.Binding(() => vm.QueuePanelOpen)
                .Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new Grid()
                    .RowDefinitions(
                        new RowDefinition { Height = GridLength.Auto }, // Header: label + count + actions
                        new RowDefinition { Height = GridLength.Auto }, // Queue rows
                        new RowDefinition { Height = GridLength.Auto }  // Per-item log panel
                    )
                    .RowSpacing(8)
                    .Children(
                        new Grid()
                            .Grid(row: 0)
                            .Children(
                                new StackPanel()
                                    .Grid(row: 0)
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        new FontIcon()
                                            .Glyph("\uE9D2")
                                            .FontSize(14),
                                        new TextBlock()
                                            .Text("Laddningskön")
                                            .FontSize(13)
                                            .FontWeight(FontWeights.SemiBold),
                                        new TextBlock()
                                            .Text(x => x.Binding(() => vm.QueueCountText))
                                            .FontSize(12)
                                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                    ),
                                new StackPanel()
                                    .Grid(row: 0)
                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        new Button()
                                            .Content("Rensa klara")
                                            .FontSize(11)
                                            .Padding(new Thickness(10, 4, 10, 4))
                                            .CornerRadius(new CornerRadius(6))
                                            .Command(x => x.Binding(() => vm.ClearFinishedQueueEntriesCommand)),
                                        // "Rensa väntande" (not just "Rensa"): this only removes
                                        // not-yet-started jobs — the active download keeps running.
                                        new Button()
                                            .Content("Rensa väntande")
                                            .FontSize(11)
                                            .Padding(new Thickness(10, 4, 10, 4))
                                            .CornerRadius(new CornerRadius(6))
                                            .Command(x => x.Binding(() => vm.ClearQueueCommand))
                                    )
                            ),
                        queueList.Grid(row: 1),
                        BuildQueueLogPanel(vm).Grid(row: 2)
                    )
            );
    }

    /// <summary>
    /// Log panel for the currently selected queue row: the live yt-dlp output of that item,
    /// auto-scrolling to the newest line while it downloads.
    /// </summary>
    private static Border BuildQueueLogPanel(LibraryViewModel vm)
    {
        // The log text is driven from code (not a binding): a binding to the
        // SelectedQueueEntry object never re-evaluates when the entry appends lines,
        // so the text used to sit frozen after a row was clicked.
        var logText = new TextBlock()
            .TextWrapping(TextWrapping.Wrap)
            .FontSize(11);

        var logViewer = new ScrollViewer()
            .MaxHeight(170)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .Padding(new Thickness(10, 8, 10, 8))
            .CornerRadius(new CornerRadius(8))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorSecondaryBrush"))
            .Content(logText);

        var noLogsPlaceholder = new TextBlock()
            .Text("Inga loggar ännu.")
            .FontSize(11)
            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
            .Visibility(Visibility.Collapsed); // driven in code

        var retryButton = new Button()
            .Content("Försök igen")
            .FontSize(11)
            .Padding(new Thickness(12, 4, 12, 4))
            .CornerRadius(new CornerRadius(6))
            .Visibility(Visibility.Collapsed) // driven in code
            .HorizontalAlignment(HorizontalAlignment.Left);
        retryButton.Click += (_, _) => vm.RetryQueueEntryCommand.Execute(vm.SelectedQueueEntry);

        // Keeps the log text, the placeholder and the retry button in sync with the
        // tracked entry, and follows the newest line — but only while the user is
        // already at the bottom, so reading older lines isn't yanked away.
        LibraryQueueEntry? tracked = null;
        void SyncFromTracked(bool forceScroll)
        {
            logText.Text = tracked?.JoinedLogs ?? string.Empty;
            noLogsPlaceholder.Visibility = tracked is null || !tracked.HasLogs
                ? Visibility.Visible
                : Visibility.Collapsed;
            retryButton.Visibility = tracked is { IsFailed: true }
                ? Visibility.Visible
                : Visibility.Collapsed;

            var nearBottom = logViewer.VerticalOffset + logViewer.ViewportHeight >= logViewer.ScrollableHeight - 64;
            if (forceScroll || nearBottom)
            {
                logViewer.ScrollToVerticalOffset(double.PositiveInfinity);
            }
        }
        void EntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(LibraryQueueEntry.JoinedLogs)
                or nameof(LibraryQueueEntry.HasLogs)
                or nameof(LibraryQueueEntry.IsFailed))
            {
                SyncFromTracked(false);
            }
        }
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(LibraryViewModel.SelectedQueueEntry))
            {
                return;
            }
            if (tracked is not null)
            {
                tracked.PropertyChanged -= EntryPropertyChanged;
            }
            tracked = vm.SelectedQueueEntry;
            if (tracked is not null)
            {
                tracked.PropertyChanged += EntryPropertyChanged;
            }
            SyncFromTracked(true);
        };

        return new Border()
            .Padding(new Thickness(10, 8, 10, 8))
            .CornerRadius(new CornerRadius(8))
            .Visibility(x => x.Binding(() => vm.IsQueuePanelExpanded)
                .Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new StackPanel()
                    .Spacing(6)
                    .Children(
                        // Log panel header with an explicit close (deselect) affordance —
                        // before this the only way to hide the panel was clearing the queue.
                        new Grid()
                            .Children(
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.SelectedQueueEntry)
                                        .Convert(e => e is null ? "Logg" : $"Logg — {e.Title}"))
                                    .FontSize(12)
                                    .FontWeight(FontWeights.SemiBold),
                                WithTip(
                                    new Button()
                                        .HorizontalAlignment(HorizontalAlignment.Right)
                                        .Padding(new Thickness(8, 2, 8, 2))
                                        .CornerRadius(new CornerRadius(4))
                                        .Command(new RelayCommand(() => { vm.SelectedQueueEntry = null; return Task.CompletedTask; }))
                                        .Content(new TextBlock().Text("Dölj").FontSize(10)),
                                    "Dölj loggen")
                            ),
                        noLogsPlaceholder,
                        retryButton,
                        logViewer
                    )
            );
    }

    private static Border BuildSelectedItemBar(LibraryViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(12, 6, 12, 6))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.IsItemSelected).Convert(sel => sel ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .Children(
                        new StackPanel()
                            .Grid(column: 0)
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new FontIcon().Glyph("\uE762").FontSize(12).Foreground(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush")),
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.SelectedItem).Convert(item => item?.Title ?? string.Empty))
                                    .FontSize(12)
                                    .FontWeight(FontWeights.SemiBold)
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                            ),
                        new StackPanel()
                            .Grid(column: 1)
                            .Orientation(Orientation.Horizontal)
                            .Spacing(6)
                            .Children(
                                new Button()
                                    .Padding(new Thickness(8, 4, 8, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.PlaySelectedCommand))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                            .Children(new FontIcon().Glyph("\uE768").FontSize(11), new TextBlock().Text("Spela").FontSize(11))
                                    ),
                                // Per-item format override: re-download as the other
                                // format (MP3/MP4) regardless of the global toggle.
                                new Button()
                                    .Padding(new Thickness(8, 4, 8, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(new RelayCommand(() => vm.DownloadSelectedAsAsync(MediaKind.Audio)))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                            .Children(new FontIcon().Glyph("\uE8D6").FontSize(11), new TextBlock().Text("MP3").FontSize(11))
                                    ),
                                new Button()
                                    .Padding(new Thickness(8, 4, 8, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(new RelayCommand(() => vm.DownloadSelectedAsAsync(MediaKind.Video)))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                            .Children(new FontIcon().Glyph("\uE714").FontSize(11), new TextBlock().Text("MP4").FontSize(11))
                                    ),
                                new Button()
                                    .Padding(new Thickness(8, 4, 8, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.OpenFolderSelectedCommand))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                            .Children(new FontIcon().Glyph("\uED25").FontSize(11), new TextBlock().Text("Öppna mapp").FontSize(11))
                                    ),
                                // Delete: irreversible when the delete mode removes files
                                // (the default) — confirm with a dialog that names the
                                // consequence. Keep-files mode never asks.
                                WithTip(
                                    new Button()
                                        .Padding(new Thickness(8, 4, 8, 4))
                                        .CornerRadius(new CornerRadius(6))
                                        .Command(new RelayCommand(async () =>
                                        {
                                            var item = vm.SelectedItem;
                                            if (item is null)
                                            {
                                                return;
                                            }
                                            if (PreferencesService.GetDeleteMode() == DeleteMode.DeleteFiles
                                                && !await ConfirmDeleteAsync(item.Title))
                                            {
                                                return;
                                            }
                                            vm.DeleteSelectedCommand.Execute(null);
                                        }))
                                        .Content(
                                            new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                                .Children(new FontIcon().Glyph("\uE74D").FontSize(11), new TextBlock().Text("Ta bort").FontSize(11))
                                        ),
                                    "Ta bort ur biblioteket"),
                                WithTip(
                                    new Button()
                                        .Padding(new Thickness(6, 4, 6, 4))
                                        .CornerRadius(new CornerRadius(6))
                                        .Command(x => x.Binding(() => vm.DeselectCommand))
                                        .Content(new FontIcon().Glyph("\uE711").FontSize(10)),
                                    "Avmarkera")
                            )
                    )
            );
    }


    private static Border BuildPlayerDock(LibraryViewModel vm)
    {
        var closeButton = new Button()
            .Padding(new Thickness(6, 3, 6, 3))
            .CornerRadius(new CornerRadius(4))
            .Command(x => x.Binding(() => vm.ClosePlayerCommand))
            .Content(
                new FontIcon().Glyph("\uE711").FontSize(11)
            );
        WithTip(closeButton, "Stäng avspelaren");
#if WINDOWS
        var popOutButton = new Button()
            .Padding(new Thickness(6, 3, 6, 3))
            .CornerRadius(new CornerRadius(4))
            .Margin(new Thickness(0, 0, 4, 0))
            .Command(x => x.Binding(() => vm.PopOutPlayerCommand))
            .Content(
                new FontIcon().Glyph("\uE835").FontSize(11)
            );
        WithTip(popOutButton, "Öppna i eget fönster");
#endif
#if WINDOWS
        var headerButtons = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(4)
            .Children(popOutButton, closeButton);
#else
        var headerButtons = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(4)
            .Children(closeButton);
#endif

#if WINDOWS
        var dockedPlayer = new MediaPlayerElement()
            .Grid(row: 1)
            .Height(280)
            .Visibility(x => x.Binding(() => vm.IsDockedPlayerVisible).Convert(v => v ? Visibility.Visible : Visibility.Collapsed))
            .AutoPlay(false)
            .AreTransportControlsEnabled(true)
            .Source(x => x.Binding(() => vm.PlayingItem).Convert(item =>
                string.IsNullOrEmpty(item?.FilePath)
                    ? null!
                    : Windows.Media.Core.MediaSource.CreateFromUri(new Uri(item.FilePath))));

        // Collapsing the element for the pop-out only hides it — its MediaPlayer keeps
        // playing, which would double up with the pop-out window's own player. Suspend
        // the docked player while the pop-out is open, and resume it only if it was
        // playing when the pop-out opened, so position and controls persist.
        bool wasPlayingBeforePopOut = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(LibraryViewModel.PlayerIsPopOut) || dockedPlayer.MediaPlayer is not { } player)
            {
                return;
            }
            if (vm.PlayerIsPopOut)
            {
                wasPlayingBeforePopOut = player.PlaybackSession?.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
                player.Pause();
            }
            else if (wasPlayingBeforePopOut && vm.PlayingItem is not null && player.Source is not null)
            {
                player.Play();
                wasPlayingBeforePopOut = false;
            }
        };
#endif

        // The "file is missing" prompt shows in place of the player surface.
        var missingPrompt = new StackPanel()
            .Grid(row: 1)
            .Spacing(10)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Visibility(x => x.Binding(() => vm.SelectedFileMissing).Convert(m => m ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                new FontIcon().Glyph("\uE895").FontSize(44).Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Filen saknas på disk").FontSize(16).FontWeight(FontWeights.SemiBold).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text("Filen har tagits bort eller flyttats. Ladda ner den igen för att spela upp den.").FontSize(12).Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new Button()
                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Padding(new Thickness(14, 8, 14, 8))
                    .CornerRadius(new CornerRadius(6))
                    .Command(x => x.Binding(() => vm.DownloadSelectedCommand))
                    .Content(
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                            .Children(
                                new FontIcon().Glyph("\uE896").FontSize(13),
                                new TextBlock().Text("Ladda ner").FontSize(13).FontWeight(FontWeights.SemiBold)
                            )
                    )
            );

#if WINDOWS
        var playerSurface = new Grid()
            .Grid(row: 1)
            .Children(dockedPlayer, missingPrompt);
#else
        var playerSurface = new Grid()
            .Grid(row: 1)
            .Children(
                new StackPanel()
                    .Spacing(2)
                    .Visibility(x => x.Binding(() => vm.SelectedFileMissing).Convert(m => m ? Visibility.Collapsed : Visibility.Visible))
                    .Children(
                        new TextBlock()
                            .Text("Spelning i appen stöds endast på Windows. Filen är sparad i biblioteket.")
                            .FontSize(11)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                    ),
                missingPrompt
            );
#endif

        return new Border()
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(12, 8, 12, 8))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.IsPlayerVisible).Convert(v => v ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new Grid()
                    .RowDefinitions(
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    )
                    .RowSpacing(6)
                    .Children(
                        new Grid()
                            .Grid(row: 0)
                            .ColumnDefinitions(
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                                new ColumnDefinition { Width = GridLength.Auto }
                            )
                            .Children(
                                new StackPanel()
                                    .Grid(column: 0)
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(8)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Children(
                                        new FontIcon()
                                            .Glyph("\uE768")
                                            .FontSize(13)
                                            .Foreground(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
                                            .VerticalAlignment(VerticalAlignment.Center),
                                        new TextBlock()
                                            .Text(x => x.Binding(() => vm.SelectedItem).Convert(item => item?.Title ?? "Spelar"))
                                            .FontSize(12)
                                            .FontWeight(FontWeights.SemiBold)
                                            .TextTrimming(TextTrimming.CharacterEllipsis)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),
                                headerButtons.Grid(column: 1)
                            ),
                        playerSurface
                    )
            );
    }

    /// <summary>Runtime theme-resource lookup for use inside .Convert lambdas (returns a real Brush).</summary>
    private static Brush BrushFromResource(string key, Brush fallback)
    {
        try
        {
            if (Application.Current?.Resources is { } res && res[key] is Brush b)
            {
                return b;
            }
        }
        catch
        {
            // Fall through to fallback.
        }
        return fallback;
    }

    /// <summary>
    /// App-wide top bar: logo, mode switcher (Snabbnedladdning/Bibliotek), tool status pill, theme toggle.
    /// </summary>
    private static Grid BuildAppBar(ShellViewModel shell, MainViewModel simpleVm, LibraryViewModel libraryVm)
    {
        // Bind the shared tool status: ready = green version pill; missing = warning pill (simple-mode VM owns install).
        simpleVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.YtDlpVersion) or nameof(MainViewModel.ToolsReady) or nameof(MainViewModel.InstallStatus))
            {
                shell.ToolStatus = !simpleVm.ToolsReady
                    ? (simpleVm.InstallStatus is { Length: > 0 } msg ? msg : "Hämtar verktyg...")
                    : simpleVm.YtDlpVersion;
            }
        };
        shell.ToolStatus = !simpleVm.ToolsReady ? "Hämtar verktyg..." : simpleVm.YtDlpVersion;

        // The queue icon mirrors the library VM's queue state (the app bar binds to ShellViewModel).
        void SyncQueueIcon()
        {
            shell.QueueState = new QueueState(libraryVm.HasQueueEntries && shell.IsLibrary, libraryVm.QueuePanelOpen, libraryVm.ActiveQueueCount);
        }

        libraryVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(LibraryViewModel.HasQueueEntries) or nameof(LibraryViewModel.QueuePanelOpen) or nameof(LibraryViewModel.ActiveQueueCount))
            {
                SyncQueueIcon();
            }
        };
        shell.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(ShellViewModel.IsLibrary))
            {
                SyncQueueIcon();
            }
        };
        SyncQueueIcon();

        // Mode switcher: two-segment pill, active segment highlighted.
        var modePanel = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(2)
            .Background(ThemeResource.Get<Brush>("ControlFillColorSecondaryBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(2));

        var simpleBtn = new Button()
            .Padding(new Thickness(14, 6, 14, 6))
            .CornerRadius(new CornerRadius(6))
            .Command(new RelayCommand(() => { shell.SetMode(ShellMode.Simple); return Task.CompletedTask; }))
            .Content(
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                    .Children(
                        new FontIcon().Glyph("\uE896").FontSize(12),
                        new TextBlock().Text("Snabbnedladdning").FontSize(12).FontWeight(FontWeights.SemiBold)
                    )
            )
            .Background(x => x.Binding(() => shell.IsSimple).Convert(b => b
                ? BrushFromResource("SubtleFillColorSecondaryBrush", new SolidColorBrush(Colors.Transparent))
                : new SolidColorBrush(Colors.Transparent)))
            .BorderThickness(new Thickness(0));

        var libraryBtn = new Button()
            .Padding(new Thickness(14, 6, 14, 6))
            .CornerRadius(new CornerRadius(6))
            .Command(new RelayCommand(() => { shell.SetMode(ShellMode.Library); return Task.CompletedTask; }))
            .Content(
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                    .Children(
                        new FontIcon().Glyph("\uE8B7").FontSize(12),
                        new TextBlock().Text("Bibliotek").FontSize(12).FontWeight(FontWeights.SemiBold)
                    )
            )
            .Background(x => x.Binding(() => shell.IsLibrary).Convert(b => b
                ? BrushFromResource("SubtleFillColorSecondaryBrush", new SolidColorBrush(Colors.Transparent))
                : new SolidColorBrush(Colors.Transparent)))
            .BorderThickness(new Thickness(0));

        modePanel.Children(simpleBtn, libraryBtn);

        return new Grid()
            .DataContext(shell)
            .Padding(new Thickness(20, 12, 20, 12))
            .Background(ThemeResource.Get<Brush>("LayerFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .ColumnDefinitions(
                new ColumnDefinition { Width = GridLength.Auto }, // Logo + title
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }, // Mode switcher
                new ColumnDefinition { Width = GridLength.Auto }, // Tool status pill
                new ColumnDefinition { Width = GridLength.Auto }, // Queue (library mode)
                new ColumnDefinition { Width = GridLength.Auto }, // Settings (gear)
                new ColumnDefinition { Width = GridLength.Auto }  // Theme toggle
            )
            .ColumnSpacing(12)
            .Children(
                new StackPanel()
                    .Grid(column: 0)
                    .Orientation(Orientation.Horizontal)
                    .Spacing(10)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Children(
                        new Border()
                            .Width(36)
                            .Height(36)
                            .CornerRadius(new CornerRadius(10))
                            .Background(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
                            .Child(
                                new FontIcon()
                                    .Glyph("\uE8D6")
                                    .FontSize(17)
                                    .Foreground(new SolidColorBrush(Colors.White))
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),
                        new TextBlock()
                            .Text("Video Piper")
                            .FontSize(17)
                            .FontWeight(FontWeights.Bold)
                            .VerticalAlignment(VerticalAlignment.Center)
                    ),
                modePanel.Grid(column: 2).VerticalAlignment(VerticalAlignment.Center),

                // Tool status pill: green = ready (version), amber = installing/missing
                new Border()
                    .Grid(column: 3)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .CornerRadius(new CornerRadius(14))
                    .Padding(new Thickness(10, 5, 10, 5))
                    .Background(x => x.Binding(() => shell.ToolStatus).Convert(v =>
                        string.IsNullOrEmpty(v) || v.StartsWith("yt-dlp v", StringComparison.OrdinalIgnoreCase)
                            ? BrushFromResource("ControlFillColorSecondaryBrush", new SolidColorBrush(Colors.Transparent))
                            : BrushFromResource("VpWarningBgBrush", new SolidColorBrush(ColorHelper.FromArgb(34, 234, 179, 8)))))
                    .BorderBrush(x => x.Binding(() => shell.ToolStatus).Convert(v =>
                        string.IsNullOrEmpty(v) || v.StartsWith("yt-dlp v", StringComparison.OrdinalIgnoreCase)
                            ? BrushFromResource("CardStrokeColorDefaultBrush", new SolidColorBrush(Colors.Transparent))
                            : BrushFromResource("VpWarningBorderBrush", new SolidColorBrush(ColorHelper.FromArgb(102, 234, 179, 8)))))
                    .BorderThickness(new Thickness(1))
                    .Visibility(x => x.Binding(() => shell.ToolStatus).Convert(v => string.IsNullOrEmpty(v) ? Visibility.Collapsed : Visibility.Visible))
                    .Child(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(6)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new Ellipse()
                                    .Width(8)
                                    .Height(8)
                                    .Fill(x => x.Binding(() => shell.ToolStatus).Convert(v =>
                                        string.IsNullOrEmpty(v) || v.StartsWith("yt-dlp v", StringComparison.OrdinalIgnoreCase)
                                            ? BrushFromResource("VpSuccessBrush", new SolidColorBrush(ColorHelper.FromArgb(255, 34, 197, 94)))
                                            : BrushFromResource("VpWarningBrush", new SolidColorBrush(ColorHelper.FromArgb(255, 234, 179, 8)))))
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new TextBlock()
                                    .Text(x => x.Binding(() => shell.ToolStatus))
                                    .FontSize(12)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                    .VerticalAlignment(VerticalAlignment.Center)
                            )
                    ),

                // Queue icon (library mode) — toggles the Laddningskön panel. Hidden until the
                // queue has entries; the badge shows how many jobs are still in flight.
                // NOTE: the command is assigned DIRECTLY (no x.Binding): the app bar Grid's
                // DataContext is the ShellViewModel, and markup bindings resolve their path
                // against it — a binding to the LibraryViewModel command silently produced a
                // null Command (the button looked enabled but did nothing). The command object
                // is stable (built once in the VM constructor), so a direct reference is safe
                // — the same pattern the mode-switcher buttons use.
                WithTip(
                    new Button()
                        .Grid(column: 4)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Padding(new Thickness(10, 8, 10, 8))
                        .CornerRadius(new CornerRadius(8))
                        .Command(libraryVm.ToggleQueuePanelCommand)
                        .Background(x => x.Binding(() => shell.QueueState).Convert(s => s.Open
                            ? BrushFromResource("SubtleFillColorSecondaryBrush", new SolidColorBrush(Colors.Transparent))
                            : new SolidColorBrush(Colors.Transparent)))
                        .Visibility(x => x.Binding(() => shell.QueueState).Convert(s => s.Visible ? Visibility.Visible : Visibility.Collapsed))
                        .Content(
                            new Grid().Children(
                                new FontIcon()
                                    .Glyph("\uE9D2")
                                    .FontSize(15)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new Border()
                                    .CornerRadius(new CornerRadius(8))
                                    .Padding(new Thickness(5, 1, 5, 1))
                                    .MinWidth(16)
                                    .HorizontalAlignment(HorizontalAlignment.Left)
                                    .VerticalAlignment(VerticalAlignment.Top)
                                    .Margin(new Thickness(0, 0, 0, 0))
                                    .Background(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
                                    .Visibility(x => x.Binding(() => shell.QueueState).Convert(s => s.Count > 0 ? Visibility.Visible : Visibility.Collapsed))
                                    .Child(
                                        new TextBlock()
                                            .Text(x => x.Binding(() => shell.QueueState).Convert(s => s.Count.ToString()))
                                            .FontSize(10)
                                            .FontWeight(FontWeights.SemiBold)
                                            .Foreground(new SolidColorBrush(Colors.White))
                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                    )
                            )
                        ),
                    "Laddningskön"),

                // Settings (gear) button — opens the Inställningar page.
                WithTip(
                    new Button()
                        .Grid(column: 5)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Padding(new Thickness(10, 8, 10, 8))
                        .CornerRadius(new CornerRadius(8))
                        .Command(x => x.Binding(() => shell.ToggleSettingsCommand))
                        .Background(x => x.Binding(() => shell.SettingsOpen).Convert(open => open
                            ? BrushFromResource("SubtleFillColorSecondaryBrush", new SolidColorBrush(Colors.Transparent))
                            : new SolidColorBrush(Colors.Transparent)))
                        .Content(
                            new FontIcon()
                                .Glyph("\uE713")
                                .FontSize(15)
                        ),
                    "Inställningar"),

                // Theme toggle — icon-only, so it carries a tooltip and uses the same
                // FontIcon language as the rest of the app (moon = dark, sun = light).
                WithTip(
                    new Button()
                        .Grid(column: 6)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Padding(new Thickness(10, 8, 10, 8))
                        .CornerRadius(new CornerRadius(8))
                        .Command(x => x.Binding(() => shell.ToggleThemeCommand))
                        .Content(
                            new FontIcon()
                                .Glyph(x => x.Binding(() => shell.IsDark).Convert(dark => dark ? "\uE793" : "\uE706"))
                                .FontSize(15)
                        ),
                    "Växla tema")
            );
    }
    /// <summary>
    /// Settings page (Inställningar): one card per managed tool (yt-dlp, ffmpeg) with
    /// installed/latest version, a status line, and per-tool "check for updates" buttons.
    /// Updates are also checked automatically once per day in the background.
    /// </summary>
    private static Grid BuildSettingsView(SettingsViewModel vm)
    {
        var header = new Grid()
            .ColumnDefinitions(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            )
            .Children(
                new StackPanel()
                    .Grid(column: 0)
                    .Spacing(4)
                    .Children(
                        new TextBlock()
                            .Text("Inställningar")
                            .FontSize(17)
                            .FontWeight(FontWeights.SemiBold),
                        new TextBlock()
                            .Text("Appen använder egna binärfiler för yt-dlp och ffmpeg – systemets används aldrig. Uppdateringar kontrolleras automatiskt en gång per dag i bakgrunden, eller manuellt via knapparna nedan.")
                            .FontSize(12)
                            .TextWrapping(TextWrapping.Wrap)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                    ),
                new Button()
                    .Grid(column: 1)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .Padding(new Thickness(14, 7, 14, 7))
                    .CornerRadius(new CornerRadius(6))
                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                    .Command(x => x.Binding(() => vm.CheckAllCommand))
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(6)
                            .Children(
                                new FontIcon().Glyph("\uE895").FontSize(12),
                                new TextBlock().Text(x => x.Binding(() => vm.CheckAllText)).FontSize(13).FontWeight(FontWeights.SemiBold)
                            )
                    )
            );

        return new Grid()
            .DataContext(vm)
            .Padding(new Thickness(24, 8, 24, 16))
            .Children(
                new ScrollViewer()
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                    .Content(
                        new StackPanel()
                            .MaxWidth(640)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Top)
                            .Padding(new Thickness(0, 16, 0, 16))
                            .Spacing(12)
                            .Children(
                                header,
                                BuildToolCard(vm.YtDlp),
                                BuildToolCard(vm.Ffmpeg),
                                BuildLibrarySettingsCard(vm)
                            )
                    )
            );
    }

    /// <summary>One settings card for a single tool (yt-dlp or ffmpeg).</summary>
    private static Border BuildToolCard(ToolSettingsModel tool)
    {
        return new Border()
            .DataContext(tool)
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(12, 10, 12, 10))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Child(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .ColumnSpacing(12)
                    .Children(
                        new Border()
                            .Grid(column: 0)
                            .Width(36)
                            .Height(36)
                            .CornerRadius(new CornerRadius(8))
                            .Background(ThemeResource.Get<Brush>("LayerFillColorDefaultBrush"))
                            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
                            .BorderThickness(new Thickness(1))
                            .Child(
                                new FontIcon()
                                    .Glyph(tool.Glyph)
                                    .FontSize(15)
                                    .Foreground(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),
                        new StackPanel()
                            .Grid(column: 1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Spacing(3)
                            .Children(
                                new TextBlock()
                                    .Text(x => x.Binding(() => tool.Title))
                                    .FontSize(14)
                                    .FontWeight(FontWeights.SemiBold),
                                new TextBlock()
                                    .Text(x => x.Binding(() => tool.InstalledText))
                                    .FontSize(12)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")),
                                // State-colored status line: errors red, an available update
                                // amber, everything else neutral secondary.
                                new TextBlock()
                                    .Text(x => x.Binding(() => tool.StatusText))
                                    .FontSize(12)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .Foreground(x => x.Binding(() => tool.StatusText).Convert(s =>
                                        string.IsNullOrEmpty(s)
                                            ? BrushFromResource("TextFillColorSecondaryBrush", new SolidColorBrush(Colors.Gray))
                                            : s.Contains("misslyckades")
                                                ? BrushFromResource("VpErrorBrush", new SolidColorBrush(Colors.OrangeRed))
                                                : s.StartsWith("Ny version tillgänglig", StringComparison.Ordinal)
                                                    ? BrushFromResource("VpWarningBrush", new SolidColorBrush(Colors.Gold))
                                                    : BrushFromResource("TextFillColorSecondaryBrush", new SolidColorBrush(Colors.Gray))))
                                    .Visibility(x => x.Binding(() => tool.StatusText).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
                            ),
                        new StackPanel()
                            .Grid(column: 2)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Spacing(6)
                            .Children(
                                // Contextual action: only shown when there is something to
                                // update/fetch — a disabled "Senaste" status button was
                                // replaced by the state-colored status line above.
                                new Button()
                                    .Padding(new Thickness(10, 4, 10, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Visibility(x => x.Binding(() => tool.HasUpdate).Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
                                    .Command(x => x.Binding(() => tool.UpdateCommand))
                                    .IsEnabled(x => x.Binding(() => tool.CanUpdate))
                                    .Content(
                                        new TextBlock()
                                            .Text(x => x.Binding(() => tool.InstalledVersion).Convert(v => v is null ? "Hämta" : "Uppdatera"))
                                            .FontSize(13)
                                            .FontWeight(FontWeights.SemiBold)
                                    ),
                                new Button()
                                    .Padding(new Thickness(10, 4, 10, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => tool.CheckCommand))
                                    .IsEnabled(x => x.Binding(() => tool.Busy).Convert(b => !b))
                                    .Content(
                                        new StackPanel()
                                            .Orientation(Orientation.Horizontal)
                                            .Spacing(6)
                                            .Children(
                                                new ProgressRing()
                                                    .Width(14)
                                                    .Height(14)
                                                    .IsActive(x => x.Binding(() => tool.Busy))
                                                    .Visibility(x => x.Binding(() => tool.Busy).Convert(b => b ? Visibility.Visible : Visibility.Collapsed)),
                                                new TextBlock()
                                                    .Text(x => x.Binding(() => tool.Busy).Convert(b => b ? "Hämtar..." : "Kontrollera uppdatering"))
                                                    .FontSize(13)
                                            )
                                    )
                            )
                    )
            );
    }

    /// <summary>One settings row: label on the left, control right-aligned — the standard
    /// app-settings layout at full card width (the old narrow right column clipped controls).</summary>
    private static Grid BuildSettingRow(string label, FrameworkElement control)
    {
        return new Grid()
            .ColumnDefinitions(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            )
            .ColumnSpacing(12)
            .Children(
                new TextBlock()
                    .Text(label)
                    .FontSize(13)
                    .VerticalAlignment(VerticalAlignment.Center),
                control
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .VerticalAlignment(VerticalAlignment.Center)
            );
    }

    /// <summary>Library settings card: delete-mode toggle plus video resolution/framerate.</summary>
    private static Border BuildLibrarySettingsCard(SettingsViewModel vm)
    {
        var deleteToggle = new ToggleSwitch()
            .IsOn(x => x.Binding(() => vm.DeleteFiles).TwoWay())
            .OffContent("Behåll filer")
            .OnContent("Ta bort filer och tomma mappar");

        var resolutionCombo = new ComboBox()
            .ItemsSource(SettingsViewModel.ResolutionLabels)
            .SelectedIndex(x => x.Binding(() => vm.ResolutionIndex).TwoWay())
            .Width(140);

        var framerateCombo = new ComboBox()
            .ItemsSource(SettingsViewModel.FramerateLabels)
            .SelectedIndex(x => x.Binding(() => vm.FramerateIndex).TwoWay())
            .Width(140);

        return new Border()
            .DataContext(vm)
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(12, 10, 12, 10))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(10)
                    .Children(
                        new Grid()
                            .ColumnDefinitions(
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                            )
                            .ColumnSpacing(12)
                            .Children(
                                new Border()
                                    .Grid(column: 0)
                                    .Width(36)
                                    .Height(36)
                                    .CornerRadius(new CornerRadius(8))
                                    .Background(ThemeResource.Get<Brush>("LayerFillColorDefaultBrush"))
                                    .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
                                    .BorderThickness(new Thickness(1))
                                    .Child(
                                        new FontIcon()
                                            .Glyph("\uE8B7")
                                            .FontSize(15)
                                            .Foreground(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),
                                new StackPanel()
                                    .Grid(column: 1)
                                    .Spacing(3)
                                    .Children(
                                        new TextBlock().Text("Bibliotek").FontSize(14).FontWeight(FontWeights.SemiBold),
                                        new TextBlock().Text("Video-kvalitet och bildfrekvens för nya nedladdningar, samt vad som händer med filer när du tar bort en post.")
                                            .FontSize(12)
                                            .TextWrapping(TextWrapping.Wrap)
                                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                    )
                            ),
                        BuildSettingRow("Filer vid borttagning", deleteToggle),
                        BuildSettingRow("Upplösning", resolutionCombo),
                        BuildSettingRow("Bildfrekvens", framerateCombo)
                    )
            );
    }



    private static Border BuildMissingToolsCard(MainViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(16))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("VpWarningBorderBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.ToolsReady).Convert(ready => ready ? Visibility.Collapsed : Visibility.Visible))
            .Child(
                new StackPanel()
                    .Spacing(10)
                    .Children(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new FontIcon()
                                    .Glyph("\uE7BA")
                                    .FontSize(16)
                                    .Foreground(ThemeResource.Get<Brush>("VpWarningBrush")),
                                new TextBlock()
                                    .Text("Saknade verktyg")
                                    .FontSize(14)
                                    .FontWeight(FontWeights.SemiBold)
                            ),
                        new TextBlock()
                            .Text("Vid nedladdning krävs yt-dlp och ffmpeg. Du kan hämta dem direkt här – de sparas lokalt i appen.")
                            .FontSize(13)
                            .TextWrapping(TextWrapping.Wrap)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")),
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                new Button()
                                    .Padding(new Thickness(14, 7, 14, 7))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.InstallYtDlpCommand))
                                    .Visibility(x => x.Binding(() => vm.YtDlpMissing).Convert(m => m ? Visibility.Visible : Visibility.Collapsed))
                                    .Content(
                                        new StackPanel()
                                            .Orientation(Orientation.Horizontal)
                                            .Spacing(8)
                                            .Children(
                                                new ProgressRing()
                                                    .Width(14)
                                                    .Height(14)
                                                    .IsActive(x => x.Binding(() => vm.InstallYtDlpBusy))
                                                    .Visibility(x => x.Binding(() => vm.InstallYtDlpBusy).Convert(b => b ? Visibility.Visible : Visibility.Collapsed)),
                                                new TextBlock()
                                                    .Text("Hämta yt-dlp")
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                            )
                                    ),
                                new Button()
                                    .Padding(new Thickness(14, 7, 14, 7))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.InstallFfmpegCommand))
                                    .Visibility(x => x.Binding(() => vm.FfmpegMissing).Convert(m => m ? Visibility.Visible : Visibility.Collapsed))
                                    .Content(
                                        new StackPanel()
                                            .Orientation(Orientation.Horizontal)
                                            .Spacing(8)
                                            .Children(
                                                new ProgressRing()
                                                    .Width(14)
                                                    .Height(14)
                                                    .IsActive(x => x.Binding(() => vm.InstallFfmpegBusy))
                                                    .Visibility(x => x.Binding(() => vm.InstallFfmpegBusy).Convert(b => b ? Visibility.Visible : Visibility.Collapsed)),
                                                new TextBlock()
                                                    .Text("Hämta ffmpeg")
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                            )
                                    )
                            ),
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.InstallStatus))
                            .FontSize(12)
                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                            .Visibility(x => x.Binding(() => vm.InstallStatus).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
                    )
            );
    }

    private static StackPanel BuildLinkField(MainViewModel vm)
    {
        return new StackPanel()
            .Spacing(8)
            .Children(
                new TextBlock()
                    .Text("YouTube Länk")
                    .FontSize(14)
                    .FontWeight(FontWeights.Medium),
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .ColumnSpacing(8)
                    .Children(
                        new TextBox()
                            .Grid(column: 0)
                            .PlaceholderText("https://www.youtube.com/watch?v=...")
                            .Text(x => x.Binding(() => vm.Link).TwoWay().UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .FontSize(14)
                            .Padding(new Thickness(12, 10, 12, 10))
                            .CornerRadius(new CornerRadius(6)),
                        new Button()
                            .Grid(column: 1)
                            .Command(x => x.Binding(() => vm.PasteCommand))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .Padding(new Thickness(14, 10, 14, 10))
                            .CornerRadius(new CornerRadius(6))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph("\uE77F").FontSize(14),
                                        new TextBlock().Text("Klistra in").FontSize(13).FontWeight(FontWeights.Medium)
                                    )
                            )
                    )
            );
    }

    private static StackPanel BuildSavePathField(MainViewModel vm)
    {
        return new StackPanel()
            .Spacing(8)
            .Children(
                new TextBlock()
                    .Text("Spara till mapp")
                    .FontSize(14)
                    .FontWeight(FontWeights.Medium),
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .ColumnSpacing(8)
                    .Children(
                        new TextBox()
                            .Grid(column: 0)
                            .PlaceholderText("Standard / Arbetskatalog (Musik)")
                            .Text(x => x.Binding(() => vm.SavePathDisplay))
                            .IsReadOnly(true)
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .FontSize(14)
                            .Padding(new Thickness(12, 10, 12, 10))
                            .CornerRadius(new CornerRadius(6)),
                        new Button()
                            .Grid(column: 1)
                            .Command(x => x.Binding(() => vm.BrowseCommand))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .Padding(new Thickness(14, 10, 14, 10))
                            .CornerRadius(new CornerRadius(6))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph("\uED25").FontSize(14),
                                        new TextBlock().Text("Bläddra").FontSize(13).FontWeight(FontWeights.Medium)
                                    )
                            )
                    )
            );
    }

    /// <summary>MP3/MP4 format switch for one-off downloads (ON = MP3, OFF = MP4).</summary>
    private static StackPanel BuildSimpleFormatToggle(MainViewModel vm)
    {
        return new StackPanel()
            .Spacing(6)
            .Children(
                new TextBlock()
                    .Text("Format")
                    .FontSize(12)
                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")),
                new ToggleSwitch()
                    .IsOn(x => x.Binding(() => vm.IsAudioSelected).TwoWay())
                    .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                    .OffContent("MP4 (video)")
                    .OnContent("MP3 (ljud)")
            );
    }

    private static Border BuildProgressCard(MainViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(16))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.ShowProgress).Convert(show => show ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new StackPanel()
                    .Spacing(10)
                    .Children(
                        new Grid()
                            .ColumnDefinitions(
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                                new ColumnDefinition { Width = GridLength.Auto }
                            )
                            .Children(
                                new TextBlock()
                                    .Grid(column: 0)
                                    .Text(x => x.Binding(() => vm.ProgressLabel))
                                    .FontSize(13)
                                    .FontWeight(FontWeights.SemiBold),
                                new TextBlock()
                                    .Grid(column: 1)
                                    .Text(x => x.Binding(() => vm.ProgressSpeed))
                                    .FontSize(12)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                    .Visibility(x => x.Binding(() => vm.ProgressSpeed).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
                            ),
                        new ProgressBar()
                            .Height(6)
                            .CornerRadius(new CornerRadius(3))
                            .Maximum(100)
                            .Value(x => x.Binding(() => vm.ProgressPercent))
                            .IsIndeterminate(x => x.Binding(() => vm.IsProgressIndeterminate)),
                        new Grid()
                            .ColumnDefinitions(
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                                new ColumnDefinition { Width = GridLength.Auto }
                            )
                            .Children(
                                new TextBlock()
                                    .Grid(column: 0)
                                    .Text(x => x.Binding(() => vm.ProgressMessage))
                                    .FontSize(12)
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Visibility(x => x.Binding(() => vm.ProgressMessage).Convert(m => string.IsNullOrEmpty(m) ? Visibility.Collapsed : Visibility.Visible)),
                                new Button()
                                    .Grid(column: 1)
                                    .Padding(new Thickness(10, 4, 10, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.StopDownloadCommand))
                                    .Visibility(x => x.Binding(() => vm.IsBusy).Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
                                    .Content(
                                        new StackPanel()
                                            .Orientation(Orientation.Horizontal)
                                            .Spacing(4)
                                            .Children(
                                                new FontIcon().Glyph("\uE711").FontSize(11),
                                                new TextBlock().Text("Avbryt").FontSize(11)
                                            )
                                    )
                            )
                    )
            );
    }

    private static Grid BuildDownloadButton(MainViewModel vm)
    {
        return new Grid()
            .Children(
                // Download button (visible when not finished)
                new Button()
                    .Height(48)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .CornerRadius(new CornerRadius(8))
                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                    .Command(x => x.Binding(() => vm.DownloadCommand))
                    .IsEnabled(x => x.Binding(() => vm.CanDownload))
                    .Visibility(x => x.Binding(() => vm.IsFinishedNotBusy).Convert(f => f ? Visibility.Collapsed : Visibility.Visible))
                    .Content(
                        new Grid()
                            .Children(
                                // Busy State
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(10)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Visibility(x => x.Binding(() => vm.IsBusy).Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
                                    .Children(
                                        new ProgressRing()
                                            .Width(18)
                                            .Height(18)
                                            .IsActive(true)
                                            .Foreground(new SolidColorBrush(Colors.White)),
                                        new TextBlock()
                                            .Text("Laddar ner & konverterar...")
                                            .FontSize(15)
                                            .FontWeight(FontWeights.SemiBold)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),

                                // Normal Ready State
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(10)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Visibility(x => x.Binding(() => vm.IsBusy).Convert(b => b ? Visibility.Collapsed : Visibility.Visible))
                                    .Children(
                                        new FontIcon()
                                            .Glyph("\uE896")
                                            .FontSize(16),
                                        new TextBlock()
                                            .Text(x => x.Binding(() => vm.DownloadButtonText))
                                            .FontSize(15)
                                            .FontWeight(FontWeights.SemiBold)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    )
                            )
                    ),

                // Finished State: success card with Open file / Open folder / Download another
                new StackPanel()
                    .Spacing(10)
                    .Visibility(x => x.Binding(() => vm.IsFinishedNotBusy).Convert(f => f ? Visibility.Visible : Visibility.Collapsed))
                    .Children(
                        // Open file (when the finished path is known)
                        new Button()
                            .Height(40)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .CornerRadius(new CornerRadius(8))
                            .Command(x => x.Binding(() => vm.OpenFileCommand))
                            .IsEnabled(x => x.Binding(() => vm.HasDestinationFile))
                            .Visibility(x => x.Binding(() => vm.HasDestinationFile).Convert(h => h ? Visibility.Visible : Visibility.Collapsed))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(8)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Children(
                                        new FontIcon().Glyph("\uE7F3").FontSize(14),
                                        new TextBlock().Text("Öppna fil").FontSize(13).FontWeight(FontWeights.SemiBold)
                                    )
                            ),
                        // Two-button row
                        new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    )
                    .ColumnSpacing(10)
                    .Children(
                        new Button()
                            .Grid(column: 0)
                            .Height(48)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .CornerRadius(new CornerRadius(8))
                            .Command(x => x.Binding(() => vm.OpenFolderCommand))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(8)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Children(
                                        new FontIcon().Glyph("\uED25").FontSize(16),
                                        new TextBlock().Text("Öppna mapp").FontSize(14).FontWeight(FontWeights.SemiBold)
                                    )
                            ),
                        new Button()
                            .Grid(column: 1)
                            .Height(48)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .CornerRadius(new CornerRadius(8))
                            .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                            .Command(x => x.Binding(() => vm.ResetCommand))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(8)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Children(
                                        new FontIcon().Glyph("\uE73E").FontSize(16),
                                        new TextBlock().Text("Ladda ner en till").FontSize(14).FontWeight(FontWeights.SemiBold)
                                    )
                            )
                    )
                    )
            );
    }
}





