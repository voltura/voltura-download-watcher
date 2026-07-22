namespace VolturaDownloadWatcher;

public enum DownloadNotificationAction
{
    Default,
    CopyFile,
    CopyAsPath,
    CutFile,
    Rename,
    CopySha256,
    Delete
}

public enum DownloadNotificationActionOutcome
{
    Succeeded,
    Cancelled,
    Failed
}

public static class DownloadNotificationActionPolicy
{
    public static bool ShouldDismiss(DownloadNotificationActionOutcome outcome) =>
        outcome is DownloadNotificationActionOutcome.Succeeded;
}
