using System.IO;
using System.Text.Json;

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
    private sealed record Prefs(string? SavePath, AppMode? Mode);

    private static string FilePath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "preferences.json");

    public static string? GetSavePath()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var prefs = JsonSerializer.Deserialize<Prefs>(File.ReadAllText(FilePath));
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(path, current?.Mode)));
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(current?.SavePath, mode)));
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

            return JsonSerializer.Deserialize<Prefs>(File.ReadAllText(FilePath));
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
