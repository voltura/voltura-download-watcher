namespace VolturaDownloadWatcher.Tests;

public sealed class ActivityLogHistoryTests
{
    [Xunit.Fact]
    public void ParseRecentDownloads_RestoresDeletedTorrentWithOriginalDownloadTimeAndSize()
    {
        var rows = ActivityLog.ParseRecentDownloads(
        [
            "2026-07-21T10:00:00.0000000+02:00\tdownload\twatcher\t\"release.torrent\"\t2048 B",
            "2026-07-21T10:00:01.0000000+02:00\tdelete\texternal\t\"release.torrent\"\t2048 B"
        ],
        40);

        var row = Xunit.Assert.Single(rows);
        Xunit.Assert.Equal("release.torrent", row.FileName);
        Xunit.Assert.Equal(new System.DateTimeOffset(2026, 7, 21, 10, 0, 0, System.TimeSpan.FromHours(2)), row.DownloadedAt);
        Xunit.Assert.Equal(2048, row.SizeBytes);
        Xunit.Assert.NotNull(row.DeletedAt);
    }

    [Xunit.Fact]
    public void ParseRecentDownloads_UsesLatestDownloadAndPreservesNewestFirstOrdering()
    {
        var rows = ActivityLog.ParseRecentDownloads(
        [
            "2026-07-21T09:00:00.0000000+02:00\tdownload\twatcher\t\"same.zip\"\t10 B",
            "malformed",
            "2026-07-21T11:00:00.0000000+02:00\tdownload\twatcher\t\"other.pdf\"\t20 B",
            "2026-07-21T12:00:00.0000000+02:00\tdownload\twatcher\t\"same.zip\"\t30 B"
        ],
        40);

        Xunit.Assert.Equal(["same.zip", "other.pdf"], rows.Select(row => row.FileName));
        Xunit.Assert.Equal(30, rows[0].SizeBytes);
        Xunit.Assert.Equal(12, rows[0].DownloadedAt.Hour);
    }

    [Xunit.Fact]
    public void ParseRecentDownloads_FollowsSuccessfulRename()
    {
        var rows = ActivityLog.ParseRecentDownloads(
        [
            "2026-07-21T09:00:00.0000000+02:00\tdownload\twatcher\t\"before.exe\"\t10 B",
            "2026-07-21T09:01:00.0000000+02:00\tinteraction:rename\tok\t\"before.exe -> after.exe\"\t10 B"
        ],
        40);

        Xunit.Assert.Equal("after.exe", Xunit.Assert.Single(rows).FileName);
    }

    [Xunit.Fact]
    public void ParseRecentDownloads_RestoresCalculatedSha256()
    {
        const string hash = "B39C46CB6E74DA0C81CE23B4D48A3C977C520B8A4DBE414DF81BBD9EC644CFBA";
        var rows = ActivityLog.ParseRecentDownloads(
        [
            "2026-07-21T09:00:00.0000000+02:00\tdownload\twatcher\t\"tool.exe\"\t10 B",
            $"2026-07-21T09:00:01.0000000+02:00\tinteraction:sha256-calculated\tok;sha256={hash}\t\"tool.exe\"\t10 B"
        ],
        40);

        Xunit.Assert.Equal(hash, Xunit.Assert.Single(rows).Sha256);
    }
}
