[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type @"
public static class VolturaSoundTestNativeMethods
{
    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    public static extern uint waveOutGetNumDevs();

    [System.Runtime.InteropServices.DllImport(
        "winmm.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode,
        SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool PlaySound(
        string sound,
        System.IntPtr module,
        uint flags);
}
"@

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputDirectory = Join-Path $repoRoot "artifacts\sound-test"
$wavePath = Join-Path $outputDirectory "voltura-electric-spark.wav"
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$sampleRate = 44100
$durationSeconds = 1.2
$sampleCount = [int]($sampleRate * $durationSeconds)
$channelCount = 2
$blockAlign = 4
$dataSize = $sampleCount * $blockAlign
$stream = [System.IO.File]::Create($wavePath)
try {
    $writer = [System.IO.BinaryWriter]::new($stream, [System.Text.Encoding]::ASCII, $true)
    try {
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF"))
        $writer.Write(36 + $dataSize)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("WAVEfmt "))
        $writer.Write(16)
        $writer.Write([int16]1)
        $writer.Write([int16]$channelCount)
        $writer.Write($sampleRate)
        $writer.Write($sampleRate * $blockAlign)
        $writer.Write([int16]$blockAlign)
        $writer.Write([int16]16)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("data"))
        $writer.Write($dataSize)

        for ($index = 0; $index -lt $sampleCount; $index++) {
            $time = $index / $sampleRate
            $wakeEnvelope = $(if ($time -lt 0.34) {
                [System.Math]::Min(1.0, $time / 0.02) * 0.08
            } else {
                0.0
            })
            $wakeTone = [System.Math]::Sin(2 * [System.Math]::PI * 240 * $time) * $wakeEnvelope

            $firstTime = $time - 0.28
            $firstEnvelope = $(if ($firstTime -ge 0) {
                [System.Math]::Min(1.0, $firstTime / 0.014) * [System.Math]::Exp(-4.1 * $firstTime)
            } else {
                0.0
            })
            $firstTone = (
                [System.Math]::Sin(2 * [System.Math]::PI * 520 * $firstTime) * 0.38 +
                [System.Math]::Sin(2 * [System.Math]::PI * 780 * $firstTime) * 0.16
            ) * $firstEnvelope

            $secondTime = $time - 0.58
            $secondEnvelope = $(if ($secondTime -ge 0) {
                [System.Math]::Min(1.0, $secondTime / 0.014) * [System.Math]::Exp(-4.6 * $secondTime)
            } else {
                0.0
            })
            $secondTone = (
                [System.Math]::Sin(2 * [System.Math]::PI * 660 * $secondTime) * 0.30 +
                [System.Math]::Sin(2 * [System.Math]::PI * 990 * $secondTime) * 0.11
            ) * $secondEnvelope

            $left = [System.Math]::Max(-0.88, [System.Math]::Min(0.88, $wakeTone + $firstTone + ($secondTone * 0.92)))
            $right = [System.Math]::Max(-0.88, [System.Math]::Min(0.88, $wakeTone + ($firstTone * 0.90) + $secondTone))
            $writer.Write([int16]($left * [int16]::MaxValue))
            $writer.Write([int16]($right * [int16]::MaxValue))
        }
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $stream.Dispose()
}

$deviceCount = [VolturaSoundTestNativeMethods]::waveOutGetNumDevs()
if ($deviceCount -eq 0) {
    throw "Windows reports no wave output devices."
}

$soundFilename = 0x00020000
$soundNodefault = 0x00000002
Write-Host "Windows wave output devices: $deviceCount"
Write-Host "Playing standalone test sound: $wavePath"
$played = [VolturaSoundTestNativeMethods]::PlaySound(
    $wavePath,
    [System.IntPtr]::Zero,
    $soundFilename -bor $soundNodefault)
if (-not $played) {
    $errorCode = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Windows PlaySound rejected the standalone WAV file. Win32 error: $errorCode"
}

Write-Host "Standalone sound playback completed successfully."
