namespace VolturaDownloadWatcher;

public partial class ActionFeedbackWindow : System.Windows.Window, System.IDisposable
{
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoActivate = 0x0010;
    private readonly System.Windows.Threading.DispatcherTimer _dismissTimer;

    public ActionFeedbackWindow()
    {
        InitializeComponent();
        _dismissTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(1600)
        };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            Hide();
        };
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr windowHandle);

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

    public void ShowFeedback(string actionText, string fileName, System.Windows.Window? anchorWindow)
    {
        FeedbackText.Text = $"{actionText} // {fileName}";
        _dismissTimer.Stop();

        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();
        PositionFeedback(anchorWindow);
        FeedbackShell.BeginAnimation(
            System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.35,
                To = 1,
                Duration = System.TimeSpan.FromMilliseconds(120)
            });
        _dismissTimer.Start();
    }

    public void Promote()
    {
        if (!IsVisible)
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != System.IntPtr.Zero)
        {
            SetWindowPos(
                handle,
                new System.IntPtr(-1),
                0,
                0,
                0,
                0,
                SetWindowPosNoActivate | SetWindowPosNoMove | SetWindowPosNoSize);
        }
    }

    private void PositionFeedback(System.Windows.Window? anchorWindow)
    {
        if (!GetCursorPos(out var pointer))
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == System.IntPtr.Zero)
        {
            return;
        }

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pointer.X, pointer.Y));
        var dpi = GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var width = (int)System.Math.Ceiling(ActualWidth * scale);
        var height = (int)System.Math.Ceiling(ActualHeight * scale);
        const int gap = 12;
        var x = pointer.X - width;
        var y = pointer.Y - height - gap;
        var anchorHandle = anchorWindow is null
            ? System.IntPtr.Zero
            : new System.Windows.Interop.WindowInteropHelper(anchorWindow).Handle;
        if (anchorHandle != System.IntPtr.Zero && GetWindowRect(anchorHandle, out var anchor))
        {
            x = anchor.Left - width - gap;
            if (x < screen.WorkingArea.Left)
            {
                x = anchor.Right + gap;
            }

            y = pointer.Y - (height / 2);
        }
        else
        {
            if (x < screen.WorkingArea.Left)
            {
                x = pointer.X + gap;
            }

            if (y < screen.WorkingArea.Top)
            {
                y = pointer.Y + gap;
            }
        }

        x = System.Math.Clamp(x, screen.WorkingArea.Left, screen.WorkingArea.Right - width);
        y = System.Math.Clamp(y, screen.WorkingArea.Top, screen.WorkingArea.Bottom - height);
        SetWindowPos(handle, new System.IntPtr(-1), x, y, width, height, SetWindowPosNoActivate);
    }

    public void Dispose()
    {
        _dismissTimer.Stop();
        Close();
    }
}
