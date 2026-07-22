namespace VolturaDownloadWatcher;

internal static class DownloadHistoryPolicy
{
    public static DownloadEntry? SelectEvictionCandidate(
        System.Collections.Generic.IEnumerable<DownloadEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.TouchedAt)
            .FirstOrDefault();
    }
}
