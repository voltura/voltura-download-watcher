namespace VolturaDownloadWatcher;

public partial class DownloadNotificationWindow : System.Windows.Window, System.IDisposable
{
    private const uint SetWindowPosNoActivate = 0x0010;
    private readonly System.Func<DownloadNotificationAction, DownloadEntry, DownloadNotificationActionOutcome> _invokeAction;
    private readonly System.Windows.Threading.DispatcherTimer _dismissTimer;
    private bool _interactionDetected;
    private bool _allowClose;

    public DownloadNotificationWindow(
        System.Func<DownloadNotificationAction, DownloadEntry, DownloadNotificationActionOutcome> invokeAction)
    {
        InitializeComponent();
        _invokeAction = invokeAction;
        _dismissTimer = new System.Windows.Threading.DispatcherTimer();
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            if (!_interactionDetected)
            {
                Hide();
            }
        };
        SourceInitialized += (_, _) => PositionAboveNotificationArea();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern System.IntPtr FindWindow(string? className, string? windowName);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern System.IntPtr FindWindowEx(
        System.IntPtr parentHandle,
        System.IntPtr childAfter,
        string? className,
        string? windowName);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(System.IntPtr windowHandle, out NativeRect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        System.IntPtr windowHandle,
        System.IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr windowHandle);

    public void ShowDownload(DownloadEntry entry, int durationSeconds)
    {
        DataContext = entry;
        _interactionDetected = false;
        _dismissTimer.Stop();

        if (!IsVisible)
        {
            Show();
        }

        PositionAboveNotificationArea();
        _interactionDetected = IsMouseOver || IsKeyboardFocusWithin;
        PanelShell.BeginAnimation(
            System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.45,
                To = 1,
                Duration = System.TimeSpan.FromMilliseconds(180)
            });

        if (durationSeconds > 0 && !_interactionDetected)
        {
            _dismissTimer.Interval = System.TimeSpan.FromSeconds(durationSeconds);
            _dismissTimer.Start();
        }
    }

    public void Dismiss()
    {
        _dismissTimer.Stop();
        Hide();
    }

    private void PositionAboveNotificationArea()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == System.IntPtr.Zero)
        {
            return;
        }

        var taskbarHandle = FindWindow("Shell_TrayWnd", null);
        var notificationHandle = taskbarHandle == System.IntPtr.Zero
            ? System.IntPtr.Zero
            : FindWindowEx(taskbarHandle, System.IntPtr.Zero, "TrayNotifyWnd", null);
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        var notificationArea = screen?.Bounds ?? System.Drawing.Rectangle.Empty;
        if (notificationHandle != System.IntPtr.Zero && GetWindowRect(notificationHandle, out var nativeRect))
        {
            notificationArea = System.Drawing.Rectangle.FromLTRB(
                nativeRect.Left,
                nativeRect.Top,
                nativeRect.Right,
                nativeRect.Bottom);
            screen = System.Windows.Forms.Screen.FromRectangle(notificationArea);
        }

        if (screen is null)
        {
            return;
        }

        var dpi = GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var panelSize = new System.Drawing.Size(
            (int)System.Math.Ceiling(Width * scale),
            (int)System.Math.Ceiling(Height * scale));
        if (notificationArea.IsEmpty)
        {
            notificationArea = new System.Drawing.Rectangle(
                screen.WorkingArea.Right - 1,
                screen.WorkingArea.Bottom,
                1,
                1);
        }

        var placement = DownloadNotificationPlacement.Calculate(
            screen.Bounds,
            screen.WorkingArea,
            notificationArea,
            panelSize);
        SetWindowPos(
            handle,
            new System.IntPtr(-1),
            placement.X,
            placement.Y,
            panelSize.Width,
            panelSize.Height,
            SetWindowPosNoActivate);
    }

    private void Invoke(DownloadNotificationAction action)
    {
        RegisterInteraction();
        if (DataContext is DownloadEntry entry)
        {
            var outcome = _invokeAction(action, entry);
            if (DownloadNotificationActionPolicy.ShouldDismiss(outcome))
            {
                Dismiss();
            }
        }
    }

    private void RegisterInteraction()
    {
        _interactionDetected = true;
        _dismissTimer.Stop();
    }

    private void DownloadRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton is System.Windows.Input.MouseButton.Left
            && e.ClickCount == 1
            && DataContext is DownloadEntry { ExistsNow: true })
        {
            e.Handled = true;
            Invoke(DownloadNotificationAction.Default);
        }
    }

    private void CopyFile_Click(object sender, System.Windows.RoutedEventArgs e) => Invoke(DownloadNotificationAction.CopyFile);
    private void CopyAsPath_Click(object sender, System.Windows.RoutedEventArgs e) => Invoke(DownloadNotificationAction.CopyAsPath);
    private void CutFile_Click(object sender, System.Windows.RoutedEventArgs e) => Invoke(DownloadNotificationAction.CutFile);
    private void Rename_Click(object sender, System.Windows.RoutedEventArgs e) => Invoke(DownloadNotificationAction.Rename);
    private void CopySha256_Click(object sender, System.Windows.RoutedEventArgs e) => Invoke(DownloadNotificationAction.CopySha256);
    private void Delete_Click(object sender, System.Windows.RoutedEventArgs e) => Invoke(DownloadNotificationAction.Delete);
    private void Dismiss_Click(object sender, System.Windows.RoutedEventArgs e) => Dismiss();

    private void Panel_Interacted(object sender, System.Windows.RoutedEventArgs e) => RegisterInteraction();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Dismiss();
    }

    public void Dispose()
    {
        _dismissTimer.Stop();
        _allowClose = true;
        Close();
    }
}
