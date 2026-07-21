namespace VolturaDownloadWatcher;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "VolturaDownloadWatcher";

    internal static bool IsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(RunValueName) is string command && !string.IsNullOrWhiteSpace(command);
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (System.UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                var executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return false;
                }

                key.SetValue(RunValueName, FormatRunCommand(executablePath));
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }

            return IsEnabled() == enabled;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (System.UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static string FormatRunCommand(string executablePath) => $"\"{executablePath}\"";
}
