namespace VolturaDownloadWatcher;

public static class DownloadNotificationPlacement
{
    public static System.Drawing.Point Calculate(
        System.Drawing.Rectangle screenBounds,
        System.Drawing.Rectangle workingArea,
        System.Drawing.Rectangle notificationArea,
        System.Drawing.Size panelSize,
        int gap = 10)
    {
        int x;
        int y;

        if (workingArea.Bottom < screenBounds.Bottom)
        {
            x = notificationArea.Right - panelSize.Width;
            y = workingArea.Bottom - panelSize.Height - gap;
        }
        else if (workingArea.Top > screenBounds.Top)
        {
            x = notificationArea.Right - panelSize.Width;
            y = workingArea.Top + gap;
        }
        else if (workingArea.Right < screenBounds.Right)
        {
            x = workingArea.Right - panelSize.Width - gap;
            y = notificationArea.Top - panelSize.Height;
        }
        else
        {
            x = workingArea.Left + gap;
            y = notificationArea.Top - panelSize.Height;
        }

        return new System.Drawing.Point(
            System.Math.Clamp(x, workingArea.Left, System.Math.Max(workingArea.Left, workingArea.Right - panelSize.Width)),
            System.Math.Clamp(y, workingArea.Top, System.Math.Max(workingArea.Top, workingArea.Bottom - panelSize.Height)));
    }
}
