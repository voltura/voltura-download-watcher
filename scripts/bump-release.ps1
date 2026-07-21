[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaDownloadWatcher\VolturaDownloadWatcher.csproj"
$project = [xml][System.IO.File]::ReadAllText($projectPath)
$currentVersion = [string]$project.Project.PropertyGroup.Version
$match = [System.Text.RegularExpressions.Regex]::Match($currentVersion, '^(0|[1-9]\d*)\.(\d)\.(\d)$')
if (-not $match.Success) {
    throw "Automatic bump requires a stable version with single-digit minor and patch components; received '$currentVersion'. Use prepare-release.ps1 for an explicit version."
}

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$patch = [int]$match.Groups[3].Value
if ($patch -lt 9) {
    $patch++
}
else {
    $patch = 0
    if ($minor -lt 9) {
        $minor++
    }
    else {
        $minor = 0
        $major++
    }
}

$nextVersion = "$major.$minor.$patch"
Write-Host "Bumping Voltura Download Watcher from $currentVersion to $nextVersion."
& (Join-Path $PSScriptRoot "prepare-release.ps1") $nextVersion
