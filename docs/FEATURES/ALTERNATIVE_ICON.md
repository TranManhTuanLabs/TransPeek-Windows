# Alternative Icon — select text inside Fast Read

Feature doc for the icon that appears when the user selects text inside
Fast Read. Settled 2026-11-22 (Manh), mac parity: mac design doc
`docs/UI/UX_DESIGN/FAST_READ_SELECT_ICON.md`, implemented on mac main
`b6388f20` (CI #338). Windows implementation: module
`src/TransPeek/AlternativeIcon/` (main, 2026-11-22).

## 1. Name — "alternative icon"

One unified name: **alternative icon**. It is an alternative to the main
float icon, used only in Read mode. The name avoids confusion with the
Read action (Fast Read) — this icon does NOT start a Read session.

## 2. Why a separate path

The float icon (`TriggerIconWindow`) works well today and its logic is
delicate (glyph state machine, Option-key polling, hover debounce, the
panel-visible guard). The Fast Read select case must **not** touch that
logic — same architecture rule as mac ("cái đang ngon thì để nguyên,
tìm đường khác nối", `docs/ARCHITECTURE.md` §5). So it gets its own
separate path: an **alternative icon** that looks like the float icon
but is a different object with its own wiring. Hovering/clicking it
calls into the existing translate and speak behaviour — it reuses them,
it does not duplicate or modify the icon's internals.

## 3. Appearance — blue A in a chat bubble, same on hover

- The icon is a **blue "A" inside a chat (speech) bubble** (Segoe Fluent
  Icons Message E8BD + a blue "A" TextBlock on top) — the earlier icon
  design, reused here.
- It displays the **same** normally and on hover — it does NOT change
  glyph on hover (unlike the main float icon). While speaking it shows
  the speaker (E767 Volume), like the main icon does.
- Size follows the **auto-show icon setting** (`AutoShowFloatIconSize`,
  default 24pt, user-adjustable in Settings → General): window =
  setting, circle = setting − 2, bubble ~80% of the circle, A = half,
  speaker ~60%.

## 4. Behaviour — hover + click both work

| Action | What happens | Notes |
| --- | --- | --- |
| **Hover** | Translate (look up the selected word) | Per surface (see §5). |
| **Click** | Speak — listen to the selected text | Through the SHARED click-to-listen flow (see §6) + separate speaker indicator. No Read. |

There is **no Read action** on this icon. The user selected one word
inside a long passage, so starting a Fast Read session on that fragment
would lose the long context. The global "Click icon for:" setting is
ignored here — the icon is always speaker + hover in Read mode.

## 5. Hover differs per surface

- **Fast Read Lite** (panel right, panel already open): hover only
  switches the panel to the **center (translate) tab** — via the new
  `TranslatePanelWindow.TranslateInPlace(text)`. It must NOT call
  `ShowPanelNear` — that would move the panel to the cursor. Switching
  tabs does NOT stop the panel-right read (only `HidePanel` does), so
  the read keeps running behind the translate tab.
- **Window Read** (separate window, panel closed): hover **opens the
  panel** (normal icon flow: `ShowTranslationFlow` — panel opens at the
  cursor on the translate tab).

App picks the flow by the foreground window at hover time (see §7).

## 6. Click uses the SHARED click-to-listen flow (mac `1cdaa44a`, 27/11)

Clicking the alternative icon starts a click-to-listen session through
the SHARED float-icon speaker path (`App.StartClickToListenAsync`) —
the icon owns NO `SystemSpeechService` of its own anymore. App pauses
any Read in progress (keeping its position), stops any old session,
then plays the selected text with the separate speaker indicator at
the spot the icon was standing (read before the icon hides). The icon
itself keeps its blue A glyph — it NEVER swaps to a speaker while
speaking; playback state (⏸/▶, pause/resume) lives only on the
indicator. Clicking the icon again starts a NEW session replacing the
old one (pause/resume is the indicator's job, never a toggle here) —
unlike the float icon, which toggles pause/resume when clicked again
for the same text. Hover still translates even while a session is
playing. Language: the source language setting when the user picked
one, else the same `DetectLanguage` heuristic as the main icon
(Vietnamese diacritics → vi, otherwise en).

## 7. When the icon appears

Only when the user is in a Fast Read surface:

- The Window Read window is open, or
- The panel is on the Fast Read (right) tab.

It does **not** appear when selecting text in the translate input or in
Settings. The global `SelectionMonitor` only sees other apps (it skips
our own process), so the alternative icon has its own monitor:
`ReadSelectionMonitor` — same UIA `TextPattern.GetSelection` poll
(~300 ms) as `SelectionMonitor`, but with the filter INVERTED (only our
own process) and gated by the foreground window: App's gate compares
`GetForegroundWindow()` against the Fast Read window's and the panel's
HWNDs (captured on the UI thread at creation — a WinUI Window must not
be touched from the monitor's background thread). Panel counts only on
its Fast Read tab (`IsFastReadTabActive`).

Windows deviation from mac (documented choice): mac uses a local
mouse-up monitor; Windows reuses the codebase's established poll
pattern (same as `SelectionMonitor` / `BlockTextHoverService`). Same
outcome for the user, and polling also catches keyboard selections
(Shift+arrows) which a mouse-up hook would miss.

Auto-hide: 5 s (`TriggerIconAutoHideSeconds`, same setting as the main
icon — read live), or when the selection clears, or when the Read
surface stops being foreground. The selectable text is the around /
context text (`BeforeTextBlock` / `AfterTextBlock` /
`ContextTextBlock` — all `IsTextSelectionEnabled`); the RSVP focus word
itself is not selectable.

## 8. Module layout

`src/TransPeek/AlternativeIcon/` — one folder per the module rule
(`docs/ARCHITECTURE.md` §5), wired from `App.xaml.cs` only:

- `ReadSelectionMonitor.cs` — UIA poll, own-process only, gated.
- `AlternativeIconWindow.xaml` / `.cs` — the floating window (hover /
  click / auto-hide / glyph state; no context menu, no Alt handling).
- `AlternativeIconController.cs` — owns the monitor + window + the
  icon's own `SystemSpeechService`; reports hover-translate to App via
  a callback.

App joints: `IsReadSurfaceForeground()` (gate), `OnAlternativeIconTranslateRequested`
(Lite vs Full), `_panelHwnd` / `_fastReadWindowHwnd` (captured at
creation), `TranslatePanelWindow.TranslateInPlace` +
`IsFastReadTabActive` (panel). No change to `TriggerIconWindow`,
its glyphs, or its settings.

## 9. Scope (what is NOT in this feature)

- No change to the real float icon's logic, glyphs, or settings.
- No Read action from a Fast Read selection.
- No icon in the translate input or Settings.
- No settings toggle for the alternative icon: it is inert unless a
  Read surface is foreground, so there is nothing to turn off.

## Links

- Mac design doc: mac repo `docs/UI/UX_DESIGN/FAST_READ_SELECT_ICON.md`
- Float Icon (feature): `FLOAT_ICON.md` — the real float icon this fake
  icon delegates to.
- Fast Read (feature): `FAST_READ.md` — the Fast Read surfaces this icon
  lives in.
- Panel UX: `../UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §11 (panel tabs).
