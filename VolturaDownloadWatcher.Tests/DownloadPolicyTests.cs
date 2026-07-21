namespace VolturaDownloadWatcher.Tests;

public sealed class DownloadPolicyTests
{
    [Theory]
    [InlineData("movie.mp4.crdownload")]
    [InlineData("Unconfirmed 496738.CRDOWNLOAD")]
    [InlineData("firefox-download.part")]
    [InlineData("browser-download.partial")]
    [InlineData("browser-download.download")]
    public void IsBrowserDownloadInProgressName_DetectsBrowserTemporaryDownloads(string fileName)
    {
        Assert.True(DownloadPolicy.IsBrowserDownloadInProgressName(fileName));
    }

    [Theory]
    [InlineData("package.json")]
    [InlineData("release.exe")]
    [InlineData("ubuntu.torrent")]
    [InlineData("archive.zip")]
    public void IsValidDownloadName_AcceptsCompletedDownloads(string fileName)
    {
        Assert.True(DownloadPolicy.IsValidDownloadName(fileName));
    }

    [Theory]
    [InlineData("Unconfirmed 496738.crdownload")]
    [InlineData("download.CRDOWNLOAD")]
    [InlineData("transfer.tmp")]
    [InlineData("transfer.part")]
    [InlineData("transfer.partial")]
    [InlineData("transfer.download")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidDownloadName_RejectsStagingFiles(string fileName)
    {
        Assert.False(DownloadPolicy.IsValidDownloadName(fileName));
    }

    [Fact]
    public void ExternalDeletion_AnimatesForFourSeconds()
    {
        Assert.True(DownloadPolicy.IsRemovalAnimationActive(deleteRequested: false, removedForSeconds: 0));
        Assert.False(DownloadPolicy.IsRemovalAnimationActive(deleteRequested: false, removedForSeconds: 4));
        Assert.False(DownloadPolicy.IsRemovalAnimationActive(deleteRequested: false, removedForSeconds: 56));
    }

    [Fact]
    public void UiDeletion_AnimatesForFourSeconds()
    {
        Assert.True(DownloadPolicy.IsRemovalAnimationActive(deleteRequested: true, removedForSeconds: 0));
        Assert.False(DownloadPolicy.IsRemovalAnimationActive(deleteRequested: true, removedForSeconds: 4));
        Assert.False(DownloadPolicy.IsRemovalAnimationActive(deleteRequested: true, removedForSeconds: 56));
    }
}
