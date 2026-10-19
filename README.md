# TokenBob

A hanging, physics-driven Claude usage meter for Windows — 5-hour and weekly limits at a glance.

TokenBob hangs from the top edge of your screen on a little rope. Its ornament shows how much of
your Claude **5-hour limit** and **weekly limit** you've used. Grab it and fling it: it swings,
stretches, spins and settles back like a real thing on a string.

![TokenBob styles: ring and hanging tag, on a twisted cord or a bead chain, in light and dark](docs/images/styles.png)

## Install

1. Download **`TokenBob.exe`** from the [latest release](https://github.com/Ajmalpshaik/TokenBob/releases/latest).
2. Double-click it and click **Install**. That's it — no admin password needed.
3. A small window asks you to **sign in to Claude once** (email code or "Continue with Google").
   After that TokenBob stays signed in for weeks.

TokenBob installs to `%LocalAppData%\Programs\TokenBob`, adds itself to the Start menu and (if you
leave the box ticked) starts with Windows. To update, download a newer `TokenBob.exe` and
double-click it. To remove it: **Settings → Apps → Installed apps → TokenBob → Uninstall**.

> **"Windows protected your PC"?** Windows shows this for new downloads that aren't signed with a
> paid publisher certificate. Click **More info → Run anyway** once. A Microsoft Store version,
> which doesn't show this, is planned.

Needs Windows 10 or 11 (64-bit) and the Microsoft Edge WebView2 Runtime, which is already part of
Windows 11.

## What it shows

| Style | What you see |
|---|---|
| **Ring** | Inner ring = 5-hour limit, outer ring = weekly limit, 5-hour % in the middle |
| **Hanging tag** | Two bars, each with its % and time until it resets |

Each limit turns **green** below 60%, **amber** from 60%, **red** above 85%.
Hover over it for exact numbers, reset times and when it last updated.

## Gestures

| Do this | It does |
|---|---|
| Drag and fling | Swings, rotates and springs back; the rope can be pulled to 140% of its length |
| Pull down hard and let go | Lamp-chain "click" — refreshes usage now |
| **Ctrl + click** | Opens Settings |
| **Shift + drag** | Moves the hanging point (left/right, even onto another monitor) and the rope length (up/down) |
| Hover | Details card |
| Double-click | Opens Claude's usage page |
| Rush the mouse past it | It sways in the "wind" |

The tray icon (near the clock) shows both percentages in its tooltip. Right-click it — or the
widget — for Settings, Hide/Show, Refresh now, Sign in/out and Quit.

## Settings

Settings opens with a live preview of your look, and follows the theme you pick.

- **Appearance:** theme (follow Windows, light or dark, for the widget and every window); ring or
  hanging tag; the number shown in the ring (5-hour or weekly); twisted cord or bead chain; cord
  colour (gold, graphite, crimson, ivory); rope length; ornament size; opacity
- **Motion and effects:** calm ↔ bouncy swing; sway in the wind; blast on reset; shiver at 90%;
  details card on hover
- **Data:** refresh every 1, 2, 5, 10 or 15 minutes; refresh now
- **Alerts:** Windows notifications at 80% and 95% (once per limit window)
- **Behavior:** start with Windows; hide while a fullscreen app is running; reset the hanging point
- **Account:** sign in / sign out

It also shivers at 90% of the 5-hour limit, and when that limit resets the old ornament **blasts**
apart with firecrackers and confetti and a fresh one pops in on the rope (there is a "Play reset
animation" button in Settings to see it any time). Settings are saved in
`%AppData%\TokenBob\settings.json`.

## How it gets your usage

- TokenBob has its **own one-time sign-in** to claude.ai, in a browser profile kept at
  `%AppData%\TokenBob\WebView2` and protected by your Windows account. It is never uploaded,
  and TokenBob never sees your password.
- It **does not** read the login of the Claude desktop app, Claude Code or your web browser.
- The sign-in window can only open Claude's own sign-in pages (and Google's, for "Continue with
  Google"). Anything else is blocked.
- It reads usage from the same endpoints the claude.ai Usage page uses. They are **unofficial**
  and may change — if that happens, TokenBob shows "Can't read usage right now" until it's updated.
- Between refreshes the browser engine is shut down, so TokenBob idles at about 20 MB and 0% CPU.

TokenBob is an independent project and is not made by or affiliated with Anthropic.
Claude is a trademark of Anthropic.

## Build from source

Requires the .NET 10 SDK on Windows.

```powershell
dotnet test                                   # automated tests
dotnet run --project src/TokenBob.App         # run it
./build/publish.ps1                           # single-file publish\TokenBob.exe
```

Developer options:

```powershell
TokenBob.exe --render-samples styles.png      # draw every style, light and dark
TokenBob.exe --demo-alerts                    # fake usage 70→96% and a reset, to see every alert
```

The code is split into `src/TokenBob.Core` (physics, usage, alerts, placement, settings — fully
unit-tested, no UI) and `src/TokenBob.App` (the WPF widget, tray icon and windows). The design is in
[docs/superpowers/specs](docs/superpowers/specs/2026-10-14-tokenbob-design.md).

## License

[MIT](LICENSE)
