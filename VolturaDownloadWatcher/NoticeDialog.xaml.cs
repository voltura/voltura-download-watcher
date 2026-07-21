namespace VolturaDownloadWatcher;

public partial class NoticeDialog : System.Windows.Window
{
    private const uint SetWindowPosNoActivate = 0x0010;
    private readonly System.Drawing.Point? _screenAnchor;

    public NoticeDialog(string message)
        : this(message, "FILE OPERATION // INTERRUPTED", NoticeDialogTone.Error)
    {
    }

    public NoticeDialog(
        string message,
        string heading,
        NoticeDialogTone tone,
        System.Drawing.Point? screenAnchor = null)
    {
        InitializeComponent();
        _screenAnchor = screenAnchor;
        MessageText.Text = message;
        HeadingText.Text = heading;
        Title = heading;

        var color = tone switch
        {
            NoticeDialogTone.Success => System.Windows.Media.Color.FromRgb(0x7C, 0xFF, 0x9A),
            NoticeDialogTone.Update => System.Windows.Media.Color.FromRgb(0xFF, 0xCC, 0x33),
            _ => System.Windows.Media.Color.FromRgb(0xFF, 0x55, 0x74)
        };
        StatusIcon.Stroke = new System.Windows.Media.SolidColorBrush(color);
        HeadingText.Foreground = new System.Windows.Media.SolidColorBrush(color);
        StatusIcon.Data = System.Windows.Media.Geometry.Parse(tone switch
        {
            NoticeDialogTone.Success => "M2,8 L6,12 L14,3",
            NoticeDialogTone.Update => "M8,2 V11 M4,7 L8,11 L12,7 M3,14 H13",
            _ => "M8,1 L15,14 H1 Z M8,5 V9 M8,11.5 V12"
        });
        if (_screenAnchor is not null)
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            SourceInitialized += PositionNearAnchor;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        System.IntPtr hWnd,
        System.IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr hWnd);

    private void PositionNearAnchor(object? sender, System.EventArgs e)
    {
        if (_screenAnchor is not { } anchor)
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == System.IntPtr.Zero)
        {
            return;
        }

        var dpi = GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var dialogSize = new System.Drawing.Size(
            (int)System.Math.Ceiling(Width * scale),
            (int)System.Math.Ceiling(Height * scale));
        var workingArea = System.Windows.Forms.Screen.FromPoint(anchor).WorkingArea;
        var placement = DialogPlacement.CalculateNear(anchor, workingArea, dialogSize);
        SetWindowPos(
            handle,
            new System.IntPtr(-1),
            placement.X,
            placement.Y,
            dialogSize.Width,
            dialogSize.Height,
            SetWindowPosNoActivate);
    }

    private void Dismiss_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Escape or System.Windows.Input.Key.Enter)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Shell_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton is System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }
}

public enum NoticeDialogTone
{
    Error,
    Success,
    Update
}
