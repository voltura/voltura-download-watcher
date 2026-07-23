namespace VolturaDownloadWatcher;

internal static class FilenameMarqueeMeasurement
{
    public static Metrics Create(
        System.Windows.Controls.TextBlock textBlock,
        double viewportWidth)
    {
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0)
        {
            return default;
        }

        var formattedText = new System.Windows.Media.FormattedText(
            textBlock.Text ?? string.Empty,
            System.Globalization.CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            new System.Windows.Media.Typeface(
                textBlock.FontFamily,
                textBlock.FontStyle,
                textBlock.FontWeight,
                textBlock.FontStretch),
            textBlock.FontSize,
            System.Windows.Media.Brushes.Transparent,
            System.Windows.Media.VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
        var fullWidth = formattedText.WidthIncludingTrailingWhitespace;

        return new Metrics(fullWidth, System.Math.Max(0, fullWidth - viewportWidth));
    }

    internal readonly record struct Metrics(double FullWidth, double Overflow);
}
