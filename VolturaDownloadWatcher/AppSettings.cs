namespace VolturaDownloadWatcher;

public sealed class AppSettings
{
    public bool IsMuted { get; set; } = true;
    public bool DeleteToRecycleBin { get; set; } = true;
    public DownloadSortMode SortMode { get; set; } = DownloadSortMode.Date;
    public bool SortDescending { get; set; } = true;
    public bool CloseToTrayNotificationShown { get; set; }
}

public enum DownloadSortMode
{
    Date,
    Size,
    Name
}
