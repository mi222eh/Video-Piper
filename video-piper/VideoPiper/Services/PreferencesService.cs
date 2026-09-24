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
/// Persists user preferences as JSON in the app's local data folder,
/// replacing the previous localStorage-based storage.
/// </summary>
public static class PreferencesService
{
    private sealed record Prefs(string? SavePath, AppMode? Mode, string? LibraryRoot, MediaKind? Format);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static string FilePath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "preferences.json");

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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(path, current?.Mode, current?.LibraryRoot, current?.Format), JsonOptions));
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, mode, current?.LibraryRoot, current?.Format), JsonOptions));
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, root, current?.Format), JsonOptions));
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, current?.Mode, current?.LibraryRoot, format), JsonOptions));
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
    private static string ThemeFile => Path.Combine(ApplicationData.Current.LocalFolder.Path, "theme.json");

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
