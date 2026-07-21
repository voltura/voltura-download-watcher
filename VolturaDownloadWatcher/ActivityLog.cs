namespace VolturaDownloadWatcher;

internal static class ActivityLog
{
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "VolturaDownloadWatcher",
        "activity.txt");
    private static System.DateTime _activeDate;

    public static string EnsureCurrentFile()
    {
        try
        {
            lock (SyncRoot)
            {
                EnsureCurrentDay();
                if (!System.IO.File.Exists(LogPath))
                {
                    System.IO.File.WriteAllText(LogPath, string.Empty, System.Text.Encoding.UTF8);
                }
            }
        }
        catch (System.IO.IOException)
        {
        }
        catch (System.UnauthorizedAccessException)
        {
        }

        return LogPath;
    }

    public static void WriteDownload(string fileName, long sizeBytes) =>
        Write("download", "watcher", fileName, sizeBytes);

    public static void WriteDeletion(string source, string fileName, long sizeBytes) =>
        Write("delete", source, fileName, sizeBytes);

    private static void Write(string activity, string source, string fileName, long sizeBytes)
    {
        try
        {
            lock (SyncRoot)
            {
                EnsureCurrentDay();
                var safeName = fileName.Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");
                var line = $"{System.DateTime.Now:O}\t{activity}\t{source}\t\"{safeName}\"\t{System.Math.Max(0, sizeBytes)} B{System.Environment.NewLine}";
                System.IO.File.AppendAllText(LogPath, line, System.Text.Encoding.UTF8);
            }
        }
        catch (System.IO.IOException)
        {
        }
        catch (System.UnauthorizedAccessException)
        {
        }
    }

    private static void EnsureCurrentDay()
    {
        var today = System.DateTime.Today;
        if (_activeDate == today)
        {
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(LogPath)!;
        System.IO.Directory.CreateDirectory(directory);
        if (System.IO.File.Exists(LogPath)
            && System.IO.File.GetLastWriteTime(LogPath).Date != today)
        {
            System.IO.File.Delete(LogPath);
        }

        _activeDate = today;
    }
}
