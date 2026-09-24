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
    public MainPage()
    {
        var simpleVm = new MainViewModel();
        var libraryVm = new LibraryViewModel();
        DataContext = simpleVm;

        var tabView = new TabView();
        tabView.IsAddTabButtonVisible = false;
        tabView.Padding = new Thickness(0, 8, 0, 0);
        tabView.TabItems.Add(BuildSimpleTab(simpleVm));
        tabView.TabItems.Add(BuildLibraryTab(libraryVm));

        var rootGrid = new Grid()
            .Padding(new Thickness(16, 12, 16, 12))
            .RowDefinitions(
                new RowDefinition { Height = GridLength.Auto }, // App Header
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // TabView
            )
            .Children(
                BuildHeader(simpleVm).Grid(row: 0),
                tabView.Grid(row: 1)
            );

        this
            .Background(ThemeResource.Get<Brush>("ApplicationPageBackgroundThemeBrush"))
            .Content(rootGrid);

        _ = simpleVm.InitializeAsync();
        _ = libraryVm.InitializeAsync();
    }

    private static TabViewItem BuildSimpleTab(MainViewModel vm)
    {
        var tab = new TabViewItem();
        tab.Header = "Nedladdning";
        tab.IsClosable = false;
        tab.Content =
            new ScrollViewer()
                .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                .Content(
                    new Grid()
                        .MaxWidth(480)
                        .HorizontalAlignment(HorizontalAlignment.Center)
                        .VerticalAlignment(VerticalAlignment.Top)
                        .Padding(new Thickness(16, 16, 16, 16))
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
                );
        return tab;
    }

    private static TabViewItem BuildLibraryTab(LibraryViewModel vm)
    {
        var tab = new TabViewItem();
        tab.Header = "Bibliotek";
        tab.IsClosable = false;
        tab.Content =
            new Grid()
                .Padding(new Thickness(8, 12, 8, 8))
                .RowDefinitions(
                    new RowDefinition { Height = GridLength.Auto }, // Controls
                    new RowDefinition { Height = GridLength.Auto }, // Player (when playing)
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // Items list
                )
                .RowSpacing(12)
                .Children(
                    BuildLibraryControls(vm).Grid(row: 0),
                    BuildPlayerCard(vm).Grid(row: 1),
                    BuildItemsList(vm).Grid(row: 2)
                );
        return tab;
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

    private static StackPanel BuildLibraryControls(LibraryViewModel vm)
    {
        return new StackPanel()
            .Spacing(10)
            .Children(
                // Library root row
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .ColumnSpacing(8)
                    .Children(
                        new TextBox()
                            .Grid(column: 0)
                            .PlaceholderText("Biblioteksmapp (standard: Musik)")
                            .Text(x => x.Binding(() => vm.RootPathDisplay))
                            .IsReadOnly(true)
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .FontSize(13)
                            .Padding(new Thickness(12, 8, 12, 8))
                            .CornerRadius(new CornerRadius(6)),
                        new Button()
                            .Grid(column: 1)
                            .Command(x => x.Binding(() => vm.BrowseCommand))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .Padding(new Thickness(12, 8, 12, 8))
                            .CornerRadius(new CornerRadius(6))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph("\uED25").FontSize(14),
                                        new TextBlock().Text("Välj mapp").FontSize(13).FontWeight(FontWeights.Medium)
                                    )
                            )
                    ),

                // URL row with Paste button
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .ColumnSpacing(8)
                    .Children(
                        new TextBox()
                            .Grid(column: 0)
                            .PlaceholderText("YouTube-länk (video, album eller spellista)")
                            .Text(x => x.Binding(() => vm.Url).TwoWay().UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .FontSize(13)
                            .Padding(new Thickness(12, 8, 12, 8))
                            .CornerRadius(new CornerRadius(6)),
                        new Button()
                            .Grid(column: 1)
                            .Command(x => x.Binding(() => vm.PasteCommand))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .Padding(new Thickness(12, 8, 12, 8))
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
                    ),

                // Format toggle
                BuildLibraryFormatToggle(vm),

                // Download button
                new Button()
                    .Height(44)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .CornerRadius(new CornerRadius(8))
                    .Style(ThemeResource.Get<Style>("AccentButtonStyle"))
                    .Command(x => x.Binding(() => vm.DownloadCommand))
                    .IsEnabled(x => x.Binding(() => vm.CanDownload))
                    .Content(
                        new Grid()
                            .Children(
                                // Busy state
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
                                            .Text(x => x.Binding(() => vm.IsFetching).Convert(fetching => fetching ? "Hämtar metadata..." : "Laddar ner..."))
                                            .FontSize(14)
                                            .FontWeight(FontWeights.SemiBold)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),

                                // Ready state
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(10)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Visibility(x => x.Binding(() => vm.IsBusy).Convert(b => b ? Visibility.Collapsed : Visibility.Visible))
                                    .Children(
                                        new FontIcon().Glyph("\uE896").FontSize(16),
                                        new TextBlock().Text("Ladda ner till biblioteket").FontSize(14).FontWeight(FontWeights.SemiBold)
                                    )
                            )
                    ),

                // Progress card
                new Border()
                    .CornerRadius(new CornerRadius(12))
                    .Padding(new Thickness(16))
                    .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
                    .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
                    .BorderThickness(new Thickness(1))
                    .Visibility(x => x.Binding(() => vm.IsBusy).Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
                    .Child(
                        new StackPanel()
                            .Spacing(8)
                            .Children(
                                new Grid()
                                    .ColumnDefinitions(
                                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                                        new ColumnDefinition { Width = GridLength.Auto }
                                    )
                                    .Children(
                                        new TextBlock()
                                            .Grid(column: 0)
                                            .Text(x => x.Binding(() => vm.JobTitle))
                                            .FontSize(13)
                                            .FontWeight(FontWeights.SemiBold)
                                            .TextTrimming(TextTrimming.CharacterEllipsis),
                                        new TextBlock()
                                            .Grid(column: 1)
                                            .Text(x => x.Binding(() => vm.JobPosition))
                                            .FontSize(12)
                                            .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                            .Visibility(x => x.Binding(() => vm.JobPosition).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
                                    ),
                                new ProgressBar()
                                    .Height(6)
                                    .CornerRadius(new CornerRadius(3))
                                    .Maximum(100)
                                    .Value(x => x.Binding(() => vm.JobPercent))
                                    .IsIndeterminate(x => x.Binding(() => vm.IsFetching)),
                                new Button()
                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                    .Padding(new Thickness(12, 4, 12, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.StopDownloadCommand))
                                    .Content(
                                        new StackPanel()
                                            .Orientation(Orientation.Horizontal)
                                            .Spacing(4)
                                            .Children(
                                                new FontIcon().Glyph("\uE711").FontSize(11),
                                                new TextBlock().Text("Avbryt").FontSize(12)
                                            )
                                    )
                            )
                    ),

                // Error text card
                new Border()
                    .CornerRadius(new CornerRadius(8))
                    .Padding(new Thickness(12, 8, 12, 8))
                    .Background(new SolidColorBrush(ColorHelper.FromArgb(25, 239, 68, 68)))
                    .BorderBrush(new SolidColorBrush(ColorHelper.FromArgb(120, 239, 68, 68)))
                    .BorderThickness(new Thickness(1))
                    .Visibility(x => x.Binding(() => vm.Error).Convert(s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))
                    .Child(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                new FontIcon()
                                    .Glyph("\uE783")
                                    .FontSize(14)
                                    .Foreground(new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68)))
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.Error))
                                    .FontSize(12)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            )
                    )
            );
    }

    private static StackPanel BuildLibraryFormatToggle(LibraryViewModel vm)
    {
        return new StackPanel()
            .Spacing(4)
            .Children(
                new TextBlock()
                    .Text("Format för nedladdning")
                    .FontSize(12)
                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush")),
                new ToggleSwitch()
                    .IsOn(x => x.Binding(() => vm.IsAudioSelected).TwoWay())
                    .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                    .OffContent("MP4 (video)")
                    .OnContent("MP3 (ljud)")
            );
    }

    private static Border BuildPlayerCard(LibraryViewModel vm)
    {
        return new Border()
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(12))
            .Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .BorderThickness(new Thickness(1))
            .Visibility(x => x.Binding(() => vm.IsPlayerVisible).Convert(v => v ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new StackPanel()
                    .Spacing(8)
                    .Children(
                        // Header with Title and Close button
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
                                        new FontIcon()
                                            .Glyph("\uE768")
                                            .FontSize(14)
                                            .Foreground(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
                                            .VerticalAlignment(VerticalAlignment.Center),
                                        new TextBlock()
                                            .Text(x => x.Binding(() => vm.PlayingItem).Convert(item => item?.Title ?? "Spelar"))
                                            .FontSize(13)
                                            .FontWeight(FontWeights.SemiBold)
                                            .TextTrimming(TextTrimming.CharacterEllipsis)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),
                                new Button()
                                    .Grid(column: 1)
                                    .Padding(new Thickness(8, 4, 8, 4))
                                    .CornerRadius(new CornerRadius(6))
                                    .Command(x => x.Binding(() => vm.ClosePlayerCommand))
                                    .Content(
                                        new FontIcon().Glyph("\uE711").FontSize(12)
                                    )
                            ),
#if WINDOWS
                        new MediaPlayerElement()
                            .AutoPlay(true)
                            .AreTransportControlsEnabled(true)
                            .Source(x => x.Binding(() => vm.PlayingItem).Convert(item =>
                                string.IsNullOrEmpty(item?.FilePath)
                                    ? null!
                                    : Windows.Media.Core.MediaSource.CreateFromUri(new Uri(item.FilePath))))
#else
                        new StackPanel()
                            .Spacing(4)
                            .Children(
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.PlayingItem).Convert(item => item?.Title))
                                    .FontSize(13)
                                    .FontWeight(FontWeights.SemiBold),
                                new TextBlock()
                                    .Text("Spelning i appen stöds endast på Windows. Filen sparas i biblioteket.")
                                    .FontSize(12)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                            )
