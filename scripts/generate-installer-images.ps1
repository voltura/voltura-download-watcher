[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$masterPath = Join-Path $repoRoot "assets\branding\voltura-download-watcher-master.png"
$assetDirectory = Join-Path $repoRoot "installer\assets"
[System.IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
if (-not (Test-Path -LiteralPath $masterPath -PathType Leaf)) {
    throw "Branding master was not found: $masterPath"
}
$master = [System.Drawing.Bitmap]::new($masterPath)

function New-InstallerBitmap {
    param(
        [Parameter(Mandatory = $true)][int]$Width,
        [Parameter(Mandatory = $true)][int]$Height,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][ValidateSet("Header", "Welcome")][string]$Kind
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.Clear([System.Drawing.Color]::FromArgb(4, 13, 8))

            $minorPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(19, 64, 38), 1)
            $majorPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(29, 91, 51), 1)
            $accentPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(96, 65, 203, 102), 2)
            try {
                for ($x = 0; $x -lt $Width; $x += 10) {
                    $graphics.DrawLine($(if (($x % 40) -eq 0) { $majorPen } else { $minorPen }), $x, 0, $x, $Height)
                }
                for ($y = 0; $y -lt $Height; $y += 10) {
                    $graphics.DrawLine($(if (($y % 40) -eq 0) { $majorPen } else { $minorPen }), 0, $y, $Width, $y)
                }

                if ($Kind -eq "Header") {
                    $graphics.DrawLine($accentPen, 8, $Height - 8, $Width - 8, $Height - 8)
                    $graphics.DrawImage($master, $Width - 49, 4, 44, 44)
                }
                else {
                    $graphics.DrawImage($master, 32, 28, 100, 100)
                    $graphics.DrawLine($accentPen, 23, 145, $Width - 23, 145)
                    $graphics.DrawLine($accentPen, 42, 171, $Width - 42, 171)
                    $graphics.DrawLine($accentPen, 54, 181, $Width - 54, 181)
                }
            }
            finally {
                $minorPen.Dispose()
                $majorPen.Dispose()
                $accentPen.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
    }
    finally {
        $bitmap.Dispose()
    }
}

try {
    New-InstallerBitmap -Width 150 -Height 57 -OutputPath (Join-Path $assetDirectory "installer-header.bmp") -Kind Header
    New-InstallerBitmap -Width 164 -Height 314 -OutputPath (Join-Path $assetDirectory "installer-welcome.bmp") -Kind Welcome
}
finally {
    $master.Dispose()
}

Write-Host "Created NSIS artwork in: $assetDirectory"
