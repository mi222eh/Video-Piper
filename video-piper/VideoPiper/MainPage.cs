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
    private readonly ShellViewModel _shell;
    private readonly MainViewModel _simpleVm;
    private readonly LibraryViewModel _libraryVm;

    public MainPage()
    {
        _shell = new ShellViewModel();
        _simpleVm = new MainViewModel();
        _libraryVm = new LibraryViewModel();

        _simpleView = BuildSimpleView(_simpleVm);
        _libraryView = BuildLibraryView(_libraryVm);

        var viewHost = new Grid()
            .Children(
                _simpleView,
                _libraryView
            );

        var root = new Grid()
            .RowDefinitions(
                new RowDefinition { Height = GridLength.Auto }, // App bar
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // Mode views
            )
            .Children(
                BuildAppBar(_shell, _simpleVm).Grid(row: 0),
                viewHost.Grid(row: 1)
            );

        this
            .Background(ThemeResource.Get<Brush>("ApplicationPageBackgroundThemeBrush"))
            .Content(root);

        SyncModeViews();
        _shell.PropertyChanged += OnShellPropertyChanged;

        _ = _simpleVm.InitializeAsync();
        _ = _libraryVm.InitializeAsync();
    }

    private void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsSimple) or nameof(ShellViewModel.IsLibrary))
        {
            SyncModeViews();
        }
    }

    private void SyncModeViews()
    {
        var showSimple = _shell.IsSimple;
        _simpleView.Visibility = showSimple ? Visibility.Visible : Visibility.Collapsed;
        _libraryView.Visibility = showSimple ? Visibility.Collapsed : Visibility.Visible;
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
                                new Button()
                                    .Padding(new Thickness(6, 3, 6, 3))
                                    .CornerRadius(new CornerRadius(4))
                                    .Command(x => x.Binding(() => vm.ResyncCommand))
                                    .Content(new FontIcon().Glyph("\uE895").FontSize(10))
                            )
                    )
            );
    }

    private static Grid BuildLibraryMainContent(LibraryViewModel vm)
    {
        return new Grid()
            .Padding(new Thickness(20, 16, 20, 16))
            .RowDefinitions(
                new RowDefinition { Height = GridLength.Auto }, // Active download / error banner
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Body (Search View or Collection View)
                new RowDefinition { Height = GridLength.Auto }  // Bottom Player Dock
            )
            .RowSpacing(12)
            .Children(
                // Top status cards (Progress & Errors)
                new StackPanel()
                    .Grid(row: 0)
                    .Spacing(8)
                    .Children(
                        BuildLibraryErrorCard(vm),
                        BuildLibraryProgressCard(vm)
                    ),

                // Main Content View (Search or Collection)
                new Grid()
                    .Grid(row: 1)
                    .Children(
                        BuildYouTubeSearchView(vm),
                        BuildLibraryCollectionView(vm)
                    ),

                // Bottom Player Dock
                BuildPlayerDock(vm).Grid(row: 2)
            );
    }

    private static Grid BuildYouTubeSearchView(LibraryViewModel vm)
    {
        var searchBox = new TextBox()
            .PlaceholderText("Sök efter musik, artist eller video på YouTube...")
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

        var searchList = new ListView()
            .ItemsSource(x => x.Binding(() => vm.SearchResults))
            .IsItemClickEnabled(true)
            .Visibility(x => x.Binding(() => vm.HasSearchResults).Convert(h => h ? Visibility.Visible : Visibility.Collapsed));
        searchList.ItemTemplate = Application.Current.Resources["SearchResultTemplate"] as DataTemplate;
        searchList.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is SearchResult result)
            {
                vm.DownloadSearchResultCommand.Execute(result);
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

        return new Grid()
            .Visibility(x => x.Binding(() => vm.IsSearchActive).Convert(a => a ? Visibility.Visible : Visibility.Collapsed))
            .RowDefinitions(
                new RowDefinition { Height = GridLength.Auto }, // Title
                new RowDefinition { Height = GridLength.Auto }, // Search Bar Row
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // Results Area
            )
            .RowSpacing(12)
            .Children(
                // Header
                new StackPanel()
                    .Grid(row: 0)
                    .Spacing(2)
                    .Children(
                        new TextBlock().Text("YouTube-sökning").FontSize(20).FontWeight(FontWeights.Bold),
                        new TextBlock().Text("Sök och spara direkt till ditt musik- och videobibliotek").FontSize(12).Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                    ),

                // Search Bar
                new Grid()
                    .Grid(row: 1)
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // Input
                        new ColumnDefinition { Width = GridLength.Auto }, // Sök Button
                        new ColumnDefinition { Width = GridLength.Auto }, // Format (MP3/MP4)
                        new ColumnDefinition { Width = GridLength.Auto }  // Klistra in
                    )
                    .ColumnSpacing(8)
                    .Children(
                        searchBox.Grid(column: 0),

                        // Search / download button — label reflects whether the box holds a URL or a query
                        new Button()
                            .Grid(column: 1)
                            .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                            .Padding(new Thickness(14, 9, 14, 9))
                            .CornerRadius(new CornerRadius(8))
                            .Command(x => x.Binding(() => vm.ExecuteSearchCommand))
                            .Content(
                                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph(x => x.Binding(() => vm.IsUrlInput).Convert(url => url ? "\uE896" : "\uE721")).FontSize(13),
                                        new TextBlock().Text(x => x.Binding(() => vm.IsUrlInput).Convert(url => url ? "Ladda ner" : "Sök")).FontSize(13).FontWeight(FontWeights.SemiBold)
                                    )
                            ),

                        // Format button
                        new Button()
                            .Grid(column: 2)
                            .Padding(new Thickness(10, 9, 10, 9))
                            .CornerRadius(new CornerRadius(8))
                            .Command(new RelayCommand(() => { vm.Kind = vm.Kind == MediaKind.Audio ? MediaKind.Video : MediaKind.Audio; return Task.CompletedTask; }))
                            .Content(
                                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph(x => x.Binding(() => vm.Kind).Convert(k => k == MediaKind.Audio ? "\uE8D6" : "\uE714")).FontSize(13),
                                        new TextBlock().Text(x => x.Binding(() => vm.Kind).Convert(k => k == MediaKind.Audio ? "MP3" : "MP4")).FontSize(12).FontWeight(FontWeights.SemiBold)
                                    )
                            ),

                        // Paste button
                        new Button()
                            .Grid(column: 3)
                            .Padding(new Thickness(10, 9, 10, 9))
                            .CornerRadius(new CornerRadius(8))
                            .Command(x => x.Binding(() => vm.PasteCommand))
                            .Content(
                                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph("\uE77F").FontSize(13),
                                        new TextBlock().Text("Klistra in").FontSize(12).FontWeight(FontWeights.Medium)
                                    )
                            )
                    ),

                // Search Results or Empty Help or Loading
                new Grid()
                    .Grid(row: 2)
                    .Children(
                        emptyHelp,
                        searchList,
                        loadingRing
                    )
            );
    }

    private static Grid BuildLibraryCollectionView(LibraryViewModel vm)
    {
        var localFilterBox = new TextBox()
            .PlaceholderText("Filtrera sparade filer...")
            .FontSize(12)
            .Padding(new Thickness(10, 7, 10, 7))
            .CornerRadius(new CornerRadius(6))
            .Width(200);

        localFilterBox.TextChanged += (s, _) =>
        {
            if (s is TextBox tb)
            {
                vm.LocalSearchQuery = tb.Text;
            }
        };

        var libraryList = new ListView()
            .ItemsSource(x => x.Binding(() => vm.FilteredItems))
            .SelectedItem(x => x.Binding(() => vm.SelectedItem).TwoWay())
            .IsItemClickEnabled(true);
        libraryList.ItemTemplate = Application.Current.Resources["LibraryItemTemplate"] as DataTemplate;
        libraryList.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is LibraryItem item)
            {
                vm.SelectedItem = item;
                if (item.CanPlay)
                {
                    _ = vm.PlayItemAsync(item);
                }
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
            .Visibility(x => x.Binding(() => vm.HasFilteredItems).Convert(has => has ? Visibility.Collapsed : Visibility.Visible))
            .Children(
                new FontIcon().Glyph(x => x.Binding(() => vm.ActiveSection).Convert(sec => sec == LibrarySidebarSection.FailedOnly ? "\uE783" : "\uE8B7")).FontSize(44).Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text(x => x.Binding(() => vm.EmptyStateTitle)).FontSize(16).FontWeight(FontWeights.SemiBold).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock().Text(x => x.Binding(() => vm.EmptyStateHint))
                    .FontSize(12).Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")).HorizontalAlignment(HorizontalAlignment.Center),
                emptySearchCta,
                emptyRetryCta
            );

        return new Grid()
            .Visibility(x => x.Binding(() => vm.IsCollectionActive).Convert(a => a ? Visibility.Visible : Visibility.Collapsed))
            .RowDefinitions(
                new RowDefinition { Height = GridLength.Auto }, // Header Row (Title + Filter)
                new RowDefinition { Height = GridLength.Auto }, // Selected item action bar
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // List
            )
            .RowSpacing(10)
            .Children(
                // Header: title + (failed section) retry + sort + filter
                new Grid()
                    .Grid(row: 0)
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
                            .FontSize(20)
                            .FontWeight(FontWeights.Bold)
                            .VerticalAlignment(VerticalAlignment.Center),
                        // Retry failed (failed section only)
                        new Button()
                            .Grid(column: 1)
                            .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                            .Padding(new Thickness(12, 7, 12, 7))
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
                    ),

                // Selected Item Action Bar
                BuildSelectedItemBar(vm).Grid(row: 1),

                // Media list
                new Grid()
                    .Grid(row: 2)
                    .Children(
                        emptyCollection,
                        libraryList
                    )
            );
    }

    private static Border BuildErrorCard(MainViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(14, 10, 14, 10))
            .Background(new SolidColorBrush(ColorHelper.FromArgb(25, 239, 68, 68)))
            .BorderBrush(new SolidColorBrush(ColorHelper.FromArgb(120, 239, 68, 68)))
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
                            .Foreground(new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68)))
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
            .Background(new SolidColorBrush(ColorHelper.FromArgb(25, 239, 68, 68)))
            .BorderBrush(new SolidColorBrush(ColorHelper.FromArgb(120, 239, 68, 68)))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.Error).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
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
                                new FontIcon().Glyph("\uE783").FontSize(13).Foreground(new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68))),
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.Error))
                                    .FontSize(11)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),
                        new Button()
                            .Grid(column: 1)
                            .Padding(new Thickness(6, 2, 6, 2))
                            .CornerRadius(new CornerRadius(4))
                            .Command(x => x.Binding(() => vm.ClearErrorCommand))
                            .Content(new FontIcon().Glyph("\uE711").FontSize(10))
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
                                                    .Text(x => x.Binding(() => vm.QueueCount).Convert(n => $"Kö {n}"))
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
                                new Button()
                                    .Padding(new Thickness(8, 4, 8, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.OpenFolderSelectedCommand))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                            .Children(new FontIcon().Glyph("\uED25").FontSize(11), new TextBlock().Text("Visa").FontSize(11))
                                    ),
                                new Button()
                                    .Padding(new Thickness(8, 4, 8, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.DeleteWithFileSelectedCommand))
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                                            .Children(new FontIcon().Glyph("\uE74D").FontSize(11), new TextBlock().Text("Ta bort").FontSize(11))
                                    ),
                                new Button()
                                    .Padding(new Thickness(6, 4, 6, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.DeselectCommand))
                                    .Content(new FontIcon().Glyph("\uE711").FontSize(10))
                            )
                    )
            );
    }


    private static Border BuildPlayerDock(LibraryViewModel vm)
    {
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
                                            .Text(x => x.Binding(() => vm.PlayingItem).Convert(item => item?.Title ?? "Spelar"))
                                            .FontSize(12)
                                            .FontWeight(FontWeights.SemiBold)
                                            .TextTrimming(TextTrimming.CharacterEllipsis)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),
                                new Button()
                                    .Grid(column: 1)
                                    .Padding(new Thickness(6, 3, 6, 3))
                                    .CornerRadius(new CornerRadius(4))
                                    .Command(x => x.Binding(() => vm.ClosePlayerCommand))
                                    .Content(
                                        new FontIcon().Glyph("\uE711").FontSize(11)
                                    )
                            ),
