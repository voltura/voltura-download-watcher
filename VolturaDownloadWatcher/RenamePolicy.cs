namespace VolturaDownloadWatcher;

public static class RenamePolicy
{
    private static readonly System.Collections.Generic.HashSet<string> ReservedNames = new(
        new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        },
        System.StringComparer.OrdinalIgnoreCase);

    public static bool IsValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName is "." or ".."
            || !string.Equals(fileName, fileName.TrimEnd(' ', '.'), System.StringComparison.Ordinal)
            || !string.Equals(System.IO.Path.GetFileName(fileName), fileName, System.StringComparison.Ordinal)
            || fileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        return !ReservedNames.Contains(stem);
    }
}
