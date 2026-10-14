# TokenBob — Design Spec

**Date:** 2026-10-14
**Status:** Draft, awaiting review
**Repo:** `Ajmalpshaik/TokenBob` (public, MIT)
**Local folder:** `D:\06_Development\08_TokenBob`

## 1. Summary

TokenBob is a Windows desktop widget that hangs from the top edge of a monitor
on a physics-driven rope. The ornament at the end of the rope shows the user's
Claude plan usage — the **5-hour limit** and the **weekly limit** — and swings,
stretches, and rotates when grabbed and flung.

It is a personal tool for one user on Windows 11, built to be fun to touch and
useful at a glance.

## 2. Goals

- Show the 5-hour and weekly limit utilization (%) and time until each resets,
  matching what Claude's own usage page shows.
- Feel physical: gravity, elastic rope, rotation, wobble, and smooth 60 fps motion.
- Stay out of the way: click-through empty space, no taskbar entry, hides in
  fullscreen apps, near-zero CPU when idle.
- Be fully customizable from a Settings window opened with Ctrl + click.
- Clean, organized repo that builds and tests itself on GitHub.

## 3. Non-goals (v1)

- No local Claude Code token/cost statistics (only plan limits).
- No macOS or Linux.
- No installer, Microsoft Store package, or auto-update. v1 ships as a single
  `TokenBob.exe`.
- TokenBob never reads Claude Desktop's or Claude Code's stored logins. It has
  its own one-time sign-in (see §7).
- No style C (spark charm) and no thin-thread rope.

## 4. User-facing behavior

### 4.1 Placement

- The rope's hanging point (anchor) sits on the **top edge of one monitor**.
  Default: top-right of the primary monitor, 120 px in from the right edge.
- TokenBob lives on **one monitor at a time**, never spanning all of them.
- Anchor position, monitor, and rope length persist across restarts.
- If the saved monitor is missing at startup, TokenBob falls back to the
  default position on the primary monitor.

### 4.2 Ornament styles (chosen in Settings)

| Style | Description |
|---|---|
| **A — Ring ornament** (default) | Round badge with two progress rings: outer = weekly, inner = 5-hour. Center number = 5-hour %. |
| **B — Hanging tag** | Rounded tag with two labeled bars (5-hour, Weekly), each with % and reset time ("resets 2h 10m", "resets Fri"). |

The ornament's card surface follows the Windows light/dark theme.

### 4.3 Rope styles (chosen in Settings)

| Rope | Description |
|---|---|
| **Twisted cord** (default) | Warm brown cord with diagonal twist stripes, a highlight line, and a soft offset shadow. |
| **Bead chain** | Metal ball chain (lamp pull) with a highlight dot per bead and soft shadow. |

Both are drawn procedurally in code — no texture or 3D model assets.

### 4.4 Gestures

| Gesture | Result |
|---|---|
| Click + drag, release | Ornament follows the cursor with spring lag, then swings, rotates, and settles. |
| Pull down hard, release | "Pull to refresh": when rope stretch exceeds 25% of rest length at release, play a lamp-chain click bounce and refresh usage immediately. 30 s cooldown between pull-refreshes. |
| **Ctrl + click** | Open the Settings window. |
| **Shift + drag** | Edit mode, entered when a drag starts with Shift held: swinging stops; horizontal movement slides the anchor along the top edge (and onto another monitor when the cursor crosses into it); vertical movement sets rope length. Edit mode ends when the mouse button is released (releasing Shift mid-drag has no effect); the new placement is saved and the ornament drops and swings. |
| Hover (400 ms) | Hover card with exact 5-hour % and weekly %, reset times as clock times ("resets 4:30 PM", "resets Fri 9:00 AM"), and "updated N min ago". |
| Double-click | Open `https://claude.ai/settings/usage` in the default browser. |
| Cursor rushes past | "Wind": cursor moving faster than a threshold within 80 px of the ornament applies a small sideways impulse. |

Rope length range: 60–400 px (logical, DPI-independent). Anchor x is clamped so
the anchor stays at least 40 px from each monitor edge.

### 4.5 Alerts

