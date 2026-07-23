namespace VolturaDownloadWatcher.Tests;

public sealed class FilenameMarqueeMeasurementTests
{
    [Fact]
    public void Create_UsesTheCompleteFilenameAndStopsWithItsEndAtTheViewportRightEdge()
    {
        RunOnStaThread(() =>
        {
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = "ABCDEF-this-part-started-outside-the-visible-row.txt",
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift SemiCondensed"),
                FontSize = 9,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Width = 24
            };
            textBlock.Measure(new System.Windows.Size(24, double.PositiveInfinity));
            textBlock.Arrange(new System.Windows.Rect(0, 0, 24, textBlock.DesiredSize.Height));

            const double viewportWidth = 24;
            var measurement = FilenameMarqueeMeasurement.Create(textBlock, viewportWidth);

            Assert.True(measurement.FullWidth > viewportWidth);
            Assert.Equal(
                viewportWidth,
                measurement.FullWidth - measurement.Overflow,
                precision: 6);
        });
    }

    [Fact]
    public void Create_DoesNotStartUntilTheViewportHasAUsableWidth()
    {
        RunOnStaThread(() =>
        {
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = "long-filename.txt",
                FontSize = 9
            };

            var measurement = FilenameMarqueeMeasurement.Create(textBlock, 0);

            Assert.Equal(default, measurement);
        });
    }

    private static void RunOnStaThread(System.Action action)
    {
        System.Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                action();
            }
            catch (System.Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException("STA WPF marquee measurement test failed.", failure);
        }
    }
}
