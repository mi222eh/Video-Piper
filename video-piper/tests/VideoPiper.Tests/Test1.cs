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

    [TestMethod]
    public void GetDescription_WithDescription_ReturnsIt()
    {
        var json = """
        {
            "id": "track999",
            "title": "Radio Ga Ga",
            "description": "Official music video from Queen."
        }
        """;

        Assert.AreEqual("Official music video from Queen.", YtDlpJson.GetDescription(json));
    }

    [TestMethod]
    public void GetDescription_MissingDescription_ReturnsNull()
    {
        var json = """
        {
            "id": "track999",
            "title": "Radio Ga Ga"
        }
        """;

        Assert.IsNull(YtDlpJson.GetDescription(json));
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
        var system = new SystemService(new ToolUpdateService());
        var status = await system.CheckToolsAsync();
        if (!status.YtDlp.Available)
        {
            Assert.Inconclusive("yt-dlp is not available on test environment.");
            return;
        }

        var search = new SearchService(system);
        var results = await search.SearchAsync("queen bohemian rhapsody", CancellationToken.None);

        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Expected at least 1 search result.");
        var first = results[0];
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Id));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Title));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.MetaText));
    }

    [TestMethod]
    public void DetailText_WithUploaderAndDuration_FormatsBoth()
    {
        var r = new SearchResult("abc", "Title", "jawed", 2520.5);
        Assert.AreEqual("Av jawed · 42:00", r.DetailText);
    }

    [TestMethod]
    public void DetailText_WithHourLength_FormatsHms()
    {
        var r = new SearchResult("abc", "Title", "jawed", 3703.0);
        Assert.AreEqual("Av jawed · 1:01:43", r.DetailText);
    }

    [TestMethod]
    public void DetailText_NoUploaderNoDuration_ReturnsNull()
    {
        var r = new SearchResult("abc", "Title", null, null);
        Assert.IsNull(r.DetailText);
    }
}

[TestClass]
public class DownloadServiceTests
{
    [TestMethod]
    public void ParseDestination_SingleDestinationLine_ReturnsPath()
    {
        var path = DownloadService.ParseDestination(new[]
        {
            "[destination] /home/user/Music/Queen - Bohemian Rhapsody.mp3",
        });

        Assert.AreEqual("/home/user/Music/Queen - Bohemian Rhapsody.mp3", path);
    }

    [TestMethod]
    public void ParseDestination_MultipleDestinationLines_ReturnsLastPath()
    {
        // yt-dlp can restate the destination after merging/conversion — the final path wins.
        var path = DownloadService.ParseDestination(new[]
        {
            "[destination] /music/Video [abc123].mp4.part",
            "[download] 100% of known size",
            "[destination] /music/Video [abc123].mp4",
        });

        Assert.AreEqual("/music/Video [abc123].mp4", path);
    }

    [TestMethod]
    public void ParseDestination_NoDestinationLines_ReturnsNull()
    {
        var path = DownloadService.ParseDestination(new[]
        {
            "[download]  42.0% of 10.00MiB at  1.00MiB/s ETA 00:10",
        });

        Assert.IsNull(path);
    }

    [TestMethod]
    public void ParseDestination_EmptyLines_ReturnsNull()
    {
        Assert.IsNull(DownloadService.ParseDestination(Array.Empty<string>()));
    }

    [TestMethod]
    public void BuildVideoFormat_NoFpsMinimum_ReturnsHeightCappedFormat()
    {
        var format = DownloadService.BuildVideoFormat(1080, 0);

        Assert.IsTrue(format.Contains("height<=1080"));
        Assert.IsFalse(format.Contains("fps>="), "no fps branch when minFps <= 0");
        // Starts with the height-capped best video branch.
        Assert.IsTrue(format.StartsWith("bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/"));
    }

    [TestMethod]
    public void BuildVideoFormat_WithFpsMinimum_PrefersFpsBranchFirstThenFallsBack()
    {
        var format = DownloadService.BuildVideoFormat(1080, 60);

        // The preferred (fps>=60) branch comes first, then the height-capped fallback.
        Assert.IsTrue(format.StartsWith("bestvideo[ext=mp4][height<=1080][fps>=60]+bestaudio[ext=m4a]/"));
        Assert.IsTrue(format.Contains("height<=1080"));
        Assert.IsTrue(format.Contains("fps>=60"));
    }

