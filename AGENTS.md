# AGENTS.md

## Project Notes

- Target framework: `net10.0-windows`
- UI stack: WPF
- The app watches the signed-in user's Downloads folder using the Windows known-folder API.
- The app starts hidden in the system tray and can be restored from the tray icon or tray menu.
- The UI should stay minimal, cyberpunk-green, and not drift into stock WinForms styling.
- The main window is borderless, resizable, transparent, always-on-top, omitted from the Windows taskbar, and starts vertically centered and physically flush against the right edge of the Windows primary monitor's working area. Startup placement must be DPI-aware in multi-monitor setups, but users remain free to move the window afterward. The notification-area icon is its only persistent shell presence; never briefly set `ShowInTaskbar` to true during startup or tray restore.
- The primary instance shows the packaged `docs/assets/voltura-download-watcher-social-preview.png` in a borderless centered splash for two seconds before showing the main window. Screenshot mode and duplicate-instance activation must skip the splash.
- The outer shell may be translucent, but the file-list surface must remain readable.
- The app uses a WPF window plus a WinForms `NotifyIcon`; fully qualify framework types where those APIs overlap.

## Download Lifecycle

- Start `FileSystemWatcher` before the startup directory snapshot so short-lived files cannot fall into a blind window.
- Attach watcher event handlers before setting `EnableRaisingEvents = true`.
- Handle `FileSystemWatcher.Error` by recreating the watcher and reconciling the directory snapshot.
- Apply `IsValidDownloadName` before limiting startup results to 40 files.
- Ignore browser/application staging names such as `.crdownload`, `.tmp`, `.part`, `.partial`, `.download`, and `Unconfirmed *`.
- Browser staging files never enter the download list. Chrome, Edge, Brave, Opera, and Vivaldi `.crdownload` files plus Firefox-style `.part`, `.partial`, and `.download` files make the header status dot and tray icon pulse until the browser renames or deletes every active temporary file.
- Real short-lived files, including `.torrent`, must still be recorded even if another app removes them immediately.
- Rows show the timestamp followed by an adaptive file size (`B`, `KB`, `MB`, `GB`, `TB`, or `PB`). Refresh sizes while files exist and retain the last known size after deletion.
- Every live startup/download/recreated/renamed file enters the single-reader SHA-256 queue. Wait for stable size and last-write metadata before hashing, verify stability afterward, and retry changed/temporarily locked files. Never hash deleted rows or screenshot fixtures, never run multiple file hashes concurrently, and cancel the worker during disposal.
- Successful SHA-256 values are stored on `DownloadEntry` and in the activity log for history restoration, but the icon-only copy action is enabled only while the file still exists. Pending/calculating rows show a themed `Calculating SHA-256...` tooltip; deletion or an unavailable file disables the action without a dialog.
- File deletion defaults to the Windows Recycle Bin and is controlled by the persisted tray setting `Delete to Recycle Bin`. Unchecked means permanent direct deletion.
- `%APPDATA%\VolturaDownloadWatcher\activity.txt` is reset when a new local day begins. It records downloads and deletion origins as `app-recycle-bin`, `app-direct`, or `external`, stores filenames rather than full paths, and must not double-log watcher events caused by the app.
- A recreated path must clear stale deletion state (`DeleteRequested` and `IsRemovalRecent`).
- Deleted entries remain in the 40-item history after their four-second neon transition. When item 41 arrives, evict the oldest deleted entry first; only evict the oldest live entry when all 40 entries are live.
- Restore current-day downloads from the timestamped activity log at startup, including short-lived files already removed by another app. Preserve the original download timestamp for sorting and eviction.
- Monitoring pause is session-only and must always reset to active on restart. While paused, disable watcher events, ignore already-queued watcher callbacks, clear browser-progress state, dim the header dot, and use the distinct paused notification-area icon; resuming must not backfill files created during the pause.
- Refresh the collection view only when membership/filter state changes. Refreshing every timer tick causes hover and cursor flicker.

## UI Interaction