#endif
                    )
            );
    }

    private static Grid BuildItemsList(LibraryViewModel vm)
    {
        var libraryList = new ListView()
            .Grid(row: 2)
            .ItemsSource(x => x.Binding(() => vm.Items))
            .SelectedItem(x => x.Binding(() => vm.SelectedItem).TwoWay())
            .Visibility(x => x.Binding(() => vm.HasSearchResults).Convert(has => has ? Visibility.Collapsed : Visibility.Visible));
        libraryList.ItemTemplate = Application.Current.Resources["LibraryItemTemplate"] as DataTemplate;
        libraryList.SelectionChanged += (_, _) =>
        {
            vm.SelectedItem = libraryList.SelectedItem as LibraryItem;
        };

        var searchList = new ListView()
            .Grid(row: 2)
            .ItemsSource(x => x.Binding(() => vm.SearchResults))
            .IsItemClickEnabled(true)
            .Visibility(x => x.Binding(() => vm.HasSearchResults).Convert(has => has ? Visibility.Visible : Visibility.Collapsed));
        searchList.ItemTemplate = Application.Current.Resources["SearchResultTemplate"] as DataTemplate;
        searchList.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is SearchResult result)
            {
                vm.DownloadSearchResultCommand.Execute(result);
            }
        };

        // Empty state when library has no items and not searching
        var emptyState = new StackPanel()
            .Grid(row: 2)
            .VerticalAlignment(VerticalAlignment.Center)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Spacing(8)
            .Visibility(x => x.Binding(() => vm.ShowEmptyState).Convert(show => show ? Visibility.Visible : Visibility.Collapsed))
            .Children(
                new FontIcon()
                    .Glyph("\uE8D6")
                    .FontSize(36)
                    .Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush"))
                    .HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock()
                    .Text("Biblioteket är tomt")
                    .FontSize(15)
                    .FontWeight(FontWeights.SemiBold)
                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                    .HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock()
                    .Text("Klistra in en YouTube-länk ovan eller sök efter video/musik för att komma igång.")
                    .FontSize(12)
                    .Foreground(ThemeResource.Get<Brush>("TextFillColorTertiaryBrush"))
                    .TextWrapping(TextWrapping.Wrap)
                    .HorizontalAlignment(HorizontalAlignment.Center)
            );

        return new Grid()
            .RowDefinitions(
                new RowDefinition { Height = GridLength.Auto }, // Search box
                new RowDefinition { Height = GridLength.Auto }, // List header + resync / clear
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Items / search results / empty state
                new RowDefinition { Height = GridLength.Auto } // Actions for selected item
            )
            .RowSpacing(8)
            .Children(
                // Search box (debounced YouTube search)
                new Grid()
                    .Grid(row: 0)
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    )
                    .ColumnSpacing(8)
                    .Children(
                        new TextBox()
                            .Grid(column: 0)
                            .PlaceholderText("Sök på YouTube...")
                            .Text(x => x.Binding(() => vm.SearchQuery).TwoWay().UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .FontSize(13)
                            .Padding(new Thickness(12, 8, 12, 8))
                            .CornerRadius(new CornerRadius(6)),
                        new Button()
                            .Grid(column: 1)
                            .Padding(new Thickness(10, 8, 10, 8))
                            .CornerRadius(new CornerRadius(6))
                            .Command(x => x.Binding(() => vm.ClearSearchCommand))
                            .Visibility(x => x.Binding(() => vm.HasSearchResults).Convert(has => has ? Visibility.Visible : Visibility.Collapsed))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph("\uE711").FontSize(12),
                                        new TextBlock().Text("Rensa").FontSize(12)
                                    )
                            )
                    ),

                // List header + resync / search indicator
                new Grid()
                    .Grid(row: 1)
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
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.HasSearchResults).Convert(has => has ? "Sökresultat" : "Biblioteket"))
                                    .FontSize(15)
                                    .FontWeight(FontWeights.SemiBold)
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new ProgressRing()
                                    .Width(14)
                                    .Height(14)
                                    .IsActive(x => x.Binding(() => vm.IsSearching))
                                    .Visibility(x => x.Binding(() => vm.IsSearching).Convert(s => s ? Visibility.Visible : Visibility.Collapsed))
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),
                        new Button()
                            .Grid(column: 1)
                            .Padding(new Thickness(12, 6, 12, 6))
                            .CornerRadius(new CornerRadius(6))
                            .Command(x => x.Binding(() => vm.ResyncCommand))
                            .IsEnabled(x => x.Binding(() => vm.IsBusy).Convert(b => !b))
                            .Visibility(x => x.Binding(() => vm.HasSearchResults).Convert(has => has ? Visibility.Collapsed : Visibility.Visible))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .Children(
                                        new FontIcon().Glyph("\uE895").FontSize(13),
                                        new TextBlock().Text("Synkronisera").FontSize(12)
                                    )
                            )
                    ),

                // Library items + search results + empty state
                libraryList,
                searchList,
                emptyState,

                // Actions for the selected library item
                new Grid()
                    .Grid(row: 3)
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    )
                    .ColumnSpacing(8)
                    .Visibility(x => x.Binding(() => vm.IsItemSelected).Convert(selected => selected ? Visibility.Visible : Visibility.Collapsed))
                    .Children(
                        new Button()
                            .Grid(column: 0)
                            .Padding(new Thickness(6, 8, 6, 8))
                            .CornerRadius(new CornerRadius(6))
                            .Command(x => x.Binding(() => vm.PlaySelectedCommand))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Children(
                                        new FontIcon().Glyph("\uE768").FontSize(13),
                                        new TextBlock().Text("Spela").FontSize(12).FontWeight(FontWeights.Medium)
                                    )
                            ),
                        new Button()
                            .Grid(column: 1)
                            .Padding(new Thickness(6, 8, 6, 8))
                            .CornerRadius(new CornerRadius(6))
                            .Command(x => x.Binding(() => vm.OpenFolderSelectedCommand))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Children(
                                        new FontIcon().Glyph("\uED25").FontSize(13),
                                        new TextBlock().Text("Visa i mapp").FontSize(12).FontWeight(FontWeights.Medium)
                                    )
                            ),
                        new Button()
                            .Grid(column: 2)
                            .Padding(new Thickness(6, 8, 6, 8))
                            .CornerRadius(new CornerRadius(6))
                            .Command(x => x.Binding(() => vm.RemoveSelectedCommand))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Children(
                                        new FontIcon().Glyph("\uE711").FontSize(13),
                                        new TextBlock().Text("Ta bort").FontSize(12).FontWeight(FontWeights.Medium)
                                    )
                            ),
                        new Button()
                            .Grid(column: 3)
                            .Padding(new Thickness(6, 8, 6, 8))
                            .CornerRadius(new CornerRadius(6))
                            .Command(x => x.Binding(() => vm.DeleteWithFileSelectedCommand))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(6)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Children(
                                        new FontIcon().Glyph("\uE74D").FontSize(13),
                                        new TextBlock().Text("Radera fil").FontSize(12).FontWeight(FontWeights.Medium)
                                    )
                            )
                    )
            );
    }

    private static Grid BuildHeader(MainViewModel vm)
    {
        return new Grid()
            .ColumnDefinitions(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            )
            .ColumnSpacing(12)
            .Children(
                // Logo + Title
                new StackPanel()
                    .Grid(column: 0)
                    .Orientation(Orientation.Horizontal)
                    .Spacing(12)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Children(
                        new Border()
                            .Width(44)
                            .Height(44)
                            .CornerRadius(new CornerRadius(12))
                            .Background(ThemeResource.Get<Brush>("AccentFillColorDefaultBrush"))
                            .Child(
                                new FontIcon()
                                    .Glyph("\uE8D6")
                                    .FontSize(20)
                                    .Foreground(new SolidColorBrush(Colors.White))
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),
                        new StackPanel()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Spacing(2)
                            .Children(
                                new TextBlock()
                                    .Text("Video Piper")
                                    .FontSize(20)
                                    .FontWeight(FontWeights.Bold),
                                new TextBlock()
                                    .Text("YouTube Ljud & Video")
                                    .FontSize(12)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                            )
                    ),

                // yt-dlp status pill
                new Border()
                    .Grid(column: 1)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .CornerRadius(new CornerRadius(14))
                    .Padding(new Thickness(10, 5, 10, 5))
                    .Background(ThemeResource.Get<Brush>("ControlFillColorSecondaryBrush"))
                    .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
                    .BorderThickness(new Thickness(1))
                    .Visibility(x => x.Binding(() => vm.YtDlpVersion).Convert(v => string.IsNullOrEmpty(v) ? Visibility.Collapsed : Visibility.Visible))
                    .Child(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(6)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new Ellipse()
                                    .Width(8)
                                    .Height(8)
                                    .Fill(new SolidColorBrush(ColorHelper.FromArgb(255, 34, 197, 94)))
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new TextBlock()
                                    .Text(x => x.Binding(() => vm.YtDlpVersion))
                                    .FontSize(12)
                                    .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))
                                    .VerticalAlignment(VerticalAlignment.Center)
                            )
                    ),

                // Theme toggle button
                new Button()
                    .Grid(column: 2)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Padding(new Thickness(10, 8, 10, 8))
                    .CornerRadius(new CornerRadius(8))
                    .Command(x => x.Binding(() => vm.ToggleThemeCommand))
                    .Content(
                        new TextBlock()
                            .Text(x => x.Binding(() => vm.IsDark).Convert(dark => dark ? "\U0001F319" : "\u2600"))
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

                // Finished State: two buttons (Open folder + Download another)
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    )
                    .ColumnSpacing(10)
                    .Visibility(x => x.Binding(() => vm.IsFinishedNotBusy).Convert(f => f ? Visibility.Visible : Visibility.Collapsed))
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
            );
    }
}





