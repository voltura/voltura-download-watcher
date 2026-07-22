namespace VolturaDownloadWatcher;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\VolturaDownloadWatcher.SingleInstance";
    private const string ShowExistingEventName = @"Local\VolturaDownloadWatcher.ShowExisting";
    private static readonly string StartupLogPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "VolturaDownloadWatcher",
        "startup.log");
    private MainWindow? _mainWindow;
    private System.Threading.Mutex? _singleInstanceMutex;
    private System.Threading.EventWaitHandle? _showExistingEvent;
    private System.Threading.RegisteredWaitHandle? _showExistingRegistration;
    private bool _ownsSingleInstanceMutex;

    private async void Application_Startup(object sender, System.Windows.StartupEventArgs e)
    {
        if (!TryBecomeSingleInstance())
        {
            WriteStartupLog("Existing instance signaled");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            WriteStartupLog(args.Exception.ToString());
            args.Handled = true;
            Shutdown();
        };

        try
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            SplashWindow? splash = null;
            System.Threading.Tasks.Task? splashDelay = null;
            if (!string.Equals(
                    System.Environment.GetEnvironmentVariable("VOLTURA_DOWNLOAD_WATCHER_SCREENSHOT"),
                    "1",
                    System.StringComparison.Ordinal))
            {
                splash = new SplashWindow();
                splash.Show();
                splashDelay = System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(2));
            }

            _mainWindow = new MainWindow();
            if (splash is not null && splashDelay is not null)
            {
                await splashDelay;
                splash.Close();
            }

            MainWindow = _mainWindow;
            ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
            ListenForShowRequests();
            _mainWindow.Loaded += (_, _) =>
            {
                _mainWindow.ShowInTaskbar = false;
                if (_mainWindow.StartMinimized)
                {
                    _mainWindow.Hide();
                    return;
                }

                _mainWindow.WindowState = System.Windows.WindowState.Normal;
                _mainWindow.Show();
                _mainWindow.Activate();
                _mainWindow.Topmost = true;
            };
            _mainWindow.Show();
            if (_mainWindow.StartMinimized)
            {
                ActivityLog.WriteLifecycle("start-minimized");
                WriteStartupLog("Startup OK");
                return;
            }

            _mainWindow.WindowState = System.Windows.WindowState.Normal;
            _mainWindow.Activate();
            _ = _mainWindow.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                _mainWindow.ShowInTaskbar = false;
                _mainWindow.WindowState = System.Windows.WindowState.Normal;
                _mainWindow.Show();
                _mainWindow.Activate();
                _mainWindow.Topmost = true;
            }));
            ActivityLog.WriteLifecycle("start");
            WriteStartupLog("Startup OK");
        }
        catch (System.Exception ex)
        {
            WriteStartupLog(ex.ToString());
            throw;
        }
    }

    private void Application_Exit(object sender, System.Windows.ExitEventArgs e)
    {
        _mainWindow?.LogExitBestEffort();
        _mainWindow?.DisposeTrayIcon();
        _showExistingRegistration?.Unregister(null);
        _showExistingRegistration = null;
        _showExistingEvent?.Dispose();
        _showExistingEvent = null;

        if (_ownsSingleInstanceMutex)
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            catch (System.ApplicationException)
            {
            }
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        _ownsSingleInstanceMutex = false;
    }

    private bool TryBecomeSingleInstance()
    {
        _showExistingEvent = new System.Threading.EventWaitHandle(
            false,
            System.Threading.EventResetMode.AutoReset,
            ShowExistingEventName);
        _singleInstanceMutex = new System.Threading.Mutex(false, SingleInstanceMutexName);

        try
        {
            _ownsSingleInstanceMutex = _singleInstanceMutex.WaitOne(0, false);
        }
        catch (System.Threading.AbandonedMutexException)
        {
            _ownsSingleInstanceMutex = true;
        }

        if (_ownsSingleInstanceMutex)
        {
            return true;
        }

        _showExistingEvent.Set();
        _showExistingEvent.Dispose();
        _showExistingEvent = null;
        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private void ListenForShowRequests()
    {
        if (_showExistingEvent is null)
        {
            return;
        }

        _showExistingRegistration = System.Threading.ThreadPool.RegisterWaitForSingleObject(
            _showExistingEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    Dispatcher.BeginInvoke(new System.Action(() => _mainWindow?.ShowFromTray()));
                }
            },
            null,
            System.Threading.Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private static void WriteStartupLog(string text)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(StartupLogPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.IO.File.AppendAllText(StartupLogPath, $"[{System.DateTime.Now:O}] {text}{System.Environment.NewLine}");
        }
        catch
        {
        }
    }
}