- **Color level**: each limit's ring (style A) or bar (style B) is colored by its
  own utilization — green below 60%, amber 60–85%, red above 85%. The hover card
  accent follows the higher of the two.
- **Shiver** at 90% or more of the 5-hour limit: a short, small jitter
  animation, repeated at most once per refresh.
- **Windows notifications** at 80% and 95% of the 5-hour limit and of the weekly
  limit. Each threshold fires once per limit window and does not repeat until
  that limit resets.
- **Reset bounce**: when a fresh fetch shows the 5-hour window has reset (its
  `resets_at` moved forward and utilization dropped), the ornament does a happy
  bounce.
- Notifications can be turned off in Settings. Color, shiver, and bounce are
  always on.

### 4.6 Auto-hide in fullscreen

Every 2 seconds TokenBob checks whether a fullscreen app (game, video,
presentation) is running on its monitor, using `SHQueryUserNotificationState`
plus a foreground-window-covers-monitor check. If so, the widget fades out; it
fades back in when the fullscreen app ends. Can be turned off in Settings.

### 4.7 Tray icon

A small tray icon with a right-click menu: **Settings**, **Hide / Show widget**,
**Refresh now**, **Sign in… / Sign out**, **Quit**. The tray icon tooltip shows "5h 42% · Week 18%".

### 4.8 Settings window

Opened by Ctrl + click on the widget or from the tray menu. Changes apply live
to the widget and save automatically.

| Section | Controls |
|---|---|
| Look | Ornament style (A / B), rope style (cord / bead chain), rope length slider, ornament size slider (80–140%) |
| Motion | Swing strength slider (bounciness / damping), wind on/off |
| Data | Refresh interval (1, 2, 5, 10, 15 min; default 5), Refresh now button, last update time and status |
| Alerts | Windows notifications on/off |
| Behavior | Start with Windows on/off, auto-hide in fullscreen on/off |
| Account | Signed-in status, Sign in / Sign out |
| About | Version, link to GitHub repo, Quit |

### 4.9 Error and empty states

TokenBob never crashes on data problems. The ornament shows a grey state and the
hover card explains it:

| Condition | Ornament | Hover text |
|---|---|---|
| Not signed in, or claude.ai session ended (HTTP 401, or 403 with a JSON body) | "?" | "Sign in to Claude to see usage" |
| Network error / timeout / HTTP 429 | Last numbers, greyed | "Offline — updated N min ago" |
| Unexpected response shape, non-JSON (HTML) response, or HTTP 5xx | Last numbers greyed, or "?" if none | "Can't read usage right now" (details in log) |
| First fetch in progress | Gentle pulse | "Loading usage…" |

## 5. Technology

- **C# / WPF on .NET 10** (`net10.0-windows`). The user already writes C# for
  Revit add-ins and has the .NET 10 SDK installed.
- Per-monitor DPI awareness (PerMonitorV2) via `app.manifest`, so the widget is
  sharp and correctly sized on mixed-scaling monitors.
- Tray icon and notifications: **H.NotifyIcon.Wpf** (tray icon + notification
  support for unpackaged apps). Verified during implementation; if it cannot
  show notifications reliably, fall back to `CommunityToolkit.WinUI.Notifications`
  (`ToastNotificationManagerCompat`).
- Sign-in and usage requests: **Microsoft.Web.WebView2** (the Edge engine built
  into Windows 11), with its own profile folder `%AppData%\TokenBob\WebView2`.
- Tests: **xUnit**.
- Publish: self-contained single-file `win-x64` exe.

## 6. Architecture

Two projects plus tests. **Core** holds all logic with no UI dependencies and
is unit-tested; **App** renders and handles input.

### 6.1 Folder structure

