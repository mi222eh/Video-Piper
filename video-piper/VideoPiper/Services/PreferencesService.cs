using System.IO;
using System.Text.Json;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Which download mode the app is in: one-off downloads, or a managed library.
/// </summary>
public enum AppMode
{
    Simple,
    Library,
}

/// <summary>
/// How removing a library item treats its media on disk: delete the file(s) (and any
/// folders left empty), or keep the files and only remove the index entry.
/// </summary>
public enum DeleteMode
{
    DeleteFiles,
    KeepFiles,
}

/// <summary>
/// Persists user preferences as JSON in the app's local data folder,
/// replacing the previous localStorage-based storage.
/// </summary>
public static class PreferencesService
{
    private sealed record Prefs(string? SavePath, AppMode? Mode, string? LibraryRoot, MediaKind? Format, DeleteMode? DeleteMode, int? MaxHeight, int? MinFps, int? WindowWidth = null, int? WindowHeight = null);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static string LocalFolderPath
    {
        get
        {
#if WINDOWS || HAS_UNO
            try
            {
                return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                // Fallback to LocalApplicationData
            }
#endif
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoPiper");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string FilePath => Path.Combine(LocalFolderPath, "preferences.json");

    public static string? GetSavePath()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var prefs = JsonSerializer.Deserialize<Prefs>(File.ReadAllText(FilePath), JsonOptions);
            return prefs?.SavePath;
        }
        catch
        {
            return null;
        }
    }

    public static void SetSavePath(string? path)
    {
        try
        {
            var current = Load();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(path, current?.Mode, current?.LibraryRoot, current?.Format, current?.DeleteMode, current?.MaxHeight, current?.MinFps, current?.WindowWidth, current?.WindowHeight), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    public static AppMode GetAppMode() => Load()?.Mode ?? AppMode.Simple;

    public static void SetAppMode(AppMode mode)
    {
        try
        {
            var current = Load();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, mode, current?.LibraryRoot, current?.Format, current?.DeleteMode, current?.MaxHeight, current?.MinFps, current?.WindowWidth, current?.WindowHeight), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    public static string? GetLibraryRoot() => Load()?.LibraryRoot;

    public static void SetLibraryRoot(string? root)
    {
        try
        {
            var current = Load();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, root, current?.Format, current?.DeleteMode, current?.MaxHeight, current?.MinFps, current?.WindowWidth, current?.WindowHeight), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    /// <summary>The preferred output format for downloads (defaults to audio/MP3).</summary>
    public static MediaKind GetFormat() => Load()?.Format ?? MediaKind.Audio;

    public static void SetFormat(MediaKind format)
    {
        try
        {
            var current = Load();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, current?.LibraryRoot, format, current?.DeleteMode, current?.MaxHeight, current?.MinFps, current?.WindowWidth, current?.WindowHeight), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    /// <summary>How removing a library item treats its files (default: delete files and emptied folders).</summary>
    public static DeleteMode GetDeleteMode() => Load()?.DeleteMode ?? DeleteMode.DeleteFiles;

    public static void SetDeleteMode(DeleteMode mode)
    {
        try
        {
            var current = Load();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, current?.LibraryRoot, current?.Format, mode, current?.MaxHeight, current?.MinFps, current?.WindowWidth, current?.WindowHeight), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    /// <summary>Maximum video height for video downloads (default: 1080).</summary>
    public static int GetMaxHeight() => Load()?.MaxHeight ?? 1080;

    public static void SetMaxHeight(int height)
    {
        try
        {
            var current = Load();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, current?.LibraryRoot, current?.Format, current?.DeleteMode, height, current?.MinFps, current?.WindowWidth, current?.WindowHeight), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    /// <summary>Minimum framerate (fps) for video downloads (default: 60; 0 = no minimum).</summary>
    public static int GetMinFps() => Load()?.MinFps ?? 60;

    public static void SetMinFps(int fps)
    {
        try
        {
            var current = Load();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, current?.LibraryRoot, current?.Format, current?.DeleteMode, current?.MaxHeight, fps, current?.WindowWidth, current?.WindowHeight), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    /// <summary>The last remembered window size (width x height), or null when never set.</summary>
    public static (int Width, int Height)? GetWindowSize()
    {
        var p = Load();
        if (p?.WindowWidth is int w && p?.WindowHeight is int h && w >= 400 && h >= 300)
        {
            return (w, h);
        }
        return null;
    }

    /// <summary>Remembers the window size (clamped to sane minimums so a collapsed window never poisons the next launch).</summary>
    public static void SetWindowSize(int width, int height)
    {
        try
        {
            var current = Load();
            width = Math.Clamp(width, 400, 4000);
            height = Math.Clamp(height, 300, 4000);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, current?.LibraryRoot, current?.Format, current?.DeleteMode, current?.MaxHeight, current?.MinFps, width, height), JsonOptions));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }
    private static Prefs? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<Prefs>(File.ReadAllText(FilePath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }
    private static string ThemeFile => Path.Combine(LocalFolderPath, "theme.json");

    public static string? GetTheme()
    {
        try
        {
            if (!File.Exists(ThemeFile))
            {
                return null;
            }
            var prefs = JsonSerializer.Deserialize<ThemePrefs>(File.ReadAllText(ThemeFile));
            return prefs?.Theme;
        }
        catch
        {
            return null;
        }
    }

    public static void SetTheme(string? theme)
    {
        try
        {
            File.WriteAllText(ThemeFile, JsonSerializer.Serialize(new ThemePrefs(theme)));
        }
        catch
        {
            // Best effort: preferences are non-critical.
        }
    }

    private sealed record ThemePrefs(string? Theme);
}
