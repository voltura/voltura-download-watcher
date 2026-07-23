namespace VolturaDownloadWatcher.Tests;

public sealed class MainWindowScrollingTests
{
    [Fact]
    public void FilenameVisualKeepsTheCompleteTextRenderedOutsideTheClippedViewport()
    {
        RunOnStaThread(() =>
        {
            using var window = new MainWindow
            {
                Width = 240,
                Height = 150
            };
            window.Downloads.Add(new DownloadEntry
            {
                FileName = "ABCDEF-this-entire-long-filename-must-remain-rendered-outside-the-viewport.txt",
                FullPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "marquee-layout.txt"),
                CreatedAt = System.DateTime.Now,
                TouchedAt = System.DateTime.Now,
                ExistsNow = true
            });

            var list = Xunit.Assert.IsType<System.Windows.Controls.ListView>(window.FindName("DownloadsList"));
            var root = Xunit.Assert.IsAssignableFrom<System.Windows.FrameworkElement>(window.Content);
            root.Measure(new System.Windows.Size(240, 150));
            root.Arrange(new System.Windows.Rect(0, 0, 240, 150));
            root.UpdateLayout();
            list.ApplyTemplate();
            list.UpdateLayout();

            var row = Xunit.Assert.IsType<System.Windows.Controls.ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
            var viewport = Xunit.Assert.IsType<System.Windows.Controls.Border>(
                FindNamedVisualChild<System.Windows.Controls.Border>(row, "FilenameViewport"));
            var filename = Xunit.Assert.IsType<System.Windows.Controls.TextBlock>(
                FindNamedVisualChild<System.Windows.Controls.TextBlock>(row, "FilenameText"));

            Xunit.Assert.True(viewport.ActualWidth > 0);
            Xunit.Assert.IsType<System.Windows.Controls.Canvas>(
                System.Windows.Media.VisualTreeHelper.GetParent(filename));
            Xunit.Assert.True(filename.ActualWidth > viewport.ActualWidth);
        });
    }

    [Fact]
    public void WheelInputOverFilenameUsesNativeListScrollingInBothDirections()
    {
        RunOnStaThread(() =>
        {
            using var window = new MainWindow
            {
                Width = 240,
                Height = 150
            };

            var now = System.DateTime.Now;
            for (var index = 0; index < 60; index++)
            {
                window.Downloads.Add(new DownloadEntry
                {
                    FileName = $"native-scroll-entry-{index:D2}.txt",
                    FullPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"native-scroll-entry-{index:D2}.txt"),
                    CreatedAt = now.AddSeconds(-index),
                    TouchedAt = now.AddSeconds(-index),
                    ExistsNow = true
                });
            }

            var list = Xunit.Assert.IsType<System.Windows.Controls.ListView>(window.FindName("DownloadsList"));
            var root = Xunit.Assert.IsAssignableFrom<System.Windows.FrameworkElement>(window.Content);
            root.Measure(new System.Windows.Size(240, 150));
            root.Arrange(new System.Windows.Rect(0, 0, 240, 150));
            root.UpdateLayout();
            list.ApplyTemplate();
            list.UpdateLayout();

            Xunit.Assert.Equal(60, list.Items.Count);
            var viewer = Xunit.Assert.IsType<System.Windows.Controls.ScrollViewer>(FindVisualChild<System.Windows.Controls.ScrollViewer>(list));
            Xunit.Assert.True(viewer.ScrollableHeight > 0);

            var row = Xunit.Assert.IsType<System.Windows.Controls.ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
            var filename = Xunit.Assert.IsType<System.Windows.Controls.TextBlock>(FindNamedVisualChild<System.Windows.Controls.TextBlock>(row, "FilenameText"));
            Xunit.Assert.Null(FindNamedVisualChild<System.Windows.Controls.ScrollViewer>(row, "FilenameViewport"));

            var initialOffset = viewer.VerticalOffset;
            RaiseWheel(filename, -30);
            window.UpdateLayout();
            Xunit.Assert.True(viewer.VerticalOffset > initialOffset);

            var downwardOffset = viewer.VerticalOffset;
            RaiseWheel(filename, 30);
            window.UpdateLayout();
            Xunit.Assert.True(viewer.VerticalOffset < downwardOffset);
        });
    }

    private static void RaiseWheel(System.Windows.UIElement source, int delta)
    {
        var args = new System.Windows.Input.MouseWheelEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice,
            System.Environment.TickCount,
            delta)
        {
            RoutedEvent = System.Windows.UIElement.MouseWheelEvent,
            Source = source
        };
        source.RaiseEvent(args);
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject element)
        where T : System.Windows.DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(element, index);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static T? FindNamedVisualChild<T>(System.Windows.DependencyObject element, string name)
        where T : System.Windows.FrameworkElement
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(element, index);
            if (child is T match && string.Equals(match.Name, name, System.StringComparison.Ordinal))
            {
                return match;
            }

            var descendant = FindNamedVisualChild<T>(child, name);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
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
            throw new Xunit.Sdk.XunitException("STA WPF scrolling test failed.", failure);
        }
    }
}
