# Focus & Rest — Pomodoro + 20-20-20 eye-rest (feature/focus-timer-decor)

> Status: on branch `feature/focus-timer-decor` (2026-11-20), ported from
> mac branch `feature/focus-timer-decor` @ `54717ca` (not merged on mac
> either at port time). Manh approved the frame 2026-11-20 08:26 +
> 08:31. Module: `src/TransPeek/FocusTimer/` (UI) + engine in
> `src/TransPeek.Core/FocusTimer/FocusTimerEngine.cs` (pure logic,
> unit-tested — the test project only references Core).

## What it is

A focus timer on panel left (Pomodoro: Work 25 / Rest 5, long break 15
after 4 cycles) + a 20-20-20 eye-rest reminder (every 20 min, rest the
eyes for 20 s). Both run independently. When a break is due **while the
user is reading** (Fast Read window open, or a read running in panel
right), the reminder does NOT steal the screen: it waits (orange dot on
the card header + a banner above the tab content on all 3 tabs), and
shows itself when the read stops.

## The card (panel left, between "How the icon works" and "Keyboard shortcuts")

`FocusTimerCard.xaml` (+ `.xaml.cs`) — same card chrome as the How card
(Border + a header Button that is the whole top bar, click to
collapse/expand; collapsed state in `FocusTimerCollapsed`).

- **Header:** Clock glyph E917 + "Focus & Rest" + an **orange dot (8px,
  #F6861F)** while a reminder is waiting (tooltip "A break reminder is
  waiting").
- **Focus (Heart EB51):** phase name ("Ready" / "Focus" / "Rest" /
  "Long break") + countdown 28pt bold Consolas + Work picker (5 / 10 /
  15 / 20 / 25 / 30 / 45 / 60, default 25) + Rest picker (5 / 10 / 15,
  default 5) + 4 cycle dots (filled #0EA5E9 per completed focus cycle)
  + "Long break 15 min after 4 cycles" + Start focus / Pause / Resume +
  Reset + **"Repeat automatically" toggle (default ON, Manh 2026-11-20
  07:25 — one Start runs Work → Rest → Work …; after 4 cycles a 15-min
  long break, then Work again)**. Changing Work while idle re-arms the
  countdown at once; mid-session it applies to the next session.
- **Rest eyes (Walk E805):** "Next in mm:ss" / "Off" + "Every 20 min,
  rest your eyes for 20 s — look at something 20 feet away." + Start /
  Stop eye reminders. **Default OFF** (Manh 2026-11-20 07:25 — no
  reminder until the user turns it on).
- **"Run reminder in:"** picker — "New Fast Read Window" (default) /
  "Right Panel". Applies ONLY to focus / rest-eyes reminders, not to
  Fast Read itself.

## The reminder rule (Manh 2026-11-20, mac parity)

- A reminder that fires while reading **waits**: `PendingReminder` +
  orange dot + banner. The banner (on all 3 tabs, above the tab
  content) shows the notification title + "Your reading stays as it
  is." + **"Take a break"** (show the waiting reminder now) +
  **"Snooze 5 min"** (fire again in 5 minutes — the reminder keeps
  waiting meanwhile).
- The user stops reading (closes the Fast Read window / ends the panel
  read / hides the panel) → the waiting reminder **shows itself** per
  the "Run reminder in" setting.
- Not reading → the reminder shows at once per the setting.
- "Take a break" while reading is impossible by construction (the
  banner's button is the only way, and it shows the reminder — the
  current read is replaced, not kept).

## Where a reminder shows

- **New Fast Read Window (default):** a separate `RestWindow` (560×400,
  fixed size, title "TransPeek — Rest") that reads the reminder text
  with the lite reader, then counts the break down (coffee cup EC32 +
  title + mm:ss 56pt bold + "The window closes when the timer ends." +
  Done) and closes itself at zero. Esc closes the window. It never
  touches the Fast Read window — a read in progress keeps reading. The
  panel hides first (it is topmost and would float above the Rest
  window).
- **Right Panel:** the reminder text is read in panel right (the panel
  comes forward, switches to the Fast Read tab, starts the read, pins
  itself). When the read finishes, the panel shows "Rest — mm:ss" +
  Done below the reader and returns to the demo at zero.

## System notifications — DEFERRED (Manh 2026-11-20)

**No Windows toast notification in this branch.** Manh prefers to wait
until the app has an installer / Store identity — a toast on the
portable build needs an app identity that would conflict with the
later signature / Store build. The banner + orange dot + auto-show on
read end carry the reminder until then. **Debt: add the toast when the
app ships as an installed / Store build.** (The store has no notifier
hook to fill in later — when notifications come, they attach to the
engine's `ReminderReady` / `PendingReminder` like the banner does.)

## Architecture

- **Engine (Core, pure, tested):** `FocusTimerEngine` — Pomodoro
  phases (Idle / Focus / Rest / LongBreak), eye-rest interval (20 min)
  + countdown (20 s), snooze (5 min), `PendingReminder`, `ReminderReady`
  event, `IsReading` func (set by the store). `Tick()` is called once a
  second by the store. 14 xUnit tests in
  `tests/TransPeek.Tests/FocusTimerEngineTests.cs`.
- **Store (app):** `FocusTimerStore` — owns the engine, drives it with
  a 1s DispatcherTimer (runs only while something counts down), routes
  reminders (Rest window vs panel right), owns the panel-right read
  (`PanelReadingViewModel`, `PanelRightRequest`, `PanelReminderCountdown`).
- **Views:** `FocusTimerCard` (panel left), `PendingReminderBanner`
  (overlay above the tab area, all tabs), `PanelFastReadView` (panel
  right — the demo + the lite reader), `CompactReadingView` (the lite
  reader, shared by panel right and the Rest window), `RestWindow`.

## Settings

- `FocusWorkMinutes` (25), `FocusRestMinutes` (5),
  `FocusLongBreakMinutes` (15), `FocusRepeatAutomatically` (true),
  `FocusReminderDestination` ("NewWindow" / "RightPanel"),
  `FocusTimerCollapsed` (false) — see `docs/FEATURES/SETTINGS.md`.
- `FastReadRunLocation` ("NewWindow" / "RightPanel") — where Fast Read
  opens; see `FAST_READ.md`.

## Glyphs (Segoe Fluent Icons, verified on Microsoft Learn 2026-11-20)

Clock **E917** (card header — mac "timer"; there is no "Timer" glyph),
Heart **EB51** (Focus — mac "heart"), Walk **E805** (Rest eyes — mac
"figure.walk"), Cafe **EC32** (Rest window countdown — mac
"cup.and.saucer.fill"), Ringer **EA8F** (banner bell — mac "bell.fill").

## Links

- Fast Read (shared view model + panel-right reading):
  `FAST_READ.md`
- Panel layout (card order, pin in the dots row, banner overlay):
  `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §11
- Mac source: `FocusTimerStore.swift`, `FocusTimerCard.swift`,
  `PanelFastReadView.swift`, `PendingReminderBanner.swift`,
  `ReminderNotifier.swift` (system notifications — NOT ported, see
  above), `RestWindowController.swift`
