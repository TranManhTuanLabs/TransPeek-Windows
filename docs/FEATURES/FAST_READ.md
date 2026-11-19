# Fast Read (RSVP) — speed reading in its own window

> Status: v1 on main since 0.1.4 (merged PR #1, 2026-11-19). The UX below
> is the settled Windows style — see "Settled style".
> Engine + window + entry points. Global shortcut is a fallback — not in v1.

## What it is

Fast Read shows text one slide at a time at a fixed focal point (RSVP —
Rapid Serial Visual Presentation): the eye stops moving, so reading gets
faster. UX copies SwiftRead's reader (see the research report on the mac
repo, `docs/RESEARCH/tts-speechify-naturalreader/rsvp-report.md` §6.1 +
§6.3 for Windows/C#). **Read only — no TTS in the reader.** Listening is
the float icon's job (click = listen); reading is Fast Read's job.

## The window

Separate `FastReadWindow` (`src/TransPeek/FastRead/`), not a panel tab: RSVP
needs a big word (40–450 pt), one fixed focal point, a progress bar and a
pause context — none of that fits the 420 px translate panel. Default
70% × 55% of the work area, min 880×550, size remembered after resize
(`RsvpWindowWidth` / `RsvpWindowHeight` in Settings). Single instance:
re-opening focuses the existing window and loads the new text.

Layout (top to bottom — settled Windows style, Manh 2026-11-19; see "Settled style" below):

- **Top bar:** "Rapid Serial Visual Presentation — RSVP" (15pt semibold)
  truly centered on the window (full-width TextBlock, buttons overlay
  at the right like mac); Settings gear (E713) + "?" at the right.
  Window title: "TransPeek — Fast Read RSVP" (Manh 2026-11-19 — one
  dash, same as mac; the "TransPeek: Fast Read — RSVP" variant has two
  separators and splits "Fast Read" from "RSVP"). Left stays empty —
  mac's left pickers live in the bottom bar on Windows.
- **Word area:** the current slide, centered on its ORP pivot (see below),
  with two short focal ticks above/below it. **Reading time** top-left
  inside the block text ("Reading time: 2mins", under a minute "0min"),
  with the **version** right after it ("0.1.4 (build 192)" — same
  format as the panel footer, Manh 2026-11-19); **hint** top-right
  ("Space play/pause • ←/→ step • ↑/↓ speed • Wheel ↑/↓ focus size" —
  shortened 2026-11-19; the scroll/drag gestures live in this doc, not
  the hint).
- **Pause context:** ±150 words around the current slide, the current
  words bold + orange. When it shows follows the Settings popover
  (Always / Only show when paused / Never); its size is the focus size
  times the "Context size" percent (default 40%).
- **Progress:** seek slider + status (`Slide 12 / 240 • 1:23 left • 240 words`).
- **Bottom bar:** Words chunk picker (1–6) + Speed slider (100–2000) at
  the left; Restart, Rewind, Play/Pause, Fast-forward, Maximize centered
  (Restart first, before Rewind); "Context size" picker (25–100%) +
  "Focus size" picker (40–450, mac round 7 options) at the right.
  Transport is truly centered (overlay layout like mac's ZStack).

Settings popover (gear, like mac): Color Scheme (Black on White / White
on Black — dark = black page + light controls via `RequestedTheme`) +
"Show context around focus word" (Always / Only show when paused /
Never) + Reset to default (speed 275, focus 110, context 40%, display
Paused — color scheme is NOT reset). "?" popover: Space play/pause,
←/→ step, ↑/↓ speed ±10, R restart, Esc close.

Wheel (Manh 2026-11-19): over the progress slider = step one word
(scroll down = next); over the focus word = focus size ±5pt per tick
(clamped 40–450, picker shows the nearest option — restored 2026-11-19
from the 191 behaviour, which Manh liked; 192 had forwarded this wheel
to scroll the context instead; step raised 2pt to 5pt per tick
2026-11-19 because 2pt took too many scrolls); over the context =
scroll it natively. The focus size drives the around text too
(context = focus x the "Context size" percent), so one wheel tick
grows/shrinks both, like 191.
The context wheel used to resize it (±5% per tick) but that clashed
with scrolling, so context sizing moved to a drag: press-hold
left-click + drag UP/DOWN on the context = context size ±5% per 24 px
(clamped 25–100%, saved on release). Only a vertical-dominant drag
resizes (≥12 px, more vertical than horizontal) — a horizontal drag is
text selection: the context text is selectable
(`IsTextSelectionEnabled`), so words can be looked up. The drag latches
into resize mode once clearly vertical, and turns selection off while
resizing so the drag stops extending the selection. A Slider consumes
the wheel itself, so the progress handler is attached in code with
`AddHandler(..., handledEventsToo: true)`; the drag handlers use the
same pattern so they still see presses the selectable text handles for
its own selection.

Keyboard (only while the window has focus): `Space` play/pause,
`Left`/`Right` step a slide, `Up`/`Down` WPM ±10, `R` restart,
`Esc`/`Q` close. Handled via `KeyDown` on the root — not
`KeyboardAccelerator` (see the panel's Esc-pill lesson).

## Settled style — Windows Fast Read (Manh 2026-11-19)

This is the locked UX/UI/behaviour for the Windows reader — one design
style, not the only one. It can change later: converge to mac, or gain
another style variant. Until Manh says otherwise, this section is the
spec — code and docs follow it, and changing it is a decision, not a
tweak.

- **Window:** title "TransPeek — Fast Read RSVP". Default 70% × 55% of
  the work area, min 880×550, size remembered after resize. Single
  instance; re-opening focuses it and loads the new text. Not topmost.
- **Top bar:** subtitle "Rapid Serial Visual Presentation — RSVP"
  (15pt semibold, truly centered); Settings gear (E713) + "?" at the
  right; left empty. (Mac dropped the "— RSVP" suffix on 2026-11-19;
  Windows keeps it as part of this style.)
- **Reading area:** focus slide centered on its ORP pivot, focal ticks
  above/below. Reading time top-left ("Reading time: 2mins", "0min"
  under a minute) with the version right after it ("0.1.4 (build N)",
  same format as the panel footer); hint top-right ("Space play/pause
  • ←/→ step • ↑/↓ speed • Wheel ↑/↓ focus size"). Pivot pinned at
  the area's center (mac pins at 35% width).
- **Context:** ±150 words, current words bold + orange, below the focus
  slide (MaxHeight 120); shows per Settings (Always / Only when paused
  / Never, default Paused); size = focus × Context size % (default 40%).
- **Progress:** seek slider + status (`Slide 12 / 240 • 1:23 left •
  240 words`).
- **Bottom bar:** Words picker (1–6, default 1) + Speed slider
  (100–2000, default 275) left; Restart / Rewind / Play-Pause /
  Fast-forward / Maximize centered (Restart first); Context size picker
  (25–100%, default 40%) + Focus size picker (40–450, default 110)
  right.
- **Scroll (wheel):** over the progress slider = step one word (down =
  next); over the focus word = focus size ±5pt per tick (40–450, picker
  shows the nearest option; the around text scales with it); over the
  context = scroll it natively.
- **Mouse:** press-hold left-click + drag UP/DOWN on the context =
  context size ±5% per 24 px (25–100%, saved on release); only a
  vertical-dominant drag resizes (≥12 px, more vertical than
  horizontal) — a horizontal drag is text selection. Window border =
  resize; caption = move.
- **Keyboard (window focused):** Space play/pause, ←/→ step a slide,
  ↑/↓ speed ±10, R restart, Esc/Q close.
- **Pivot highlight (SwiftRead, like mac):** the middle word of a chunk
  (`floor(n/2)`) carries the pivot — its middle character, orange
  #F6861F — so the highlight never disappears at chunk 2+.
- **Settings popover (gear):** Color Scheme + "Show context around
  focus word" + Reset to default (speed 275, focus 110, context 40%,
  display Paused; color scheme is NOT reset). "?" popover: the
  shortcuts above.

## The engine

`src/TransPeek.Core/FastRead/RsvpEngine.cs` — pure logic, unit-tested
(`tests/TransPeek.Tests/RsvpEngineTests.cs`):

- **Chunk 1–6, default 1.** A chunk breaks early at a sentence end
  (`. ? !`) or a paragraph end, so a sentence is never cut mid-way.
- **WPM default 275, max 2000.** Base delay = `60000 / WPM` ms per word.
- **Micro-pauses** (SwiftRead legacy, additive on the base delay):
  number ×0.5, punctuation (`, ; :`) ×0.9, sentence end ×1.1, long word
  (> 13 chars) ×0.5, paragraph end ×1.1. Switched off when
  `chunk × WPM ≤ 100` (slow reading needs no extra pauses). Minus 30 ms
  execution overhead per slide, clamped to at least 10 ms.
- **ORP pivot = middle character, `floor(n/2)`, orange `#F6861F`.**
  SwiftRead-style (like mac): the middle word of a chunk
  (`floor(n/2)`) carries the pivot — its middle character, as an
  offset into the joined text — so the orange highlight never
  disappears at chunk 2+. The window aligns it by measuring the text
  and shifting it with a `TranslateTransform` so the pivot char's
  center sits exactly at the word area's center.
- **Paragraphs** are runs of non-empty lines (blank lines separate).
- **Context ±150 words** on pause (`RsvpDocument.Context`).

## Module layout (Manh 2026-11-17 13:08)

Features live in their own folder/module, not inside the main/translate
code — so a feature can never break the translate panel again. One
folder per feature, UI + logic together in it; only the thin joints
(button, menubar/tray, Alt+click, Settings toggle) touch shared code.
Same structure on mac + Windows: `FastRead/` (RSVP engine + reader
window + view here), `BreakTimer/` (Pomodoro/focus/20-20-20 timer +
tab, later), `Shortcuts/` (one shortcut table shared by the "?" embed
and the shortcut panel — still a TODO refactor, not this branch).

## Entry points (Manh's order, 2026-11-17)

1. **Alt+click on the float icon** → the OTHER action (Fast Read when
   Click = Speak; Speak when Click = Fast Read). Stops a running speech first.
2. **Right-click on the float icon** → menu: Translate / Listen /
   Fast Read / Settings. (The auto-hide timer stops while the menu is open.)
3. **"Fast Read" button in the translate panel** (Actions row, left end,
   glyph E736 ReadingMode + text "Fast Read", like mac): opens the reader
   with the text being translated; the panel hides first (it is topmost).
4. **Tray menu** → "Fast Read" (right under "Translate Selection").
5. **Global shortcut** — fallback only, not in v1.

**A+ linger (Style B, Manh 2026-11-18):** after any Read trigger from the
float icon (plain click with Click = Fast Read, Alt+click, right-click menu)
the icon shows A+ for **2.5s** as a confirmation, then hides; clicks are
ignored during those 2.5s. The icon always previews the next click (Speak =
plain A, Fast Read = A+); see `docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md`.

**Text source** (in order): the panel's source text (button path) →
fresh selection (< 10 s old, else the clipboard re-grab) → clipboard
text. Nothing to read shows a toast.

## Settings

- `IconClickAction` (`"Speak"` default / `"FastRead"`): what a plain
  click on the float icon does. ComboBox in Settings → General
  ("Click icon"). Alt+click does the OTHER action.
- `RsvpWpm` (275), `RsvpChunkSize` (1), `RsvpFontSize` (110),
  `RsvpContextSizeRatio` (0.40), `RsvpContextDisplay` ("Paused"),
  `RsvpColorScheme` ("Light") — changed live from the reader window's
  controls (saved immediately, like the other sliders); changing
  WPM/chunk rebuilds the document keeping the
  current word.
- `RsvpWindowWidth` / `RsvpWindowHeight` — remembered window size (0 = auto).

## Known limits (v1)

- No global shortcut yet (fallback, later).
- No Flow/Spotlight mode — RSVP (Focus) only. The 3 SwiftRead modes live
  in its web app, not in the extension source; they are display toggles
  on the same engine, not a separate engine (research report §1.1).
- No "whole page" capture, no PDF/ePub file opening from the reader —
  selection + clipboard only (report §8.2: file opening is a later phase).
- Long words are NOT split (jetzt/Sprint Reader split > 13 / > 17 chars);
  the long-word pause (×0.5) compensates for now.
- No abbreviation handling ("Dr." counts as a sentence end).
- The reader window is not topmost and has no OS minimum-size
  enforcement (the initial size is clamped; a manual drag can shrink it
  below 880×550).

## Links

- Research: mac repo `docs/RESEARCH/tts-speechify-naturalreader/rsvp-report.md`
- Panel multi-tab hub (the Fast Read placeholder tab): `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §10
- Branch standard: `docs/TEST-BUILD-WORKFLOW-GITHUB/GIT-CI-BUILD-SETUP.md`
