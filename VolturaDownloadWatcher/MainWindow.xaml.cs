namespace VolturaDownloadWatcher;

public partial class MainWindow : System.Windows.Window, System.ComponentModel.INotifyPropertyChanged, System.IDisposable
{
    private const int MaxItems = 40;
    private const int FreshDownloadSeconds = 8;
    private const int TrayCompletionStatusSeconds = 4;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint PlaySoundNodefault = 0x0002;
    private const uint PlaySoundFilename = 0x00020000;
    private static readonly string SettingsPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "VolturaDownloadWatcher",
        "settings.json");

    private readonly System.Collections.ObjectModel.ObservableCollection<DownloadEntry> _downloads = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<FilterChip> _filterButtons = new();
    private readonly System.Collections.Generic.HashSet<string> _activeBrowserDownloads = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _appRenameTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _sha256QueuedPaths = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly MonitoringSession _monitoringSession = new();
    private readonly System.Threading.Channels.Channel<Sha256WorkItem> _sha256Queue =
        System.Threading.Channels.Channel.CreateUnbounded<Sha256WorkItem>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    private readonly System.Threading.CancellationTokenSource _sha256Cancellation = new();
    private readonly System.Threading.Tasks.Task _sha256Worker;
    private readonly System.ComponentModel.ICollectionView _downloadsView;
    private readonly string _downloadsPath = GetDownloadsPath();
    private readonly bool _isScreenshotMode = string.Equals(
        System.Environment.GetEnvironmentVariable("VOLTURA_DOWNLOAD_WATCHER_SCREENSHOT"),
        "1",
        System.StringComparison.Ordinal);
    private readonly System.Windows.Threading.DispatcherTimer _freshnessTimer;
    private readonly System.Windows.Threading.DispatcherTimer _trayPulseTimer;
    private readonly System.Windows.Threading.DispatcherTimer _releaseCheckTimer;
    private readonly System.ComponentModel.IContainer _trayComponents = new System.ComponentModel.Container();
    private System.IO.FileSystemWatcher? _watcher;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIcon;
    private System.Drawing.Icon? _trayActiveIcon;
    private System.Drawing.Icon? _trayPausedIcon;
    private System.Windows.Forms.ToolStripMenuItem? _playSoundMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _startMinimizedMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _deleteToRecycleBinMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _monitoringMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _aboutMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _checkReleaseMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _downloadReleaseMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _dailyUpdateChecksMenuItem;
    private DownloadNotificationWindow? _downloadNotificationWindow;
    private readonly string _sparkSoundPath;
    private bool _isMuted;
    private bool _startMinimized;
    private System.Windows.Controls.ToolTip? _activeToolTip;
    private int _watcherRecoveryQueued;
    private bool _trayPulseIsBright;
    private bool _deleteToRecycleBin;
    private bool _closeToTrayNotificationShown;
    private bool _allowClose;
    private bool _monitoringPaused;
    private bool _releaseCheckInProgress;
    private bool _updateAvailable;
    private System.DateTimeOffset? _lastReleaseCheckUtc;
    private string? _latestReleaseVersion;
    private string? _latestReleaseUrl;
    private bool _checkForUpdatesDaily;
    private int _downloadNotificationDurationSeconds;
    private DownloadDefaultAction _defaultAction;
    private int _exitLogged;
    private DownloadSortMode _sortMode;
    private bool _sortDescending;
    private readonly System.Collections.Generic.List<(System.DateTime OccurredAt, string FileName)> _recentDownloads = new();
    private long _nextSortPinOrder = System.DateTime.UtcNow.Ticks;
    private FilterMode _activeFilter = FilterMode.All;

    public bool StartMinimized => _startMinimized && !_isScreenshotMode;

    public MainWindow()
    {
        InitializeComponent();
        _sha256Worker = System.Threading.Tasks.Task.Run(ProcessSha256QueueAsync);
        DataContext = this;
        DownloadsPathLabel = _downloadsPath;

        _downloadsView = System.Windows.Data.CollectionViewSource.GetDefaultView(_downloads);
        _downloadsView.Filter = FilterDownloads;

        BuildFilterButtons();

        if (!_isScreenshotMode)
        {
            ActivityLog.EnsureCurrentFile();
        }
        var settings = LoadSettings();
        _startMinimized = settings.StartMinimized;
        _isMuted = settings.IsMuted;
        _deleteToRecycleBin = settings.DeleteToRecycleBin;
        _closeToTrayNotificationShown = settings.CloseToTrayNotificationShown;
        _sortMode = settings.SortMode;
        _sortDescending = settings.SortDescending;
        _defaultAction = DownloadDefaultActionPolicy.Normalize(settings.DefaultAction);
        _lastReleaseCheckUtc = settings.LastReleaseCheckUtc;
        _latestReleaseVersion = settings.LatestReleaseVersion;
        _latestReleaseUrl = settings.LatestReleaseUrl;
        _checkForUpdatesDaily = settings.CheckForUpdatesDaily;
        _downloadNotificationDurationSeconds = DownloadNotificationDurationPolicy.NormalizePersisted(
            settings.DownloadNotificationDurationSeconds);
        _updateAvailable = ReleaseUpdateChecker.IsNewer(_latestReleaseVersion, GetDisplayVersion());
        ApplySort();
        UpdateMuteIcon();
        _sparkSoundPath = System.IO.Path.Combine(
            System.AppContext.BaseDirectory,
            "Assets",
            "electric-spark.wav");

        _freshnessTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(1)
        };
        _freshnessTimer.Tick += (_, _) =>
        {
            RefreshFreshness();
            UpdateTrayStatus();
            EnforceTopmost();
        };
        _trayPulseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(420)
        };
        _trayPulseTimer.Tick += (_, _) =>
        {
            if (_monitoringPaused)
            {
                if (_notifyIcon is not null && _trayPausedIcon is not null)
                {
                    _notifyIcon.Icon = _trayPausedIcon;
                }

                return;
            }

            if (_notifyIcon is null || _trayIcon is null || _trayActiveIcon is null)
            {
                return;
            }

            _trayPulseIsBright = !_trayPulseIsBright;
            _notifyIcon.Icon = _trayPulseIsBright ? _trayActiveIcon : _trayIcon;
        };
        _releaseCheckTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromHours(1)
        };
        _releaseCheckTimer.Tick += async (_, _) => await CheckForReleaseAsync(force: false);
        Activated += (_, _) => EnforceTopmost();
    }

    public System.Collections.ObjectModel.ObservableCollection<DownloadEntry> Downloads => _downloads;
    public System.ComponentModel.ICollectionView DownloadsView => _downloadsView;
    public System.Collections.ObjectModel.ObservableCollection<FilterChip> FilterButtons => _filterButtons;

    private string _downloadsPathLabel = string.Empty;
    public string DownloadsPathLabel
    {
        get => _downloadsPathLabel;
        set => SetField(ref _downloadsPathLabel, value);
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        System.IntPtr hWnd,
        System.IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(System.IntPtr iconHandle);

    private async void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        EnforceTopmost();
        PositionWindow();
        if (_isScreenshotMode)
        {
            Left = 0;
            Top = 0;
            Background = System.Windows.Media.Brushes.Black;
        }
        SetupTrayIcon();

        if (_isScreenshotMode)
        {
            LoadScreenshotDownloads();
            RefreshDownloadsView();
            return;
        }

        if (System.IO.Directory.Exists(_downloadsPath))
        {
            StartWatcher();
            LoadInitialDownloads();
        }

        var loggedDownloads = await ActivityLog.ReadRecentDownloadsAsync(MaxItems);
        MergeLoggedDownloads(loggedDownloads);
        TrimHistoryToLimit();
        QueueSha256ForLiveDownloads();
        RefreshDownloadsView();
        _freshnessTimer.Start();
        _releaseCheckTimer.Start();
        _ = CheckForReleaseAsync(force: false);
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose
            || _isScreenshotMode
            || System.Windows.Application.Current.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        e.Cancel = true;
        HideToTray(showFirstTimeNotification: true);
    }

    private void LoadScreenshotDownloads()
    {
        var names = new[]
        {
            "neon-grid-reference.png",
            "signal-decoder-v2.4.1-win-x64.exe",
            "archive-node-07.zip",
            "city-sector-map.pdf",
            "night-shift-notes.md",
            "project-briefing.pptx",
            "sector-ledger.xlsx",
            "drone-feed.mp4",
            "electric-spark.flac",
            "relay-map.torrent"
        };
        var now = System.DateTime.Now;
        for (var index = names.Length - 1; index >= 0; index--)
        {
            var timestamp = now.AddMinutes(-index * 7);
            _downloads.Insert(0, new DownloadEntry
            {
                FileName = names[index],
                FullPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VolturaScreenshot", names[index]),
                CreatedAt = timestamp,
                TouchedAt = timestamp,
                FileSizeBytes = 384L * 1024 * (index + 1),
                IsFresh = index == 0,
                IsNewest = index == 0,
                ExistsNow = true
            });
        }
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Panel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left
            || FindVisualParent<System.Windows.Controls.Button>(e.OriginalSource as System.Windows.DependencyObject) is not null
            || FindVisualParent<System.Windows.Controls.ListViewItem>(e.OriginalSource as System.Windows.DependencyObject) is not null
            || FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as System.Windows.DependencyObject) is not null
            || FindVisualParent<System.Windows.Controls.Primitives.Thumb>(e.OriginalSource as System.Windows.DependencyObject) is not null
            || FindVisualParent<System.Windows.Controls.Primitives.Track>(e.OriginalSource as System.Windows.DependencyObject) is not null
            || FindVisualParent<System.Windows.Controls.ScrollViewer>(e.OriginalSource as System.Windows.DependencyObject) is not null)
        {
            return;
        }

        DragMove();
    }

    private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var source = e.OriginalSource as System.Windows.DependencyObject;
        var overScrollBar = FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(source) is not null;
        var overRow = FindVisualParent<System.Windows.Controls.ListViewItem>(source) is not null;
        System.Windows.Input.Mouse.OverrideCursor = overRow && !overScrollBar
            ? System.Windows.Input.Cursors.Hand
            : null;
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        System.Windows.Input.Mouse.OverrideCursor = null;
    }

    private void ToolTip_Opened(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ToolTip toolTip)
        {
            return;
        }

        _activeToolTip = toolTip;
        EnforceTopmost();
        PromoteTooltip(toolTip);
    }

    private void ToolTip_Closed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ReferenceEquals(_activeToolTip, sender))
        {
            _activeToolTip = null;
        }

        EnforceTopmost();
    }

    private static void PromoteTooltip(System.Windows.Controls.ToolTip toolTip)
    {
        if (!toolTip.IsOpen
            || System.Windows.PresentationSource.FromVisual(toolTip) is not System.Windows.Interop.HwndSource source)
        {
            return;
        }

        SetWindowPos(source.Handle, new System.IntPtr(-1), 0, 0, 0, 0,
            SetWindowPosNoActivate | SetWindowPosNoMove | SetWindowPosNoSize);
    }

    private static T? FindVisualParent<T>(System.Windows.DependencyObject? element)
        where T : System.Windows.DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject? element)
        where T : System.Windows.DependencyObject
    {
        if (element is null)
        {
            return null;
        }

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

    private static T? FindNamedVisualChild<T>(System.Windows.DependencyObject? element, string name)
        where T : System.Windows.FrameworkElement
    {
        if (element is null)
        {
            return null;
        }

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

    private void OpenDownloadsFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ActivityLog.WriteInteraction("open-downloads-folder", _downloadsPath, 0);
        OpenPathInShell(_downloadsPath);
    }

    private async void OpenActivityLog_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var logPath = await ActivityLog.EnsureCurrentFileAsync();
        ActivityLog.WriteInteraction("open-log", logPath, 0);
        OpenPathInShell(logPath);
    }

    private void ToggleSortMenu_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SortPopup.IsOpen = !SortPopup.IsOpen;
        UpdateSortIndicators();
    }

    private void SortOption_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button
            || button.Tag is not string modeName
            || !System.Enum.TryParse<DownloadSortMode>(modeName, out var selectedMode))
        {
            return;
        }

        if (_sortMode == selectedMode)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortMode = selectedMode;
            _sortDescending = selectedMode is not DownloadSortMode.Name;
        }

        SortPopup.IsOpen = false;
        ApplySort();
        SaveCurrentSettings("sort", $"{_sortMode}:{(_sortDescending ? "descending" : "ascending")}");
    }

    private void ToggleMute_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        SaveCurrentSettings("play-sound-on-download", (!_isMuted).ToString(System.Globalization.CultureInfo.InvariantCulture));
        UpdateMuteIcon();
        if (!_isMuted)
        {
            PlaySpark();
        }
    }

    private void ToggleMonitoring_Click(object sender, System.Windows.RoutedEventArgs e) => ToggleMonitoring();

    private void ToggleMonitoring()
    {
        _monitoringSession.Advance();
        _monitoringPaused = !_monitoringPaused;
        if (_monitoringPaused)
        {
            _watcher?.Dispose();
            _watcher = null;
        }
        else if (System.IO.Directory.Exists(_downloadsPath))
        {
            StartWatcher();
        }

        _activeBrowserDownloads.Clear();
        UpdateMonitoringUi();
        ActivityLog.WriteInteraction(
            _monitoringPaused ? "monitoring-paused" : "monitoring-resumed",
            string.Empty,
            0);
    }

    private void UpdateMonitoringUi()
    {
        if (_monitoringMenuItem is not null)
        {
            _monitoringMenuItem.Text = _monitoringPaused ? "Resume monitoring" : "Pause monitoring";
            _monitoringMenuItem.Checked = false;
        }

        if (FindName("MonitoringToggleButton") is System.Windows.Controls.Button button)
        {
            button.ToolTip = _monitoringPaused ? "Resume monitoring" : "Pause monitoring";
        }

        if (FindName("MonitoringToggleIconPath") is System.Windows.Shapes.Path path)
        {
            path.Data = System.Windows.Media.Geometry.Parse(_monitoringPaused
                ? "M5,3 L14,9 L5,15 Z"
                : "M5,4 H8 V14 H5 Z M10,4 H13 V14 H10 Z");
        }

        UpdateBrowserActivityIndicator();
    }

    private void HideToTray_Click(object sender, System.Windows.RoutedEventArgs e) =>
        HideToTray(showFirstTimeNotification: true);

    private void FilterButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is FilterChip chip)
        {
            _activeFilter = chip.Mode;
            RefreshDownloadsView();
        }
    }

    private void ClearHistory_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _downloads.Clear();
    }

    private void Filename_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Border rowBorder
            || FindNamedVisualChild<System.Windows.Controls.TextBlock>(rowBorder, "FilenameText") is not System.Windows.Controls.TextBlock textBlock
            || FindNamedVisualChild<System.Windows.Controls.Border>(rowBorder, "FilenameViewport") is not System.Windows.Controls.Border viewport)
        {
            return;
        }

        if (textBlock.RenderTransform is not System.Windows.Media.TranslateTransform transform
            || transform.IsFrozen)
        {
            transform = new System.Windows.Media.TranslateTransform();
            textBlock.RenderTransform = transform;
        }

        var measurement = FilenameMarqueeMeasurement.Create(textBlock, viewport.ActualWidth);
        if (measurement.Overflow <= 0.25)
        {
            return;
        }

        textBlock.Width = measurement.FullWidth;

        var timeline = FilenameMarqueeTimeline.Create(measurement.Overflow);
        var animation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            BeginTime = System.TimeSpan.Zero
        };
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.Zero)));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(timeline.StartPauseEnd)));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-measurement.Overflow,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(timeline.ForwardEnd)));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-measurement.Overflow,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(timeline.FarEdgePauseEnd)));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(timeline.ReturnEnd)));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(timeline.CycleEnd)));
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }

    private void Filename_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Border rowBorder
            && FindNamedVisualChild<System.Windows.Controls.TextBlock>(rowBorder, "FilenameText") is System.Windows.Controls.TextBlock textBlock
            && textBlock.RenderTransform is System.Windows.Media.TranslateTransform transform
            && transform.HasAnimatedProperties == false)
        {
            Filename_MouseEnter(sender, e);
        }
    }

    private void Filename_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Border rowBorder
            && FindNamedVisualChild<System.Windows.Controls.TextBlock>(rowBorder, "FilenameText") is System.Windows.Controls.TextBlock textBlock
            && textBlock.RenderTransform is System.Windows.Media.TranslateTransform transform
            && !transform.IsFrozen)
        {
            transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            transform.X = 0;
            textBlock.Width = double.NaN;
        }
    }

    private void OpenRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left
            && e.ClickCount == 1
            && sender is System.Windows.Controls.Border border
            && border.Tag is DownloadEntry entry
            && entry.ExistsNow)
        {
            e.Handled = true;
            PerformDefaultAction(entry);
        }
    }

    private DownloadNotificationActionOutcome PerformDefaultAction(DownloadEntry entry)
    {
        return _defaultAction switch
        {
            DownloadDefaultAction.ShowInExplorer => ShowFileInExplorer(entry),
            DownloadDefaultAction.CopyAsPath => CopyPathToClipboard(entry),
            DownloadDefaultAction.CopyFile => SetFileClipboard(entry, isCut: false),
            DownloadDefaultAction.CutFile => SetFileClipboard(entry, isCut: true),
            _ => OpenDefaultPath(entry)
        };
    }

    private DownloadNotificationActionOutcome OpenDefaultPath(DownloadEntry entry)
    {
        ActivityLog.WriteInteraction("open", entry.FileName, entry.FileSizeBytes);
        return OpenPathInShell(entry.FullPath)
            ? DownloadNotificationActionOutcome.Succeeded
            : DownloadNotificationActionOutcome.Failed;
    }

    private void OpenFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is DownloadEntry entry)
        {
            OpenDefaultPath(entry);
        }
    }

    private void CopyFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is DownloadEntry entry)
        {
            SetFileClipboard(entry, isCut: false);
        }
    }

    private void CutFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is DownloadEntry entry)
        {
            SetFileClipboard(entry, isCut: true);
        }
    }

    private void CopyAsPath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is DownloadEntry entry)
        {
            CopyPathToClipboard(entry);
        }
    }

    private void CopySha256_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is DownloadEntry entry)
        {
            CopySha256ToClipboard(entry);
        }
    }

    private DownloadNotificationActionOutcome CopySha256ToClipboard(DownloadEntry entry)
    {
        if (!entry.IsSha256Available || string.IsNullOrWhiteSpace(entry.Sha256))
        {
            return DownloadNotificationActionOutcome.Failed;
        }

        try
        {
            System.Windows.Clipboard.SetText(entry.Sha256);
            ActivityLog.WriteInteraction("copy-sha256", entry.FileName, entry.FileSizeBytes);
            return DownloadNotificationActionOutcome.Succeeded;
        }
        catch (System.Exception ex)
        {
            LogOperationFailure("copy-sha256", entry, FormatError(ex));
            ShowOperationFailure("copy-sha256", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }
    }

    private DownloadNotificationActionOutcome CopyPathToClipboard(DownloadEntry entry)
    {
        if (!System.IO.File.Exists(entry.FullPath))
        {
            LogOperationFailure("copy-as-path", entry, "System.IO.FileNotFoundException: file no longer exists");
            ShowOperationFailure("copy-as-path", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }

        try
        {
            System.Windows.Clipboard.SetText(ClipboardPathFormatter.Format(entry.FullPath));
            ActivityLog.WriteInteraction("copy-as-path", entry.FileName, entry.FileSizeBytes);
            return DownloadNotificationActionOutcome.Succeeded;
        }
        catch (System.Exception ex)
        {
            LogOperationFailure("copy-as-path", entry, FormatError(ex));
            ShowOperationFailure("copy-as-path", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }
    }

    private DownloadNotificationActionOutcome ShowFileInExplorer(DownloadEntry entry)
    {
        if (!System.IO.File.Exists(entry.FullPath))
        {
            LogOperationFailure("show-in-explorer", entry, "System.IO.FileNotFoundException: file no longer exists");
            ShowOperationFailure("show-in-explorer", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{entry.FullPath}\"",
                UseShellExecute = true
            });
            ActivityLog.WriteInteraction("show-in-explorer", entry.FileName, entry.FileSizeBytes);
            return DownloadNotificationActionOutcome.Succeeded;
        }
        catch (System.Exception ex)
        {
            LogOperationFailure("show-in-explorer", entry, FormatError(ex));
            ShowOperationFailure("show-in-explorer", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }
    }

    private DownloadNotificationActionOutcome SetFileClipboard(DownloadEntry entry, bool isCut)
    {
        var action = isCut ? "cut" : "copy";
        if (!System.IO.File.Exists(entry.FullPath))
        {
            LogOperationFailure(action, entry, "System.IO.FileNotFoundException: file no longer exists");
            ShowOperationFailure(action, entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }

        try
        {
            var files = new System.Collections.Specialized.StringCollection { entry.FullPath };
            var data = new System.Windows.DataObject();
            data.SetFileDropList(files);
            data.SetData("Preferred DropEffect", new System.IO.MemoryStream(
                System.BitConverter.GetBytes(isCut ? 2u : 1u),
                writable: false));
            System.Windows.Clipboard.SetDataObject(data, copy: true);
            ActivityLog.WriteInteraction(action, entry.FileName, entry.FileSizeBytes);
            return DownloadNotificationActionOutcome.Succeeded;
        }
        catch (System.Exception ex)
        {
            LogOperationFailure(action, entry, FormatError(ex));
            ShowOperationFailure(action, entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }
    }

    private void RenameFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is DownloadEntry entry)
        {
            RenameFile(entry);
        }
    }

    private DownloadNotificationActionOutcome RenameFile(DownloadEntry entry)
    {
        if (!System.IO.File.Exists(entry.FullPath))
        {
            LogOperationFailure("rename", entry, "System.IO.FileNotFoundException: file no longer exists");
            ShowOperationFailure("rename", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }

        var dialog = new RenameDialog(entry.FullPath);
        var owner = GetFileOperationDialogOwner();
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        if (dialog.ShowDialog() is not true)
        {
            ActivityLog.WriteInteraction("rename", entry.FileName, entry.FileSizeBytes, "cancelled");
            return DownloadNotificationActionOutcome.Cancelled;
        }

        var targetPath = System.IO.Path.Combine(_downloadsPath, dialog.NewFileName);
        if (string.Equals(targetPath, entry.FullPath, System.StringComparison.Ordinal))
        {
            ActivityLog.WriteInteraction("rename", entry.FileName, entry.FileSizeBytes, "unchanged");
            return DownloadNotificationActionOutcome.Cancelled;
        }

        var oldName = entry.FileName;
        _appRenameTargets[targetPath] = 0;
        try
        {
            var overwrittenEntry = _downloads.FirstOrDefault(item =>
                !ReferenceEquals(item, entry)
                && string.Equals(item.FullPath, targetPath, System.StringComparison.OrdinalIgnoreCase));
            System.IO.File.Move(entry.FullPath, targetPath, dialog.OverwriteExisting);
            if (overwrittenEntry is not null)
            {
                _downloads.Remove(overwrittenEntry);
            }

            entry.FileName = dialog.NewFileName;
            entry.FullPath = targetPath;
            entry.ExistsNow = true;
            entry.DeleteRequested = false;
            entry.DeletionLogged = false;
            QueueSha256Calculation(entry, resetExistingHash: true);
            ActivityLog.WriteInteraction(
                dialog.OverwriteExisting ? "rename-overwrite" : "rename",
                $"{oldName} -> {entry.FileName}",
                entry.FileSizeBytes);
            RefreshDownloadsView();
            return DownloadNotificationActionOutcome.Succeeded;
        }
        catch (System.Exception ex)
        {
            _appRenameTargets.TryRemove(targetPath, out _);
            LogOperationFailure("rename", entry, FormatError(ex));
            ShowOperationFailure("rename", oldName);
            return DownloadNotificationActionOutcome.Failed;
        }
    }

    private static DownloadEntry? GetContextMenuEntry(object sender)
    {
        if (sender is not System.Windows.Controls.MenuItem menuItem)
        {
            return null;
        }

        if (menuItem.Tag is DownloadEntry taggedEntry)
        {
            return taggedEntry;
        }

        return menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu
            && contextMenu.PlacementTarget is System.Windows.Controls.Border rowBorder
                ? rowBorder.Tag as DownloadEntry
                : null;
    }

    private void DeleteFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is DownloadEntry entry)
        {
            DeleteFile(entry);
        }
    }

    private DownloadNotificationActionOutcome DeleteFile(DownloadEntry entry)
    {
        if (!System.IO.File.Exists(entry.FullPath))
        {
            LogOperationFailure("delete", entry, "System.IO.FileNotFoundException: file no longer exists");
            ShowOperationFailure("delete", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }

        try
        {
            entry.DeleteRequested = true;
            if (_deleteToRecycleBin)
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    entry.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                    Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
            }
            else
            {
                System.IO.File.Delete(entry.FullPath);
            }

            entry.ExistsNow = false;
            entry.TouchedAt = System.DateTime.Now;
            entry.DeletionLogged = true;
            ActivityLog.WriteDeletion(
                _deleteToRecycleBin ? "app-recycle-bin" : "app-direct",
                entry.FileName,
                entry.FileSizeBytes);
            return DownloadNotificationActionOutcome.Succeeded;
        }
        catch (System.Exception ex)
        {
            entry.DeleteRequested = false;
            LogOperationFailure("delete", entry, FormatError(ex));
            ShowOperationFailure("delete", entry.FileName);
            return DownloadNotificationActionOutcome.Failed;
        }
    }

    private void ShowOperationFailure(string action, string fileName)
    {
        var message = action switch
        {
            "copy" => $"Could not copy '{fileName}'. The file may have been moved, renamed, or deleted, or the clipboard may be unavailable.",
            "copy-as-path" => $"Could not copy the path for '{fileName}'. The file may have been moved, renamed, or deleted, or the clipboard may be unavailable.",
            "copy-sha256" => $"Could not copy the SHA-256 for '{fileName}'. The clipboard may be unavailable.",
            "show-in-explorer" => $"Could not show '{fileName}' in Explorer. The file may have been moved, renamed, or deleted.",
            "cut" => $"Could not cut '{fileName}'. The file may have been moved, renamed, or deleted, or the clipboard may be unavailable.",
            "rename" => $"Could not rename '{fileName}'. The file may have been moved, deleted, locked, or access may have changed.",
            "delete" => $"Could not delete '{fileName}'. The file may have been moved, renamed, locked, or access may have changed.",
            _ => $"Could not process '{fileName}'. The file may have changed or become unavailable."
        };
        var dialog = new NoticeDialog(message);
        var owner = GetFileOperationDialogOwner();
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    private System.Windows.Window? GetFileOperationDialogOwner() =>
        _downloadNotificationWindow?.IsVisible is true
            ? _downloadNotificationWindow
            : IsVisible
                ? this
                : null;

    private static void LogOperationFailure(string action, DownloadEntry entry, string error) =>
        ActivityLog.WriteInteraction(action, entry.FileName, entry.FileSizeBytes, $"failed:{error}");

    private static string FormatError(System.Exception exception) =>
        $"{exception.GetType().FullName}: {exception.Message}";

    private async System.Threading.Tasks.Task LogExitAndFlushAsync()
    {
        LogExitBestEffort();
        await System.Threading.Tasks.Task.WhenAny(
            ActivityLog.FlushAsync(),
            System.Threading.Tasks.Task.Delay(500));
    }

    public void LogExitBestEffort()
    {
        if (System.Threading.Interlocked.Exchange(ref _exitLogged, 1) == 0)
        {
            ActivityLog.WriteLifecycle("exit");
        }
    }

    public void HideToTray(bool showFirstTimeNotification = false)
    {
        Hide();
        ShowInTaskbar = false;
        ActivityLog.WriteInteraction("hide-to-tray", string.Empty, 0);
        if (showFirstTimeNotification)
        {
            ShowCloseToTrayNotificationOnce();
        }
    }

    private void ShowCloseToTrayNotificationOnce()
    {
        if (_closeToTrayNotificationShown || _notifyIcon is null)
        {
            return;
        }

        _closeToTrayNotificationShown = true;
        SaveCurrentSettings("close-to-tray-notification-shown", bool.TrueString);
        _notifyIcon.ShowBalloonTip(
            3000,
            "Voltura Download Watcher is still running",
            "The window was closed, but Downloads monitoring continues. Use the notification-area icon to reopen the window or exit the app.",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    public void ShowFromTray()
    {
        ActivityLog.WriteInteraction("show-from-tray", string.Empty, 0);
        _downloadNotificationWindow?.Dismiss();
        ShowInTaskbar = false;
        if (!IsVisible)
        {
            Show();
        }

        WindowState = System.Windows.WindowState.Normal;
        Topmost = true;
        EnforceTopmost();
        Activate();

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != System.IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }
    }

    private void EnforceTopmost()
    {
        Topmost = true;
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == System.IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            handle,
            new System.IntPtr(-1),
            0,
            0,
            0,
            0,
            SetWindowPosNoActivate | SetWindowPosNoMove | SetWindowPosNoSize);

        if (_activeToolTip?.IsOpen is true)
        {
            PromoteTooltip(_activeToolTip);
        }
    }

    public void DisposeTrayIcon()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _trayComponents.Dispose();
        _notifyIcon.Visible = false;
        _trayPulseTimer.Stop();
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _trayActiveIcon?.Dispose();
        _trayActiveIcon = null;
        _trayPausedIcon?.Dispose();
        _trayPausedIcon = null;
        _playSoundMenuItem = null;
        _deleteToRecycleBinMenuItem = null;
        _monitoringMenuItem = null;
        _aboutMenuItem = null;
        _checkReleaseMenuItem = null;
        _downloadReleaseMenuItem = null;
        _dailyUpdateChecksMenuItem = null;
    }

    private static string GetDownloadsPath()
    {
        try
        {
            var path = GetKnownFolderPath(new System.Guid("374DE290-123F-4565-9164-39C4925E467B"));
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch
        {
        }

        return System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private void PositionWindow()
    {
        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (primaryScreen is null || handle == System.IntPtr.Zero)
        {
            var workArea = System.Windows.SystemParameters.WorkArea;
            Left = workArea.Right - Width;
            Top = workArea.Top + (workArea.Height - Height) / 2 - 120;
            return;
        }

        var dpi = GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var windowSize = new System.Drawing.Size(
            (int)System.Math.Ceiling((ActualWidth > 0 ? ActualWidth : Width) * scale),
            (int)System.Math.Ceiling((ActualHeight > 0 ? ActualHeight : Height) * scale));
        var placement = DialogPlacement.CalculateRightCenter(primaryScreen.WorkingArea, windowSize);
        SetWindowPos(
            handle,
            System.IntPtr.Zero,
            placement.X,
            placement.Y,
            0,
            0,
            SetWindowPosNoActivate | SetWindowPosNoSize | SetWindowPosNoZOrder);
    }

    private void SetupTrayIcon()
    {
        _trayIcon = CreateTrayIcon(isActive: false, _updateAvailable);
        _trayActiveIcon = CreateTrayIcon(isActive: true, _updateAvailable);
        _trayPausedIcon = CreatePausedTrayIcon(_updateAvailable);
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = TrayStatusText.Build(isDownloadInProgress: false, completedFileName: null),
            Icon = _trayIcon,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ShowFromTray);
            }
        };
        TrayIconVisibilityPromoter.PromoteWhenReady(_trayComponents, _notifyIcon);
        UpdateTrayStatus();
    }

    private System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menuFont = new System.Drawing.Font(
            "Bahnschrift SemiCondensed",
            9.5f,
            System.Drawing.FontStyle.Regular,
            System.Drawing.GraphicsUnit.Point);
        var menu = new System.Windows.Forms.ContextMenuStrip
        {
            BackColor = System.Drawing.Color.FromArgb(7, 16, 11),
            ForeColor = System.Drawing.Color.FromArgb(95, 210, 122),
            Font = menuFont,
            Renderer = new CyberpunkTrayMenuRenderer(),
            ShowCheckMargin = true,
            ShowImageMargin = false,
            Padding = new System.Windows.Forms.Padding(1)
        };

        var show = new System.Windows.Forms.ToolStripMenuItem("Show Voltura Download Watcher")
        {
            Font = new System.Drawing.Font(menuFont, System.Drawing.FontStyle.Bold),
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        show.Click += (_, _) => Dispatcher.Invoke(ShowFromTray);

        _monitoringMenuItem = new System.Windows.Forms.ToolStripMenuItem(
            _monitoringPaused ? "Resume monitoring" : "Pause monitoring")
        {
            CheckOnClick = false,
            Checked = false,
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _monitoringMenuItem.Click += (_, _) => Dispatcher.Invoke(ToggleMonitoring);

        var settingsMenu = new System.Windows.Forms.ToolStripMenuItem("Settings")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        settingsMenu.DropDown = new System.Windows.Forms.ContextMenuStrip
        {
            BackColor = menu.BackColor,
            ForeColor = menu.ForeColor,
            Font = menu.Font,
            Renderer = menu.Renderer,
            ShowCheckMargin = true,
            ShowImageMargin = false,
            Padding = menu.Padding
        };

        var startWithWindows = new System.Windows.Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = false,
            Checked = StartupRegistration.IsEnabled(),
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        startWithWindows.Click += (_, _) =>
        {
            StartupRegistration.SetEnabled(!startWithWindows.Checked);
            startWithWindows.Checked = StartupRegistration.IsEnabled();
            ActivityLog.WriteSettingChange(
                "start-with-windows",
                startWithWindows.Checked.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };

        _startMinimizedMenuItem = new System.Windows.Forms.ToolStripMenuItem("Start minimized")
        {
            CheckOnClick = false,
            Checked = _startMinimized,
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _startMinimizedMenuItem.Click += (_, _) =>
        {
            _startMinimized = !_startMinimized;
            _startMinimizedMenuItem.Checked = _startMinimized;
            SaveCurrentSettings(
                "start-minimized",
                _startMinimized.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };

        _playSoundMenuItem = new System.Windows.Forms.ToolStripMenuItem("Play sound on download")
        {
            CheckOnClick = false,
            Checked = !_isMuted,
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _playSoundMenuItem.Click += (_, _) =>
        {
            _isMuted = !_isMuted;
            SaveCurrentSettings("play-sound-on-download", (!_isMuted).ToString(System.Globalization.CultureInfo.InvariantCulture));
            UpdateMuteIcon();
            if (!_isMuted)
            {
                PlaySpark();
            }
        };

        _deleteToRecycleBinMenuItem = new System.Windows.Forms.ToolStripMenuItem("Delete to Recycle Bin")
        {
            CheckOnClick = false,
            Checked = _deleteToRecycleBin,
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _deleteToRecycleBinMenuItem.Click += (_, _) =>
        {
            _deleteToRecycleBin = !_deleteToRecycleBin;
            _deleteToRecycleBinMenuItem.Checked = _deleteToRecycleBin;
            SaveCurrentSettings("delete-to-recycle-bin", _deleteToRecycleBin.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };

        var defaultActionMenu = new System.Windows.Forms.ToolStripMenuItem("Default action")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        defaultActionMenu.DropDown = new System.Windows.Forms.ContextMenuStrip
        {
            BackColor = menu.BackColor,
            ForeColor = menu.ForeColor,
            Font = menu.Font,
            Renderer = menu.Renderer,
            ShowCheckMargin = true,
            ShowImageMargin = false,
            Padding = menu.Padding
        };
        var defaultActionItems = new System.Collections.Generic.Dictionary<DownloadDefaultAction, System.Windows.Forms.ToolStripMenuItem>();
        var defaultActions = new (DownloadDefaultAction Action, string Label)[]
        {
            (DownloadDefaultAction.OpenFile, "Open file"),
            (DownloadDefaultAction.ShowInExplorer, "Show in Explorer"),
            (DownloadDefaultAction.CopyAsPath, "Copy as path"),
            (DownloadDefaultAction.CopyFile, "Copy file"),
            (DownloadDefaultAction.CutFile, "Cut file")
        };
        foreach (var choice in defaultActions)
        {
            var actionItem = new System.Windows.Forms.ToolStripMenuItem(choice.Label)
            {
                CheckOnClick = false,
                Checked = _defaultAction == choice.Action,
                ForeColor = menu.ForeColor,
                Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
            };
            actionItem.Click += (_, _) =>
            {
                _defaultAction = choice.Action;
                foreach (var pair in defaultActionItems)
                {
                    pair.Value.Checked = pair.Key == _defaultAction;
                }

                SaveCurrentSettings("default-action", _defaultAction.ToString());
            };
            defaultActionItems.Add(choice.Action, actionItem);
            defaultActionMenu.DropDownItems.Add(actionItem);
        }

        var notificationMenu = new System.Windows.Forms.ToolStripMenuItem("Download Notification")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        notificationMenu.DropDown = new System.Windows.Forms.ContextMenuStrip
        {
            BackColor = menu.BackColor,
            ForeColor = menu.ForeColor,
            Font = menu.Font,
            Renderer = menu.Renderer,
            ShowCheckMargin = true,
            ShowImageMargin = false,
            Padding = menu.Padding
        };
        var notificationItems = new System.Collections.Generic.Dictionary<int, System.Windows.Forms.ToolStripMenuItem>();
        var notificationChoices = new (int Seconds, string Label)[]
        {
            (DownloadNotificationDurationPolicy.Off, "Off"),
            (5, "5 seconds"),
            (10, "10 seconds"),
            (DownloadNotificationDurationPolicy.UntilDismissed, "Until dismissed")
        };
        var customNotification = new System.Windows.Forms.ToolStripMenuItem("Custom...")
        {
            CheckOnClick = false,
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        void UpdateNotificationChecks()
        {
            foreach (var pair in notificationItems)
            {
                pair.Value.Checked = pair.Key == _downloadNotificationDurationSeconds;
            }

            customNotification.Checked = !DownloadNotificationDurationPolicy.IsPreset(
                _downloadNotificationDurationSeconds);
        }

        foreach (var choice in notificationChoices)
        {
            var notificationItem = new System.Windows.Forms.ToolStripMenuItem(choice.Label)
            {
                CheckOnClick = false,
                ForeColor = menu.ForeColor,
                Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
            };
            notificationItem.Click += (_, _) =>
            {
                _downloadNotificationDurationSeconds = choice.Seconds;
                UpdateNotificationChecks();
                if (_downloadNotificationDurationSeconds == DownloadNotificationDurationPolicy.Off)
                {
                    Dispatcher.Invoke(() => _downloadNotificationWindow?.Dismiss());
                }

                SaveCurrentSettings(
                    "download-notification",
                    _downloadNotificationDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            };
            notificationItems.Add(choice.Seconds, notificationItem);
            notificationMenu.DropDownItems.Add(notificationItem);
        }

        customNotification.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            var dialog = new DownloadNotificationDurationDialog(
                _downloadNotificationDurationSeconds,
                System.Windows.Forms.Cursor.Position);
            if (IsVisible && WindowState is not System.Windows.WindowState.Minimized)
            {
                dialog.Owner = this;
            }

            if (dialog.ShowDialog() is true)
            {
                _downloadNotificationDurationSeconds = dialog.DurationSeconds;
                UpdateNotificationChecks();
                SaveCurrentSettings(
                    "download-notification",
                    _downloadNotificationDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        });
        notificationMenu.DropDownItems.Add(customNotification);
        UpdateNotificationChecks();

        var openLog = new System.Windows.Forms.ToolStripMenuItem("Open log")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        openLog.Click += (_, _) => Dispatcher.Invoke(() => OpenActivityLog_Click(this, new System.Windows.RoutedEventArgs()));

        settingsMenu.DropDownItems.Add(startWithWindows);
        settingsMenu.DropDownItems.Add(_startMinimizedMenuItem);
        settingsMenu.DropDownItems.Add(_playSoundMenuItem);
        settingsMenu.DropDownItems.Add(_deleteToRecycleBinMenuItem);
        settingsMenu.DropDownItems.Add(defaultActionMenu);
        settingsMenu.DropDownItems.Add(notificationMenu);
        settingsMenu.DropDownItems.Add(openLog);

        _aboutMenuItem = new System.Windows.Forms.ToolStripMenuItem(_updateAvailable ? "! About" : "About")
        {
            ForeColor = _updateAvailable
                ? System.Drawing.Color.FromArgb(255, 204, 51)
                : menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _aboutMenuItem.DropDown = new System.Windows.Forms.ContextMenuStrip
        {
            BackColor = menu.BackColor,
            ForeColor = menu.ForeColor,
            Font = menu.Font,
            Renderer = menu.Renderer,
            ShowCheckMargin = true,
            ShowImageMargin = false,
            Padding = menu.Padding
        };
        var aboutHeader = new System.Windows.Forms.ToolStripMenuItem($"Voltura Download Watcher v{GetDisplayVersion()}")
        {
            Font = new System.Drawing.Font(menuFont, System.Drawing.FontStyle.Bold),
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        var productPage = new System.Windows.Forms.ToolStripMenuItem("Product page")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        productPage.Click += (_, _) => Dispatcher.Invoke(() => OpenWebPage(
            ReleaseUpdateChecker.ProductPageUrl,
            "open-product-page"));
        _checkReleaseMenuItem = new System.Windows.Forms.ToolStripMenuItem("Check for new version now")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _checkReleaseMenuItem.Click += async (_, _) =>
        {
            var invocationPoint = System.Windows.Forms.Cursor.Position;
            await Dispatcher.InvokeAsync(async () =>
                await CheckForReleaseAsync(force: true, invocationPoint)).Task.Unwrap();
        };
        _dailyUpdateChecksMenuItem = new System.Windows.Forms.ToolStripMenuItem("Check for new version daily")
        {
            CheckOnClick = false,
            Checked = _checkForUpdatesDaily,
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _dailyUpdateChecksMenuItem.Click += async (_, _) =>
        {
            _checkForUpdatesDaily = !_checkForUpdatesDaily;
            _dailyUpdateChecksMenuItem.Checked = _checkForUpdatesDaily;
            SaveCurrentSettings(
                "check-for-new-version-daily",
                _checkForUpdatesDaily.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (_checkForUpdatesDaily)
            {
                await Dispatcher.InvokeAsync(async () => await CheckForReleaseAsync(force: false)).Task.Unwrap();
            }
        };
        _downloadReleaseMenuItem = new System.Windows.Forms.ToolStripMenuItem("Download latest release")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        _downloadReleaseMenuItem.Click += (_, _) => Dispatcher.Invoke(() => OpenWebPage(
            string.IsNullOrWhiteSpace(_latestReleaseUrl)
                ? ReleaseUpdateChecker.LatestReleasePageUrl
                : _latestReleaseUrl,
            "open-latest-release"));
        _aboutMenuItem.DropDownItems.Add(aboutHeader);
        _aboutMenuItem.DropDownItems.Add(productPage);
        _aboutMenuItem.DropDownItems.Add(_checkReleaseMenuItem);
        _aboutMenuItem.DropDownItems.Add(_dailyUpdateChecksMenuItem);
        _aboutMenuItem.DropDownItems.Add(_downloadReleaseMenuItem);
        UpdateReleaseMenuState();

        var openDownloads = new System.Windows.Forms.ToolStripMenuItem("Open Downloads folder")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        openDownloads.Click += (_, _) => Dispatcher.Invoke(() => OpenDownloadsFolder_Click(this, new System.Windows.RoutedEventArgs()));

        var deleteAllDownloads = new System.Windows.Forms.ToolStripMenuItem("Delete all downloads")
        {
            ForeColor = System.Drawing.Color.FromArgb(220, 255, 84, 112),
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        deleteAllDownloads.Click += (_, _) =>
        {
            var invocationPoint = System.Windows.Forms.Cursor.Position;
            Dispatcher.Invoke(() => ConfirmDeleteAllDownloads(invocationPoint));
        };

        var cleanupDeletedFiles = new System.Windows.Forms.ToolStripMenuItem("Cleanup deleted files")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        cleanupDeletedFiles.Click += (_, _) => Dispatcher.Invoke(CleanupDeletedFiles);

        var exit = new System.Windows.Forms.ToolStripMenuItem("Exit")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        exit.Click += async (_, _) =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                _allowClose = true;
                await LogExitAndFlushAsync();
                DisposeTrayIcon();
                System.Windows.Application.Current.Shutdown();
            }).Task.Unwrap();
        };

        menu.Items.Add(show);
        menu.Items.Add(_monitoringMenuItem);
        menu.Items.Add(settingsMenu);
        menu.Items.Add(openDownloads);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(deleteAllDownloads);
        menu.Items.Add(cleanupDeletedFiles);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(_aboutMenuItem);
        menu.Items.Add(exit);
        return menu;
    }

    private async System.Threading.Tasks.Task CheckForReleaseAsync(
        bool force,
        System.Drawing.Point? invocationPoint = null)
    {
        if (_isScreenshotMode || _releaseCheckInProgress || (!force && !_checkForUpdatesDaily))
        {
            return;
        }

        var checkedAt = System.DateTimeOffset.UtcNow;
        if (!force
            && _lastReleaseCheckUtc is { } lastCheck
            && checkedAt - lastCheck < System.TimeSpan.FromDays(1))
        {
            return;
        }

        _releaseCheckInProgress = true;
        _lastReleaseCheckUtc = checkedAt;
        UpdateReleaseMenuState();
        ActivityLog.WriteInteraction(
            "check-release",
            string.Empty,
            0,
            force ? "manual-started" : "automatic-started");

        var result = await ReleaseUpdateChecker.CheckAsync(GetDisplayVersion());
        if (result.Success)
        {
            _latestReleaseVersion = result.LatestVersion;
            _latestReleaseUrl = result.ReleaseUrl;
            _updateAvailable = result.UpdateAvailable;
            ActivityLog.WriteInteraction(
                "check-release",
                string.Empty,
                0,
                $"ok;running={GetDisplayVersion()};latest={_latestReleaseVersion};update={_updateAvailable}");
            if (force)
            {
                ShowReleaseCheckResult(invocationPoint);
            }
        }
        else
        {
            ActivityLog.WriteInteraction("check-release", string.Empty, 0, $"failed:{result.Error}");
        }

        _releaseCheckInProgress = false;
        PersistReleaseState();
        UpdateReleaseMenuState();
        RefreshTrayIconsForUpdateState();
    }

    private void ShowReleaseCheckResult(System.Drawing.Point? invocationPoint)
    {
        var runningVersion = GetDisplayVersion();
        var dialog = new ReleaseCheckResultDialog(
            _updateAvailable,
            runningVersion,
            _latestReleaseVersion,
            invocationPoint ?? System.Windows.Forms.Cursor.Position);
        if (IsVisible)
        {
            dialog.Owner = this;
        }
        else
        {
            dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
        }

        ActivityLog.WriteInteraction(
            "show-release-check-result",
            _latestReleaseVersion ?? runningVersion,
            0,
            _updateAvailable ? "update-available" : "current");
        dialog.ShowDialog();
        if (dialog.DownloadLatestReleaseRequested)
        {
            OpenWebPage(
                string.IsNullOrWhiteSpace(_latestReleaseUrl)
                    ? ReleaseUpdateChecker.LatestReleasePageUrl
                    : _latestReleaseUrl,
                "open-latest-release");
        }
    }

    private void UpdateReleaseMenuState()
    {
        if (_aboutMenuItem is not null)
        {
            _aboutMenuItem.Text = _updateAvailable ? "! About" : "About";
            _aboutMenuItem.ForeColor = _updateAvailable
                ? System.Drawing.Color.FromArgb(255, 204, 51)
                : System.Drawing.Color.FromArgb(95, 210, 122);
        }

        if (_checkReleaseMenuItem is not null)
        {
            _checkReleaseMenuItem.Enabled = !_releaseCheckInProgress;
            _checkReleaseMenuItem.Text = _releaseCheckInProgress
                ? "Checking for new version..."
                : "Check for new version now";
            _checkReleaseMenuItem.ForeColor = _updateAvailable
                ? System.Drawing.Color.FromArgb(255, 204, 51)
                : System.Drawing.Color.FromArgb(95, 210, 122);
        }

        if (_downloadReleaseMenuItem is not null)
        {
            _downloadReleaseMenuItem.Text = _updateAvailable && !string.IsNullOrWhiteSpace(_latestReleaseVersion)
                ? $"Download v{_latestReleaseVersion}"
                : "Download latest release";
        }
    }

    private static void OpenWebPage(string url, string activity)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            ActivityLog.WriteInteraction(activity, url, 0);
        }
        catch (System.Exception ex)
        {
            ActivityLog.WriteInteraction(activity, url, 0, $"failed:{FormatError(ex)}");
        }
    }

    private async void ConfirmDeleteAllDownloads(System.Drawing.Point invocationPoint)
    {
        var warning = _deleteToRecycleBin
            ? "This will delete all downloaded files, not only the files visible in the app. Files will be sent to the Recycle Bin."
            : "This will permanently delete all downloaded files, not only the files visible in the app. Recycle Bin is OFF and these files cannot be restored by the app.";
        var dialog = new ConfirmationDialog(warning, invocationPoint) { Owner = this };
        if (dialog.ShowDialog() is not true)
        {
            ActivityLog.WriteInteraction("delete-all", _downloadsPath, 0, "cancelled");
            return;
        }

        string[] files;
        try
        {
            files = System.IO.Directory.GetFiles(_downloadsPath, "*", System.IO.SearchOption.TopDirectoryOnly);
        }
        catch (System.Exception ex)
        {
            ActivityLog.WriteInteraction("delete-all", _downloadsPath, 0, $"enumeration-failed:{FormatError(ex)}");
            new NoticeDialog("Could not read the Downloads folder. It may be unavailable or access may have changed.") { Owner = this }.ShowDialog();
            return;
        }

        var recycle = _deleteToRecycleBin;
        foreach (var entry in _downloads.Where(entry => files.Contains(entry.FullPath, System.StringComparer.OrdinalIgnoreCase)))
        {
            entry.DeleteRequested = true;
            entry.DeletionLogged = true;
            entry.TouchedAt = System.DateTime.Now;
        }

        ActivityLog.WriteInteraction(
            "delete-all",
            _downloadsPath,
            0,
            $"{(recycle ? "confirmed-recycle-bin" : "confirmed-permanent")};count={files.LongLength}");
        var failedPaths = await System.Threading.Tasks.Task.Run(() => DeleteAllDownloads(files, recycle));
        foreach (var entry in _downloads.Where(entry =>
                     files.Contains(entry.FullPath, System.StringComparer.OrdinalIgnoreCase)
                     && !failedPaths.Contains(entry.FullPath, System.StringComparer.OrdinalIgnoreCase)))
        {
            entry.ExistsNow = false;
        }

        foreach (var entry in _downloads.Where(entry => failedPaths.Contains(entry.FullPath, System.StringComparer.OrdinalIgnoreCase)))
        {
            entry.DeleteRequested = false;
            entry.DeletionLogged = false;
        }

        if (failedPaths.Count > 0)
        {
            var suffix = failedPaths.Count == 1 ? "file" : "files";
            new NoticeDialog($"Could not delete {failedPaths.Count} {suffix}. They may have been moved, renamed, locked, or access may have changed. Details were written to the activity log.")
            {
                Owner = this
            }.ShowDialog();
        }
    }

    private void CleanupDeletedFiles()
    {
        var deletedEntries = _downloads
            .Where(entry => !System.IO.File.Exists(entry.FullPath))
            .ToArray();
        foreach (var entry in deletedEntries)
        {
            _downloads.Remove(entry);
            ActivityLog.WriteInteraction("cleanup-deleted", entry.FileName, entry.FileSizeBytes);
        }

        ActivityLog.WriteInteraction(
            "cleanup-deleted-summary",
            string.Empty,
            0,
            $"ok;count={deletedEntries.Length}");
        RefreshDownloadsView();
    }

    private static System.Collections.Generic.List<string> DeleteAllDownloads(
        System.Collections.Generic.IEnumerable<string> files,
        bool recycle)
    {
        var failedPaths = new System.Collections.Generic.List<string>();
        foreach (var filePath in files)
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var fileSize = GetFileSize(filePath, 0);
            try
            {
                if (recycle)
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        filePath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                        Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                }
                else
                {
                    System.IO.File.Delete(filePath);
                }

                ActivityLog.WriteDeletion(recycle ? "app-delete-all-recycle-bin" : "app-delete-all-permanent", fileName, fileSize);
            }
            catch (System.Exception ex)
            {
                failedPaths.Add(filePath);
                ActivityLog.WriteInteraction("delete-all-file", fileName, fileSize, $"failed:{FormatError(ex)}");
            }
        }

        return failedPaths;
    }

    private static string GetDisplayVersion()
    {
        var attribute = System.Reflection.Assembly.GetExecutingAssembly()
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault();
        var informationalVersion = attribute?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static System.Drawing.Icon CreateTrayIcon(bool isActive, bool hasUpdate = false)
    {
        return CreateTrayStateIcon(isActive, isPaused: false, hasUpdate);
    }

    private static System.Drawing.Icon CreatePausedTrayIcon(bool hasUpdate = false)
    {
        return CreateTrayStateIcon(isActive: false, isPaused: true, hasUpdate);
    }

    private static System.Drawing.Icon CreateTrayStateIcon(bool isActive, bool isPaused, bool hasUpdate)
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.Clear(System.Drawing.Color.Transparent);

            var resource = System.Windows.Application.GetResourceStream(
                new System.Uri("pack://application:,,,/Assets/voltura-download-watcher.ico", System.UriKind.Absolute));
            if (resource is not null)
            {
                using (resource.Stream)
                using (var sourceIcon = new System.Drawing.Icon(resource.Stream, 32, 32))
                using (var sourceBitmap = sourceIcon.ToBitmap())
                using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    var opacity = isPaused ? 0.66f : isActive ? 1.0f : 0.96f;
                    var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
                    attributes.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                    graphics.DrawImage(
                        sourceBitmap,
                        new System.Drawing.Rectangle(0, 0, 32, 32),
                        0,
                        0,
                        sourceBitmap.Width,
                        sourceBitmap.Height,
                        System.Drawing.GraphicsUnit.Pixel,
                        attributes);
                }
            }

            if (isActive)
            {
                using var pulse = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 90, 255, 145), 1.4f);
                graphics.DrawLine(pulse, 2, 3, 9, 3);
                graphics.DrawLine(pulse, 3, 2, 3, 9);
                graphics.DrawLine(pulse, 23, 29, 30, 29);
                graphics.DrawLine(pulse, 29, 23, 29, 30);
            }

            if (isPaused)
            {
                using var pauseGlow = new System.Drawing.Pen(System.Drawing.Color.FromArgb(175, 5, 28, 12), 5.5f);
                using var pauseFill = new System.Drawing.Pen(System.Drawing.Color.FromArgb(245, 218, 255, 73), 3.1f);
                pauseGlow.StartCap = System.Drawing.Drawing2D.LineCap.Square;
                pauseGlow.EndCap = System.Drawing.Drawing2D.LineCap.Square;
                pauseFill.StartCap = System.Drawing.Drawing2D.LineCap.Square;
                pauseFill.EndCap = System.Drawing.Drawing2D.LineCap.Square;
                graphics.DrawLine(pauseGlow, 12, 10, 12, 21);
                graphics.DrawLine(pauseGlow, 20, 10, 20, 21);
                graphics.DrawLine(pauseFill, 12, 10, 12, 21);
                graphics.DrawLine(pauseFill, 20, 10, 20, 21);
            }

            if (hasUpdate)
            {
                using var updateFill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(245, 255, 204, 51));
                using var updateBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 35, 25, 8), 1.2f);
                graphics.FillEllipse(updateFill, 23, 2, 7, 7);
                graphics.DrawEllipse(updateBorder, 23, 2, 7, 7);
            }
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var borrowedIcon = System.Drawing.Icon.FromHandle(iconHandle);
            return (System.Drawing.Icon)borrowedIcon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private void RefreshTrayIconsForUpdateState()
    {
        var normal = CreateTrayIcon(isActive: false, _updateAvailable);
        var active = CreateTrayIcon(isActive: true, _updateAvailable);
        var paused = CreatePausedTrayIcon(_updateAvailable);
        var oldNormal = _trayIcon;
        var oldActive = _trayActiveIcon;
        var oldPaused = _trayPausedIcon;
        _trayIcon = normal;
        _trayActiveIcon = active;
        _trayPausedIcon = paused;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Icon = _monitoringPaused
                ? _trayPausedIcon
                : _activeBrowserDownloads.Count > 0 && _trayPulseIsBright
                    ? _trayActiveIcon
                    : _trayIcon;
        }

        oldNormal?.Dispose();
        oldActive?.Dispose();
        oldPaused?.Dispose();
    }

    private void QueueSha256ForLiveDownloads()
    {
        foreach (var entry in _downloads.Where(entry => entry.ExistsNow))
        {
            QueueSha256Calculation(entry, resetExistingHash: true);
        }
    }

    private void QueueSha256Calculation(DownloadEntry entry, bool resetExistingHash)
    {
        if (_isScreenshotMode || !entry.ExistsNow || !System.IO.File.Exists(entry.FullPath))
        {
            entry.Sha256State = Sha256State.Unavailable;
            return;
        }

        var path = entry.FullPath;
        if (resetExistingHash)
        {
            entry.Sha256 = null;
        }
        else if (entry.IsSha256Available)
        {
            return;
        }

        entry.Sha256State = Sha256State.Pending;
        if (!_sha256QueuedPaths.TryAdd(path, 0))
        {
            return;
        }

        if (!_sha256Queue.Writer.TryWrite(new Sha256WorkItem(entry, path)))
        {
            _sha256QueuedPaths.TryRemove(path, out _);
            entry.Sha256State = Sha256State.Unavailable;
        }
    }

    private async System.Threading.Tasks.Task ProcessSha256QueueAsync()
    {
        try
        {
            await foreach (var workItem in _sha256Queue.Reader.ReadAllAsync(_sha256Cancellation.Token)
                .ConfigureAwait(false))
            {
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (workItem.Entry.ExistsNow
                            && string.Equals(
                                workItem.Entry.FullPath,
                                workItem.Path,
                                System.StringComparison.OrdinalIgnoreCase))
                        {
                            workItem.Entry.Sha256State = Sha256State.Calculating;
                        }
                    });

                    var result = await Sha256Calculator.CalculateStableAsync(
                        workItem.Path,
                        _sha256Cancellation.Token).ConfigureAwait(false);
                    await Dispatcher.InvokeAsync(() => ApplySha256Result(workItem, result));
                }
                catch (System.OperationCanceledException) when (_sha256Cancellation.IsCancellationRequested)
                {
                    break;
                }
                catch (System.Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (string.Equals(
                                workItem.Entry.FullPath,
                                workItem.Path,
                                System.StringComparison.OrdinalIgnoreCase))
                        {
                            workItem.Entry.Sha256State = Sha256State.Unavailable;
                        }
                    });
                    ActivityLog.WriteInteraction(
                        "sha256-calculated",
                        workItem.Entry.FileName,
                        workItem.Entry.FileSizeBytes,
                        $"failed:{FormatError(ex)}");
                }
                finally
                {
                    _sha256QueuedPaths.TryRemove(workItem.Path, out _);
                }
            }
        }
        catch (System.OperationCanceledException) when (_sha256Cancellation.IsCancellationRequested)
        {
        }
    }

    private void ApplySha256Result(Sha256WorkItem workItem, Sha256CalculationResult result)
    {
        if (!_downloads.Contains(workItem.Entry)
            || !string.Equals(workItem.Entry.FullPath, workItem.Path, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!workItem.Entry.ExistsNow || !System.IO.File.Exists(workItem.Path))
        {
            workItem.Entry.Sha256State = Sha256State.Unavailable;
            return;
        }

        if (result.Success && result.Hash is not null)
        {
            workItem.Entry.Sha256 = result.Hash;
            workItem.Entry.Sha256State = Sha256State.Available;
            workItem.Entry.FileSizeBytes = result.SizeBytes;
            ActivityLog.WriteInteraction(
                "sha256-calculated",
                workItem.Entry.FileName,
                workItem.Entry.FileSizeBytes,
                $"ok;sha256={result.Hash}");
            return;
        }

        workItem.Entry.Sha256State = Sha256State.Pending;
        _ = RetrySha256AfterDelayAsync(workItem.Entry, workItem.Path);
    }

    private async System.Threading.Tasks.Task RetrySha256AfterDelayAsync(DownloadEntry entry, string path)
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(
                System.TimeSpan.FromSeconds(3),
                _sha256Cancellation.Token).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() =>
            {
                if (entry.ExistsNow
                    && string.Equals(entry.FullPath, path, System.StringComparison.OrdinalIgnoreCase)
                    && System.IO.File.Exists(path)
                    && entry.Sha256State is not Sha256State.Available)
                {
                    QueueSha256Calculation(entry, resetExistingHash: false);
                }
            });
        }
        catch (System.OperationCanceledException) when (_sha256Cancellation.IsCancellationRequested)
        {
        }
    }

    private void StartWatcher()
    {
        var session = _monitoringSession.Current;
        var watcher = new System.IO.FileSystemWatcher(_downloadsPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.CreationTime | System.IO.NotifyFilters.LastWrite,
            Filter = "*",
            EnableRaisingEvents = false,
            InternalBufferSize = 64 * 1024
        };

        watcher.Created += (_, e) => OnCreated(session, e);
        watcher.Renamed += (_, e) => OnRenamed(session, e);
        watcher.Deleted += (_, e) => OnDeleted(session, e);
        watcher.Error += (_, e) => OnWatcherError(session, e);
        _watcher = watcher;
        watcher.EnableRaisingEvents = !_monitoringPaused;
    }

    private void LoadInitialDownloads()
    {
        try
        {
            _activeBrowserDownloads.Clear();
            foreach (var activeFile in new System.IO.DirectoryInfo(_downloadsPath).EnumerateFiles())
            {
                if (DownloadPolicy.IsBrowserDownloadInProgressName(activeFile.Name))
                {
                    _activeBrowserDownloads.Add(activeFile.FullName);
                }
            }
            UpdateBrowserActivityIndicator();

            var files = new System.IO.DirectoryInfo(_downloadsPath)
                .EnumerateFiles()
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.CreationTimeUtc)
                .Where(file => DownloadPolicy.IsValidDownloadName(file.Name))
                .Take(MaxItems)
                .ToList();

            foreach (var file in files.Reverse<System.IO.FileInfo>())
            {
                AddInitialDownload(file);
            }
        }
        catch
        {
        }
    }

    private void AddInitialDownload(System.IO.FileInfo file)
    {
        if (!DownloadPolicy.IsValidDownloadName(file.Name))
        {
            return;
        }

        if (_downloads.Any(x => string.Equals(x.FullPath, file.FullName, System.StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _downloads.Insert(0, new DownloadEntry
        {
            FileName = file.Name,
            FullPath = file.FullName,
            CreatedAt = file.LastWriteTime,
            TouchedAt = file.LastWriteTime,
            FileSizeBytes = file.Length,
            IsFresh = false,
            IsNewest = false,
            ExistsNow = true,
            IsRemovalRecent = false,
            DeleteRequested = false
        });
    }

    private void MergeLoggedDownloads(
        System.Collections.Generic.IReadOnlyList<ActivityLog.LoggedDownload> loggedDownloads)
    {
        foreach (var logged in loggedDownloads)
        {
            var fullPath = System.IO.Path.Combine(_downloadsPath, logged.FileName);
            var existing = _downloads.FirstOrDefault(entry =>
                string.Equals(entry.FullPath, fullPath, System.StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (logged.DownloadedAt.LocalDateTime > existing.CreatedAt)
                {
                    existing.CreatedAt = logged.DownloadedAt.LocalDateTime;
                }

                existing.FileSizeBytes = System.Math.Max(existing.FileSizeBytes, logged.SizeBytes);
                continue;
            }

            var existsNow = System.IO.File.Exists(fullPath);
            _downloads.Add(new DownloadEntry
            {
                FileName = logged.FileName,
                FullPath = fullPath,
                CreatedAt = logged.DownloadedAt.LocalDateTime,
                TouchedAt = logged.DeletedAt?.LocalDateTime ?? logged.DownloadedAt.LocalDateTime,
                FileSizeBytes = existsNow ? GetFileSize(fullPath, logged.SizeBytes) : logged.SizeBytes,
                IsFresh = false,
                IsNewest = false,
                ExistsNow = existsNow,
                IsRemovalRecent = false,
                DeleteRequested = false,
                DeletionLogged = !existsNow,
                Sha256 = existsNow ? null : logged.Sha256,
                Sha256State = existsNow
                    ? Sha256State.Pending
                    : string.IsNullOrWhiteSpace(logged.Sha256)
                        ? Sha256State.Unavailable
                        : Sha256State.Available
            });
        }
    }

    private void TrimHistoryToLimit()
    {
        while (_downloads.Count > MaxItems)
        {
            var candidate = DownloadHistoryPolicy.SelectEvictionCandidate(_downloads);
            if (candidate is null)
            {
                break;
            }

            _downloads.Remove(candidate);
        }
    }

    private void OnCreated(long session, System.IO.FileSystemEventArgs e)
    {
        if (!IsCurrentMonitoringSession(session))
        {
            return;
        }

        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.Name ?? string.Empty))
        {
            SetBrowserDownloadActive(session, e.FullPath, isActive: true);
            return;
        }

        AddDownload(session, e.FullPath);
    }

    private void OnRenamed(long session, System.IO.RenamedEventArgs e)
    {
        if (!IsCurrentMonitoringSession(session))
        {
            return;
        }

        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.OldName ?? string.Empty))
        {
            SetBrowserDownloadActive(session, e.OldFullPath, isActive: false);
        }

        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.Name ?? string.Empty))
        {
            SetBrowserDownloadActive(session, e.FullPath, isActive: true);
            return;
        }

        if (_appRenameTargets.TryRemove(e.FullPath, out _))
        {
            return;
        }

        AddDownload(session, e.FullPath);
    }

    private void OnDeleted(long session, System.IO.FileSystemEventArgs e)
    {
        if (!IsCurrentMonitoringSession(session))
        {
            return;
        }

        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.Name ?? string.Empty))
        {
            SetBrowserDownloadActive(session, e.FullPath, isActive: false);
            return;
        }

        Dispatcher.BeginInvoke(() => LogExternalDeletion(session, e.FullPath));
    }

    private void SetBrowserDownloadActive(long session, string fullPath, bool isActive)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsCurrentMonitoringSession(session))
            {
                return;
            }

            if (isActive)
            {
                _activeBrowserDownloads.Add(fullPath);
            }
            else
            {
                _activeBrowserDownloads.Remove(fullPath);
            }

            UpdateBrowserActivityIndicator();
        });
    }

    private void UpdateBrowserActivityIndicator()
    {
        if (FindName("HeaderStatusDot") is not System.Windows.Shapes.Ellipse dot
            || dot.RenderTransform is not System.Windows.Media.ScaleTransform scale)
        {
            return;
        }

        if (_monitoringPaused)
        {
            _trayPulseTimer.Stop();
            _trayPulseIsBright = false;
            if (_notifyIcon is not null && _trayPausedIcon is not null)
            {
                _notifyIcon.Icon = _trayPausedIcon;
            }

            dot.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            dot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x49, 0x3D));
            dot.Opacity = 0.18;
            scale.ScaleX = 0.72;
            scale.ScaleY = 0.72;
            UpdateTrayStatus();
            return;
        }

        dot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x46, 0x97, 0x5A));

        if (_activeBrowserDownloads.Count == 0)
        {
            _trayPulseTimer.Stop();
            _trayPulseIsBright = false;
            if (_notifyIcon is not null && _trayIcon is not null)
            {
                _notifyIcon.Icon = _trayIcon;
            }

            dot.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            dot.Opacity = 0.6;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            UpdateTrayStatus();
            return;
        }

        if (!_trayPulseTimer.IsEnabled)
        {
            _trayPulseIsBright = true;
            if (_notifyIcon is not null && _trayActiveIcon is not null)
            {
                _notifyIcon.Icon = _trayActiveIcon;
            }
            _trayPulseTimer.Start();
        }

        UpdateTrayStatus();

        var pulse = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.25,
            To = 0.8,
            Duration = System.TimeSpan.FromMilliseconds(420),
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        var scalePulse = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.78,
            To = 1.35,
            Duration = System.TimeSpan.FromMilliseconds(420),
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };

        dot.BeginAnimation(System.Windows.UIElement.OpacityProperty, pulse);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scalePulse);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scalePulse);
    }

    private void NotifyDownloadArrival(DownloadEntry entry)
    {
        _recentDownloads.Add((System.DateTime.Now, entry.FileName));
        UpdateTrayStatus();
        PlaySpark();

        if (_downloadNotificationDurationSeconds == DownloadNotificationDurationPolicy.Off
            || (IsVisible && WindowState is not System.Windows.WindowState.Minimized))
        {
            return;
        }

        _downloadNotificationWindow ??= new DownloadNotificationWindow(InvokeDownloadNotificationAction);
        _downloadNotificationWindow.ShowDownload(entry, _downloadNotificationDurationSeconds);
    }

    private DownloadNotificationActionOutcome InvokeDownloadNotificationAction(
        DownloadNotificationAction action,
        DownloadEntry entry)
    {
        return action switch
        {
            DownloadNotificationAction.OpenFile => OpenDefaultPath(entry),
            DownloadNotificationAction.CopyFile => SetFileClipboard(entry, isCut: false),
            DownloadNotificationAction.CopyAsPath => CopyPathToClipboard(entry),
            DownloadNotificationAction.CutFile => SetFileClipboard(entry, isCut: true),
            DownloadNotificationAction.Rename => RenameFile(entry),
            DownloadNotificationAction.CopySha256 => CopySha256ToClipboard(entry),
            DownloadNotificationAction.Delete => DeleteFile(entry),
            _ => PerformDefaultAction(entry)
        };
    }

    private void UpdateTrayStatus()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        if (_monitoringPaused)
        {
            _notifyIcon.Text = "Voltura Download Watcher - Monitoring paused";
            return;
        }

        var cutoff = System.DateTime.Now.AddSeconds(-TrayCompletionStatusSeconds);
        _recentDownloads.RemoveAll(download => download.OccurredAt <= cutoff);
        var completedFileName = _recentDownloads.Count == 1
            ? _recentDownloads[0].FileName
            : null;
        _notifyIcon.Text = TrayStatusText.Build(
            _activeBrowserDownloads.Count > 0,
            completedFileName,
            _recentDownloads.Count);
    }

    private void OnWatcherError(long session, System.IO.ErrorEventArgs e)
    {
        if (!IsCurrentMonitoringSession(session)
            || System.Threading.Interlocked.CompareExchange(ref _watcherRecoveryQueued, 1, 0) != 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (!IsCurrentMonitoringSession(session))
                {
                    return;
                }

                _watcher?.Dispose();
                _watcher = null;
                StartWatcher();
                LoadInitialDownloads();

                TrimHistoryToLimit();
                QueueSha256ForLiveDownloads();
                RefreshDownloadsView();
            }
            finally
            {
                System.Threading.Volatile.Write(ref _watcherRecoveryQueued, 0);
            }
        });
    }

    private void AddDownload(long session, string fullPath)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsCurrentMonitoringSession(session))
            {
                return;
            }

            var fileName = System.IO.Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName) || !DownloadPolicy.IsValidDownloadName(fileName))
            {
                return;
            }

            var existing = _downloads.FirstOrDefault(x => string.Equals(x.FullPath, fullPath, System.StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var arrivalTime = System.DateTime.Now;
                var existsNow = System.IO.File.Exists(fullPath);
                existing.ExistsNow = existsNow;
                existing.CreatedAt = arrivalTime;
                existing.TouchedAt = arrivalTime;
                existing.FileSizeBytes = GetFileSize(fullPath, existing.FileSizeBytes);
                existing.IsFresh = true;
                PinFreshDownload(existing, arrivalTime);
                existing.DeleteRequested = false;
                existing.DeletionLogged = !existsNow;
                existing.IsRemovalRecent = !existsNow;
                QueueSha256Calculation(existing, resetExistingHash: true);
                MoveToTop(existing);
                ActivityLog.WriteDownload(fileName, existing.FileSizeBytes);
                NotifyDownloadArrival(existing);
                if (!existsNow)
                {
                    ActivityLog.WriteDeletion("external", fileName, existing.FileSizeBytes);
                }
                RefreshDownloadsView();
                return;
            }

            foreach (var entry in _downloads)
            {
                entry.IsNewest = false;
            }

            var now = System.DateTime.Now;
            var fileSizeBytes = GetFileSize(fullPath, 0);
            var fileExistsNow = System.IO.File.Exists(fullPath);
            var download = new DownloadEntry
            {
                FileName = fileName,
                FullPath = fullPath,
                CreatedAt = now,
                TouchedAt = now,
                FileSizeBytes = fileSizeBytes,
                IsFresh = true,
                IsNewest = true,
                ExistsNow = fileExistsNow,
                DeletionLogged = !fileExistsNow
            };
            PinFreshDownload(download, now);
            _downloads.Insert(0, download);
            QueueSha256Calculation(download, resetExistingHash: true);

            ActivityLog.WriteDownload(fileName, fileSizeBytes);
            NotifyDownloadArrival(download);
            if (!fileExistsNow)
            {
                ActivityLog.WriteDeletion("external", fileName, fileSizeBytes);
            }

            TrimHistoryToLimit();
            RefreshDownloadsView();
        });
    }

    private void MoveToTop(DownloadEntry entry)
    {
        var index = _downloads.IndexOf(entry);
        if (index > 0)
        {
            _downloads.RemoveAt(index);
            _downloads.Insert(0, entry);
        }

        foreach (var item in _downloads)
        {
            item.IsNewest = false;
        }

        if (_downloads.Count > 0)
        {
            _downloads[0].IsNewest = true;
        }
    }

    private void RefreshFreshness()
    {
        var now = System.DateTime.Now;
        var viewNeedsRefresh = false;
        foreach (var entry in _downloads)
        {
            entry.IsFresh = (now - entry.TouchedAt).TotalSeconds < FreshDownloadSeconds;
            if (entry.SortPinOrder > 0 && now >= entry.SortPinnedUntil)
            {
                entry.SortPinOrder = 0;
            }
            var existsNow = System.IO.File.Exists(entry.FullPath);
            if (existsNow)
            {
                entry.FileSizeBytes = GetFileSize(entry.FullPath, entry.FileSizeBytes);
            }
            if (entry.ExistsNow != existsNow)
            {
                viewNeedsRefresh = true;
            }

            if (entry.ExistsNow && !existsNow)
            {
                if (!entry.DeleteRequested && !entry.DeletionLogged)
                {
                    ActivityLog.WriteDeletion("external", entry.FileName, entry.FileSizeBytes);
                    entry.DeletionLogged = true;
                }

                entry.IsRemovalRecent = true;
                entry.TouchedAt = now;
            }

            var becameAvailable = !entry.ExistsNow && existsNow;
            entry.ExistsNow = existsNow;
            if (becameAvailable)
            {
                QueueSha256Calculation(entry, resetExistingHash: true);
            }
            if (!existsNow)
            {
                var removedForSeconds = (now - entry.TouchedAt).TotalSeconds;
                entry.IsRemovalRecent = DownloadPolicy.IsRemovalAnimationActive(entry.DeleteRequested, removedForSeconds);
            }
        }

        if (viewNeedsRefresh)
        {
            RefreshDownloadsView();
        }
    }

    private void RefreshDownloadsView()
    {
        _downloadsView.Refresh();
        UpdateEmptyState();
        OnPropertyChanged(nameof(DownloadsView));
    }

    private void ApplySort()
    {
        using (_downloadsView.DeferRefresh())
        {
            _downloadsView.SortDescriptions.Clear();
            _downloadsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                nameof(DownloadEntry.SortPinOrder),
                System.ComponentModel.ListSortDirection.Descending));
            var direction = _sortDescending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;
            var propertyName = _sortMode switch
            {
                DownloadSortMode.Date => nameof(DownloadEntry.CreatedAt),
                DownloadSortMode.Size => nameof(DownloadEntry.FileSizeBytes),
                DownloadSortMode.Name => nameof(DownloadEntry.FileName),
                _ => nameof(DownloadEntry.CreatedAt)
            };
            _downloadsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(propertyName, direction));
            if (_sortMode is not DownloadSortMode.Date)
            {
                _downloadsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                    nameof(DownloadEntry.CreatedAt),
                    System.ComponentModel.ListSortDirection.Descending));
            }
        }

        if (_downloadsView is System.ComponentModel.ICollectionViewLiveShaping liveShaping)
        {
            liveShaping.LiveSortingProperties.Clear();
            liveShaping.LiveSortingProperties.Add(nameof(DownloadEntry.SortPinOrder));
            liveShaping.LiveSortingProperties.Add(_sortMode switch
            {
                DownloadSortMode.Date => nameof(DownloadEntry.CreatedAt),
                DownloadSortMode.Size => nameof(DownloadEntry.FileSizeBytes),
                DownloadSortMode.Name => nameof(DownloadEntry.FileName),
                _ => nameof(DownloadEntry.CreatedAt)
            });
            liveShaping.IsLiveSorting = true;
        }

        UpdateSortIndicators();
    }

    private void PinFreshDownload(DownloadEntry entry, System.DateTime arrivedAt)
    {
        entry.SortPinOrder = ++_nextSortPinOrder;
        entry.SortPinnedUntil = arrivedAt.AddSeconds(FreshDownloadSeconds);
    }

    private void UpdateSortIndicators()
    {
        UpdateSortIndicator(DateSortButton, DateSortDirection, DownloadSortMode.Date);
        UpdateSortIndicator(SizeSortButton, SizeSortDirection, DownloadSortMode.Size);
        UpdateSortIndicator(NameSortButton, NameSortDirection, DownloadSortMode.Name);
    }

    private void UpdateSortIndicator(
        System.Windows.Controls.Button button,
        System.Windows.Shapes.Path directionPath,
        DownloadSortMode mode)
    {
        var isActive = _sortMode == mode;
        button.Background = isActive
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x58, 0x17, 0x4A, 0x28))
            : System.Windows.Media.Brushes.Transparent;
        button.BorderBrush = isActive
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xC0, 0x5C, 0xD7, 0x7A))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x35, 0x3B, 0x80, 0x4B));
        directionPath.Data = System.Windows.Media.Geometry.Parse(isActive
            ? _sortDescending ? "M2,3 L5,7 L8,3" : "M2,7 L5,3 L8,7"
            : "M2,5 H8");
        directionPath.Opacity = isActive ? 1 : 0.22;
    }

    private bool IsCurrentMonitoringSession(long session) =>
        !_monitoringPaused && _monitoringSession.IsCurrent(session);

    private void LogExternalDeletion(long session, string fullPath)
    {
        if (!IsCurrentMonitoringSession(session))
        {
            return;
        }

        var entry = _downloads.FirstOrDefault(item => string.Equals(
            item.FullPath,
            fullPath,
            System.StringComparison.OrdinalIgnoreCase));
        if (entry is null || entry.DeleteRequested || entry.DeletionLogged)
        {
            return;
        }

        entry.DeletionLogged = true;
        ActivityLog.WriteDeletion("external", entry.FileName, entry.FileSizeBytes);
    }

    private static long GetFileSize(string path, long fallback)
    {
        try
        {
            return new System.IO.FileInfo(path).Length;
        }
        catch (System.IO.IOException)
        {
            return fallback;
        }
        catch (System.UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    private bool FilterDownloads(object obj)
    {
        if (obj is not DownloadEntry entry)
        {
            return false;
        }

        return _activeFilter switch
        {
            FilterMode.All => true,
            FilterMode.Apps => entry.IsApplication,
            FilterMode.Docs => entry.IsDocument,
            FilterMode.Archives => entry.IsArchive,
            FilterMode.Removed => !entry.ExistsNow,
            _ => true
        };
    }

    private void BuildFilterButtons()
    {
        _filterButtons.Add(new FilterChip(FilterMode.All, "M7,7 H13 V13 H7 Z", "#6CFF91", "All"));
        _filterButtons.Add(new FilterChip(FilterMode.Apps, "M6,6 H12 V12 H6 Z M8,8 H10 V10 H8 Z", "#7CFF9A", "Apps"));
        _filterButtons.Add(new FilterChip(FilterMode.Docs, "M6,5 H11 L13,7 V13 H6 Z M8,8 H11 M8,10 H11", "#A6FFBE", "Docs"));
        _filterButtons.Add(new FilterChip(FilterMode.Archives, "M5,8 H13 V12 H5 Z M7,6 H11 V8 H7 Z", "#4BFF7A", "Archives"));
        _filterButtons.Add(new FilterChip(FilterMode.Removed, "M6,6 L12,12 M12,6 L6,12", "#FF4D7A", "Removed"));
        OnPropertyChanged(nameof(FilterButtons));
    }

    private void UpdateEmptyState()
    {
        if (FindName("EmptyState") is System.Windows.FrameworkElement empty)
        {
            empty.Visibility = _downloadsView.Cast<object>().Any() ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }
    }

    private bool OpenPathInShell(string path)
    {
        try
        {
            if (RunningExecutableActivator.TryActivate(path))
            {
                return true;
            }

            var info = ExecutableLaunchPolicy.CreateStartInfo(path);
            System.Diagnostics.Process.Start(info);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PlaySpark()
    {
        if (_isMuted)
        {
            return;
        }

        var soundPath = _sparkSoundPath;
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                if (!System.IO.File.Exists(soundPath))
                {
                    ActivityLog.WriteInteraction("play-sound", soundPath, 0, "failed:asset-missing");
                    return;
                }

                if (!PlaySound(soundPath, System.IntPtr.Zero, PlaySoundFilename | PlaySoundNodefault))
                {
                    ActivityLog.WriteInteraction(
                        "play-sound",
                        string.Empty,
                        0,
                        $"failed:winmm:{System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                }
            }
            catch (System.Exception ex)
            {
                ActivityLog.WriteInteraction("play-sound", string.Empty, 0, $"failed:{FormatError(ex)}");
            }
        });
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (System.IO.File.Exists(SettingsPath))
            {
                var json = System.IO.File.ReadAllText(SettingsPath);
                return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
        }

        return new AppSettings();
    }

    private void SaveSettings(AppSettings settings)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(SettingsPath, json);
        }
        catch
        {
        }
    }

    private void SaveCurrentSettings(string setting, string value)
    {
        SaveSettings(CreateCurrentSettings());
        ActivityLog.WriteSettingChange(setting, value);
    }

    private void PersistReleaseState() => SaveSettings(CreateCurrentSettings());

    private AppSettings CreateCurrentSettings() =>
        new()
        {
            StartMinimized = _startMinimized,
            IsMuted = _isMuted,
            DeleteToRecycleBin = _deleteToRecycleBin,
            SortMode = _sortMode,
            SortDescending = _sortDescending,
            CloseToTrayNotificationShown = _closeToTrayNotificationShown,
            DefaultAction = _defaultAction,
            LastReleaseCheckUtc = _lastReleaseCheckUtc,
            LatestReleaseVersion = _latestReleaseVersion,
            LatestReleaseUrl = _latestReleaseUrl,
            CheckForUpdatesDaily = _checkForUpdatesDaily,
            DownloadNotificationDurationSeconds = _downloadNotificationDurationSeconds
        };

    private void UpdateMuteIcon()
    {
        if (_playSoundMenuItem is not null)
        {
            _playSoundMenuItem.Checked = !_isMuted;
        }

        if (FindName("MuteIconPath") is System.Windows.Shapes.Path path)
        {
            path.Data = _isMuted
                ? System.Windows.Media.Geometry.Parse("M3,10 L6,10 L10,6 L10,14 L6,10 Z M11,8 C12.4,9 12.4,11 11,12 M11.8,7.2 L14.6,14.8")
                : System.Windows.Media.Geometry.Parse("M3,10 L6,10 L10,6 L10,14 L6,10 Z M11,8 C12.1,8.6 12.1,11.4 11,12");
        }
    }

    private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName ?? string.Empty);
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] System.Guid rfid,
        uint dwFlags,
        System.IntPtr hToken,
        out System.IntPtr ppszPath);

    [System.Runtime.InteropServices.DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(System.IntPtr ptr);

    [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PlaySound(
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string sound,
        System.IntPtr module,
        uint flags);

    private static string? GetKnownFolderPath(System.Guid folderId)
    {
        var hr = SHGetKnownFolderPath(folderId, 0, System.IntPtr.Zero, out var pathPtr);
        if (hr != 0 || pathPtr == System.IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringUni(pathPtr);
        }
        finally
        {
            CoTaskMemFree(pathPtr);
        }
    }

    public void Dispose()
    {
        _freshnessTimer.Stop();
        _trayPulseTimer.Stop();
        _releaseCheckTimer.Stop();
        _downloadNotificationWindow?.Dispose();
        _downloadNotificationWindow = null;
        _sha256Queue.Writer.TryComplete();
        _sha256Cancellation.Cancel();
        _watcher?.Dispose();
        _watcher = null;
        DisposeTrayIcon();
    }

    public sealed class FilterChip
    {
        public FilterChip(FilterMode mode, string glyph, string brushHex, string tooltip)
        {
            Mode = mode;
            Glyph = System.Windows.Media.Geometry.Parse(glyph);
            Brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(brushHex)!;
            Tooltip = tooltip;
        }

        public FilterMode Mode { get; }
        public System.Windows.Media.Geometry Glyph { get; }
        public System.Windows.Media.Brush Brush { get; }
        public string Tooltip { get; }
    }

    private sealed record Sha256WorkItem(DownloadEntry Entry, string Path);

    public enum FilterMode
    {
        All,
        Apps,
        Docs,
        Archives,
        Removed
    }
}
