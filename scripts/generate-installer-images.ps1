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
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $graphics.Clear([System.Drawing.Color]::FromArgb(4, 13, 8))

            $minorPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(19, 64, 38), 1)
            $majorPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(29, 91, 51), 1)
            $accentPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(96, 65, 203, 102), 2)
            $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(63, 203, 101))
            $mutedBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(83, 143, 98))
            try {
                for ($x = 0; $x -lt $Width; $x += 10) {
                    $graphics.DrawLine($(if (($x % 40) -eq 0) { $majorPen } else { $minorPen }), $x, 0, $x, $Height)
                }
                for ($y = 0; $y -lt $Height; $y += 10) {
                    $graphics.DrawLine($(if (($y % 40) -eq 0) { $majorPen } else { $minorPen }), 0, $y, $Width, $y)
                }

                if ($Kind -eq "Header") {
                    $graphics.DrawLine($accentPen, 8, $Height - 8, $Width - 8, $Height - 8)
                    $graphics.DrawImage($master, $Width - 45, 6, 40, 40)
                    $font = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 10, [System.Drawing.FontStyle]::Bold)
                    try {
                        $graphics.DrawString("DOWNLOAD WATCHER", $font, $accentBrush, 9, 11)
                    }
                    finally {
                        $font.Dispose()
                    }
                }
                else {
                    $graphics.DrawImage($master, 32, 28, 100, 100)
                    $graphics.DrawLine($accentPen, 23, 145, $Width - 23, 145)

                    $titleFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 14, [System.Drawing.FontStyle]::Bold)
                    $labelFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 8, [System.Drawing.FontStyle]::Regular)
                    $format = [System.Drawing.StringFormat]::new()
                    try {
                        $format.Alignment = [System.Drawing.StringAlignment]::Center
                        $graphics.DrawString("VOLTURA", $titleFont, $accentBrush, [System.Drawing.RectangleF]::new(0, 169, $Width, 24), $format)
                        $graphics.DrawString("DOWNLOAD WATCHER", $labelFont, $mutedBrush, [System.Drawing.RectangleF]::new(0, 197, $Width, 18), $format)
                        $graphics.DrawString("DIRECTORY SIGNAL ONLINE", $labelFont, $mutedBrush, [System.Drawing.RectangleF]::new(0, $Height - 38, $Width, 18), $format)
                    }
                    finally {
                        $format.Dispose()
                        $titleFont.Dispose()
                        $labelFont.Dispose()
                    }
                }
            }
            finally {
                $minorPen.Dispose()
                $majorPen.Dispose()
                $accentPen.Dispose()
                $accentBrush.Dispose()
                $mutedBrush.Dispose()
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
