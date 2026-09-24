using Microsoft.VisualStudio.TestTools.UnitTesting;
using VideoPiper.Models;
using VideoPiper.Services;

namespace VideoPiper.Tests;

[TestClass]
public class YtDlpJsonTests
{
    [TestMethod]
    public void TryParsePlaylist_WithValidEntries_ReturnsParsedEntries()
    {
        var sampleJson = """
        {
            "_type": "playlist",
            "title": "My Favorite Tracks",
            "entries": [
                {
                    "id": "abc12345",
                    "title": "Bohemian Rhapsody",
                    "channel": "Queen Official",
                    "duration": 355,
                    "url": "https://www.youtube.com/watch?v=abc12345"
                },
                {
                    "id": "xyz98765",
                    "title": "Don't Stop Me Now",
                    "uploader": "Queen",
                    "duration": 210
                }
            ]
        }
        """;

        var result = YtDlpJson.TryParsePlaylist(sampleJson);

        Assert.IsNotNull(result);
        var (entries, playlistTitle) = result.Value;
        Assert.AreEqual("My Favorite Tracks", playlistTitle);
        Assert.AreEqual(2, entries.Count);

        Assert.AreEqual("abc12345", entries[0].Id);
        Assert.AreEqual("Bohemian Rhapsody", entries[0].Title);
        Assert.AreEqual("Queen Official", entries[0].Uploader);
        Assert.AreEqual(355.0, entries[0].DurationSeconds);
        Assert.AreEqual("https://www.youtube.com/watch?v=abc12345", entries[0].Url);

        Assert.AreEqual("xyz98765", entries[1].Id);
        Assert.AreEqual("Don't Stop Me Now", entries[1].Title);
        Assert.AreEqual("Queen", entries[1].Uploader);
        Assert.AreEqual("https://www.youtube.com/watch?v=xyz98765", entries[1].Url);
    }

    [TestMethod]
    public void TryParsePlaylist_WithNonPlaylistJson_ReturnsNull()
    {
        var singleVideoJson = """
        {
            "id": "single123",
            "title": "Single Video",
            "uploader": "Artist"
        }
        """;

        var result = YtDlpJson.TryParsePlaylist(singleVideoJson);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseSingle_WithValidJson_ReturnsMediaEntry()
    {
        var singleJson = """
        {
            "id": "track999",
            "title": "Radio Ga Ga",
            "channel": "Queen Official",
            "duration": 343,
            "url": "https://www.youtube.com/watch?v=track999"
        }
        """;

        var entry = YtDlpJson.ParseSingle(singleJson);
        Assert.AreEqual("track999", entry.Id);
        Assert.AreEqual("Radio Ga Ga", entry.Title);
        Assert.AreEqual("Queen Official", entry.Uploader);
        Assert.AreEqual(343.0, entry.DurationSeconds);
    }
}

[TestClass]
public class LibraryItemTests
{
    [TestMethod]
    public void LibraryItem_DurationFormatting_FormatsCorrectly()
    {
        var item1 = new LibraryItem { Id = "1", Title = "Title", Uploader = "Artist", Kind = MediaKind.Audio, DurationSeconds = 215, Status = ItemStatus.Complete };
        Assert.AreEqual("3:35", item1.DurationFormatted);

        var item2 = new LibraryItem { Id = "2", Title = "Title", Uploader = "Artist", Kind = MediaKind.Video, DurationSeconds = 3665, Status = ItemStatus.Complete };
        Assert.AreEqual("1:01:05", item2.DurationFormatted);

        var item3 = new LibraryItem { Id = "3", Title = "Title", Uploader = "Artist", Kind = MediaKind.Audio, DurationSeconds = null, Status = ItemStatus.Complete };
        Assert.AreEqual(string.Empty, item3.DurationFormatted);
    }

