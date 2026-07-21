namespace VolturaDownloadWatcher;

internal static class DownloadHistoryPolicy
{
    public static DownloadEntry? SelectEvictionCandidate(
        System.Collections.Generic.IEnumerable<DownloadEntry> entries)
    {
        var ordered = entries
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.TouchedAt);

        return ordered.FirstOrDefault(entry => !entry.ExistsNow)
            ?? ordered.FirstOrDefault();
    }
}
