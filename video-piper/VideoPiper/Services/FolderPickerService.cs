using Windows.Storage.Pickers;

namespace VideoPiper.Services;

/// <summary>
/// Opens the native folder picker (Windows.Storage.Pickers is supported on Windows).
/// </summary>
public static class FolderPickerService
{
    public static async Task<string?> PickFolderAsync()
    {
        try
        {
            var picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.MusicLibrary
            };
            picker.FileTypeFilter.Add("*");

#if WINDOWS
            if (App.MainWindowInstance is not null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }
            }
#endif

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FolderPicker failed: {ex.Message}");
            return null;
        }
    }
}

