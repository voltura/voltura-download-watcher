# AGENTS.md

## Project Notes

- Target framework: `net10.0-windows`
- UI stack: WPF
- The app watches the signed-in user's Downloads folder using the Windows known-folder API.
- The app starts hidden in the system tray and can be restored from the tray icon or tray menu.
- The UI should stay minimal, cyberpunk-green, and not drift into stock WinForms styling.
- The main window is borderless, resizable, transparent, always-on-top, and starts flush against the right edge of the working area.
- The outer shell may be translucent, but the file-list surface must remain readable.
- The app uses a WPF window plus a WinForms `NotifyIcon`; fully qualify framework types where those APIs overlap.

## Download Lifecycle

- Start `FileSystemWatcher` before the startup directory snapshot so short-lived files cannot fall into a blind window.
- Attach watcher event handlers before setting `EnableRaisingEvents = true`.
- Handle `FileSystemWatcher.Error` by recreating the watcher and reconciling the directory snapshot.
- Apply `IsValidDownloadName` before limiting startup results to 20 files.
- Ignore browser/application staging names such as `.crdownload`, `.tmp`, `.part`, `.partial`, `.download`, and `Unconfirmed *`.
- Browser staging files never enter the download list. Chrome, Edge, Brave, Opera, and Vivaldi `.crdownload` files plus Firefox-style `.part`, `.partial`, and `.download` files make the header status dot and tray icon pulse until the browser renames or deletes every active temporary file.
- Real short-lived files, including `.torrent`, must still be recorded even if another app removes them immediately.
- Rows show the timestamp followed by an adaptive file size (`B`, `KB`, `MB`, `GB`, `TB`, or `PB`). Refresh sizes while files exist and retain the last known size after deletion.
- File deletion defaults to the Windows Recycle Bin and is controlled by the persisted tray setting `Delete to Recycle Bin`. Unchecked means permanent direct deletion.
- `%APPDATA%\VolturaDownloadWatcher\activity.txt` is reset when a new local day begins. It records downloads and deletion origins as `app-recycle-bin`, `app-direct`, or `external`, stores filenames rather than full paths, and must not double-log watcher events caused by the app.
- A recreated path must clear stale deletion state (`DeleteRequested` and `IsRemovalRecent`).
- External deletions remain visible for about 60 seconds; UI-requested deletions disappear immediately after their roughly 1.5-second neon pulse.
- Refresh the collection view only when membership/filter state changes. Refreshing every timer tick causes hover and cursor flicker.

## UI Interaction

- The whole live-file row opens exactly once on a single left-button release; deleted rows must not open.
- Do not put a clickable `Button` inside a clickable download row. Competing routed handlers make launches duplicate or appear inconsistent.
- Hover and removal glyphs overlay the right edge of the row; never reserve a permanent grid column for them because it needlessly shortens every filename viewport.
- Keep external `ListViewItem` vertical padding at zero. Gaps outside `RowShell` make row-hover glyphs flicker while the pointer crosses between entries.
- Right-click deletion is a separate handled event and must never bubble into the row open action.
- Keep scrollbar template part `PART_Track`; without it, thumb dragging does not work.
- Exempt `ScrollBar`, `Thumb`, `Track`, `ScrollViewer`, buttons, and list items from the panel drag handler.
- Filename marquee behavior uses the complete filename, starts when hovering anywhere on the row, pauses for two seconds at both ends, and only runs when text exceeds the actual filename viewport.
- Marquee travel duration must be `overflow pixels / pixels per second` without a minimum-duration clamp; otherwise long and short filenames visibly move at different speeds.
- Avoid refreshing/recreating row visuals on a periodic timer because it resets hover animations.
- WPF may freeze template transforms; replace a frozen transform before animating it.
- Tooltip popups must stay above the topmost main window without competing topmost timers.
- Keep stock Windows hover chrome out of buttons, tooltips, scrollbars, and context menus.
- The tray menu is custom-rendered and owns the persisted `Start with Windows` and `Play sound on download` toggles. Clean installs default both settings to off.
- The tray menu also exposes `Delete to Recycle Bin` and `Open log`; the header log glyph opens the same `.txt` file through the Windows default application.

## Native Interop

