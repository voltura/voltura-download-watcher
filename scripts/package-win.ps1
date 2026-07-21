[CmdletBinding()]
param(
    [string]$Version,
    [string]$Runtime = "win-x64",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaDownloadWatcher\VolturaDownloadWatcher.csproj"
$projectXml = [xml][System.IO.File]::ReadAllText($projectPath)
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = [string]$projectXml.Project.PropertyGroup.Version
}

$semVerPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'
if ([string]::IsNullOrWhiteSpace($Version) -or $Version -notmatch $semVerPattern) {
    throw "Version '$Version' is not a supported semantic version."
}

$versionCore = ($Version -split '[+-]', 2)[0]
foreach ($part in @($versionCore -split '\.')) {
    if ([int64]$part -gt 65535) {
        throw "Each numeric version part must be between 0 and 65535."
    }
}
$versionQuad = "$versionCore.0"

$publishRoot = Join-Path $repoRoot "artifacts\publish"
$fullPublishDir = Join-Path $publishRoot "VolturaDownloadWatcher-$Runtime-full"
$smallPublishDir = Join-Path $publishRoot "VolturaDownloadWatcher-$Runtime-framework-dependent"
$fullInstallerPath = Join-Path $publishRoot "VolturaDownloadWatcher-Setup-$Version-$Runtime-full.exe"
$smallInstallerPath = Join-Path $publishRoot "VolturaDownloadWatcher-Setup-$Version-$Runtime.exe"
$installerScript = Join-Path $repoRoot "installer\VolturaDownloadWatcher.nsi"

[System.IO.Directory]::CreateDirectory($publishRoot) | Out-Null

& (Join-Path $PSScriptRoot "generate-icon.ps1")
& (Join-Path $PSScriptRoot "generate-installer-images.ps1")

if (-not $SkipBuild) {
    foreach ($directory in @($fullPublishDir, $smallPublishDir)) {
        if (Test-Path -LiteralPath $directory) {
            Remove-Item -LiteralPath $directory -Recurse -Force
        }
    }

    & dotnet publish $projectPath `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $fullPublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Self-contained dotnet publish failed with exit code $LASTEXITCODE."
    }

    & dotnet publish $projectPath `
        -c Release `
        -r $Runtime `
        --self-contained false `
        -p:PublishSingleFile=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $smallPublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Framework-dependent dotnet publish failed with exit code $LASTEXITCODE."
    }
}

foreach ($directory in @($fullPublishDir, $smallPublishDir)) {
    $publishedExecutable = Join-Path $directory "VolturaDownloadWatcher.exe"
    if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
        throw "Expected published executable was not found: $publishedExecutable"
    }
}

$makensis = Get-Command makensis -ErrorAction SilentlyContinue
$makensisPath = if ($null -ne $makensis) { $makensis.Source } else { $null }
if ([string]::IsNullOrWhiteSpace($makensisPath)) {
    $makensisPath = @(
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "$env:ProgramFiles\NSIS\makensis.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($makensisPath)) {
    throw "makensis was not found. Install NSIS 3.12 or later and rerun this script."
}

function Invoke-InstallerBuild {
    param(
        [Parameter(Mandatory = $true)][string]$PublishDirectory,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [switch]$FrameworkDependent
    )

    $installedBytes = (Get-ChildItem -LiteralPath $PublishDirectory -Recurse -File | Measure-Object Length -Sum).Sum
    $installedSizeKb = [int][math]::Ceiling($installedBytes / 1KB)
    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }

    $arguments = @(
        "/DAPP_VERSION=$Version",
        "/DAPP_VERSION_QUAD=$versionQuad",
        "/DAPP_ESTIMATED_SIZE_KB=$installedSizeKb",
        "/DPUBLISH_DIR=$PublishDirectory",
        "/DOUTPUT_FILE=$OutputPath"
    )
    if ($FrameworkDependent) {
        $arguments += "/DFRAMEWORK_DEPENDENT"
    }
    $arguments += $installerScript

    & $makensisPath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "makensis failed with exit code $LASTEXITCODE."
    }
    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Expected installer was not created: $OutputPath"
    }
}

Invoke-InstallerBuild -PublishDirectory $fullPublishDir -OutputPath $fullInstallerPath
Invoke-InstallerBuild -PublishDirectory $smallPublishDir -OutputPath $smallInstallerPath -FrameworkDependent

Write-Host "Created full installer: $fullInstallerPath"
Write-Host "Created small installer: $smallInstallerPath"
