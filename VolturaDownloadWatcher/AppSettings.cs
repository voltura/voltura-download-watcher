namespace VolturaDownloadWatcher;

public sealed class AppSettings
{
    public bool StartMinimized { get; set; } = true;
    public bool IsMuted { get; set; } = true;
    public bool DeleteToRecycleBin { get; set; } = true;
    public DownloadSortMode SortMode { get; set; } = DownloadSortMode.Date;
    public bool SortDescending { get; set; } = true;
    public bool CloseToTrayNotificationShown { get; set; }
    public DownloadDefaultAction DefaultAction { get; set; } = DownloadDefaultAction.OpenFile;
    public System.DateTimeOffset? LastReleaseCheckUtc { get; set; }
    public string? LatestReleaseVersion { get; set; }
    public string? LatestReleaseUrl { get; set; }
    public bool CheckForUpdatesDaily { get; set; } = true;
    public int DownloadNotificationDurationSeconds { get; set; } = DownloadNotificationDurationPolicy.DefaultSeconds;
}

public enum DownloadDefaultAction
{
    OpenFile,
    ShowInExplorer,
    CopyAsPath,
    CopyFile,
    CutFile
}

public enum DownloadSortMode
{
    Date,
    Size,
    Name
}
