namespace VolturaDownloadWatcher;

public partial class RenameDialog : System.Windows.Window
{
    private readonly string _sourcePath;
    private bool _awaitingOverwrite;
    private string? _pendingTargetPath;

    public RenameDialog(string sourcePath)
    {
        InitializeComponent();
        _sourcePath = sourcePath;
        FileNameBox.Text = System.IO.Path.GetFileName(sourcePath);
        Loaded += (_, _) =>
        {
            var extensionLength = System.IO.Path.GetExtension(FileNameBox.Text).Length;
            FileNameBox.Focus();
            FileNameBox.Select(0, System.Math.Max(0, FileNameBox.Text.Length - extensionLength));
        };
    }

    public string NewFileName { get; private set; } = string.Empty;

    public bool OverwriteExisting { get; private set; }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var candidate = FileNameBox.Text;
        if (!RenamePolicy.IsValidFileName(candidate))
        {
            _awaitingOverwrite = false;
            StatusText.Text = "INVALID // USE A WINDOWS FILE NAME";
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x5A, 0x73));
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(_sourcePath)!;
        var targetPath = System.IO.Path.Combine(directory, candidate);
        var sameFile = string.Equals(targetPath, _sourcePath, System.StringComparison.OrdinalIgnoreCase);
        if (!sameFile && System.IO.File.Exists(targetPath))
        {
            if (!_awaitingOverwrite || !string.Equals(_pendingTargetPath, targetPath, System.StringComparison.OrdinalIgnoreCase))
            {
                _awaitingOverwrite = true;
                _pendingTargetPath = targetPath;
                StatusText.Text = $"OVERWRITE // {candidate}";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xCB, 0x45));
                FileNameBox.IsReadOnly = true;
                return;
            }

            OverwriteExisting = true;
        }

        NewFileName = candidate;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;

    private void FileNameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsInitialized || FileNameBox.IsReadOnly)
        {
            return;
        }

        _awaitingOverwrite = false;
        _pendingTargetPath = null;
        StatusText.Text = RenamePolicy.IsValidFileName(FileNameBox.Text) ? string.Empty : "INVALID FILE NAME";
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x77, 0x9B, 0x80));
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key is System.Windows.Input.Key.Enter)
        {
            Confirm_Click(this, new System.Windows.RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void Shell_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton is System.Windows.Input.MouseButton.Left
            && e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            DragMove();
        }
    }
}
