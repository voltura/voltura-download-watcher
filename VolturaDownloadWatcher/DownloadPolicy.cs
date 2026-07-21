namespace VolturaDownloadWatcher;

internal static class DownloadPolicy
{
    internal static bool IsBrowserDownloadInProgressName(string fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName);
        return extension.Equals(".crdownload", System.StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".part", System.StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".partial", System.StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".download", System.StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsValidDownloadName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(fileName);
        return !extension.Equals(".crdownload", System.StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".tmp", System.StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".part", System.StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".partial", System.StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".download", System.StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("Unconfirmed ", System.StringComparison.OrdinalIgnoreCase);
    }

    internal static double GetRemovalLifetimeSeconds(bool deleteRequested) => deleteRequested ? 1.5 : 60;

    internal static bool IsRemovalAnimationActive(bool deleteRequested, double removedForSeconds) =>
        removedForSeconds < 4 || (!deleteRequested && removedForSeconds >= 56);
}
