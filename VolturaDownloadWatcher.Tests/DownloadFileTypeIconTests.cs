namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadFileTypeIconTests
{
    [Xunit.Theory]
    [Xunit.InlineData("photo.webp", DownloadFileType.Image)]
    [Xunit.InlineData("setup.msixbundle", DownloadFileType.Executable)]
    [Xunit.InlineData("source.tar.gz", DownloadFileType.Archive)]
    [Xunit.InlineData("manual.pdf", DownloadFileType.Pdf)]
    [Xunit.InlineData("release-notes.md", DownloadFileType.Document)]
    [Xunit.InlineData("pitch-deck.pptx", DownloadFileType.Presentation)]
    [Xunit.InlineData("budget.xlsx", DownloadFileType.Spreadsheet)]
    [Xunit.InlineData("demo.mp4", DownloadFileType.Video)]
    [Xunit.InlineData("signal.flac", DownloadFileType.Audio)]
    [Xunit.InlineData("release.torrent", DownloadFileType.Torrent)]
    [Xunit.InlineData("payload.unknown", DownloadFileType.Generic)]
    public void Classify_MapsCommonExtensionsToRecognizableFamilies(string fileName, DownloadFileType expected) =>
        Xunit.Assert.Equal(expected, DownloadFileTypeIcon.Classify(fileName));

    [Xunit.Fact]
    public void GetGlyph_ReturnsVectorDataForUnknownFiles() =>
        Xunit.Assert.NotEmpty(DownloadFileTypeIcon.GetGlyph("payload.unknown"));
}
