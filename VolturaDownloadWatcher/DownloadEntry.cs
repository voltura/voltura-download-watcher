using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;

namespace VolturaDownloadWatcher;

public sealed class DownloadEntry : INotifyPropertyChanged
{
    private bool _isFresh = true;
    private bool _existsNow = true;
    private bool _isRemovalRecent;
    private bool _deleteRequested;
    private bool _isNewest;
    private long _fileSizeBytes;
    private long _sortPinOrder;
    private DateTime _createdAt;
    private DateTime _touchedAt = DateTime.Now;

    public required string FileName
    {
        get;
        set => SetField(ref field, value);
    }

    public required string FullPath
    {
        get;
        set => SetField(ref field, value);
    }
    public required DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    public DateTime TouchedAt
    {
        get => _touchedAt;
        set => SetField(ref _touchedAt, value);
    }

    public bool IsFresh
    {
        get => _isFresh;
        set => SetField(ref _isFresh, value);
    }

    public bool ExistsNow
    {
        get => _existsNow;
        set => SetField(ref _existsNow, value);
    }

    public bool IsRemovalRecent
    {
        get => _isRemovalRecent;
        set => SetField(ref _isRemovalRecent, value);
    }

    public bool DeleteRequested
    {
        get => _deleteRequested;
        set => SetField(ref _deleteRequested, value);
    }

    public bool DeletionLogged { get; set; }

    public bool IsNewest
    {
        get => _isNewest;
        set => SetField(ref _isNewest, value);
    }

    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set => SetField(ref _fileSizeBytes, System.Math.Max(0, value));
    }

    public long SortPinOrder
    {
        get => _sortPinOrder;
        set => SetField(ref _sortPinOrder, value);
    }

    public System.DateTime SortPinnedUntil { get; set; }

    public string DownloadedAtText => CreatedAt.ToString("MMdd HH:mm:ss", CultureInfo.InvariantCulture);
    public string FileSizeText => FormatFileSize(FileSizeBytes);
    public string DownloadedMetaText => $"{DownloadedAtText}  {FileSizeText}";
    public string FileExtension => System.IO.Path.GetExtension(FileName).ToLowerInvariant();
    public bool IsDocument => FileExtension is ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" or ".md" or ".odt" or ".xls" or ".xlsx" or ".ppt" or ".pptx";
    public bool IsArchive => FileExtension is ".zip" or ".rar" or ".7z" or ".tar" or ".gz";
    public bool IsApplication => FileExtension is ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1";
    public MediaBrush DisplayBrush => IsFresh
        ? new SolidColorBrush(WpfColor.FromRgb(0xE4, 0xFF, 0xEA))
        : ExistsNow
            ? new SolidColorBrush(WpfColor.FromRgb(0x62, 0xB7, 0x73))
            : new SolidColorBrush(WpfColor.FromRgb(0xFF, 0x33, 0x66));
    public string RemovalGlyph => ExistsNow ? string.Empty : "M6,6 L12,12 M6,12 L12,6 M4,5 H14";
    public MediaBrush RemovalNoteBrush => new SolidColorBrush(WpfColor.FromRgb(0xFF, 0x4D, 0x7A));
    public MediaBrush FreshGlowBrush => new SolidColorBrush(WpfColor.FromRgb(0x72, 0xFF, 0xA1));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (propertyName is nameof(IsFresh))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayBrush)));
        }
        else if (propertyName is nameof(ExistsNow))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemovalGlyph)));
        }
        else if (propertyName is nameof(IsNewest))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreshGlowBrush)));
        }
        else if (propertyName is nameof(FileSizeBytes))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSizeText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedMetaText)));
        }
        else if (propertyName is nameof(CreatedAt))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedAtText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedMetaText)));
        }
    }

    internal static string FormatFileSize(long bytes)
    {
        var safeBytes = System.Math.Max(0, bytes);
        if (safeBytes < 1024)
        {
            return $"{safeBytes} B";
        }

        var units = new[] { "KB", "MB", "GB", "TB", "PB" };
        var value = (double)safeBytes;
        var unitIndex = -1;
        do
        {
            value /= 1024;
            unitIndex++;
        }
        while (value >= 1024 && unitIndex < units.Length - 1);

        var format = value < 10 ? "0.#" : "0";
        return $"{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }
}
