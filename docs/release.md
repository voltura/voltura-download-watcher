# Release

This repository uses PowerShell and GitHub Actions; Node.js and npm are not required.

## Prepare a version

```powershell
.\scripts\prepare-release.ps1 0.2.0
dotnet test .\VolturaDownloadWatcher.Tests\VolturaDownloadWatcher.Tests.csproj -c Release
.\scripts\package-win.ps1
```

For the normal stable sequence, `.\scripts\bump-release.ps1` advances the version as an odometer: `0.1.9` becomes `0.2.0`, and `0.9.9` becomes `1.0.0`. Use `prepare-release.ps1` when choosing an explicit or prerelease version.

Review, commit, and push the version change to `main`. The release workflow derives `v<version>` from the project and creates a draft GitHub release after tests and packaging. `workflow_dispatch` can retry the prepared version.

## Installer outputs

- `VolturaDownloadWatcher-Setup-<version>-win-x64.exe` is the small framework-dependent installer. It downloads the signed Microsoft .NET 10 Windows Desktop runtime installer only when required.
- `VolturaDownloadWatcher-Setup-<version>-win-x64-full.exe` is self-contained and works offline.

Both installers are per-user, create Start Menu and Apps & Features entries, and default Start with Windows and download sound to off on clean installs.

## GitHub Actions

The Windows workflow installs .NET 10 when needed and NSIS 3.12 through Chocolatey. It rejects an existing tag, creates a new tag at the workflow commit, and opens a draft GitHub release containing both installers.

Draft notes include the same optional [Ko-fi](https://ko-fi.com/voltura) and [PayPal](https://www.paypal.me/voltura) support links used by Voltura Air. Review and expand those notes before publishing.
