using System.Text.Json;
using VideoPiper.Models;

namespace VideoPiper.Services;

/// <summary>
/// Owns a library root directory and its <c>.videopiper/library.json</c> index.
/// The store is the single writer of the index: mutations (add/remove) apply in memory,
/// then call <see cref="SaveAsync"/> to persist atomically (temp file + rename) so a crash
/// can never corrupt the index. Layout: <c>&lt;root&gt;/&lt;Channel&gt;/[&lt;Playlist&gt;]/files</c>,
/// resolved via <see cref="ResolveTargetFolder"/>.
/// </summary>
public sealed class LibraryStore
{
    private const int SchemaVersion = 1;
    private const string FallbackFolder = "Misc";
    private const int MaxFolderNameLength = 80;

    /// <summary>
    /// Characters to strip from folder names. On Linux/macOS, <see cref="Path.GetInvalidFileNameChars"/>
    /// only forbids '/', so the Windows-invalid set is unioned in — otherwise channel/playlist names
    /// with e.g. ':' or '?' would leak into folder names (valid on Linux, broken on Windows).
    /// </summary>
    private static readonly char[] InvalidFolderNameChars =
        Path.GetInvalidFileNameChars().Concat("<>:\"|?*").Distinct().ToArray();

    private sealed record Index(int Version, List<LibraryItem> Items);

    private readonly object _gate = new();
    private readonly List<LibraryItem> _items = new();

    public LibraryStore(string rootPath) => RootPath = rootPath;

    /// <summary>The library root folder (user-selected).</summary>
    public string RootPath { get; }

    /// <summary>Dot folder holding the index and app bookkeeping.</summary>
    public string IndexDirectory => Path.Combine(RootPath, ".videopiper");

    public string IndexFilePath => Path.Combine(IndexDirectory, "library.json");

    public IReadOnlyList<LibraryItem> Items
    {
        get { lock (_gate) { return _items.ToList(); } }
    }

    /// <summary>
    /// Creates the folder structure if needed and loads the index.
    /// Items left in a downloading/queued state (interrupted runs) are marked failed.
    /// </summary>
    public Task LoadAsync()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(IndexDirectory);

            _items.Clear();
            if (File.Exists(IndexFilePath))
            {
                try
                {
                    var index = JsonSerializer.Deserialize<Index>(File.ReadAllText(IndexFilePath));
                    if (index is not null)
                    {
                        _items.AddRange(index.Items);
                    }
                }
                catch (Exception ex)
                {
                    // Corrupt index: preserve it for inspection, start fresh.
                    Log.For(typeof(LibraryStore)).LogWarning(ex, "Index {Path} kunde inte läsas och sparades som .corrupt", IndexFilePath);
                    try
                    {
                        File.Move(IndexFilePath, IndexFilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                    }
                    catch
                    {
                        // Best effort.
                    }
                }
            }

            RecoverInterrupted();
        }

        return Task.CompletedTask;
    }

    /// <summary>Adds the item or replaces the existing entry with the same id.</summary>
    public void AddOrUpdate(LibraryItem item)
    {
        lock (_gate)
        {
            var existing = _items.FirstOrDefault(i => i.Id == item.Id);
            if (existing is not null)
            {
                _items[_items.IndexOf(existing)] = item;
            }
            else
            {
                _items.Add(item);
            }
        }
    }

    /// <summary>Removes the item by id from the index. Returns the removed item, or <c>null</c> if it was not present.</summary>
    public LibraryItem? Remove(string id)
    {
        LibraryItem? removed;
        lock (_gate)
        {
            var existing = _items.FirstOrDefault(i => i.Id == id);
            if (existing is null)
            {
                return null;
            }
            _items.Remove(existing);
            removed = existing;
        }

        return removed;
    }

    /// <summary>
    /// Deletes the item's media file(s) from disk: the recorded final file plus any
    /// orphaned <c>.part</c> / fragment files left behind by an interrupted download,
    /// matched by the item's <c>[id]</c> marker in the output template. Best effort —
    /// a missing folder or locked file is silently ignored.
    /// </summary>
    public void DeleteMediaFiles(LibraryItem item)
    {
        if (string.IsNullOrEmpty(item.FilePath))
        {
            return;
        }

        var folder = Path.GetDirectoryName(item.FilePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var marker = $"[{item.Id}]";
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (file.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // A file may already in use, or the folder may have been removed; nothing to clean up.
        }
    }

    /// <summary>
    /// Removes the now-empty folders that an item's channel/playlist structure created,
    /// walking up from the item's folder to (but never including) the library root.
    /// Only truly empty folders are deleted, so siblings and unrelated content are safe.
    /// </summary>
    public void DeleteEmptyFolders(string? folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var root = Path.GetFullPath(RootPath);
        var current = Path.GetFullPath(folder);

        try
        {
            while (Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any()
                   && !string.Equals(current, root, StringComparison.OrdinalIgnoreCase)
                   && current.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(current);
                current = Path.GetDirectoryName(current) ?? string.Empty;
            }
        }
        catch
        {
            // A folder may be in use; cleanup is best effort.
        }
    }

    /// <summary>Re-syncs the index with disk: complete items whose file vanished are marked failed.</summary>
    public int SyncWithDisk()
    {
        var changed = 0;
        lock (_gate)
        {
            foreach (var item in _items)
            {
                if (item.Status == ItemStatus.Complete &&
                    (!string.IsNullOrEmpty(item.FilePath) && !File.Exists(item.FilePath)))
                {
                    item.Status = ItemStatus.Failed;
                    item.Error = "Filen hittades inte på disk.";
                    changed++;
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Resolves (but does not create) the target folder for an item:
    /// <c>&lt;root&gt;/&lt;Channel&gt;</c> or <c>&lt;root&gt;/&lt;Channel&gt;/&lt;Playlist&gt;</c>.
    /// Unknown channels fall back to <c>Misc</c>.
    /// </summary>
    public string ResolveTargetFolder(string? uploader, string? playlist)
    {
        var folder = Path.Combine(RootPath, Sanitize(uploader));
        if (!string.IsNullOrWhiteSpace(playlist))
        {
            folder = Path.Combine(folder, Sanitize(playlist));
        }

        return folder;
    }

    /// <summary>Persists the index atomically.</summary>
    public Task SaveAsync()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(IndexDirectory);

            var tempPath = IndexFilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(new Index(SchemaVersion, _items.ToList())));
            File.Move(tempPath, IndexFilePath, overwrite: true);
            Log.For(typeof(LibraryStore)).LogDebug("Sparade index med {Count} objekt", _items.Count);
        }

        return Task.CompletedTask;
    }

    private void RecoverInterrupted()
    {
        foreach (var item in _items)
        {
            if (item.Status is ItemStatus.Downloading or ItemStatus.Queued)
            {
                item.Status = ItemStatus.Failed;
                item.Error = "Nedladdningen avbröts.";
                item.Percent = null;
            }
        }
    }

    private static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return FallbackFolder;
        }

        var cleaned = new string(name.Select(c => InvalidFolderNameChars.Contains(c) ? '_' : c).ToArray()).Trim();
        if (cleaned.Length > MaxFolderNameLength)
        {
            cleaned = cleaned[..MaxFolderNameLength].TrimEnd('_', ' ');
        }

        return string.IsNullOrWhiteSpace(cleaned) ? FallbackFolder : cleaned;
    }
}
