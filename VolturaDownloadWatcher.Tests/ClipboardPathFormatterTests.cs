namespace VolturaDownloadWatcher.Tests;

public sealed class ClipboardPathFormatterTests
{
    [Fact]
    public void Format_ReturnsPathWithoutQuotes_WhenPathHasNoSpaces() =>
        Assert.Equal(
            @"C:\Users\joaki\Downloads\signal-decoder.exe",
            ClipboardPathFormatter.Format(@"C:\Users\joaki\Downloads\signal-decoder.exe"));

    [Fact]
    public void Format_SurroundsPathWithQuotes_WhenPathContainsSpaces() =>
        Assert.Equal(
            "\"C:\\Users\\joaki\\Downloads\\signal decoder.exe\"",
            ClipboardPathFormatter.Format(@"C:\Users\joaki\Downloads\signal decoder.exe"));
}
