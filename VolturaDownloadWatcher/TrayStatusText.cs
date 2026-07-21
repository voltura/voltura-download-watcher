namespace VolturaDownloadWatcher;

internal static class TrayStatusText
{
    private const int MaximumLength = 63;
    private const string IdleText = "Voltura Download Watcher";

    internal static string Build(bool isDownloadInProgress, string? completedFileName, int completedCount = 0)
    {
        var text = isDownloadInProgress
            ? "Download in progress..."
            : completedCount > 1
                ? $"{completedCount} files downloaded..."
                : string.IsNullOrWhiteSpace(completedFileName)
                ? IdleText
                : $"Downloaded file {completedFileName}";

        return text.Length <= MaximumLength
            ? text
            : $"{text[..(MaximumLength - 3)]}...";
    }
}
