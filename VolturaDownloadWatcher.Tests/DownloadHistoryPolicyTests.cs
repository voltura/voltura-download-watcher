namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadHistoryPolicyTests
{
    [Xunit.Fact]
    public void SelectEvictionCandidate_TreatsDeletedEntriesLikeEveryOtherDownload()
    {
        var oldestLive = CreateEntry("old-live.txt", 1, existsNow: true);
        var newerDeleted = CreateEntry("deleted.torrent", 2, existsNow: false);
        var newestLive = CreateEntry("new-live.txt", 3, existsNow: true);

        var selected = DownloadHistoryPolicy.SelectEvictionCandidate(
            new[] { newestLive, newerDeleted, oldestLive });

        Xunit.Assert.Same(oldestLive, selected);
    }

    [Xunit.Fact]
    public void SelectEvictionCandidate_UsesOldestEntryWhenAllAreLive()
    {
        var oldest = CreateEntry("old.txt", 1, existsNow: true);
        var newest = CreateEntry("new.txt", 2, existsNow: true);

        var selected = DownloadHistoryPolicy.SelectEvictionCandidate(new[] { newest, oldest });

        Xunit.Assert.Same(oldest, selected);
    }

    [Xunit.Fact]
    public void SelectEvictionCandidate_KeepsEarlierDeletedTorrentWhenItIsNotOldest()
    {
        var oldest = CreateEntry("oldest.zip", 1, existsNow: true);
        var firstDeletedTorrent = CreateEntry("first.torrent", 2, existsNow: false);
        var secondDeletedTorrent = CreateEntry("second.torrent", 3, existsNow: false);

        var selected = DownloadHistoryPolicy.SelectEvictionCandidate(
            new[] { secondDeletedTorrent, firstDeletedTorrent, oldest });

        Xunit.Assert.Same(oldest, selected);
        Xunit.Assert.NotSame(firstDeletedTorrent, selected);
        Xunit.Assert.NotSame(secondDeletedTorrent, selected);
    }

    private static DownloadEntry CreateEntry(string fileName, int minute, bool existsNow)
    {
        var timestamp = new System.DateTime(2026, 7, 21, 10, minute, 0, System.DateTimeKind.Local);
        return new DownloadEntry
        {
            FileName = fileName,
            FullPath = System.IO.Path.Combine("C:\\Downloads", fileName),
            CreatedAt = timestamp,
            TouchedAt = timestamp,
            ExistsNow = existsNow
        };
    }
}
