# Block text hover — show the icon by resting on text (feature/block-text-detection)

> Status: on branch `feature/block-text-detection` (2026-11-21), not
> merged. Manh 2026-11-20 23:58: no manual mouse selection needed — the
> trigger icon appears when the mouse RESTS on a block of text, ready
> to translate / listen / read that block.

## Model A, not Model B — the research framing

This feature is **Model A** exactly as the TTS research defines it
(`research_notes/tts-speechify-naturalreader-20261116/`:
`recommendation.md` §1–2, `speechify-report.md` §9–11; Pro plan
`research_notes/transpeek-pro-premium-plan-20261119/plan.md` —
"Auto-detect block text"):

- **Model A (what this is):** hover a block → hit-test ONE point
  (`IUIAutomation.ElementFromPoint`; FlaUI `automation.FromPoint`) →
  show ONE icon. Feasible natively, with limits.
- **Model B (NOT this):** pre-show an icon in EVERY block without
  hovering. Not done — it needs a continuous scan of the whole UI
  Automation tree (expensive CPU, no MutationObserver / scroll
  notification on native), and the icon cannot follow scroll. Even
  Speechify only does Model B on hand-configured sites; on any other
  page it uses Model A. Manh's 17/11 decision ("Model B KHÔNG làm")
  stands; the 19/11 revisit ("research mới cho thấy khả năng cao
  thành công") is answered by this Model A build, not by Model B.

## What it is

- **Hover a block of text** (a paragraph in a browser, a line in
  Notepad, a label, a text box): keep the mouse still briefly and the
  float icon appears next to the cursor, exactly as if the text had
  been selected.
- **Every icon action works on the hovered block:** hover the icon =
  translate (panel opens with the block's text); click = Speak or Fast
  Read per "Click icon for:"; Alt+click = the other action; right-click
  menu = Translate / Listen / Fast Read / Settings. The icon's
  translate-first face is unchanged (see `FLOAT_ICON.md`).
- **Settings → General → "Show icon when hovering over text"**
  (key `HoverBlockText`, default ON), right under "Watch selection
  automatically". Turning it off restores selection-only behaviour.

## How it works — research point by point

- **Module:** `src/TransPeek/BlockText/BlockTextHoverService.cs` (UIA
  polling) + `src/TransPeek.Core/BlockText/BlockTextHoverState.cs`
  (pure dwell + rectangle cache, unit-tested). Same stack as
  `SelectionMonitor`: FlaUI UIA3, background thread, every UIA call
  in try/catch, own process excluded. **No tree scan anywhere** — one
  `FromPoint` per dwell session only.
- **Dwell 150 ms (fixed), poll 100 ms.** The research recommends a
  ~150 ms debounce (`speechify-report.md` §11.1 Windows: "only
  hit-test when the mouse has stopped ~100–150 ms"; `recommendation.md`
  §2: "debounce ~150 ms"). Each tick only reads the cursor position
  (`GetCursorPos` — trivial) plus a few integer compares, so polling
  every 100 ms costs nothing measurable; the expensive UIA work happens
  only on a probe. Effective timing: the icon appears ~150–250 ms
  after the mouse stops.
- **Reading the block**, richest source first:
  1. **Paragraph under the cursor** — `TextPattern.RangeFromPoint`
     → `ExpandToEnclosingUnit(Paragraph)` → `GetText(-1)` +
     `GetBoundingRectangles()` (research §11.1: "lấy rect dòng bằng
     `TextPattern.GetBoundingRectangles()`"). This finds the REAL
     paragraph even inside a giant document element (a browser page,
     a Word document — one TextPattern for everything), where
     `DocumentRange` alone would return the whole document for any
     line in it. Providers vary (Word/Edge/Notepad support it;
     Chrome/Electron only partly; some degrade Paragraph up to
     Document) — every step is guarded, failure falls through to (2).
  2. `TextPattern.DocumentRange.GetText(-1)` — the whole element.
  3. `ValuePattern.Value` — single-value controls (text boxes, combos).
  4. `Name` — buttons, links, labels.
  Text not exposed → nothing happens.
- **Rectangle cache:** the cached rect is the paragraph's rect (the
  union of its line rects from step 1) or the element's rect
  (steps 2–4) — so roaming inside the same paragraph re-shows the
  cached text without re-probing, and moving to another paragraph
  probes fresh text for it. A text-less element's rectangle is
  deliberately NOT cached (it can be a whole window, hiding text
  children inside it); the next dwell session probes again.
- **Length cap:** one UIA text element can be a whole document. Hovered
  text is capped at 4000 chars (`MaxHoverTextLength`, cut at a word
  boundary) so a hover never drags a whole document into
  translate/speak.
- **Hiding:** this service never hides the icon. The icon hides on its
  own auto-hide timer (`TriggerIconWindow`, default 5 s, restarted by
  every `ShowAt`) — when the mouse leaves the block, no new show fires
  and the icon fades on its own. While the mouse is over the icon, the
  timer is paused (existing behaviour).
- **Wiring (`App.xaml.cs`):** the hover callback feeds the block's text
  into the same `_lastSelection` / `_lastPoint` + `ShowAt` path as a
  manual selection — that is why all icon actions just work. The
  service is created in `OnLaunched` and rebuilt in `OnSettingsSaved`,
  mirroring the selection monitor (a changed toggle applies).

## Where this build deliberately differs from the research

- **Debounce is FIXED at 150 ms, not `HoverDelayMs` (default 500 ms).**
  The research's ~150 ms and the setting's 500 ms conflict; the
  research wins for this gesture. `HoverDelayMs` governs a different
  interaction — how long to hover the ICON before the PANEL opens —
  and reusing it would make block hover feel laggy (icon half a second
  after the mouse stops). The 150 ms value is a named const
  (`BlockTextHoverState.DefaultDwellMilliseconds`, unit-tested), not a
  magic number. If real-machine testing finds 150 ms too eager, a
  separate setting can be added; the toggle is the off-switch.
- **Default ON, not opt-in.** The research (§11.1) recommends opt-in
  (default OFF) because hover quality varies by app. Manh chose
  default ON (2026-11-21 00:13, both repos: "Settings có tùy chọn,
  MẶC ĐỊNH BẬT") so the feature is visible at once when testing.
  Flipping it back is one line (`HoverBlockText` initializer) if
  testing finds the icon too eager.
- **No >10-word filter.** Speechify's Hover Player only triggers on
  paragraphs longer than 10 words, and excludes buttons/links/headings
  (`speechify-report.md` §3.3) — because its icon means "listen to
  this paragraph". TransPeek's icon means "interact with this text",
  and translate works on any length, so any non-empty exposed text
  shows the icon — a single word, a button label, a paragraph. The
  trade-off is noted under "Test on a real machine" (click
  interference on buttons).
- **Scroll-follow: NOT in v1 (deferred, by design).** The research
  says to poll the rect every ~100–200 ms while the icon shows and
  move the icon with the block (`speechify-report.md` §11.1). In v1
  the icon does NOT follow scroll: after a scroll it sits at the old
  position for at most 5 s (auto-hide), exactly like the selection
  flow's icon — which also never follows scroll. Why deferred: a
  correct follow needs the PARAGRAPH range held across ticks (an
  element rect does not move when content scrolls inside a giant
  document element — the common browser case), plus a position-update
  callback and App-side guards against overwriting a selection; none
  of that can be verified on this Linux VM, and a half-follow that
  works in Notepad but not in Chrome would be worse than none.
  The follow design for the next round: hold the paragraph range,
  re-read its `GetBoundingRectangles` union every ~200 ms while the
  cursor is still, re-fire `ShowAt` at the old point + rect delta
  (gated on "no active selection" both sides), stop on cursor move /
  rect read failure / 5 s cap. The alternative pattern (ENVI,
  `recommendation.md` §4) is to HIDE on scroll instead of following —
  to be decided after real-machine testing, not guessed here.

## Priority rule — selection ALWAYS beats hover

- The probe only runs when the FOCUSED element has **no active text
  selection**, checked at probe time (not from a cached flag). So:
  - user selects text → the selection's icon shows; hovering another
    block while the selection stays active does nothing;
  - user selects text while a hovered icon shows → the selection
    overwrites the hovered text at once;
  - user clears the selection (click elsewhere) → the next dwell can
    hover again.
- **Staleness:** a selection older than 10 s is re-grabbed via the
  clipboard in the Speak / Fast Read / translate paths (it may have
  changed). Hovered text is exempt (`_lastTextIsHover` + `IsLastTextStale`
  in `App.xaml.cs`): the block is still under the cursor, so no
  clipboard re-grab — otherwise a click 10 s after hovering would
  speak the clipboard instead of the block.

## Limits (known, by design)

- **Only exposed text.** Apps that draw their own text without UIA show nothing. (Handling those is a separate research task for a later session — not this one.)
- **Whole-element blocks.** If an app exposes one giant text element with no paragraph support, the hovered "block" is that whole element, capped at 4000 chars.
- **Scroll:** the icon does not follow the block when the page scrolls (see above); it lingers at most 5 s, then hides. Moving the mouse re-probes the block now under the cursor.
- **Admin apps (UIPI):** TransPeek running as a normal user cannot read UI Automation of an app running as administrator — hovering text there shows nothing.
- **Perfectly still mouse.** If the mouse stays exactly still, the icon auto-hides after 5 s and only comes back when the mouse moves (a new dwell session). A tiny wiggle is enough.
- **No test on CI Linux.** UIA needs a real Windows desktop: the polling service is compile-checked by CI only. The dwell/rect rules are unit-tested in `tests/TransPeek.Tests/BlockTextHoverStateTests.cs` (9 tests).

## How to test / spike on a real Windows machine

The spike the plan called for (measure CPU + UIA latency + block
accuracy on a real machine) could NOT run on this Linux VM — these are
the items Manh needs to measure / check live:

1. Open Notepad, type two paragraphs; rest the mouse on a paragraph
   ~1 s → icon appears next to the cursor; hover the icon → panel
   translates that paragraph; click → listens to it.
2. Select some text in the same file first, then hover another
   paragraph without clearing the selection → icon must NOT switch to
   the hovered paragraph (selection wins); click elsewhere to clear,
   hover again → icon follows the hover.
3. Rest on a button / a link label (e.g. in Settings) → icon appears
   with the short label text. **Check click interference:** hover a
   button, wait for the icon, then click the button — does the click
   still hit the button (icon sits 8 px down-right of the cursor), or
   does the icon get in the way?
4. Rest on an empty area / a picture → nothing appears.
5. Settings → General → turn "Show icon when hovering over text" OFF
   → hovering shows nothing; selecting still shows the icon.
6. Hover a block, wait 10+ s, click the icon to Listen → it must read
   the hovered block, NOT the clipboard.
7. **CPU:** open Task Manager while moving the mouse over a long page
   in Edge/Chrome for a minute — TransPeek CPU must stay ~0% while
   idle (the only per-tick work is `GetCursorPos`; UIA probes fire
   once per dwell session).
8. **Block accuracy (the paragraph path):** in Edge/Chrome, hover
   different paragraphs of one long article — each hover must show
   THAT paragraph, not the article's start (which would mean the
   provider degraded Paragraph → Document and the 4000-char cap is
   doing the work). Try the same in Word and Notepad.
9. **Scroll:** hover a paragraph, icon shows, scroll the page with the
   wheel — the icon stays up to 5 s at the old spot, then hides. Note
   whether that feels wrong enough to want the follow (next round) or
   the hide-on-scroll alternative.
10. **Latency feel:** is 150 ms too eager (icon popping up while just
    reading with the mouse resting on text)? If yes, note it — the fix
    is a separate dwell setting, not a code change to the probe.