    [TestMethod]
    public void LibraryItem_KindLabelAndGlyphs_MatchMediaKind()
    {
        var audioItem = new LibraryItem { Id = "1", Title = "Song", Uploader = "Artist", Kind = MediaKind.Audio, DurationSeconds = 100, Status = ItemStatus.Complete };
        Assert.AreEqual("MP3", audioItem.KindLabel);
        Assert.AreEqual("\uE8D6", audioItem.IconGlyph);

        var videoItem = new LibraryItem { Id = "2", Title = "Vid", Uploader = "Artist", Kind = MediaKind.Video, DurationSeconds = 100, Status = ItemStatus.Complete };
        Assert.AreEqual("MP4", videoItem.KindLabel);
        Assert.AreEqual("\uE714", videoItem.IconGlyph);
    }

    [TestMethod]
    public void LibraryItem_PropertyChanged_FiresOnStatusAndPercentChange()
    {
        var item = new LibraryItem { Id = "1", Title = "Song", Uploader = "Artist", Kind = MediaKind.Audio, DurationSeconds = 100, Status = ItemStatus.Queued };
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) changedProps.Add(e.PropertyName);
        };

        item.Status = ItemStatus.Downloading;
        item.Percent = 45.5;

        CollectionAssert.Contains(changedProps, nameof(LibraryItem.Status));
        CollectionAssert.Contains(changedProps, nameof(LibraryItem.IsDownloading));
        CollectionAssert.Contains(changedProps, nameof(LibraryItem.Percent));
        CollectionAssert.Contains(changedProps, nameof(LibraryItem.PercentFormatted));
    }
}

[TestClass]
public class LibraryStoreTests
{
    private string _tempRoot = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "VideoPiperTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task LibraryStore_AddSaveAndLoad_PreservesItems()
    {
        var store = new LibraryStore(_tempRoot);
        var item = new LibraryItem
        {
            Id = "yt_1",
            Title = "Test Song",
            Uploader = "Great Channel",
            Playlist = "Best Playlist",
            Kind = MediaKind.Audio,
            FilePath = Path.Combine(_tempRoot, "test.mp3"),
            DurationSeconds = 180,
            Status = ItemStatus.Complete
        };

        store.AddOrUpdate(item);
        await store.SaveAsync();

        var reloadedStore = new LibraryStore(_tempRoot);
        await reloadedStore.LoadAsync();

        Assert.AreEqual(1, reloadedStore.Items.Count);
        Assert.AreEqual("yt_1", reloadedStore.Items[0].Id);
        Assert.AreEqual("Test Song", reloadedStore.Items[0].Title);
        Assert.AreEqual("Great Channel", reloadedStore.Items[0].Uploader);
        Assert.AreEqual("Best Playlist", reloadedStore.Items[0].Playlist);
    }

    [TestMethod]
    public void LibraryStore_ResolveTargetFolder_SanitizesSpecialCharacters()
    {
        var store = new LibraryStore(_tempRoot);
        var folder = store.ResolveTargetFolder("Artist / Band <Official>: Star*?", "Album: The \"Hits\" | Vol? 1");
        var relative = Path.GetRelativePath(_tempRoot, folder);

        Assert.IsFalse(relative.Contains(':'));
        Assert.IsFalse(relative.Contains('*'));
        Assert.IsFalse(relative.Contains('?'));
        Assert.IsFalse(relative.Contains('"'));
        Assert.IsFalse(relative.Contains('<'));
        Assert.IsFalse(relative.Contains('>'));
        Assert.IsFalse(relative.Contains('|'));
    }
}

[TestClass]
public class SearchServiceTests
{
    [TestMethod]
    public async Task SearchAsync_WithValidQuery_ReturnsResults()
    {
        // Tests real yt-dlp search execution
        var status = await SystemService.CheckToolsAsync();
        if (!status.YtDlp.Available)
        {
            Assert.Inconclusive("yt-dlp is not available on test environment.");
            return;
        }

        var results = await SearchService.SearchAsync("queen bohemian rhapsody", CancellationToken.None);

        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Expected at least 1 search result.");
        var first = results[0];
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Id));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Title));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.MetaText));
    }
}
