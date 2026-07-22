## v0.1.4

- Keeps the app icon visible in the notification area instead of the hidden overflow area when Windows allows it.

- Added a compact download notification panel above the notification area when the main panel is hidden, with quick file actions and configurable display time.
- Reorganized startup, sound, deletion, default-action, and download-notification options under a new Settings menu.
- Download notifications now close after a successful file action and remain available when an action is cancelled or fails.
- Tooltips now prefer to appear above controls so the pointer does not cover their text.
- Mouse wheels and two-finger touchpads now scroll the download list naturally while the pointer is anywhere over a download row.
- Removed downloads, including short-lived torrent files, now remain in history like every other download until they become the oldest item pushed out by the 40-item limit.

## v0.1.3

- Windows and Task Manager now show the properly spaced Voltura Download Watcher name.
- Task Manager, the app, and the notification area now use the same brighter neon download icon.
- Upgrades preserve an existing Start with Windows choice.

## v0.1.2

- Introduced a brighter neon download icon across the app, notification area, splash screen, and installers.
- Improved the active-download and paused notification-area indicators.
- Improved the optional download sound and added Cleanup deleted files.

## v0.1.1

- Added a persistent 40-item history that remembers short-lived downloads such as torrent files removed by another application.
- Added session-only pause and resume monitoring with distinct notification-area states.
- Added sorting, configurable single-click actions, compact file controls, and background checksums.
- Added file-type icons, browser progress indication, update checks, and improved recovery.

## v0.1.0

- First public release of the compact, borderless, always-on-top Downloads activity panel.
- Tracks valid downloads and preserves quickly removed files while ignoring browser staging files.
- Includes sorting, file actions, close-to-tray behavior, optional startup and sound, and activity logging.
