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
                new DownloadNotificationDurationDialog(10, new System.Drawing.Point(100, 100))
            };

            foreach (var window in windows)
            {
                var style = Assert.IsType<System.Windows.Style>(
                    window.Resources[typeof(System.Windows.Controls.ToolTip)]);
                Assert.Equal(
                    System.Windows.Controls.Primitives.PlacementMode.Top,
                    GetSetterValue(style, "Placement"));
                Assert.Equal(-6d, GetSetterValue(style, "VerticalOffset"));
            }
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