- The whole live-file row opens exactly once on a single left-button release; deleted rows must not open.
- The persisted tray submenu `Default action` controls live-row single-left-click behavior: open file, show in Explorer, copy as path, copy file, or cut file. `Open file` is the backward-compatible default, and invalid persisted enum values must normalize to it.
- WinForms nested `ToolStripDropDown` instances do not reliably inherit the parent tray theme. Explicitly assign the cyberpunk renderer, colors, font, padding, check margin, and hidden image margin to every submenu.
- The downloads list uses pixel scrolling plus a short render-timer easing step for wheel/touchpad input. Suppress row actions for 400ms after list scrolling to avoid accidental K400-style touchpad clicks, but keep scrollbar dragging immediate and native.
- Do not put a clickable `Button` inside a clickable download row. Competing routed handlers make launches duplicate or appear inconsistent.
- Hover and removal glyphs overlay the right edge of the row; never reserve a permanent grid column for them because it needlessly shortens every filename viewport.
- Each row has a fixed 11px monochrome cyberpunk file-family glyph in a narrow left rail. Only the filename inside `FilenameViewport` may marquee; the type glyph must never move. Keep extension grouping in `DownloadFileTypeIcon` and retain a generic fallback.
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
- Every accepted download arrival must pass through `NotifyDownloadArrival`, including a path already present in the startup/history rows, so enabled spark sound and tray completion status are never skipped.
- The accepted download cue is `VolturaDownloadWatcher/Assets/electric-spark.wav`: stereo 44.1 kHz PCM with enough duration for external audio devices to wake. Play the packaged physical file asynchronously through WinMM `PlaySoundW` with `SND_FILENAME`; do not replace it with a short generated memory WAV or `SoundPlayer`. Enabling sound must preview this exact cue.
- The tray menu also exposes `Delete to Recycle Bin` and `Open log`; the header log glyph opens the same `.txt` file through the Windows default application.
- The themed `About` tray submenu opens URLs through the Windows HTTPS handler, offers a manual GitHub release check, a persisted `Check for new version daily` toggle that defaults on, and a latest-release link. Automatic checks run at most once per 24 hours, fail silently outside the activity log, never install automatically, and show a small yellow marker on the tray icon/menu when a newer stable GitHub release exists.
- The header sort glyph toggles an icon-only WPF `Popup` with date, size, and name rows. Clicking the active row reverses direction; a newly selected mode defaults to date descending, size descending, or name ascending.
- Sort mode and direction persist in `AppSettings`. Use `ICollectionView.SortDescriptions` and live sorting for file-size changes; do not rebuild the row collection to sort because that resets hover/marquee state.
- Fresh downloads temporarily pin above the selected sort for their eight-second pulse. Every arrival has an independent sequence and deadline so bursts remain at the top and release into the selected sort first-in-first-out.
- The row context menu is an icon-only horizontal strip for copy, copy as path, cut, rename, copy SHA-256, and delete. Copy/cut use Windows `FileDrop` plus `Preferred DropEffect`; copy as path writes the absolute path as text and adds double quotes only when it contains a space; rename is limited to a validated filename in the same directory and uses the custom `RenameDialog` for overwrite confirmation.
- File-menu interactions and actual setting transitions are written to the daily activity log. Do not replace themed file prompts with stock `MessageBox` UI.
- The WPF window/taskbar icon comes from the generated multi-resolution application ICO. The notification-area icon remains a separately drawn muted icon so changing one does not accidentally shrink the other.
- Normal WPF close is close-to-tray. Keep screenshot mode and explicit tray Exit exempt, persist the one-time explanatory balloon flag, and never let ordinary `WM_CLOSE` terminate monitoring.
- Tray status is immediate and replace-only, not a balloon queue: progress pulses the active icon, one completion names the file, and bursts consolidate to a count for four seconds.
- The tray menu can delete every top-level file in Downloads, not only tracked rows. It must always show `ConfirmationDialog`, respect `DeleteToRecycleBin`, mark tracked entries as app-deleted, process files off the UI thread, and log every result.
- The bulk-delete confirmation is modal-owned by the main window but manually positioned near the tray-menu invocation point. Prefer above-left, flip when needed, and clamp the complete dialog to the cursor monitor's working area; never return it to `CenterOwner`.
- `ActivityLog` is a single-reader background channel. Capture occurrence timestamps before enqueueing; never perform log creation, rotation, cleanup, retry delays, or appends on the WPF dispatcher.
- File-operation exceptions belong in the log as type plus message. UI errors must use the friendly dismiss-only `NoticeDialog`; never show raw exception text or allow copy/cut/rename/delete races to escape an event handler.

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
- Keep the list capped at the 40 most recent items.
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
- Release comparison reads the previous `.csproj` through `git show`; strip a leading `0xFEFF` BOM before casting that content to XML.
- GitHub Pages is built from the root `README.md` by `.github/workflows/pages.yml`. Do not create a second copy of site content; the workflow creates a temporary Jekyll `index.md` during CI.
- `assets/branding/voltura-download-watcher-master.png` is the replaceable source for the README mark, application/form/installer ICO, and NSIS bitmap artwork. Keep it at least 256x256 with transparency and run `scripts/generate-branding.ps1` after replacement.
- `assets/branding/voltura-download-watcher-tray-master.png` is the canonical neon source for the ICO's 16-48px Windows frames and therefore for normal, download-in-progress, and paused notification-area states. Keep its transparent exterior and high-contrast circular plate; the detailed master remains the 256px frame and installer artwork.
- Runtime normal, download-in-progress, and paused notification-area icons must composite their state treatment over the embedded generated ICO. Do not reintroduce independently drawn legacy tray logos.
- The screenshot window intentionally has no taskbar button. Capture it by enumerating the screenshot process's visible top-level window rather than relying on `Process.MainWindowHandle`.

## Verification

- Build the WPF project after changes.
- If a change affects UI layout or animation, recheck the XAML for missing handlers or resource names.
- After rebuilding, launch the executable and verify it remains alive and responsive for several seconds.
- Check `%APPDATA%\VolturaDownloadWatcher\startup.log` after startup or any apparent crash.
- Manually verify mouse-wheel scrolling, scrollbar thumb dragging, window edge resizing, row open, context-menu delete, tray restore, tooltip layering, and long-filename marquee behavior.
- Test with an empty Downloads folder, fewer than 40 files, more than 40 files, temporary browser files, a quickly removed `.torrent`, a deleted-and-recreated path, and watcher recovery.
- Inspect generated PNG/BMP assets visually after branding changes. CI regenerates branding and validates ICO frames plus NSIS bitmap dimensions/pixel format. `System.Drawing` PNG/BMP encoding differs between Windows PowerShell 5.1 and PowerShell 7, so CI must not binary-diff generated image bytes. CI intentionally does not capture the interactive desktop screenshot.
