namespace VolturaDownloadWatcher;

public enum DownloadFileType
{
    Generic,
    Image,
    Executable,
    Archive,
    Pdf,
    Document,
    Presentation,
    Spreadsheet,
    Video,
    Audio,
    Torrent
}

public static class DownloadFileTypeIcon
{
    public static DownloadFileType Classify(string fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg" or ".ico"
                or ".tif" or ".tiff" or ".avif" or ".heic" or ".apng" => DownloadFileType.Image,
            ".exe" or ".msi" or ".msix" or ".appx" or ".appxbundle" or ".msixbundle" or ".bat"
                or ".cmd" or ".com" or ".ps1" or ".jar" => DownloadFileType.Executable,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".tgz" or ".bz" or ".bz2" or ".xz"
                or ".cab" or ".iso" or ".zst" => DownloadFileType.Archive,
            ".pdf" => DownloadFileType.Pdf,
            ".txt" or ".md" or ".rtf" or ".doc" or ".docx" or ".odt" or ".epub" or ".mobi"
                or ".html" or ".htm" or ".json" or ".xml" or ".yaml" or ".yml" or ".log" or ".nfo"
                or ".ini" or ".config" => DownloadFileType.Document,
            ".ppt" or ".pptx" or ".odp" or ".key" => DownloadFileType.Presentation,
            ".xls" or ".xlsx" or ".ods" or ".csv" or ".tsv" => DownloadFileType.Spreadsheet,
            ".mp4" or ".webm" or ".mkv" or ".avi" or ".mov" or ".m4v" or ".mpg" or ".mpeg"
                or ".wmv" or ".flv" => DownloadFileType.Video,
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".opus" or ".m4a" or ".aac" or ".wma"
                => DownloadFileType.Audio,
            ".torrent" => DownloadFileType.Torrent,
            _ => DownloadFileType.Generic
        };
    }

    public static string GetGlyph(string fileName) => GetGlyph(Classify(fileName));

    public static string GetGlyph(DownloadFileType fileType) => fileType switch
    {
        DownloadFileType.Image => "M3,3 H15 V15 H3 Z M5,12 L8,8 L10,10 L12,7 L15,11 M6,6 A1,1 0 1 0 6,8 A1,1 0 1 0 6,6",
        DownloadFileType.Executable => "M2,4 H16 V14 H2 Z M2,7 H16 M4,5.5 H5 M7,5.5 H8 M5,10 L7,11.5 L5,13 M9,13 H13",
        DownloadFileType.Archive => "M4,2 H13 L15,4 V16 H4 Z M9,2 V4 H11 V6 H9 V8 H11 V10 H9 V12 H11 M8,14 H12",
        DownloadFileType.Pdf => "M4,2 H11 L15,6 V16 H4 Z M11,2 V6 H15 M6,9 H13 M6,12 H11 M6,14 H9",
        DownloadFileType.Document => "M4,2 H11 L15,6 V16 H4 Z M11,2 V6 H15 M6,9 H13 M6,11.5 H13 M6,14 H11",
        DownloadFileType.Presentation => "M3,3 H15 V11 H3 Z M6,14 L9,11 L12,14 M6,7 H8 V9 H6 Z M10,5 H13 V9 H10 Z",
        DownloadFileType.Spreadsheet => "M3,3 H15 V15 H3 Z M3,7 H15 M3,11 H15 M7,3 V15 M11,3 V15",
        DownloadFileType.Video => "M2,4 H16 V14 H2 Z M7,7 L12,9 L7,12 Z",
        DownloadFileType.Audio => "M11,3 V12 M11,4 L15,3 V10 M8.5,11 A2.5,2 0 1 0 8.5,15 A2.5,2 0 1 0 8.5,11 M12.5,9 A2.5,2 0 1 0 12.5,13 A2.5,2 0 1 0 12.5,9",
        DownloadFileType.Torrent => "M9,3 V10 M6,7 L9,10 L12,7 M4,13 H14 M3,15 H15 M4,4 A1,1 0 1 0 4,6 A1,1 0 1 0 4,4 M14,4 A1,1 0 1 0 14,6 A1,1 0 1 0 14,4",
        _ => "M4,2 H11 L15,6 V16 H4 Z M11,2 V6 H15 M7,10 H12 M7,13 H10"
    };
}
