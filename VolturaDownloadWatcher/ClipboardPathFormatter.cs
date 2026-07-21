namespace VolturaDownloadWatcher;

public static class ClipboardPathFormatter
{
    public static string Format(string fullPath)
    {
        System.ArgumentNullException.ThrowIfNull(fullPath);
        return fullPath.Contains(' ') ? $"\"{fullPath}\"" : fullPath;
    }
}