    [TestMethod]
    public void BuildVideoFormat_HeightCapAppliedInBothBranches()
    {
        var format = DownloadService.BuildVideoFormat(720, 30);

        // The height cap is applied to every video sub-branch (fps-preferred + both fallbacks).
        var occurrences = format.Split("height<=720").Length - 1;
        Assert.AreEqual(3, occurrences, "height cap appears in the fps-preferred and both fallback branches");
    }

    [TestMethod]
    public void BuildVideoOnlyFormat_NeverIncludesAudio()
    {
        Assert.IsFalse(DownloadService.BuildVideoOnlyFormat(1080, 0).Contains("bestaudio"));
        Assert.IsFalse(DownloadService.BuildVideoOnlyFormat(1080, 60).Contains("bestaudio"));
    }

    [TestMethod]
    public void BuildVideoOnlyFormat_NoFpsMinimum_ReturnsHeightCappedVideoOnly()
    {
        var format = DownloadService.BuildVideoOnlyFormat(1080, 0);

        Assert.IsTrue(format.StartsWith("bestvideo[ext=mp4][height<=1080]/"));
        Assert.IsFalse(format.Contains("fps>="), "no fps branch when minFps <= 0");
        Assert.IsFalse(format.Contains("+"), "video-only format never requests an audio stream");
    }

    [TestMethod]
    public void BuildVideoOnlyFormat_WithFpsMinimum_PrefersFpsBranchFirstThenFallsBack()
    {
        var format = DownloadService.BuildVideoOnlyFormat(720, 30);

        Assert.IsTrue(format.StartsWith("bestvideo[ext=mp4][height<=720][fps>=30]/"));
        // The height cap is applied to the fps-preferred and both fallback branches.
        Assert.AreEqual(3, format.Split("height<=720").Length - 1);
    }

    [TestMethod]
    public void BuildVideoOnlyFormat_FallsBackToAnyBestVideo()
    {
        // The last resort is any best video (no height/codec constraints) — never a combined stream.
        var format = DownloadService.BuildVideoOnlyFormat(480, 0);

        Assert.IsTrue(format.EndsWith("/bestvideo"));
        Assert.IsFalse(format.EndsWith("/best") || format.EndsWith("best[ext=mp4]"), "no combined-stream fallback");
    }
}

[TestClass]
public class ToolUpdateServiceTests
{
    [TestMethod]
    public void IsNewerVersion_MissingCurrent_ReturnsTrue()
    {
        Assert.IsTrue(ToolUpdateService.IsNewerVersion(null, "2026.08.19"));
        Assert.IsTrue(ToolUpdateService.IsNewerVersion("", "2026.08.19"));
    }

    [TestMethod]
    public void IsNewerVersion_UnparseableCurrent_ReturnsTrue()
    {
        Assert.IsTrue(ToolUpdateService.IsNewerVersion("not a version", "2026.08.19"));
    }

    [TestMethod]
    public void IsNewerVersion_UnparseableCandidate_ReturnsFalse()
    {
        Assert.IsFalse(ToolUpdateService.IsNewerVersion("2026.08.19", "not a version"));
    }

    [TestMethod]
    public void IsNewerVersion_CandidateIsNewer_ReturnsTrue()
    {
        Assert.IsTrue(ToolUpdateService.IsNewerVersion("2026.07.04", "2026.08.19"));
        Assert.IsTrue(ToolUpdateService.IsNewerVersion("7.1", "7.2"));
        Assert.IsTrue(ToolUpdateService.IsNewerVersion("2026.08.19", "2026.08.20"));
    }

    [TestMethod]
    public void IsNewerVersion_CandidateIsOlderOrEqual_ReturnsFalse()
    {
        Assert.IsFalse(ToolUpdateService.IsNewerVersion("2026.08.19", "2026.07.04"));
        Assert.IsFalse(ToolUpdateService.IsNewerVersion("2026.08.19", "2026.08.19"));
        Assert.IsFalse(ToolUpdateService.IsNewerVersion("7.1", "7.1"));
    }

