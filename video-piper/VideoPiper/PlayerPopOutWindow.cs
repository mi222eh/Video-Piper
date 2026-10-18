#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoPiper.Models;

namespace VideoPiper;

/// <summary>
/// A standalone, resizable window that plays a library item with its own
/// <see cref="MediaPlayerElement"/> and transport controls, plus a toggle to
/// fill the whole screen (<c>AppWindowPresenterKind.FullScreen</c>) and a
/// close button. Created on the UI thread; <see cref="ClosedChanged"/> fires
/// (on the UI thread) when the window is dismissed, so the caller can restore
/// the docked player. Named <c>ClosedChanged</c> (not <c>Closed</c>) to avoid
/// hiding the inherited <see cref="Microsoft.UI.Xaml.Window.Closed"/> event.
/// </summary>
public sealed class PlayerPopOutWindow : Window
{
    private Button _fsButton;
    private bool _isFullScreen;

    /// <summary>Raised on the UI thread when the pop-out window is closed.</summary>
    public event EventHandler? ClosedChanged;

    public PlayerPopOutWindow(LibraryItem item)
    {
        var isVideo = item.Kind == MediaKind.Video;
        Title = string.IsNullOrWhiteSpace(item.Title) ? "Video Piper" : item.Title;

        var media = new MediaPlayerElement
        {
            AutoPlay = false,
            AreTransportControlsEnabled = true,
            Stretch = Stretch.Uniform,
            Source = string.IsNullOrEmpty(item.FilePath)
                ? null
                : Windows.Media.Core.MediaSource.CreateFromUri(new Uri(item.FilePath))
        };

        _fsButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE740", FontSize = 12 }, // maximize
            Padding = new Microsoft.UI.Xaml.Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        _fsButton.Click += OnToggleFullScreen;
        SetTip(_fsButton, "Helskärm");

        var closeButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }, // close
            Padding = new Microsoft.UI.Xaml.Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Microsoft.UI.Xaml.Thickness(6, 0, 0, 0)
        };
        closeButton.Click += (_, _) => Close();
        SetTip(closeButton, "Stäng");

        var title = new TextBlock
        {
            Text = Title,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { title, _fsButton, closeButton }
        };
        Grid.SetColumn(_fsButton, 1);
        Grid.SetColumn(closeButton, 2);

        Content = new StackPanel
        {
            Spacing = 8,
            Padding = new Microsoft.UI.Xaml.Thickness(12),
            Children = { header, media }
        };

        Closed += (_, _) => ClosedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Icon-only buttons in this window carry tooltips.</summary>
    private static void SetTip(DependencyObject element, string text)
    {
        ToolTipService.SetToolTip(element, new ToolTip { Content = text });
    }

    /// <summary>
    /// Best-effort initial size. Must be called after <see cref="Activate"/>
    /// so the HWND exists.
    /// </summary>
    public void Configure(int width, int height)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow?.Resize(new Windows.Graphics.SizeInt32(width, height));
        }
        catch { /* sizing is best-effort */ }
    }

    private void OnToggleFullScreen(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow is null)
            {
                return;
            }

            if (_isFullScreen)
            {
                // Back to a normal window (keeps the caption/title bar).
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
                _fsButton.Content = new FontIcon { Glyph = "\uE740", FontSize = 12 };
                SetTip(_fsButton, "Helskärm");
            }
            else
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                _fsButton.Content = new FontIcon { Glyph = "\uE923", FontSize = 12 };
                SetTip(_fsButton, "Återställ helskärm");
            }
            _isFullScreen = !_isFullScreen;
        }
        catch { /* fullscreen toggle is best-effort */ }
    }
}
#endif
