# Fast Read (RSVP) — speed reading in its own window or in panel right

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

A compact version of the same reader DOES fit panel right — the lite
reader (`CompactReadingView`, `src/TransPeek/FocusTimer/`), sharing the
view model below. See "Shared view model + reading in panel right".

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
window + view here), `FocusTimer/` (Pomodoro/focus/20-20-20 timer +
panel-right lite reader + Rest window — `feature/focus-timer-decor`,
2026-11-20; see `FOCUS_REST.md`), `Shortcuts/` (one shortcut table
shared by the "?" embed and the shortcut panel — still a TODO refactor,
not this branch).

## Shared view model + reading in panel right (feature/focus-timer-decor, 2026-11-20)

Playback lives in **`RsvpReaderViewModel`** (`src/TransPeek/FastRead/`,
shared by the Fast Read window and the panel-right lite reader — one
playback engine, two layouts). The window only renders the slide and
the pause context and syncs its own controls from the view model's
`PropertyChanged`; the lite reader (`CompactReadingView`,
`src/TransPeek/FocusTimer/`) does the same in the compact layout. The
view model owns the document, the current slide, the timer and the
Settings-backed options (WPM / chunk / focus size / context size /
context display / color scheme). Behaviour is the window's, moved
unchanged: same delays, same clamps on WPM/chunk/context ratio, same
status and reading-time text.

**"Run FastRead in:"** (panel right demo, radios — also the setting
`FastReadRunLocation`, "NewWindow" default / "RightPanel"): where Fast
Read opens from EVERY entry point (panel button, float icon, tray,
shortcut). Labels (Manh 2026-11-20, mac parity): **"FastRead Lite"**
(panel right) / **"FastRead Full Window (recommended)"** (window).
The demo's **"Read in panel" button always reads in the panel**,
whatever the radios say. Panel-right reading: the panel stays open,
switches to the Fast Read tab, starts the read and pins itself; the
demo shows the word "reading" with its "a" in yellow between two
half-bars (the pivot preview). The demo title is **"Fast Read Lite"**
(no more "coming soon"), and the "Run FastRead in:" section is
LEFT-aligned (the rest of the demo stays centered). The lite design
is FROZEN as it stands (Manh 2026-11-20) — no more changes to it
beyond this batch.

**Panel vs window differences (settled):**

- Panel wheel on the focus line: focus size ±5pt per tick, **NO cap**
  (Manh 2026-11-20, 07:02 — swipe freely, the user trims it to the
  frame). The window follows its context style: Style 1 no cap (like
  the panel), Style 2 clamps 40–450. The view model itself does not
  clamp the focus size.
- Panel ↑/↓ keys: speed ±50 WPM (like mac). Window ↑/↓: the WPM
  slider ±10 (unchanged).
- Panel has no sentence step (Shift+←/→ on mac): the window never had
  one either, so the panel does not either.
- Panel Esc is two-stage: first Esc stops the read, second hides the
  panel (pinned: Esc never hides). The Rest window closes on Esc.
- While a panel-right read runs, auto-fit is frozen (the panel size
  stays put for the reader) and resumes when the read stops.
- The lite reader's Maximize toggles the panel between default and max
  size (max clamped to the visible work area — never covers the
  taskbar/Dock, mac 08:11 fix). The panel's horizontal wheel over the
  Fast Read tab switches tabs (mac: horizontal swipe passes through).
