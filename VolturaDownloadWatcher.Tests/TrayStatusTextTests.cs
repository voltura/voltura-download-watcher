namespace VolturaDownloadWatcher.Tests;

public sealed class TrayStatusTextTests
{
    [Fact]
    public void Build_ReturnsIdleStatusWithoutActivity() =>
        Assert.Equal("Voltura Download Watcher", TrayStatusText.Build(false, null));

    [Fact]
    public void Build_PrioritizesDownloadProgress() =>
        Assert.Equal("Download in progress...", TrayStatusText.Build(true, "finished.zip"));

    [Fact]
    public void Build_ShowsCompletedFileName() =>
        Assert.Equal("Downloaded file finished.zip", TrayStatusText.Build(false, "finished.zip", 1));

    [Fact]
    public void Build_ConsolidatesDownloadBursts() =>
        Assert.Equal("12 files downloaded...", TrayStatusText.Build(false, null, 12));

    [Fact]
    public void Build_TruncatesToNotifyIconLimit()
    {
        var status = TrayStatusText.Build(false, new string('x', 100) + ".zip");

        Assert.Equal(63, status.Length);
        Assert.EndsWith("...", status);
    }
}
