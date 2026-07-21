[CmdletBinding()]
param(
    [string]$OutputPath,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class ScreenshotNativeMethods
{
    public delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);

    public static IntPtr FindVisibleTopLevelWindow(uint processId)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((window, parameter) =>
        {
            uint ownerProcessId;
            GetWindowThreadProcessId(window, out ownerProcessId);
            if (ownerProcessId == processId && IsWindowVisible(window))
            {
                result = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }
}
"@

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "docs\assets\voltura-download-watcher.png"
}
else {
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
}

$projectPath = Join-Path $repoRoot "VolturaDownloadWatcher\VolturaDownloadWatcher.csproj"
$executablePath = Join-Path $repoRoot "VolturaDownloadWatcher\bin\Release\net10.0-windows\VolturaDownloadWatcher.exe"
if (-not $SkipBuild) {
    & dotnet build $projectPath --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE."
    }
}
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Application executable was not found: $executablePath"
}

$existing = @(Get-Process -Name VolturaDownloadWatcher -ErrorAction SilentlyContinue)
if ($existing.Count -gt 0) {
    throw "Close Voltura Download Watcher before capturing its deterministic black-backed screenshot."
}

$process = $null
try {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($executablePath)
    $startInfo.UseShellExecute = $false
    $startInfo.Environment["VOLTURA_DOWNLOAD_WATCHER_SCREENSHOT"] = "1"
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $deadline = [System.DateTime]::UtcNow.AddSeconds(12)
    $windowHandle = [System.IntPtr]::Zero
    while ([System.DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        if ($process.HasExited) {
            throw "The watcher exited before its screenshot could be captured."
        }
        $windowHandle = [ScreenshotNativeMethods]::FindVisibleTopLevelWindow([uint32]$process.Id)
        if ($windowHandle -ne [System.IntPtr]::Zero) {
            break
        }
    }
    if ($windowHandle -eq [System.IntPtr]::Zero) {
        throw "Timed out waiting for the watcher window."
    }

    $rect = [ScreenshotNativeMethods+RECT]::new()
    if (-not [ScreenshotNativeMethods]::GetWindowRect($windowHandle, [ref]$rect)) {
        throw "Could not read the watcher window bounds."
    }

    $padding = 36
    $dpi = [ScreenshotNativeMethods]::GetDpiForWindow($windowHandle)
    $dpiScale = $(if ($dpi -gt 0) { $dpi / 96.0 } else { 1.0 })
    $width = [int][System.Math]::Ceiling(($rect.Right - $rect.Left) * $dpiScale)
    $height = [int][System.Math]::Ceiling(($rect.Bottom - $rect.Top) * $dpiScale)
    $captureWidth = $width + (2 * $padding)
    $captureHeight = $height + (2 * $padding)
    [ScreenshotNativeMethods]::SetForegroundWindow($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 650

    $bitmap = [System.Drawing.Bitmap]::new($captureWidth, $captureHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Black)
            $windowBitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $windowGraphics = [System.Drawing.Graphics]::FromImage($windowBitmap)
                try {
                    $windowGraphics.Clear([System.Drawing.Color]::Black)
                    $deviceContext = $windowGraphics.GetHdc()
                    try {
                        if (-not [ScreenshotNativeMethods]::PrintWindow($windowHandle, $deviceContext, 2)) {
                            throw "Windows could not render the watcher window for capture."
                        }
                    }
                    finally {
                        $windowGraphics.ReleaseHdc($deviceContext)
                    }
                }
                finally {
                    $windowGraphics.Dispose()
                }

                $graphics.DrawImageUnscaled($windowBitmap, $padding, $padding)
            }
            finally {
                $windowBitmap.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        [System.IO.Directory]::CreateDirectory((Split-Path $OutputPath -Parent)) | Out-Null
        $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(2000)) {
            $process.Kill()
            $process.WaitForExit()
        }
    }
    if ($null -ne $process) {
        $process.Dispose()
    }
}

Write-Host "Created black-backed application screenshot: $OutputPath"
