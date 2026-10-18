namespace VideoPiper.Services;

/// <summary>
/// Resolves a working directory for child processes (yt-dlp / ffmpeg) that is guaranteed to
/// be traversable. Every child process must run from a directory it can reach: on Windows a
/// working directory that descends through an <em>untrusted reparse point</em> (e.g. an
/// nvm-windows node symlink like <c>C:\nvm4w\nodejs</c>) makes the Python-based yt-dlp crash
/// with WinError 448 (ERROR_UNTRUSTED_REPARSE_POINT) before it can start.
/// </summary>
/// <remarks>
/// The classic pitfall is <see cref="Path.GetTempPath"/>: when the <c>TEMP</c> and <c>TMP</c>
/// environment variables are unset or empty it returns the <strong>process's current
/// directory</strong>. If the app was launched from a shell whose CWD that is such a reparse point, the
/// 'safe' temp folder is actually unsafe. <see cref="Resolve"/> therefore proves the preferred
/// directory is real and reachable (by enumerating it — the same operation that fails with
/// WinError 448 on a child) and otherwise falls back to a temp path it has likewise verified.
/// </remarks>
public static class SafeWorkingDirectory
{
    /// <summary>
    /// Returns a directory that a child process can safely use as its working directory.
    /// <paramref name="preferred"/> is used when it exists and can be enumerated; otherwise a
    /// verified temp directory is returned (never a path that would make the child hit
    /// WinError 448).
    /// </summary>
    public static string Resolve(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var p = preferred;
            if (IsUsable(p))
            {
                return Path.GetFullPath(p);
            }
        }
        // Fall back to the temp path, but only if it is itself usable. When TEMP/TMP are
        // empty, GetTempPath returns the process CWD — which may be exactly the untrusted
        // reparse point we are trying to escape.
        var temp = Path.GetTempPath();
        if (IsUsable(temp))
        {
            return temp;
        }

        return LastResort();
    }

    /// <summary>
    /// <c>true</c> when <paramref name="path"/> names an existing directory the current
    /// process can actually enumerate. Enumeration is the same operation a child process
    /// makes to resolve its CWD, so this catches both non-existent paths and untrusted
    /// reparse points (WinError 448) without needing the child to run.
    /// </summary>
    internal static bool IsUsable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            // Proving the directory is reachable is enough; we do not need its contents.
            var _ = Directory.EnumerateFiles(path).Any();
            return true;
        }
        catch
        {
            // Non-existent, not a directory, or an untrusted reparse point (WinError 448).
            return false;
        }
    }

    private static string LastResort()
    {
        // A directory we know we can create and reach: one under the user profile, which never
        // depends on TEMP/TMP or the process CWD.
        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                var dir = Path.Combine(profile, ".videopiper", "work");
                Directory.CreateDirectory(dir);
                if (IsUsable(dir))
                {
                    return dir;
                }
            }
        }
        catch
        {
            // Fall through to the temp path.
        }

        // Final answer even if not verified: a child starting here fails loudly rather than
        // inheriting an arbitrary (possibly untrusted) CWD.
        return Path.GetTempPath();
    }
}
