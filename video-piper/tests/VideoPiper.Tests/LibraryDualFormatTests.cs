using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VideoPiper.Models;
using VideoPiper.Services;

namespace VideoPiper.Tests;

[TestClass]
public class LibraryDualFormatTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "videopiper-dual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    [TestMethod]
    public void LibraryItem_TracksBothFormatsAndFinalState()
    {
        var mp3 = Touch("track.mp3");
        var mp4 = Touch("track.mp4");

        var item = new LibraryItem
        {
            Id = "abc",
            Title = "Track",
            Kind = MediaKind.Video,
        };
        item.SetPath(MediaKind.Audio, mp3);
        item.SetPath(MediaKind.Video, mp4);

        Assert.IsTrue(item.HasAudioFile);
        Assert.IsTrue(item.HasVideoFile);
        Assert.IsTrue(item.HasAudio);
        Assert.IsTrue(item.HasVideo);
        Assert.AreEqual("MP3 · MP4", item.KindLabel);
        Assert.AreEqual(MediaKind.Video, item.PreferredKind);
        Assert.AreEqual(mp4, item.PathFor(MediaKind.Video));
        Assert.AreEqual(mp3, item.PathFor(MediaKind.Audio));

        item.LastPlayedKind = MediaKind.Audio;
        Assert.AreEqual(MediaKind.Audio, item.PreferredKind);
    }

    [TestMethod]
    public void LibraryItem_FallsBackToTheOnlyDownloadedFormat()
    {
        var mp3 = Touch("only-audio.mp3");
        var item = new LibraryItem
        {
            Id = "abc",
            Title = "Track",
            Kind = MediaKind.Video, // a failed video attempt left only the MP3
        };
        item.SetPath(MediaKind.Audio, mp3);

        Assert.IsTrue(item.HasAudio);
        Assert.IsFalse(item.HasVideo);
        Assert.AreEqual(MediaKind.Audio, item.PreferredKind);
        Assert.AreEqual("MP3", item.KindLabel);
    }

    [TestMethod]
    public void LibraryStore_MigratesLegacyIndexAndFindsSiblingFormat()
    {
        var folder = Path.Combine(_root, "Channel");
        Directory.CreateDirectory(folder);
        var mp4 = Path.Combine(folder, "Song [abc123].mp4");
        var mp3 = Path.Combine(folder, "Song [abc123].mp3");
        File.WriteAllText(mp4, "video");
        File.WriteAllText(mp3, "audio");

        Directory.CreateDirectory(Path.Combine(_root, ".videopiper"));
        var legacy = $$"""
        {
          "Version": 1,
          "Items": [
            {
              "Id": "abc123",
              "Title": "Song",
              "Uploader": "Channel",
              "Kind": 1,
              "Status": 2,
              "FilePath": {{JsonSerializer.Serialize(mp4)}},
              "AddedUtc": "2026-01-01T00:00:00+00:00"
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(_root, ".videopiper", "library.json"), legacy);

        var store = new LibraryStore(_root);
        store.LoadAsync().GetAwaiter().GetResult();

        var item = store.Items.Single();
        Assert.AreEqual(mp4, item.VideoFilePath);
        Assert.AreEqual(mp3, item.AudioFilePath);
        Assert.AreEqual("MP3 · MP4", item.KindLabel);
        Assert.IsNotNull(item.ThumbnailUrl);
    }

    [TestMethod]
    public void LibraryStore_SyncKeepsTheRemainingFormatAndFailsOnlyWhenEmpty()
    {
        var folder = Path.Combine(_root, "Channel");
        Directory.CreateDirectory(folder);
        var mp4 = Path.Combine(folder, "Song [abc123].mp4");
        var mp3 = Path.Combine(folder, "Song [abc123].mp3");
        File.WriteAllText(mp4, "video");
        File.WriteAllText(mp3, "audio");

        var store = new LibraryStore(_root);
        store.LoadAsync().GetAwaiter().GetResult();
        var item = new LibraryItem
        {
            Id = "abc123",
            Title = "Song",
            Kind = MediaKind.Video,
            Status = ItemStatus.Complete,
        };
        item.SetPath(MediaKind.Audio, mp3);
        item.SetPath(MediaKind.Video, mp4);
        store.AddOrUpdate(item);

        File.Delete(mp3);
        var firstSync = store.SyncWithDisk();
        Assert.AreEqual(1, firstSync);
        Assert.IsNull(item.AudioFilePath);
        Assert.AreEqual(ItemStatus.Complete, item.Status);

        File.Delete(mp4);
        // One change for the forgotten path, one for the failed status.
        Assert.AreEqual(2, store.SyncWithDisk());
        Assert.AreEqual(ItemStatus.Failed, item.Status);
    }

    [TestMethod]
    public void BuildStreamFormat_PrefersH264WithinTheHeightCap()
    {
        var format = DownloadService.BuildStreamFormat(1080, 60);

        StringAssert.Contains(format, "vcodec^=avc1");
        StringAssert.Contains(format, "height<=1080");
        StringAssert.Contains(format, "fps>=60");
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "x");
        return path;
    }
}
