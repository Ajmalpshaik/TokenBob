# Changelog

All notable changes to TokenBob are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Limit reset blast: when the 5-hour limit resets, the old ornament squeezes, glows and bursts into
  its own pieces with a shock ring, firecrackers, sparks and confetti; a fresh ornament springs in
  with the new numbers. A "Play reset animation" button in Settings shows it any time.
- Settings: live preview of the widget; theme (follow Windows / light / dark) for the widget and
  every window; number shown in the ring (5-hour or weekly); cord colour; opacity; toggles for the
  reset blast, shiver and the hover card; "Reset position" for the hanging point.
- A themed look for Settings, Install and Sign in (dark title bars included), the tray menu and
  the widget's own menu.

### Changed
- The widget window now covers everything the rope can reach, so the ornament is never cut off
  at an invisible edge; dragging feels like a rope pulled taut instead of hitting a wall.
- Lower memory: the GC is set to conserve memory, the working set is trimmed a few seconds after
  the widget stops moving, and the render loop no longer creates garbage every frame.
- The rope now ends exactly on the ornament's loop (it sat 1.5 px off); the ring and tag get a fine
  edge line, a soft sheen and a metal loop / eyelet.

### Fixed
- Losing mouse capture mid-drag (lock screen, a prompt) no longer leaves the ornament grabbed or
  frozen with the render loop running.
- A read-only or locked logs folder no longer stops usage polling.
- Quitting during a fetch no longer logs a bogus "Usage polling stopped" error.
- "resets 24h 0m" is now shown as the weekday; unknown values in settings.json fall back to defaults.
- Changing the refresh interval applies at once instead of after the wait already in progress.
- A hard pull always plays the lamp-chain click, even within the 30 s refresh cooldown.
- Releases are stamped with the tag's version instead of always reporting 0.1.0.

## [0.1.0] - 2026-10-14

### Added
- Hanging widget with rope physics: gravity, stretch, rotation and wobble; sleeps when still.
- Ornament styles: ring and hanging tag. Ropes: twisted cord and bead chain. Light and dark theme.
- Claude 5-hour and weekly limit usage with reset times, via a one-time claude.ai sign-in.
- Gestures: fling, pull to refresh, Ctrl + click for Settings, Shift + drag to move the hanging
  point and rope length (across monitors), hover details, double-click for the usage page, wind.
- Settings window, tray icon with menu and usage tooltip, start with Windows.
- Alerts: green/amber/red levels, notifications at 80% and 95%, shiver at 90%, bounce on reset.
- Hides while a fullscreen app runs on its monitor.
- Single-file `TokenBob.exe` release built by GitHub Actions.

[Unreleased]: https://github.com/Ajmalpshaik/TokenBob/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Ajmalpshaik/TokenBob/releases/tag/v0.1.0