#if WINDOWS
                        new MediaPlayerElement()
                            .Grid(row: 1)
                            .Height(x => x.Binding(() => vm.PlayingItem).Convert(item => item?.Kind == MediaKind.Video ? 180 : 48))
                            .AutoPlay(true)
                            .AreTransportControlsEnabled(true)
                            .Source(x => x.Binding(() => vm.PlayingItem).Convert(item =>
                                string.IsNullOrEmpty(item?.FilePath)
                                    ? null!
                                    : Windows.Media.Core.MediaSource.CreateFromUri(new Uri(item.FilePath))))
#else
                        new StackPanel()
                            .Grid(row: 1)
                            .Spacing(2)
                            .Children(
                                new TextBlock()
                                    .Text("Spelning i appen stöds endast på Windows. Filen är sparad i biblioteket.")
                                    .FontSize(11)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                            )
#endif
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
    private static Grid BuildAppBar(ShellViewModel shell, MainViewModel simpleVm)
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
                                            ? new SolidColorBrush(ColorHelper.FromArgb(255, 34, 197, 94))
                                            : new SolidColorBrush(ColorHelper.FromArgb(255, 234, 179, 8))))
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new TextBlock()
                                    .Text(x => x.Binding(() => shell.ToolStatus))
                                    .FontSize(12)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                    .VerticalAlignment(VerticalAlignment.Center)
                            )
                    ),

                new Button()
                    .Grid(column: 4)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Padding(new Thickness(10, 8, 10, 8))
                    .CornerRadius(new CornerRadius(8))
                    .Command(x => x.Binding(() => shell.ToggleThemeCommand))
                    .Content(
                        new TextBlock()
                            .Text(x => x.Binding(() => shell.IsDark).Convert(dark => dark ? "\U0001F319" : "\u2600"))
                            .FontSize(16)
                    )
            );
    }


    private static Border BuildMissingToolsCard(MainViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(16))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(new SolidColorBrush(ColorHelper.FromArgb(140, 234, 179, 8)))
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
                                    .Foreground(new SolidColorBrush(ColorHelper.FromArgb(255, 234, 179, 8))),
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





