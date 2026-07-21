namespace VolturaDownloadWatcher;

internal static class RunningExecutableActivator
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint SnapshotProcesses = 0x00000002;
    private const int ShowNormal = 1;
    private const int Restore = 9;
    private static readonly System.IntPtr InvalidHandleValue = new(-1);

    private delegate bool EnumWindowsCallback(System.IntPtr windowHandle, System.IntPtr parameter);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern System.IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        System.IntPtr processHandle,
        uint flags,
        System.Text.StringBuilder executableName,
        ref uint size);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseHandle(System.IntPtr handle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern System.IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool Process32First(System.IntPtr snapshot, ref ProcessEntry processEntry);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool Process32Next(System.IntPtr snapshot, ref ProcessEntry processEntry);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, System.IntPtr parameter);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(System.IntPtr windowHandle, out uint processId);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(System.IntPtr windowHandle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsIconic(System.IntPtr windowHandle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(System.IntPtr windowHandle, int command);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(System.IntPtr windowHandle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(System.IntPtr windowHandle);

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential,
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct ProcessEntry
    {
        internal uint Size;
        internal uint Usage;
        internal uint ProcessId;
        internal System.IntPtr DefaultHeapId;
        internal uint ModuleId;
        internal uint ThreadCount;
        internal uint ParentProcessId;
        internal int BasePriority;
        internal uint Flags;

        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
        internal string ExecutableFile;
    }

    internal static bool TryActivate(string executablePath)
    {
        if (!string.Equals(System.IO.Path.GetExtension(executablePath), ".exe", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var processes = System.Diagnostics.Process.GetProcesses();
        try
        {
            var parentProcessIds = GetParentProcessIds();
            var matchingProcessIds = new System.Collections.Generic.HashSet<int>();

            foreach (var process in processes)
            {
                int processId;
                try
                {
                    processId = process.Id;
                }
                catch (System.InvalidOperationException)
                {
                    continue;
                }

                var runningPath = TryGetExecutablePath(process);
                if (runningPath is not null && AreSameExecutableFamily(executablePath, runningPath))
                {
                    matchingProcessIds.Add(processId);
                }
            }

            foreach (var process in processes)
            {
                int processId;
                try
                {
                    processId = process.Id;
                }
                catch (System.InvalidOperationException)
                {
                    continue;
                }

                var belongsToMatch = matchingProcessIds.Contains(processId);
                if (!belongsToMatch)
                {
                    foreach (var matchId in matchingProcessIds)
                    {
                        if (IsDescendantOf(processId, matchId, parentProcessIds))
                        {
                            belongsToMatch = true;
                            break;
                        }
                    }
                }

                if (!belongsToMatch)
                {
                    continue;
                }

                var windowHandle = FindVisibleWindow(process);
                if (windowHandle != System.IntPtr.Zero)
                {
                    ActivateWindow(windowHandle);
                    return true;
                }
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    internal static bool IsDescendantOf(
        int processId,
        int ancestorProcessId,
        System.Collections.Generic.IReadOnlyDictionary<int, int> parentProcessIds)
    {
        var visited = new System.Collections.Generic.HashSet<int>();
        var currentProcessId = processId;
        while (parentProcessIds.TryGetValue(currentProcessId, out var parentProcessId)
            && parentProcessId > 0
            && visited.Add(currentProcessId))
        {
            if (parentProcessId == ancestorProcessId)
            {
                return true;
            }

            currentProcessId = parentProcessId;
        }

        return false;
    }

    internal static bool AreSameExecutableFamily(string requestedPath, string runningPath)
    {
        var requestedFullPath = System.IO.Path.GetFullPath(requestedPath);
        var runningFullPath = System.IO.Path.GetFullPath(runningPath);
        if (string.Equals(requestedFullPath, runningFullPath, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(
                System.IO.Path.GetDirectoryName(requestedFullPath),
                System.IO.Path.GetDirectoryName(runningFullPath),
                System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestedName = System.IO.Path.GetFileNameWithoutExtension(requestedFullPath);
        var runningName = System.IO.Path.GetFileNameWithoutExtension(runningFullPath);
        return IsArchitectureVariant(requestedName, runningName)
            || IsArchitectureVariant(runningName, requestedName);
    }

    private static bool IsArchitectureVariant(string baseName, string possibleVariant)
    {
        return string.Equals(possibleVariant, baseName + "64", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(possibleVariant, baseName + "32", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(possibleVariant, baseName + "x64", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(possibleVariant, baseName + "x86", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(possibleVariant, baseName + "-x64", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(possibleVariant, baseName + "-x86", System.StringComparison.OrdinalIgnoreCase);
    }

    private static System.Collections.Generic.Dictionary<int, int> GetParentProcessIds()
    {
        var parentProcessIds = new System.Collections.Generic.Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot == InvalidHandleValue)
        {
            return parentProcessIds;
        }

        try
        {
            var entry = new ProcessEntry
            {
                Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<ProcessEntry>(),
                ExecutableFile = string.Empty
            };
            if (!Process32First(snapshot, ref entry))
            {
                return parentProcessIds;
            }

            do
            {
                parentProcessIds[(int)entry.ProcessId] = (int)entry.ParentProcessId;
                entry.Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<ProcessEntry>();
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return parentProcessIds;
    }

    private static void ActivateWindow(System.IntPtr windowHandle)
    {
        ShowWindowAsync(windowHandle, IsIconic(windowHandle) ? Restore : ShowNormal);
        BringWindowToTop(windowHandle);
        SetForegroundWindow(windowHandle);
    }

    private static string? TryGetExecutablePath(System.Diagnostics.Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (System.InvalidOperationException)
        {
        }

        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)process.Id);
        if (processHandle == System.IntPtr.Zero)
        {
            return null;
        }

        try
        {
            const int maximumPathLength = 32768;
            var path = new System.Text.StringBuilder(maximumPathLength);
            uint pathLength = maximumPathLength;
            return QueryFullProcessImageName(processHandle, 0, path, ref pathLength)
                ? path.ToString()
                : null;
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private static System.IntPtr FindVisibleWindow(System.Diagnostics.Process process)
    {
        try
        {
            if (process.MainWindowHandle != System.IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
            {
                return process.MainWindowHandle;
            }
        }
        catch (System.InvalidOperationException)
        {
            return System.IntPtr.Zero;
        }

        var match = System.IntPtr.Zero;
        EnumWindows((windowHandle, _) =>
        {
            GetWindowThreadProcessId(windowHandle, out var processId);
            if (processId == (uint)process.Id && IsWindowVisible(windowHandle))
            {
                match = windowHandle;
                return false;
            }

            return true;
        }, System.IntPtr.Zero);
        return match;
    }
}
