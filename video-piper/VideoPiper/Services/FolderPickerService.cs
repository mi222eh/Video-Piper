#if WINDOWS
using Windows.Storage.Pickers;
#endif

namespace VideoPiper.Services;

/// <summary>
/// Opens the native folder picker on Windows (Windows.Storage.Pickers).
/// On non-Windows targets (Skia desktop) there is no native WinUI picker, so this
/// returns null and the caller falls back to a manual path / default Music folder.
/// </summary>
public static class FolderPickerService
{
    public static async Task<string?> PickFolderAsync()
    {
#if WINDOWS
        try
        {
            var picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.MusicLibrary
            };
            picker.FileTypeFilter.Add("*");

            if (App.MainWindowInstance is not null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }
            }

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FolderPicker failed: {ex.Message}");
            return null;
        }
#else
        // No native WinUI folder picker on non-Windows targets. The UI exposes an
        // editable path field, so the user can type a location or use the default.
        await Task.CompletedTask;
        return null;
#endif
    }
}