```
08_TokenBob\
├─ src\
│  ├─ TokenBob.App\                 WPF app (net10.0-windows)
│  │  ├─ App.xaml(.cs)                startup, single-instance mutex, DI wiring
│  │  ├─ app.manifest                 PerMonitorV2 DPI awareness
│  │  ├─ Widget\
│  │  │  ├─ WidgetWindow.xaml(.cs)      transparent topmost tool window
│  │  │  ├─ RopeRenderer.cs             draws cord / bead chain from rope points
│  │  │  ├─ Styles\RingOrnament.xaml    style A
│  │  │  ├─ Styles\HangingTag.xaml      style B
│  │  │  ├─ HoverCard.xaml              details card
│  │  │  └─ FullscreenWatcher.cs        auto-hide detection
│  │  ├─ Settings\SettingsWindow.xaml(.cs)
│  │  ├─ Account\SignInWindow.xaml(.cs)   one-time claude.ai sign-in (WebView2)
│  │  ├─ Account\WebView2Fetcher.cs       hidden WebView2 that runs usage requests
│  │  ├─ Tray\TrayIconHost.cs
│  │  ├─ Input\GestureController.cs   maps mouse + modifier keys to gestures
│  │  ├─ Platform\                    Win32 interop: monitors, startup registry, browser
│  │  └─ Assets\                      app/tray icons
│  └─ TokenBob.Core\                classlib (net10.0), no WPF references
│     ├─ Physics\RopeSim.cs             Verlet rope + ornament rotation
│     ├─ Physics\Vec2.cs
│     ├─ Usage\IUsageProvider.cs
│     ├─ Usage\UsageSnapshot.cs
│     ├─ Usage\IWebFetcher.cs
│     ├─ Usage\ClaudeWebUsageProvider.cs
│     ├─ Usage\SignInNavigationPolicy.cs  allowed sign-in hosts
│     ├─ Usage\UsagePoller.cs           timer, cooldown, state machine
│     ├─ Alerts\AlertRules.cs           color level, thresholds, shiver, reset detection
│     ├─ Screens\Placement.cs           anchor clamp, monitor choice, fallback
│     ├─ Config\WidgetSettings.cs
│     ├─ Config\SettingsStore.cs        JSON in %AppData%\TokenBob
│     └─ Logging\FileLog.cs
├─ tests\
│  └─ TokenBob.Core.Tests\          xUnit
├─ docs\
│  ├─ superpowers\specs\            this spec
│  ├─ superpowers\plans\            implementation plan
│  └─ images\                       README screenshots / demo GIF
├─ build\publish.ps1                single-file exe publish
├─ .github\workflows\
│  ├─ build.yml                     build + test on every push / PR
│  └─ release.yml                   on tag v*: publish exe, attach to GitHub Release
├─ TokenBob.sln
├─ Directory.Build.props            shared version, nullable, warnings-as-errors
├─ .editorconfig
├─ .gitignore                       .NET / Visual Studio
├─ README.md
├─ CHANGELOG.md
└─ LICENSE                          MIT
```

### 6.2 Components and interfaces

**`RopeSim` (Core/Physics)** — a rope of 14 Verlet points from a fixed anchor to
the ornament mass.
- Inputs: anchor position, rest length, swing strength (maps to damping and
  stiffness), external target (while dragged), impulses (wind, bounce, shiver).
- Outputs: point positions, ornament angle (angular spring that lags the last
  rope segment, giving wobble), current stretch ratio, `IsAtRest`.
- Fixed timestep of 1/120 s with an accumulator, independent of frame rate.
- Rope segments are slightly elastic (constraint stiffness < 1) so the rope can
  stretch for pull-to-refresh.
- `IsAtRest` becomes true when kinetic energy stays under a threshold for 1 s;
  the App then stops its render loop until input, an impulse, or new data wakes it.

**`IUsageProvider` (Core/Usage)**
```csharp
Task<UsageResult> FetchAsync(CancellationToken ct);
```
`UsageResult` is either a `UsageSnapshot` (FiveHour, Weekly — each with
`Utilization` 0–100 and `ResetsAt`; plus `FetchedAt`) or a typed error
(`NotSignedIn`, `Offline`, `RateLimited`, `Unreadable`).

**`ClaudeWebUsageProvider`** — the only class that knows the claude.ai endpoints
and response shapes. It makes requests through an injected `IWebFetcher`
(`Task<WebResponse> GetAsync(string path, CancellationToken ct)`), so tests use a
fake fetcher. The App's `WebView2Fetcher` implements it with a hidden WebView2
that shares the sign-in profile.

**`UsagePoller`** — runs the refresh timer (interval from settings), exposes
`RefreshNow()` with a 30 s cooldown, keeps the last good snapshot, and raises
`Updated(UsageState)` where `UsageState` = last snapshot + current status.