    [TestMethod]
    public void IsNewerVersion_DiffersInLength_ReturnsTrueWhenCandidateLonger()
    {
        // 7.1 vs 7.1.1 — the longer candidate is newer.
        Assert.IsTrue(ToolUpdateService.IsNewerVersion("7.1", "7.1.1"));
        Assert.IsFalse(ToolUpdateService.IsNewerVersion("7.1.1", "7.1"));
    }

    [TestMethod]
    public void TryParseVersion_Empty_ReturnsFalse()
    {
        Assert.IsFalse(ToolUpdateService.TryParseVersion(null, out _));
        Assert.IsFalse(ToolUpdateService.TryParseVersion("", out _));
        Assert.IsFalse(ToolUpdateService.TryParseVersion("abc", out _));
    }

    [TestMethod]
    public void TryParseVersion_FfmpegHeader_ReturnsParts()
    {
        Assert.IsTrue(ToolUpdateService.TryParseVersion("ffmpeg version 7.1-full_build Copyright (c) 2000-2025", out var parts));
        CollectionAssert.AreEqual(new[] { 7, 1 }, parts);
    }

    [TestMethod]
    public void FormatVersion_YtDlpOutput_ReturnsVersion()
    {
        Assert.AreEqual("2026.08.19", ToolUpdateService.FormatVersion("2026.08.19"));
    }

    [TestMethod]
    public void FormatVersion_FfmpegOutput_ReturnsVersion()
    {
        Assert.AreEqual("7.1", ToolUpdateService.FormatVersion("ffmpeg version 7.1-full_build Copyright (c) 2000-2025 the FFmpeg developers"));
    }

    [TestMethod]
    public void FormatVersion_NoVersion_ReturnsToDetect()
    {
        Assert.IsNull(ToolUpdateService.FormatVersion(null));
        Assert.IsNull(ToolUpdateService.FormatVersion("no version here"));
    }
}

[TestClass]
public class SafeWorkingDirectoryTests
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
    public void Resolve_ExistingDirectory_ReturnsItsFullPath()
    {
        var result = SafeWorkingDirectory.Resolve(_tempRoot);
        Assert.AreEqual(Path.GetFullPath(_tempRoot), result);
    }

    [TestMethod]
    public void Resolve_MissingDirectory_FallsBackToUsableDirectory()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist", "deeper");
        var result = SafeWorkingDirectory.Resolve(missing);

        Assert.AreNotEqual(Path.GetFullPath(missing), result);
        Assert.IsTrue(Directory.Exists(result), $"fallback {result} should exist");
        Assert.IsTrue(SafeWorkingDirectory.IsUsable(result), $"fallback {result} should be usable");
    }

    [TestMethod]
    public void Resolve_NullPreferred_FallsBackToUsableDirectory()
    {
        var result = SafeWorkingDirectory.Resolve(null);
        Assert.IsTrue(Directory.Exists(result), $"fallback {result} should exist");
        Assert.IsTrue(SafeWorkingDirectory.IsUsable(result), $"fallback {result} should be usable");
    }

    [TestMethod]
    public void IsUsable_ExistingDirectory_ReturnsTrue()
    {
        Assert.IsTrue(SafeWorkingDirectory.IsUsable(_tempRoot));
    }

    [TestMethod]
    public void IsUsable_NonExistentAndFilePaths_ReturnFalse()
    {
        Assert.IsFalse(SafeWorkingDirectory.IsUsable(null));
        Assert.IsFalse(SafeWorkingDirectory.IsUsable(string.Empty));
        Assert.IsFalse(SafeWorkingDirectory.IsUsable(Path.Combine(_tempRoot, "nope")));

        var file = Path.Combine(_tempRoot, "a.txt");
        File.WriteAllText(file, "x");
        Assert.IsFalse(SafeWorkingDirectory.IsUsable(file), "a file is not a usable working directory");
    }
}