- `SetWindowPos` is used to enforce `HWND_TOPMOST`; do not toggle `Topmost` back to `false` during startup or tray restore.
- The app is single-instance per Windows session. A named mutex rejects duplicate instances, and a named auto-reset event tells the existing instance to restore and come forward.
- Keep the activation event listener off the UI thread and dispatch `ShowFromTray` back onto the WPF dispatcher.
- `SHGetKnownFolderPath` resolves the signed-in user's Downloads folder. Always free its result with `CoTaskMemFree`.
- Shell opening uses `ProcessStartInfo.UseShellExecute = true` so Windows selects the associated application.
- Read an `.exe` PE subsystem before launch. GUI executables launch normally; console executables run through persistent `cmd.exe /k` so output and missing-argument errors remain visible.
- Do not infer CLI arguments. The watcher can preserve console output but cannot know a tool's required parameters.
- Before launching an `.exe`, look for an existing visible process from the same executable path or a same-folder architecture variant such as `tool.exe`/`tool64.exe`. Restore and foreground that window instead of launching another instance.
- Some launchers keep a windowless process at the downloaded path and spawn the real UI executable elsewhere (Process Explorer extracts `procexp64.exe` into `%TEMP%`). Follow the exact match's process descendants and activate a visible descendant window before launching again.
- Do not suppress a CLI/background executable merely because its process exists; require a visible top-level window before treating an executable as an already-running app.

## Code Style

- Prefer fully qualified namespaces for framework types when adding new code, especially when WPF and WinForms/Drawing overlap.
- Avoid introducing ambiguous `using` aliases unless absolutely necessary.
- Keep edits small and localized when fixing UI or interop issues.

## Behavior To Preserve

- Capture file create and rename events immediately so short-lived downloads still appear.
- Keep the list capped at the 20 most recent items.
- Fresh items should appear brighter than older ones.
- Preserve the tray-first startup behavior unless the user asks otherwise.

## Branding And Packaging

- Run `scripts/generate-branding.ps1` to regenerate the application icon, NSIS header/welcome artwork, and the curated app screenshot.
- Run `scripts/generate-branding.ps1 -SkipScreenshot` in non-interactive validation. The screenshot job needs an interactive desktop.
- Screenshot mode is activated only through the child-process environment variable `VOLTURA_DOWNLOAD_WATCHER_SCREENSHOT=1`. It must disable the real watcher and display fictional filenames so private Downloads history is never captured.
- Screenshot mode places the window at `0,0` and uses a black WPF base behind the normally translucent panel. Do not use the live desktop as the screenshot background.
- NSIS artwork must remain opaque 24-bit BMP at exactly `150x57` for the header and `164x314` for welcome/finish pages.
- `scripts/package-win.ps1` regenerates icon and installer artwork, then creates both the small framework-dependent installer and full self-contained installer under `artifacts/publish`.
- The small installer downloads the signed .NET 10 Windows Desktop runtime from Microsoft only when missing. The full installer is offline/self-contained.
- Version is sourced from `<Version>` in `VolturaDownloadWatcher.csproj`. `scripts/bump-release.ps1` uses the Voltura Air odometer convention and the GitHub release workflow creates a draft release for manual release-note editing and publication.
- GitHub Pages is built from the root `README.md` by `.github/workflows/pages.yml`. Do not create a second copy of site content; the workflow creates a temporary Jekyll `index.md` during CI.
- `assets/branding/voltura-download-watcher-master.png` is the replaceable source for the README mark, application/form/installer ICO, and NSIS bitmap artwork. Keep it at least 256x256 with transparency and run `scripts/generate-branding.ps1` after replacement.

## Verification

- Build the WPF project after changes.
- If a change affects UI layout or animation, recheck the XAML for missing handlers or resource names.
- After rebuilding, launch the executable and verify it remains alive and responsive for several seconds.
- Check `%APPDATA%\VolturaDownloadWatcher\startup.log` after startup or any apparent crash.
- Manually verify mouse-wheel scrolling, scrollbar thumb dragging, window edge resizing, row open, context-menu delete, tray restore, tooltip layering, and long-filename marquee behavior.
- Test with an empty Downloads folder, fewer than 20 files, more than 20 files, temporary browser files, a quickly removed `.torrent`, a deleted-and-recreated path, and watcher recovery.
- Inspect generated PNG/BMP assets visually after branding changes. CI regenerates branding and validates ICO frames plus NSIS bitmap dimensions/pixel format. `System.Drawing` PNG/BMP encoding differs between Windows PowerShell 5.1 and PowerShell 7, so CI must not binary-diff generated image bytes. CI intentionally does not capture the interactive desktop screenshot.
