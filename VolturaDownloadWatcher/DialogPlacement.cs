namespace VolturaDownloadWatcher;

public static class DialogPlacement
{
    private const int MainWindowVerticalOffset = 120;

    public static System.Drawing.Point CalculateRightCenter(
        System.Drawing.Rectangle workingArea,
        System.Drawing.Size windowSize) =>
        new(
            workingArea.Right - windowSize.Width,
            workingArea.Top + ((workingArea.Height - windowSize.Height) / 2) - MainWindowVerticalOffset);

    public static System.Drawing.Point CalculateNear(
        System.Drawing.Point anchor,
        System.Drawing.Rectangle workingArea,
        System.Drawing.Size dialogSize,
        int gap = 16)
    {
        var x = anchor.X - dialogSize.Width - gap;
        if (x < workingArea.Left)
        {
            x = anchor.X + gap;
        }

        var y = anchor.Y - dialogSize.Height - gap;
        if (y < workingArea.Top)
        {
            y = anchor.Y + gap;
        }

        return new System.Drawing.Point(
            System.Math.Clamp(x, workingArea.Left, System.Math.Max(workingArea.Left, workingArea.Right - dialogSize.Width)),
            System.Math.Clamp(y, workingArea.Top, System.Math.Max(workingArea.Top, workingArea.Bottom - dialogSize.Height)));
    }
}
