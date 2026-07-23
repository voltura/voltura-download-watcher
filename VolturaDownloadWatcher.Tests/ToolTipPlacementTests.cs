namespace VolturaDownloadWatcher.Tests;

public sealed class ToolTipPlacementTests
{
    [Fact]
    public void EveryWindowToolTipStylePrefersPlacementAboveItsControl()
    {
        RunOnStaThread(() =>
        {
            using var mainWindow = new MainWindow();
            using var notificationWindow = new DownloadNotificationWindow((_, _) =>
                DownloadNotificationActionOutcome.Succeeded);
            var windows = new System.Windows.Window[]
            {
                mainWindow,
                notificationWindow,
                new ConfirmationDialog("Confirm", new System.Drawing.Point(100, 100)),
                new NoticeDialog("Notice"),
                new RenameDialog(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rename.txt")),
                new DownloadNotificationDurationDialog(10, new System.Drawing.Point(100, 100)),
                new ReleaseCheckResultDialog(false, "1.0.0", null, new System.Drawing.Point(100, 100))
            };

            foreach (var window in windows)
            {
                var style = Assert.IsType<System.Windows.Style>(
                    window.Resources[typeof(System.Windows.Controls.ToolTip)]);
                Assert.Equal(
                    System.Windows.Controls.Primitives.PlacementMode.Top,
                    GetSetterValue(style, "Placement"));
                Assert.Equal(-6d, GetSetterValue(style, "VerticalOffset"));
                Assert.Equal(60, GetSetterValue(style, "InitialShowDelay"));
            }
        });
    }

    [Fact]
    public void EveryTooltipOwnerStyleUsesTheFastInitialDelay()
    {
        RunOnStaThread(() =>
        {
            using var mainWindow = new MainWindow();
            using var notificationWindow = new DownloadNotificationWindow((_, _) =>
                DownloadNotificationActionOutcome.Succeeded);
            var buttonStyles = new[]
            {
                Assert.IsType<System.Windows.Style>(
                    mainWindow.Resources[typeof(System.Windows.Controls.Button)]),
                Assert.IsType<System.Windows.Style>(
                    notificationWindow.Resources["HeaderActionButton"]),
                Assert.IsType<System.Windows.Style>(
                    new ConfirmationDialog("Confirm", new System.Drawing.Point(100, 100))
                        .Resources[typeof(System.Windows.Controls.Button)]),
                Assert.IsType<System.Windows.Style>(
                    new NoticeDialog("Notice").Resources[typeof(System.Windows.Controls.Button)]),
                Assert.IsType<System.Windows.Style>(
                    new RenameDialog(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rename.txt"))
                        .Resources[typeof(System.Windows.Controls.Button)]),
                Assert.IsType<System.Windows.Style>(
                    new DownloadNotificationDurationDialog(10, new System.Drawing.Point(100, 100))
                        .Resources[typeof(System.Windows.Controls.Button)]),
                Assert.IsType<System.Windows.Style>(
                    new ReleaseCheckResultDialog(false, "1.0.0", null, new System.Drawing.Point(100, 100))
                        .Resources[typeof(System.Windows.Controls.Button)])
            };

            foreach (var style in buttonStyles)
            {
                Assert.Equal(60, GetSetterValue(style, "InitialShowDelay"));
            }

            var menuItemStyle = Assert.IsType<System.Windows.Style>(
                mainWindow.Resources[typeof(System.Windows.Controls.MenuItem)]);
            Assert.Equal(60, GetSetterValue(menuItemStyle, "InitialShowDelay"));
        });
    }

    [Fact]
    public void HeaderActionStripsUseContiguousHitTargetsAndFastTooltipHandoff()
    {
        RunOnStaThread(() =>
        {
            using var mainWindow = new MainWindow();
            using var notificationWindow = new DownloadNotificationWindow((_, _) =>
                DownloadNotificationActionOutcome.Succeeded);
            var styles = new[]
            {
                Assert.IsType<System.Windows.Style>(
                    mainWindow.Resources["HeaderQuickActionButton"]),
                Assert.IsType<System.Windows.Style>(
                    notificationWindow.Resources["HeaderActionButton"])
            };

            foreach (var style in styles)
            {
                Assert.Equal(24d, GetSetterValue(style, "Width"));
                Assert.Equal(500, GetSetterValue(style, "BetweenShowDelay"));
            }

            Assert.Equal(
                new System.Windows.Thickness(0),
                GetSetterValue(styles[1], "Margin"));
        });
    }

    private static object? GetSetterValue(System.Windows.Style style, string propertyName) =>
        style.Setters
            .OfType<System.Windows.Setter>()
            .Single(setter => string.Equals(setter.Property.Name, propertyName, System.StringComparison.Ordinal))
            .Value;

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
            throw new Xunit.Sdk.XunitException("STA tooltip placement test failed.", failure);
        }
    }
}
