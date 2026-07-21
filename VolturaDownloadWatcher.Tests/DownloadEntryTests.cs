namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadEntryTests
{
    [Xunit.Fact]
    public void FileTypeProperties_FollowRenamedFileExtension()
    {
        var entry = new DownloadEntry
        {
            FileName = "neon-grid.png",
            FullPath = @"C:\Users\joaki\Downloads\neon-grid.png",
            CreatedAt = System.DateTime.Now
        };
        var imageGlyph = entry.FileTypeGlyph;

        entry.FileName = "signal-decoder.exe";

        Xunit.Assert.Equal(DownloadFileType.Executable, entry.FileType);
        Xunit.Assert.True(entry.IsApplication);
        Xunit.Assert.False(entry.IsDocument);
        Xunit.Assert.NotEqual(imageGlyph, entry.FileTypeGlyph);
    }

    [Xunit.Theory]
    [Xunit.InlineData(0L, "0 B")]
    [Xunit.InlineData(987L, "987 B")]
    [Xunit.InlineData(1024L, "1 KB")]
    [Xunit.InlineData(1536L, "1.5 KB")]
    [Xunit.InlineData(12L * 1024, "12 KB")]
    [Xunit.InlineData(5L * 1024 * 1024, "5 MB")]
    [Xunit.InlineData(3L * 1024 * 1024 * 1024, "3 GB")]
    [Xunit.InlineData(2L * 1024 * 1024 * 1024 * 1024, "2 TB")]
    [Xunit.InlineData(1L * 1024 * 1024 * 1024 * 1024 * 1024, "1 PB")]
    public void FormatFileSize_UsesCompactAdaptiveUnits(long bytes, string expected)
    {
        Xunit.Assert.Equal(expected, DownloadEntry.FormatFileSize(bytes));
    }

    [Xunit.Fact]
    public void Sha256Action_IsEnabledOnlyForExistingFilesWithCalculatedHash()
    {
        var entry = new DownloadEntry
        {
            FileName = "installer.exe",
            FullPath = @"C:\Users\joaki\Downloads\installer.exe",
            CreatedAt = System.DateTime.Now,
            ExistsNow = true,
            Sha256 = new string('A', 64),
            Sha256State = Sha256State.Available
        };

        Xunit.Assert.True(entry.IsSha256Available);
        Xunit.Assert.Equal("Copy SHA-256", entry.Sha256ActionToolTip);

        entry.ExistsNow = false;

        Xunit.Assert.False(entry.IsSha256Available);
        Xunit.Assert.Equal("SHA-256 unavailable for deleted file", entry.Sha256ActionToolTip);
    }
}
