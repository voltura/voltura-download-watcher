namespace VolturaDownloadWatcher;

public partial class ConfirmationDialog : System.Windows.Window
{
    private const uint SetWindowPosNoActivate = 0x0010;
    private readonly System.Drawing.Point _screenAnchor;

    public ConfirmationDialog(string message, System.Drawing.Point screenAnchor)
    {
        InitializeComponent();
        MessageText.Text = message;
        _screenAnchor = screenAnchor;
        SourceInitialized += PositionNearAnchor;
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
        var workingArea = System.Windows.Forms.Screen.FromPoint(_screenAnchor).WorkingArea;
        var placement = DialogPlacement.CalculateNear(_screenAnchor, workingArea, dialogSize);
        SetWindowPos(
            handle,
            new System.IntPtr(-1),
            placement.X,
            placement.Y,
            dialogSize.Width,
            dialogSize.Height,
            SetWindowPosNoActivate);
    }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
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
