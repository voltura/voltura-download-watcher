[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaDownloadWatcher\VolturaDownloadWatcher.csproj"

& dotnet build $projectPath --configuration $Configuration
if ($LASTEXITCODE -ne 0)
{
    throw "Build failed with exit code $LASTEXITCODE."
}