**`AlertRules`** — pure functions plus a small persisted state:
`ColorLevel Level(double pct)`, `IEnumerable<Alert> Evaluate(previous, current, firedState)`,
returning notification, shiver, and reset-bounce events. `firedState` is saved
so restarts don't re-fire the same threshold in the same window.

**`Placement`** — given monitor bounds (logical px), clamps anchor x, chooses the
monitor under the cursor during Shift + drag, and falls back when the saved
monitor id is missing. Monitors are identified by device name plus bounds.

**`SettingsStore`** — loads/saves `WidgetSettings` as JSON at
`%AppData%\TokenBob\settings.json`. Corrupt or missing file → defaults (and the
corrupt file is renamed `.bak`).

**`WidgetWindow` (App)** — borderless, `AllowsTransparency=True`, `Topmost`,
`ShowInTaskbar=False`, tool-window style (hidden from Alt+Tab). Sized to the
rope's swing area (≈ 2.5 × rope length wide, 1.4 × rope length + ornament tall)
with the anchor at top center; fully transparent pixels are click-through.
Renders on `CompositionTarget.Rendering` only while the sim is awake.

**`GestureController` (App)** — turns pointer events + Ctrl/Shift state into
gesture commands (`DragStart/Move/End`, `OpenSettings`, `EditStart/Move/End`,
`OpenUsagePage`, `Hover`, `Wind`). Contains no physics.

### 6.3 Data flow

```
UsagePoller ──(timer / RefreshNow)──► ClaudeWebUsageProvider ─(IWebFetcher)─► UsageResult
     │
     └─ Updated(UsageState) ─► AlertRules ─► notifications (TrayIconHost)
                                    │       shiver / bounce impulses (RopeSim)
                                    ▼
                              WidgetWindow ─► ornament view + hover card + tray tooltip

Mouse ─► GestureController ─► RopeSim (drag / impulses) | Placement (edit) | Settings
RopeSim ─► WidgetWindow render loop (positions, angle, stretch)
```

## 7. Usage data source

> **Changed 2026-10-14.** The first build step found that this PC has no Claude
> Code login file. The Claude desktop app keeps its login encrypted inside
> itself, and TokenBob will not try to read it. TokenBob therefore has its own
> one-time claude.ai sign-in, approved by the user.

### 7.1 Sign-in

- On first run (and whenever TokenBob is not signed in) it opens a small
  **Sign-in window** containing a WebView2 browser at `https://claude.ai/login`.
  The user signs in normally (email code or "Continue with Google").
- The WebView2 profile lives in `%AppData%\TokenBob\WebView2`, protected by the
  user's Windows account. It is never uploaded, committed, or logged. TokenBob
  never sees the password.
- **Navigation lock:** the Sign-in window only navigates to `https` URLs on
  `claude.ai`, `anthropic.com` (and their subdomains) and `accounts.google.com`.
  Anything else is cancelled. Pop-up requests open in the same window under the
  same rule.
- Sign-in is complete when `GET /api/organizations` returns 200 with JSON; the
  window then closes itself and usage refreshes.
- **Sign out** (tray or Settings) deletes all cookies in the profile.

### 7.2 Endpoints

Requests run inside a hidden WebView2 that shares the sign-in profile, as
same-origin `fetch` calls from a `https://claude.ai` page, so they behave exactly
like the claude.ai website.

```
GET https://claude.ai/api/organizations              → [ { "uuid": "…", "capabilities": ["chat", …] }, … ]
GET https://claude.ai/api/organizations/{uuid}/usage → { "five_hour": {…}, "seven_day": {…}, … }
```

- Organization choice: the first organization whose `capabilities` contains
  `"chat"`, else the first one. Cached in memory and re-fetched if a usage call
  returns 404.
- Expected usage fields (confirmed against the real response on the first
  successful sign-in in build step 11): `five_hour.utilization` (0–100),
  `five_hour.resets_at` (ISO-8601), `seven_day.utilization`,
  `seven_day.resets_at`. A limit object or `resets_at` may be `null` (no active
  window) → 0% / no reset time. Unknown extra fields are ignored. Missing
  `five_hour` or `seven_day` keys → `Unreadable`.
