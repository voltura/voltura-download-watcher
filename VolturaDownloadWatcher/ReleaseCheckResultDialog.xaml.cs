namespace VolturaDownloadWatcher;

public partial class ReleaseCheckResultDialog : System.Windows.Window
{
    private const uint SetWindowPosNoActivate = 0x0010;
    private readonly System.Drawing.Point _screenAnchor;
    public bool DownloadLatestReleaseRequested { get; private set; }

    public ReleaseCheckResultDialog(bool updateAvailable, string runningVersion, string? latestVersion, System.Drawing.Point screenAnchor)
    {
        InitializeComponent();
        _screenAnchor = screenAnchor;
        var color = updateAvailable ? System.Windows.Media.Color.FromRgb(0xFF, 0xCC, 0x33) : System.Windows.Media.Color.FromRgb(0x7C, 0xFF, 0x9A);
        var brush = new System.Windows.Media.SolidColorBrush(color);
        StatusIcon.Stroke = brush; HeadingText.Foreground = brush;
        StatusIcon.Data = System.Windows.Media.Geometry.Parse(updateAvailable ? "M8,2 V11 M4,7 L8,11 L12,7 M3,14 H13" : "M2,8 L6,12 L14,3");
        HeadingText.Text = updateAvailable ? "RELEASE SIGNAL // UPDATE AVAILABLE" : "RELEASE SIGNAL // SYSTEM CURRENT";
        MessageText.Text = updateAvailable ? $"A new version {latestVersion} exists." : "You have the latest version.";
        DownloadButton.Visibility = updateAvailable ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        SourceInitialized += PositionNearAnchor;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetDpiForWindow(System.IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr hWndInsertAfter, int x, int y, int width, int height, uint flags);

    private void PositionNearAnchor(object? sender, System.EventArgs e)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dpi = GetDpiForWindow(handle); var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var size = new System.Drawing.Size((int)System.Math.Ceiling(Width * scale), (int)System.Math.Ceiling(Height * scale));
        var placement = DialogPlacement.CalculateNear(_screenAnchor, System.Windows.Forms.Screen.FromPoint(_screenAnchor).WorkingArea, size);
        SetWindowPos(handle, new System.IntPtr(-1), placement.X, placement.Y, size.Width, size.Height, SetWindowPosNoActivate);
    }
    private void Download_Click(object sender, System.Windows.RoutedEventArgs e) { DownloadLatestReleaseRequested = true; Close(); }
    private void Ok_Click(object sender, System.Windows.RoutedEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key is System.Windows.Input.Key.Escape or System.Windows.Input.Key.Enter) { Close(); e.Handled = true; } }
    private void Shell_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.ChangedButton is System.Windows.Input.MouseButton.Left) DragMove(); }
}
