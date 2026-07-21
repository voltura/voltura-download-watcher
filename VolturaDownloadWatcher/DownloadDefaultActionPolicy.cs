namespace VolturaDownloadWatcher;

public static class DownloadDefaultActionPolicy
{
    public static DownloadDefaultAction Normalize(DownloadDefaultAction action) =>
        System.Enum.IsDefined(action) ? action : DownloadDefaultAction.OpenFile;
}
