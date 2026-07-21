[CmdletBinding()]
param(
    [switch]$SkipScreenshot,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "generate-icon.ps1")
if (-not $SkipScreenshot) {
    & (Join-Path $PSScriptRoot "capture-app-screenshot.ps1") -SkipBuild:$SkipBuild
}
& (Join-Path $PSScriptRoot "generate-installer-images.ps1")

Write-Host "Voltura Download Watcher branding is up to date."
