namespace VolturaDownloadWatcher.Tests;

public sealed class StartupRegistrationTests
{
    [Fact]
    public void FormatRunCommand_QuotesExecutablePath()
    {
        Assert.Equal(
            "\"C:\\Program Files\\Voltura Download Watcher\\VolturaDownloadWatcher.exe\"",
            StartupRegistration.FormatRunCommand(@"C:\Program Files\Voltura Download Watcher\VolturaDownloadWatcher.exe"));
    }

    [Fact]
    public void AppSettings_DefaultsSoundToOff()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsMuted);
        Assert.True(settings.DeleteToRecycleBin);
        Assert.Equal(DownloadSortMode.Date, settings.SortMode);
        Assert.Equal(DownloadDefaultAction.OpenFile, settings.DefaultAction);
        Assert.True(settings.SortDescending);
        Assert.False(settings.CloseToTrayNotificationShown);
    }

    [Fact]
    public void AppSettings_DefaultsDailyUpdateChecksToOn()
    {
        var settings = new AppSettings();

        Assert.True(settings.CheckForUpdatesDaily);
    }
}
