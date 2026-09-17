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

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

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
                catch
                {
                    // Corrupt index: preserve it for inspection, start fresh.
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

    /// <summary>Removes the item by id. When <paramref name="deleteFile"/> is set, the media file is deleted too.</summary>
    public bool Remove(string id, bool deleteFile)
    {
        LibraryItem? removed;
        lock (_gate)
        {
            var existing = _items.FirstOrDefault(i => i.Id == id);
            if (existing is null)
            {
                return false;
            }
            _items.Remove(existing);
            removed = existing;
        }

        if (deleteFile && !string.IsNullOrEmpty(removed.FilePath))
        {
            try
            {
                File.Delete(removed.FilePath);
            }
            catch
            {
                // File may already be gone; the index entry is removed either way.
            }
        }

        return true;
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

        var cleaned = new string(name.Select(c => InvalidFileNameChars.Contains(c) ? '_' : c).ToArray()).Trim();
        if (cleaned.Length > MaxFolderNameLength)
        {
            cleaned = cleaned[..MaxFolderNameLength].TrimEnd('_', ' ');
        }

        return string.IsNullOrWhiteSpace(cleaned) ? FallbackFolder : cleaned;
    }
}
