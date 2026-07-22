# Voltura Download Watcher

<p align="center">
  <img src="assets/branding/voltura-download-watcher-master.png" alt="Voltura Download Watcher application icon" width="128">
</p>

A minimal .NET 10 Windows panel for live Downloads activity. It keeps short-lived downloads visible even when another app immediately removes them, shows browser download progress, opens files with the Windows shell, and stays available as a compact always-on-top panel or tray app.

Safe Recycle Bin deletion, direct-delete opt-out, optional sound/startup behavior, adaptive file sizes, and a daily activity log are built in without stock Windows UI chrome.

![Voltura Download Watcher with fictional capture data](docs/assets/voltura-download-watcher.png)

## Highlights

- Borderless, resizable, always-on-top WPF panel with a compact cyberpunk blueprint UI.
- Shows the latest 40 downloads with compact timestamps and adaptive file sizes, marks removed files without launching dead paths, and can clean missing-file rows permanently from restored history.
- Provides a compact icon flyout for persistent date, size, or filename sorting with reversible direction.
- Detects browser staging activity without listing temporary `.crdownload`, `.part`, `.partial`, or `.download` files.
- Single-click shell opening plus icon-only copy, copy-as-path, cut, safe in-folder rename, SHA-256 copy, and themed file deletion actions.
- Calculates SHA-256 automatically through a serialized background queue after each live file becomes stable. Large files never block the UI; the hash action remains disabled with a themed calculating tooltip until ready and is never enabled for deleted files.
- Recycle Bin deletion by default, with a direct-delete opt-out and a daily activity text log for downloads, calculated hashes, file actions, setting changes, and removals.
- Tray actions open Downloads or explicitly delete every top-level file in Downloads, with a themed warning that reflects Recycle Bin versus permanent deletion.
- File-operation races fail gracefully with a themed dismiss-only notice, while timestamped diagnostic details are queued to the log off the UI thread.
- Closing the taskbar window hides it to the notification area, explains that behavior once, and keeps monitoring until the tray Exit command is used.
- The neon notification-area icon pulses during browser download staging; its tooltip updates immediately for progress and briefly consolidates completed download bursts. Optional download sound uses a tested stereo cue and previews immediately when enabled.
- A quiet daily GitHub release check can be disabled under `About`; available updates add a small yellow tray/menu marker without automatic installation.
- Single-instance activation brings the existing watcher forward instead of opening another panel.

## Develop

Requires the .NET 10 SDK on Windows.

```powershell
dotnet test .\VolturaDownloadWatcher.Tests\VolturaDownloadWatcher.Tests.csproj --configuration Release
dotnet run --project .\VolturaDownloadWatcher\VolturaDownloadWatcher.csproj
```

## Branding

The repository uses PowerShell jobs rather than npm as a task runner:

```powershell
.\scripts\generate-icon.ps1
.\scripts\generate-installer-images.ps1
.\scripts\capture-app-screenshot.ps1
.\scripts\generate-branding.ps1
```

Screenshot capture launches an isolated demo mode at `0,0`, renders only fictional filenames, and composites the translucent UI onto black. It never enumerates the real Downloads folder.

## Package

NSIS is discovered from `PATH` or its standard Program Files locations. Packaging produces a small framework-dependent installer and a full self-contained installer:

```powershell
.\scripts\package-win.ps1
```

Outputs are written to `artifacts\publish`. Release details are in [docs/release.md](docs/release.md).

## Trust And Distribution

Voltura Download Watcher is freeware from Voltura AB and is open source under the [MIT License](LICENSE). It can be used without payment, registration, trial limits, or feature locks.

Release binaries are currently not code-signed. Windows can therefore show an unknown-publisher or Microsoft Defender SmartScreen warning. Download only from the [official GitHub releases](https://github.com/voltura/voltura-download-watcher/releases/latest).

## Commands

```powershell
# Build or run the app under the Debug configuration
.\scripts\build.ps1
.\scripts\build.ps1 -Configuration Debug
.\scripts\run-debug.ps1

# Test and regenerate visual assets
dotnet test .\VolturaDownloadWatcher.Tests\VolturaDownloadWatcher.Tests.csproj --configuration Release
.\scripts\generate-branding.ps1

# Build both local NSIS installers
.\scripts\package-win.ps1

# Complete local release: bump, test, build, push, upload, and publish as Latest
.\scripts\release-local.ps1

# Release an explicit stable version instead of the automatic odometer bump
.\scripts\release-local.ps1 -Version 0.2.0
```

The local release command requires a clean `main` branch, .NET 10, NSIS, Git, and an authenticated GitHub CLI. It regenerates the privacy-safe screenshot and branding, runs tests, builds both installers, commits and pushes the version, audits a draft, and publishes it as Latest. If publication is interrupted after the release commit or draft is created, rerun the same command to resume that version rather than bumping again.

End-user notes are maintained in [docs/release-notes.md](docs/release-notes.md). The paid release workflow is preserved but disabled; re-enable and dispatch it only when intentionally returning to GitHub-hosted builds:

```powershell
gh workflow enable release.yml
gh workflow run release.yml
```

The Pages workflow publishes this README as the project site at `https://voltura.github.io/voltura-download-watcher/`; no separate website source or hosting is required.

## Optional Support

- [Ko-fi](https://ko-fi.com/voltura)
- [PayPal](https://www.paypal.me/voltura)

## Statistics

[![Code size](https://img.shields.io/github/languages/code-size/voltura/voltura-download-watcher)](https://github.com/voltura/voltura-download-watcher)
[![Stars](https://img.shields.io/github/stars/voltura/voltura-download-watcher)](https://github.com/voltura/voltura-download-watcher/stargazers)
[![Forks](https://img.shields.io/github/forks/voltura/voltura-download-watcher)](https://github.com/voltura/voltura-download-watcher/forks)
[![Last commit](https://img.shields.io/github/last-commit/voltura/voltura-download-watcher?color=red)](https://github.com/voltura/voltura-download-watcher/commits)
[![Languages](https://img.shields.io/github/languages/count/voltura/voltura-download-watcher)](https://github.com/voltura/voltura-download-watcher)
[![Top language](https://img.shields.io/github/languages/top/voltura/voltura-download-watcher)](https://github.com/voltura/voltura-download-watcher)
