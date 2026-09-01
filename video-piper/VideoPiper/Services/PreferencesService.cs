using System.IO;
using System.Text.Json;

namespace VideoPiper.Services;

/// <summary>
/// Persists user preferences (save path) as JSON in the app's local data folder,
/// replacing the previous localStorage-based storage.
/// </summary>
public static class PreferencesService
{
    private sealed record Prefs(string? SavePath);

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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Prefs(path)));
        }
        catch
        {
            // Best effort: preferences are non-critical.
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