- Space in panel right: the keys already existed in code
  (`CompactReadingView.HandleKey` + the panel's KeyDown forwarding),
  but they never arrived — the panel shows without activation
  (NOACTIVATE). A user-started panel-right read (panel button / float
  icon / tray) now activates the panel on start
  (`ShowPanelForReading`), so Space/←/→/↑/↓/R work at once.
  Reminder reads do NOT activate (they must never steal focus).
- Completion: when a full read finishes, both layouts show
  "Congratulations! You just read X words in Y minutes, Z seconds!"
  + the share row (see "Completion" below). The lite shows the full
  sentence + share only at full size (width ≥ 540); at the small size
  just "Congratulations!".

## Context styles — Style 1 / Style 2 (2026-11-20)

The Window Read's Settings popover offered two **context styles** for
the focus + around-text display and interaction only — the other
controls stay the same. Setting `RsvpContextStyle` ("Style1" default /
"Style2"). **The picker was removed from the popover 2026-11-21
(Manh — it duplicated "Reading Style"); the setting stays at its
default ("Style1") and the code still reads it, only the UI is gone.**

- **Style 1 (default) — FastRead Lite:** the book page: the
  before-words above the focus line, the after-words below it, the
  focus line pinned at the vertical center. Wheel over the reading
  area changes the focus size with NO cap; no context scroll, no
  drag-to-resize the context (the picker still sets the size).
- **Style 2 — the window's own theme:** one scrollable context
  paragraph below the focus line, the current words bold + orange
  inline. Wheel over the focus word changes the size clamped 40–450;
  wheel over the context scrolls it; press-hold drag up/down on the
  context resizes it (5% per 24 px).

Identical in both styles: the pivot split + alignment, the orange
pivot (#F6861F), the tick bars, the context font (focus × "Context
size"), Light/Dark. The lite reader IS Style 1 (it has no style
picker). The style is a theme preference — Reset does NOT reset it
(same as the color scheme).

## Reading styles — RSVP Focus / Basic Reading + speaker (2026-11-21, feature/fastread-speaker-style2)

**Two reading styles, not one** (mac parity: `ReadingStyle`, display
names — never "Style 1" / "Style 2"). Picked in the Settings popover
of BOTH the reader window and the lite reader; any style works with
any color scheme. Setting `RsvpReadingStyle` ("RsvpFocus" default /
"BasicReading"). A mode preference like the color scheme — Reset does
NOT reset it. **Not the same as `RsvpContextStyle`** ("Style1" /
"Style2" for the around-text display) — different keys, different
values, so the two can never be mixed up.

- **RSVP Focus (default):** the layout above — one chunk centred at
  the ORP pivot, context around it, progress in slides.
- **Basic Reading:** a scrollable page (`Style2ReadingView`, shared by
  the window and the lite reader — window padding 32, lite 12) with
  the whole text at once; Play advances a focus line one line at a
  time. Lines are paragraphs split into ≤12 words each
  (`RsvpEngine.BuildLines` — a logical split, not the visual wrap;
  matching visual lines is a later refinement). Body size = focus
  size × 0.4; the page scrolls the focus line to the middle. Progress
  counts lines ("Line 3 / 42"). The pause context does NOT show in
  this style — the page IS the context. The chunk picker has no
  effect here (lines are the unit); WPM still paces the lines.
  **Wheel over the page changes the font size** (Manh 2026-11-21,
  both surfaces — the control is shared): 5pt per tick, clamped
  40–450 on the focus size (16–180pt body); the wheel is the zoom
  here, so the page scrolls by dragging the scrollbar, and the
  current line stays centered while zooming. Switching style mid-read
  keeps the position as a fraction of the text.

**Speaker button (both surfaces, transport order: Restart, Speaker,
Play, Rewind, Forward, Maximize — lite adds Close first):** the
speaker is a mute/unmute toggle for the read, NOT a separate "speak
the whole text" action. Sound is OFF by default and NOT persisted —
a new read starts silent, like today. Sound on: Play/Space speaks
one phrase at a time and the speech itself paces the read —
continuous reading (phương án 1, Manh 2026-11-21): the text is split
into sentence/clause phrases (`RsvpEngine.BuildPhrases`, cap 20
words) and each phrase is one utterance, so the voice never stutters
between words; the focus follows the voice phrase by phrase (the
slide/line containing the phrase's first word). Sound off: the WPM
timer paces, exactly like today. The timer path is untouched by the
sound path. Pausing, restarting, or switching style stops the
speech; a failed speak (no voice installed) falls back to the timer
for that unit, so the read never stalls. Language is the vi/en
heuristic (same as the float icon's speak). The Rest window has NO
speaker button (it never wires a TTS service — mac parity: no
coordinator, no button).

**Voice panel (hover the speaker):** an in-tree overlay above the
transport — NOT a Flyout/Popup, so clicking the speaker toggles the
sound at once instead of dismissing a popup first (that was mac
CI314 bug 2; the other mac bug was the speaker reading the whole
text on its own thread — both avoided here by design). It stays open
while the mouse is on the button or on the panel, or while a voice
picker dropdown is open (the dropdown is a Popup — hover alone
would hide the panel mid-pick, Manh 2026-11-21); it hides 300 ms
after leaving all three. In the reader window it lives on the root
grid (rows 1–5), NOT inside the bottom-bar grid — inside the
Auto-sized bar it inflated the bar's height and shoved the buttons
around (Manh 2026-11-21). In lite it also hides when playback starts
(the transport hides then). Contents: English voice + Vietnamese voice
pickers (same keys as Settings → TTS: `EnglishVoiceId` /
`VietnameseVoiceId`) and a **Speed slider 0.6x–4x** (mac parity) —
the REAL voice-speed multiplier, shared with Settings → TTS
(`SpeechRate`, whose clamp widened 0.5–2.0 → 0.5–4.0 and whose
combo gained 2.5x/3x/4x so a panel speed shows correctly there).
Speed takes effect from the next phrase; WPM is untouched (it
only paces silent reading). Glyphs (Microsoft Learn, Segoe Fluent
Icons): sound on **E767 Volume**, sound off **E74F Mute**.

**"Highlight current line"** (toggle in the same Settings popover,
setting `RsvpHighlightCurrentLine`, default OFF): a soft accent
background on the current line in Basic Reading. While TTS is
reading, the highlight turns itself on (computed, not stored), so
the spoken line stays visible. Not reset by Reset (mode preference).

**Continuous reading + Pro gate (2026-11-21, phương án 1):** Windows
has no per-word callback (unlike mac's `willSpeakRangeOfSpeechString`),
so the focus follows the voice phrase by phrase, not word by word —
`ProGate.WordSyncEnabled` (in `RsvpReaderViewModel.cs`, default `true`,
free for now) gates it at the pacing routing: gate off means the
silent WPM timer even with the speaker on. A phrase ends at a natural
pause — sentence end (. ? !), clause punctuation (, ; :), paragraph
end, or a 20-word cap. Pause resumes from the phrase containing the
current word; seek / style / WPM changes re-sync the phrase from the
new position. Future demo idea (NOT built yet, comment only): when
gated, the speaker plays a short demo — a different voice reading
different text, with a "Pro feature" note.

**Space during a read (2026-11-21):** Space is always Play/Pause
while a read is active — the hosts intercept it at `PreviewKeyDown`
(tunnel), so it never falls through to a focused button (the repo
link at the panel bottom got activated that way). After the read
finishes, Space is left alone — the share / Done buttons work
normally. The "Context style" picker was removed from the reader
window's Settings popover the same round (Manh 2026-11-21 — it
duplicated "Reading Style"); the `RsvpContextStyle` setting stays at
its default ("Style1"), only the UI is gone.

**Deferred (like mac):** true word-by-word sync (highlight each word
as it is spoken — needs a per-word callback Windows TTS does not
expose), Style 2 lines matching the visual wrap, selecting text in
the Style 2 page to look it up.

## Completion — congratulations + share (2026-11-20, mac §14.5)

The view model tracks the **elapsed reading time** (banked across the
playing stretches of the current read; reset on a new text, on
Restart, and on play-after-finish) and exposes `CompletionText`
("Congratulations! You just read X words in Y minutes, Z seconds!")
and `ShareText` ("I just read X words in … with TransPeek Fast Read
— read 2–3x faster and improve your learning experience!"). When a
full read finishes, the congratulations line + the share row
(`ShareRowBuilder`, same row as About) show **above the progress
bar** — in the window, and in the lite reader (full-size only, see
above). The window's "Reading time: Xmins" at the top-left is the
ESTIMATE (unchanged); the completion line reports the time actually
spent.

## Entry points (Manh's order, 2026-11-17)

1. **Alt+click on the float icon** → the OTHER action (Fast Read when
   Click = Speak; Speak when Click = Fast Read). Stops a running speech first.
2. **Right-click on the float icon** → menu: Translate / Listen /
   Fast Read / Settings. (The auto-hide timer stops while the menu is open.)
3. **"Fast Read" button in the translate panel** (Actions row, left end,
   glyph E736 ReadingMode + text "Fast Read", like mac): opens the reader
   with the text being translated — where it opens depends on "Run
   FastRead in" (see above): the Fast Read window hides the panel first
   (it is topmost); panel-right reading keeps the panel open.
4. **Tray menu** → "Fast Read" (right under "Translate Selection").
5. **Global shortcut** — fallback only, not in v1.

**A+ linger (translate-first, Manh 2026-11-20 18:09):** after any Read trigger from the
float icon (plain click with Click = Fast Read, Alt+click, right-click menu)
the icon shows A+ for **2.5s** as a confirmation, then hides; clicks are
ignored during those 2.5s. The icon is translate-first now: idle is ALWAYS
the plain blue A (hover = translate), whatever Click is set to — A+ only
appears on Alt held (Click = Speak) or as this 2.5s feedback, and the
speaker on Alt held (Click = Fast Read) or while speaking; see
`docs/FEATURES/FLOAT_ICON.md` §3.

**Text source** (in order): the panel's source text (button path) →
fresh selection (< 10 s old, else the clipboard re-grab) → clipboard
text. Nothing to read shows a toast.

## Settings

- `IconClickAction` (`"Speak"` default / `"FastRead"`): what a plain
  click on the float icon does. ComboBox in Settings → General
  ("Click icon"). Alt+click does the OTHER action.
- `RsvpWpm` (275), `RsvpChunkSize` (1), `RsvpFontSize` (110),
  `RsvpContextSizeRatio` (0.40), `RsvpContextDisplay` ("Paused"),
  `RsvpColorScheme` ("Light"), `RsvpContextStyle` ("Style1") — changed
  live from the reader window's controls (saved immediately, like the
  other sliders); changing WPM/chunk rebuilds the document keeping the
  current word.
- `RsvpReadingStyle` ("RsvpFocus" default / "BasicReading") +
  `RsvpHighlightCurrentLine` (false) — the reading style picker and
  the line-highlight toggle in the Settings popover of both the reader
  window and the lite reader; see "Reading styles" above. Not reset
  by Reset to default (mode preferences, like the color scheme).
- `RsvpWindowWidth` / `RsvpWindowHeight` — remembered window size (0 = auto).
- `FastReadRunLocation` ("NewWindow" default / "RightPanel"): where Fast
  Read opens from every entry point — the Fast Read window, or the
  panel-right lite reader. Set from the "Run FastRead in:" radios on
  panel right; see "Shared view model" above.

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
- Basic Reading: no TTS word-sync yet, lines are a logical ≤12-word
  split (not the visual wrap), and the page text is not selectable
  for lookup yet (see "Reading styles" above).

## Links

- Research: mac repo `docs/RESEARCH/tts-speechify-naturalreader/rsvp-report.md`
- Panel multi-tab hub (the Fast Read tab, now the lite reader):
  `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §10–§11
- Focus & Rest (the lite reader's other host + the Rest window):
  `FOCUS_REST.md`
- Branch standard: `docs/TEST-BUILD-WORKFLOW-GITHUB/GIT-CI-BUILD-SETUP.md`
