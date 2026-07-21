namespace VolturaDownloadWatcher;

public partial class MainWindow : System.Windows.Window, System.ComponentModel.INotifyPropertyChanged, System.IDisposable
{
    private const int MaxItems = 20;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoActivate = 0x0010;
    private static readonly string SettingsPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "VolturaDownloadWatcher",
        "settings.json");

    private readonly System.Collections.ObjectModel.ObservableCollection<DownloadEntry> _downloads = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<FilterChip> _filterButtons = new();
    private readonly System.Collections.Generic.HashSet<string> _activeBrowserDownloads = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.ComponentModel.ICollectionView _downloadsView;
    private readonly string _downloadsPath = GetDownloadsPath();
    private readonly bool _isScreenshotMode = string.Equals(
        System.Environment.GetEnvironmentVariable("VOLTURA_DOWNLOAD_WATCHER_SCREENSHOT"),
        "1",
        System.StringComparison.Ordinal);
    private readonly System.Windows.Threading.DispatcherTimer _freshnessTimer;
    private readonly System.Windows.Threading.DispatcherTimer _trayPulseTimer;
    private System.IO.FileSystemWatcher? _watcher;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIcon;
    private System.Drawing.Icon? _trayActiveIcon;
    private System.Windows.Forms.ToolStripMenuItem? _playSoundMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _deleteToRecycleBinMenuItem;
    private System.Media.SoundPlayer? _sparkPlayer;
    private System.IO.MemoryStream? _sparkStream;
    private bool _isMuted;
    private bool _isTooltipOpen;
    private bool _watcherRecoveryQueued;
    private bool _trayPulseIsBright;
    private bool _deleteToRecycleBin;
    private FilterMode _activeFilter = FilterMode.All;

    public MainWindow()
    {
        InitializeComponent();
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
        _isMuted = settings.IsMuted;
        _deleteToRecycleBin = settings.DeleteToRecycleBin;
        UpdateMuteIcon();
        _sparkStream = new System.IO.MemoryStream(CreateSparkWaveBytes());
        _sparkPlayer = new System.Media.SoundPlayer(_sparkStream);

        _freshnessTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(1)
        };
        _freshnessTimer.Tick += (_, _) =>
        {
            RefreshFreshness();
            EnforceTopmost();
        };
        _trayPulseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(420)
        };
        _trayPulseTimer.Tick += (_, _) =>
        {
            if (_notifyIcon is null || _trayIcon is null || _trayActiveIcon is null)
            {
                return;
            }

            _trayPulseIsBright = !_trayPulseIsBright;
            _notifyIcon.Icon = _trayPulseIsBright ? _trayActiveIcon : _trayIcon;
        };
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
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(System.IntPtr iconHandle);

    private void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
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

        if (!System.IO.Directory.Exists(_downloadsPath))
        {
            UpdateEmptyState();
            return;
        }

        StartWatcher();
        LoadInitialDownloads();
        RefreshDownloadsView();
        _freshnessTimer.Start();
    }

    private void LoadScreenshotDownloads()
    {
        var names = new[]
        {
            "neon-grid-reference.png",
            "night-shift-notes.md",
            "signal-decoder-v2.4.1-win-x64.exe",
            "city-sector-map.pdf",
            "archive-node-07.zip",
            "terminal-color-profile.json",
            "cyberdeck-firmware.bin",
            "relay-diagnostics.txt",
            "electric-skyline-wallpaper.webp",
            "download-watcher-release-notes.md"
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

        _isTooltipOpen = true;
        PromoteTooltip(toolTip);
    }

    private void ToolTip_Closed(object sender, System.Windows.RoutedEventArgs e)
    {
        _isTooltipOpen = false;
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

    private void OpenDownloadsFolder_Click(object sender, System.Windows.RoutedEventArgs e) => OpenPathInShell(_downloadsPath);

    private void OpenActivityLog_Click(object sender, System.Windows.RoutedEventArgs e) =>
        OpenPathInShell(ActivityLog.EnsureCurrentFile());

    private void ToggleMute_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        SaveCurrentSettings();
        UpdateMuteIcon();
    }

    private void HideToTray_Click(object sender, System.Windows.RoutedEventArgs e) => HideToTray();

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
            || FindNamedVisualChild<System.Windows.Controls.ScrollViewer>(rowBorder, "FilenameViewport") is not System.Windows.Controls.ScrollViewer viewport)
        {
            return;
        }

        if (textBlock.RenderTransform is not System.Windows.Media.TranslateTransform transform
            || transform.IsFrozen)
        {
            transform = new System.Windows.Media.TranslateTransform();
            textBlock.RenderTransform = transform;
        }

        textBlock.Width = double.NaN;
        textBlock.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var fullWidth = textBlock.DesiredSize.Width;
        var availableWidth = viewport.ActualWidth;
        var overflow = fullWidth - availableWidth;
        if (overflow <= 0.25)
        {
            return;
        }

        textBlock.Width = fullWidth;

        var travelSeconds = overflow / 72;
        var animation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            BeginTime = System.TimeSpan.Zero
        };
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.Zero)));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.2))));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-overflow,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0.2 + travelSeconds))));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-overflow,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(2.2 + travelSeconds))));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(2.2 + (travelSeconds * 2)))));
        animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(4.2 + (travelSeconds * 2)))));
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
            OpenPathInShell(entry.FullPath);
            e.Handled = true;
        }
    }

    private void CopyFilename_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && item.Tag is DownloadEntry entry)
        {
            System.Windows.Clipboard.SetText(entry.FileName);
        }
    }

    private void CopyFullPath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && item.Tag is DownloadEntry entry)
        {
            System.Windows.Clipboard.SetText(entry.FullPath);
        }
    }

    private void DeleteFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.MenuItem menuItem
            || menuItem.Parent is not System.Windows.Controls.ContextMenu contextMenu
            || contextMenu.PlacementTarget is not System.Windows.Controls.Border rowBorder
            || rowBorder.Tag is not DownloadEntry entry
            || !System.IO.File.Exists(entry.FullPath))
        {
            return;
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

            entry.TouchedAt = System.DateTime.Now;
            entry.DeletionLogged = true;
            ActivityLog.WriteDeletion(
                _deleteToRecycleBin ? "app-recycle-bin" : "app-direct",
                entry.FileName,
                entry.FileSizeBytes);
        }
        catch
        {
            entry.DeleteRequested = false;
        }
    }

    public void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;
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
        if (_isTooltipOpen)
        {
            return;
        }

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
    }

    public void DisposeTrayIcon()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _trayPulseTimer.Stop();
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _trayActiveIcon?.Dispose();
        _trayActiveIcon = null;
        _playSoundMenuItem = null;
        _deleteToRecycleBinMenuItem = null;
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
        var workArea = System.Windows.SystemParameters.WorkArea;
        Left = workArea.Right - Width;
        Top = workArea.Top + (workArea.Height - Height) / 2;
    }

    private void SetupTrayIcon()
    {
        _trayIcon = CreateTrayIcon(isActive: false);
        _trayActiveIcon = CreateTrayIcon(isActive: true);
        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            _trayIcon.Handle,
            System.Windows.Int32Rect.Empty,
            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        Icon.Freeze();

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Voltura Download Watcher",
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

        var show = new System.Windows.Forms.ToolStripMenuItem($"Show Voltura Download Watcher  v{GetDisplayVersion()}")
        {
            Font = new System.Drawing.Font(menuFont, System.Drawing.FontStyle.Bold),
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        show.Click += (_, _) => Dispatcher.Invoke(ShowFromTray);

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
            SaveCurrentSettings();
            UpdateMuteIcon();
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
            SaveCurrentSettings();
        };

        var openLog = new System.Windows.Forms.ToolStripMenuItem("Open log")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        openLog.Click += (_, _) => Dispatcher.Invoke(() => OpenPathInShell(ActivityLog.EnsureCurrentFile()));

        var exit = new System.Windows.Forms.ToolStripMenuItem("Exit")
        {
            ForeColor = menu.ForeColor,
            Padding = new System.Windows.Forms.Padding(8, 5, 10, 5)
        };
        exit.Click += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                DisposeTrayIcon();
                System.Windows.Application.Current.Shutdown();
            });
        };

        menu.Items.Add(show);
        menu.Items.Add(startWithWindows);
        menu.Items.Add(_playSoundMenuItem);
        menu.Items.Add(_deleteToRecycleBinMenuItem);
        menu.Items.Add(openLog);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
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

    private static System.Drawing.Icon CreateTrayIcon(bool isActive)
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(System.Drawing.Color.Transparent);

            using var outer = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(isActive ? 18 : 7, 45, 126, 68));
            using var ring = new System.Drawing.Pen(
                System.Drawing.Color.FromArgb(isActive ? 205 : 125, isActive ? 70 : 55, isActive ? 220 : 145, isActive ? 110 : 75),
                2.1f);
            using var glow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(isActive ? 48 : 18, 59, 158, 82));
            using var arrow = new System.Drawing.SolidBrush(
                System.Drawing.Color.FromArgb(isActive ? 235 : 165, isActive ? 55 : 35, isActive ? 235 : 150, isActive ? 100 : 65));
            using var arrowDark = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(180, 7, 30, 16));

            graphics.FillEllipse(outer, 0, 0, 32, 32);
            graphics.FillEllipse(glow, 1, 1, 30, 30);
            graphics.DrawEllipse(ring, 1, 1, 30, 30);

            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddPolygon(new[]
            {
                new System.Drawing.PointF(16, 23),
                new System.Drawing.PointF(25, 14),
                new System.Drawing.PointF(21, 14),
                new System.Drawing.PointF(21, 5),
                new System.Drawing.PointF(11, 5),
                new System.Drawing.PointF(11, 14),
                new System.Drawing.PointF(7, 14)
            });

            var tray = new System.Drawing.Drawing2D.GraphicsPath();
            tray.AddPolygon(new[]
            {
                new System.Drawing.PointF(3, 23),
                new System.Drawing.PointF(29, 23),
                new System.Drawing.PointF(27, 28),
                new System.Drawing.PointF(5, 28)
            });

            var scan = new System.Drawing.Drawing2D.GraphicsPath();
            scan.AddPolygon(new[]
            {
                new System.Drawing.PointF(4, 22),
                new System.Drawing.PointF(28, 22),
                new System.Drawing.PointF(26, 24),
                new System.Drawing.PointF(6, 24)
            });

            graphics.FillPath(arrowDark, tray);
            using var scanFill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(isActive ? 90 : 42, 38, 166, 73));
            using var arrowOutline = new System.Drawing.Pen(System.Drawing.Color.FromArgb(isActive ? 145 : 78, 92, 172, 112), 1.05f);
            using var trayOutline = new System.Drawing.Pen(System.Drawing.Color.FromArgb(isActive ? 165 : 92, 59, 158, 82), 1.2f);
            using var scanOutline = new System.Drawing.Pen(System.Drawing.Color.FromArgb(isActive ? 120 : 60, 59, 158, 82), 1f);
            graphics.FillPath(scanFill, scan);
            graphics.FillPath(arrow, path);
            graphics.DrawPath(arrowOutline, path);
            graphics.DrawPath(trayOutline, tray);
            graphics.DrawPath(scanOutline, scan);
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

    private void StartWatcher()
    {
        _watcher = new System.IO.FileSystemWatcher(_downloadsPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.CreationTime | System.IO.NotifyFilters.LastWrite,
            Filter = "*",
            EnableRaisingEvents = false,
            InternalBufferSize = 64 * 1024
        };

        _watcher.Created += OnCreated;
        _watcher.Renamed += OnRenamed;
        _watcher.Deleted += OnDeleted;
        _watcher.Error += OnWatcherError;
        _watcher.EnableRaisingEvents = true;
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

    private void OnCreated(object sender, System.IO.FileSystemEventArgs e)
    {
        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.Name ?? string.Empty))
        {
            SetBrowserDownloadActive(e.FullPath, isActive: true);
            return;
        }

        AddDownload(e.FullPath);
    }

    private void OnRenamed(object sender, System.IO.RenamedEventArgs e)
    {
        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.OldName ?? string.Empty))
        {
            SetBrowserDownloadActive(e.OldFullPath, isActive: false);
        }

        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.Name ?? string.Empty))
        {
            SetBrowserDownloadActive(e.FullPath, isActive: true);
            return;
        }

        AddDownload(e.FullPath);
    }

    private void OnDeleted(object sender, System.IO.FileSystemEventArgs e)
    {
        if (DownloadPolicy.IsBrowserDownloadInProgressName(e.Name ?? string.Empty))
        {
            SetBrowserDownloadActive(e.FullPath, isActive: false);
            return;
        }

        Dispatcher.BeginInvoke(() => LogExternalDeletion(e.FullPath));
    }

    private void SetBrowserDownloadActive(string fullPath, bool isActive)
    {
        Dispatcher.BeginInvoke(() =>
        {
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

    private void OnWatcherError(object sender, System.IO.ErrorEventArgs e)
    {
        if (_watcherRecoveryQueued)
        {
            return;
        }

        _watcherRecoveryQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _watcher?.Dispose();
                _watcher = null;
                StartWatcher();
                LoadInitialDownloads();

                while (_downloads.Count > MaxItems)
                {
                    _downloads.RemoveAt(_downloads.Count - 1);
                }

                RefreshDownloadsView();
            }
            finally
            {
                _watcherRecoveryQueued = false;
            }
        });
    }

    private void AddDownload(string fullPath)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var fileName = System.IO.Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName) || !DownloadPolicy.IsValidDownloadName(fileName))
            {
                return;
            }

            var existing = _downloads.FirstOrDefault(x => string.Equals(x.FullPath, fullPath, System.StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var existsNow = System.IO.File.Exists(fullPath);
                existing.ExistsNow = existsNow;
                existing.TouchedAt = System.DateTime.Now;
                existing.FileSizeBytes = GetFileSize(fullPath, existing.FileSizeBytes);
                existing.IsFresh = true;
                existing.DeleteRequested = false;
                existing.DeletionLogged = !existsNow;
                existing.IsRemovalRecent = !existsNow;
                MoveToTop(existing);
                ActivityLog.WriteDownload(fileName, existing.FileSizeBytes);
                if (!existsNow)
                {
                    ActivityLog.WriteDeletion("external", fileName, existing.FileSizeBytes);
                }
                RefreshDownloadsView();
                return;
            }

            foreach (var entry in _downloads)
            {
                entry.IsFresh = false;
                entry.IsNewest = false;
            }

            var now = System.DateTime.Now;
            var fileSizeBytes = GetFileSize(fullPath, 0);
            var fileExistsNow = System.IO.File.Exists(fullPath);
            _downloads.Insert(0, new DownloadEntry
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
            });

            ActivityLog.WriteDownload(fileName, fileSizeBytes);
            if (!fileExistsNow)
            {
                ActivityLog.WriteDeletion("external", fileName, fileSizeBytes);
            }

            PlaySpark();

            while (_downloads.Count > MaxItems)
            {
                _downloads.RemoveAt(_downloads.Count - 1);
            }

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
        var expired = new System.Collections.Generic.List<DownloadEntry>();
        var viewNeedsRefresh = false;
        foreach (var entry in _downloads)
        {
            entry.IsFresh = (now - entry.TouchedAt).TotalSeconds < 8;
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

            entry.ExistsNow = existsNow;
            if (!existsNow)
            {
                var removedForSeconds = (now - entry.TouchedAt).TotalSeconds;
                entry.IsRemovalRecent = DownloadPolicy.IsRemovalAnimationActive(entry.DeleteRequested, removedForSeconds);
                var removalLifetime = DownloadPolicy.GetRemovalLifetimeSeconds(entry.DeleteRequested);
                if (removedForSeconds >= removalLifetime)
                {
                    expired.Add(entry);
                }
            }
        }

        foreach (var entry in expired)
        {
            _downloads.Remove(entry);
        }

        if (viewNeedsRefresh || expired.Count > 0)
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

    private void LogExternalDeletion(string fullPath)
    {
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

    private void OpenPathInShell(string path)
    {
        try
        {
            if (RunningExecutableActivator.TryActivate(path))
            {
                return;
            }

            var info = ExecutableLaunchPolicy.CreateStartInfo(path);
            System.Diagnostics.Process.Start(info);
        }
        catch
        {
        }
    }

    private void PlaySpark()
    {
        if (_isMuted || _sparkPlayer is null)
        {
            return;
        }

        _sparkPlayer.Stop();
        _sparkPlayer.Play();
    }

    private static byte[] CreateSparkWaveBytes()
    {
        const int sampleRate = 22050;
        const double durationSeconds = 0.12;
        const int channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = (int)(sampleRate * durationSeconds);
        var dataSize = sampleCount * channels * (bitsPerSample / 8);
        var bytes = new byte[44 + dataSize];
        using var ms = new System.IO.MemoryStream(bytes);
        using var writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.ASCII, true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRate;
            var env = System.Math.Exp(-18 * t);
            var sample = System.Math.Sin(2 * System.Math.PI * 900 * t) * env * 0.35 +
                         System.Math.Sin(2 * System.Math.PI * 1320 * t) * System.Math.Exp(-28 * t) * 0.22;
            writer.Write((short)(sample * short.MaxValue));
        }

        return bytes;
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

    private void SaveCurrentSettings() => SaveSettings(new AppSettings
    {
        IsMuted = _isMuted,
        DeleteToRecycleBin = _deleteToRecycleBin
    });

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

    public enum FilterMode
    {
        All,
        Apps,
        Docs,
        Archives,
        Removed
    }
}
