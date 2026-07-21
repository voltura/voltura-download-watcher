namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadEntryTests
{
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
}