- Status mapping: 200 + JSON → snapshot; 401, or 403 with a JSON body →
  `NotSignedIn`; 429 → `RateLimited`; non-JSON body (e.g. an HTML challenge page)
  or 5xx → `Unreadable`; network failure or timeout → `Offline`.

### 7.3 Risk and limits

- These are **unofficial endpoints** used by the claude.ai website. They may
  change without notice. All knowledge of them lives in `ClaudeWebUsageProvider`,
  so a change means editing only that file.
- Requests time out after 10 s. HTTP 429 → back off to double the interval
  (max 30 min) until a success.

## 8. Testing

### 8.1 Automated (xUnit, `TokenBob.Core.Tests`)

- **Physics:** a flung rope comes to rest (`IsAtRest`) within 5 s of simulated
  time; rest length is held within 2% at rest; a hard pull exceeds the 25%
  stretch threshold; ornament angle converges to the rope angle.
- **Placement:** anchor clamp at both monitor edges; cursor crossing into a second
  monitor switches the monitor; missing saved monitor falls back to primary
  top-right; rope length clamped to 60–400.
- **AlertRules:** color boundaries at 59.9 / 60 / 85 / 85.1; 79 → no alert,
  80 → one alert, 81 → none; 95 fires once; thresholds re-arm after reset;
  reset detection triggers bounce; shiver at ≥ 90.
- **Usage provider:** valid JSON → snapshot; null limit → 0%; missing keys /
  garbage / HTML page → `Unreadable`; 401 or 403+JSON → `NotSignedIn`; 429 →
  `RateLimited`; network failure or timeout → `Offline`; organization choice and
  404 re-fetch. All tests use a fake `IWebFetcher` — no real network, no account.
- **Sign-in navigation lock:** allowed and blocked URLs (http, look-alike hosts
  such as `claude.ai.evil.com`).
- **UsagePoller:** `RefreshNow` cooldown; last good snapshot kept on error;
  429 backoff.
- **SettingsStore:** round-trip; missing file → defaults; corrupt file → defaults
  + `.bak`.

### 8.2 Manual checklist (App)

- Swing feel for both styles × both ropes; pull-to-refresh click.
- Ctrl + click opens Settings; live changes apply.
- Shift + drag moves the anchor, changes rope length, and moves to a second monitor.
- Hover card, double-click to usage page, wind sway.
- Clicks pass through empty areas to windows behind.
- Fullscreen video hides the widget; it returns afterward.
- First run opens the Sign-in window; after signing in, real numbers match
  claude.ai's Usage page; Sign out returns to "?"; the Sign-in window refuses
  to open non-Claude sites.
- Tray menu items; start with Windows survives a reboot; notifications appear.
- Idle CPU near 0% when at rest.

## 9. GitHub

- Public repo `Ajmalpshaik/TokenBob`, default branch `main`, MIT license.
- Description: "A hanging, physics-driven Claude usage meter for Windows — 5-hour
  and weekly limits at a glance."
- Topics: `windows`, `wpf`, `dotnet`, `desktop-widget`, `claude`, `usage-meter`.
- `build.yml`: on push and pull request, `windows-latest`, restore, build,
  run tests.
- `release.yml`: on tag `v*`, run tests, publish single-file `win-x64`
  `TokenBob.exe`, attach to a GitHub Release.
- `.gitignore` excludes build output, user settings, and logs. No credentials
  are ever committed.

## 10. Build order

1. **Spike (done):** found no Claude Code login on this PC → switched to the
   one-time claude.ai sign-in (§7).
2. Scaffold solution, projects, repo files; create GitHub repo; first push; CI green.
3. Core physics (`RopeSim`) with tests.
4. Widget window + rope renderer + ring ornament, driven by fake data.
5. Usage provider + poller with tests; wire real data.
6. Gestures: drag, Ctrl + click, Shift + drag edit, placement + persistence.
7. Settings window + tray icon.
8. Extras: hanging tag style, bead chain, alerts + notifications, hover card,
   auto-hide, pull-to-refresh, wind, double-click, start with Windows.
9. README with demo GIF, CHANGELOG, tag `v0.1.0`, release.
