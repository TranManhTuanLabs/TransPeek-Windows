# Decisions — TransPeek Windows v0.1

Every locked decision, with the reason. New decisions append here;
nothing is removed, only superseded (note the date).

## Stack

- **WinUI 3 + C#, Windows App SDK 1.8.12 (.NET 8)** — native Windows
  UI, direct access to UI Automation / Windows.Media.Ocr /
  SpeechSynthesis-class APIs, ~10–20 MB app. Alternatives rejected:
  Electron (~150 MB), Flutter (UIA/hotkey plugins weak), Python
  (packaging pain), Tauri (lightest but Rust+JS to maintain and
  weaker panel/accessibility). WPF + C# is the fallback if WinUI 3
  blocks packaging.
- **Windows App SDK 1.8.x, not 2.x** (2026-11-08): 2.x exists (2.6.5,
  Nov 2 2026) but refactors packaging (metapackage). Upgrade is a
  deliberate follow-up, not a silent bump.
- **Target `net8.0-windows10.0.19041.0`, min `10.0.17763.0`**
  (Windows 10 1809+ / Windows 11).

## Packaging

- **Unpackaged (no MSIX) for v0.1** (2026-11-08): Manh runs the exe
  directly to test. Self-contained Windows App SDK runtime ships with
  the app. Trade-off: no Store, no auto-update, no package identity
  (so system toasts are unreliable — see below). Packaged/MSIX at
  release time.
- **No binary assets in v0.1**: the GitHub connector cannot push
  binaries. The tray icon is an embedded base64 PNG
  (`Assets/EmbeddedIcon.cs`); the real `.ico` + manifest logos come
  with packaging. The manifest still references logo files that do
  not exist yet — harmless for the unpackaged build, must be fixed
  when packaging.

## Architecture

- **`TransPeek.Core` (`net8.0`, platform-neutral)** — providers,
  coordinator, dictionary links, favicons, OCR interfaces +
  Tesseract, settings, update check. No WinUI/WinRT, so it builds
  and tests anywhere (including Linux CI). **`TransPeek` app** —
  windows, tray, hotkeys, UIA, capture, Windows OCR. **`TransPeek.Tests`**
  (xUnit) — Core logic only.
- **OCR interface takes PNG bytes** (platform-neutral), so the
  Tesseract provider can live in Core and only the Windows.Media.Ocr
  provider needs WinRT.

## Selection & panel

- **UIA `TextPattern.GetSelection` (FlaUI.UIA3) is the primary
  selection source**, polled every ~300 ms. **Clipboard Ctrl+C is the
  fallback, user-initiated only** (hotkey / tray menu): simulating
  Ctrl+C unprompted would clobber the clipboard; the helper restores
  the previous clipboard content afterwards. (MoePeek's 3-layer
  approach has no AppleScript layer on Windows.)
- **Click-outside-to-dismiss via a low-level mouse hook
  (WH_MOUSE_LL)** — WinUI has no built-in event for a no-activate
  tool window.
- **Toast is a custom in-app window**, NOT AppNotifications:
  unpackaged apps have no app identity, so system toasts are
  unreliable. Same approach as the mac app's CopyToast.
- **Hotkeys**: translate panel `Ctrl+Alt+T`, OCR `Ctrl+Shift+S`,
  both changeable in Settings. The hotkey service runs a message-only
  window on its own thread; callbacks marshal to the UI queue.
- **Launch at login via the registry Run key**
  (HKCU\…\CurrentVersion\Run) — the standard approach for unpackaged
  apps, no admin rights needed.

## Translation

- **Google Translate (free GTX endpoint) is the default**, plus
  **custom OpenAI-compatible endpoints** (user key/endpoint).
  **No Apple Translation** — it does not exist on Windows.
- **Fallback order**: primary → fallback → other enabled →
  Google (free) safety net last. The coordinator reports the FIRST
  provider's error when all fail (the one the user actually picked).
- **API keys in plain text in `settings.json` (v0.1 debt)** —
  documented in README + Settings. Credential Locker (PasswordVault)
  is the post-v0.1 fix; the mac app uses the Keychain.

## Dictionary

- **Mirror of mac Phase 1**: same 5 defaults (Oxford / Cambridge /
  Cambridge Vi / Google Dictionary / YouGlish), same `{word}` / `{text}`
  templates, same validation rules, Icon+Text / Icon-only. Cambridge Vi
  (english-vietnamese) added 2026-11-12, last in the list, mac parity.
- **Favicon chain**: `<host>/favicon.ico` → Google s2 → DuckDuckGo,
  disk cache `%LOCALAPPDATA%\TransPeek\Favicons`, magic-byte image
  check, generic letter tile on failure. Loading is async and never
  blocks the panel.

## OCR

- **Tesseract is the DEFAULT for Vietnamese** (NuGet wrapper 5.2.0,
  tessdata_best downloaded on demand into ApplicationData, language
  manager in Settings). Reason: Windows.Media.Ocr's Vietnamese
  quality depends on the "Vietnamese OCR" optional feature, which
  most machines do not have; Tesseract brings its own data.
- **Windows.Media.Ocr is the fallback** — built in, offline, no
  install; on Tesseract failure the flow retries once with it.
- **Capture: Windows.Graphics.Capture first, GDI BitBlt fallback.**
  GraphicsCapture is modern but has several WinRT/D3D interop steps;
  BitBlt is old, dependency-free, and almost always works for text.
- **Region picker covers the display containing the cursor**
  (single-display v0.1); coordinates are converted from effective
  to physical pixels by the window's DPI factor.

## About & updates

- **About page**: app name + running version/build (from the
  assembly `<Version>`, also the release-notes tag), author line with
  GitHub icon + link to `TranManhTuanLabs/TransPeek-Windows`,
  release-notes link to `releases/tag/vX.Y.Z` of the running version.
- **Update check: SIMPLE CHECK ONLY (Manh's decision, 2026-11-08)**.
  The **Check for Updates** button calls the GitHub Releases API
  (`releases/latest`), compares the latest tag with the running
  version, and — when a newer release exists — shows a link that
  opens the release page in the browser. The user downloads and
  installs by hand. **No updater library, no auto-download, no
  auto-install.** No network → gentle error, never a crash; no
  releases published yet → "Chưa có bản release nào trên GitHub."
  **Velopack (auto download + install + relaunch) is deferred to
  release time**, when there is a real release pipeline.

## License

- **AGPL-3.0, same as the mac edition** (fork of MoePeek), with
  attribution to cosZone/MoePeek in the README. The Windows edition
  is rewritten from scratch in C# — it mirrors design/behaviour and
  copies no Swift code.

## v0.1.2 — Panel ngang mac, batch A (2026-11-11)

Đối chiếu mac hiện tại (PopupView / DictionaryBarView / DictionaryLink /
LanguageBarView / Settings) với Windows, làm trước nhóm panel + từ điển:
- **Tra từ theo text chọn trong panel (Gap 1):** bản dịch đổi từ
  `TextBlock` sang `TextBox` read-only để chọn được; `SelectionChanged`
  ở cả ô nguồn và bản dịch đổi `_lookupText`, bỏ chọn quay về text gốc.
- **Link mặc định khớp mac (Gap 5):** Oxford thêm `?q={word}`, tên
  "Google Dictionary" → "Google", YouGlish `{word}` → `{text}`; `{word}`
  viết thường trước khi encode (Oxford/Cambridge phân biệt hoa-thường);
  `MigrateDefaults` nâng cấp 1 lần link cũ đã lưu, `FreshDefaults` để
  sửa link mặc định không làm hỏng `Defaults` dùng chung.
- **Ô nhập sửa được + Enter dịch lại (Gap 2), picker ngôn ngữ + ⇄
  trong panel (Gap 3):** đổi picker lưu setting và dịch lại ngay.
- **DictionaryBar (Gap 6):** cuộn ngang, tooltip tên link ở chế độ chỉ
  icon, không text tra thì không hiện nút.
- **Dictionary Settings (Gap 11):** nút Reset to Defaults, thêm xong
  xóa trắng ô nhập. **OCR Settings (Gap 12 nhỏ):** khóa toggle tự copy
  khi chế độ CopyOnly.
- **Chưa làm, để đợt sau:** card từng provider + Retry (Gap 4, đụng
  coordinator, tốn công nhất); kéo/resize/nhớ vị trí/pin/nút Settings
  trong panel (Gap 7); tab Excluded Apps/Order, Favorite languages,
  thêm phím tắt (Gap 8–10); OCR chọn ngôn ngữ bằng checkbox + About
  Build CI # (Gap 12–13). TTS vẫn ngoài phạm vi (chờ mac ổn, SPEC §5).

## v0.1.1 — OCR overlay trap + no visible menu (2026-11-11, Manh live test)

- **OCR overlay trapped the user fullscreen.** Root cause: Esc was
  only a `KeyboardAccelerator` on the root `Grid`, and a `Grid`
  never takes keyboard focus, so Esc never fired; there was no
  Cancel button; `Finish()` only hid the window, so Alt+F4 left the
  selection `TaskCompletionSource` uncompleted and the OCR flow hung
  forever. Fix (`RegionSelectWindow`): grid is focusable and focused
  on show/activate, `KeyDown` handles Esc directly (accelerator kept
  as backup), `RightTapped` handles right-click, a visible
  "✕ Hủy (Esc)" button, `Closed` completes the task, `Finish()`
  closes the window exactly once (guarded). Same `KeyDown` backup
  added to the translate panel's Esc path.
- **App showed no menu/UI at all.** Root causes: tray-only design
  + Windows 11 hides new tray icons in the overflow, so v0.1 looked
  like it never started; and `SettingsWindow` was a single instance
  that WinUI destroys on close, so `Activate()` on the dead window
  silently did nothing on reopen. Fix (`App`, `SettingsWindow`):
  Settings opens centered (960×640) on launch plus a startup toast
  explaining the tray; App drops the `SettingsWindow` reference on
  `Closed` and rebuilds it next time. Tray menu labels are now
  Vietnamese, with a tooltip explaining left-click = Settings,
  right-click = menu.

## Baseline Windows = mac main CI #84–#93 (2026-11-11, Manh)

- **Reference pinned:** mac main at CI runs #84–#93 (head 5530bb98,
  local copy at 3779269 — only the #93 workflow-only commit is
  missing, no feature impact). Runs #85–#93 are all red/cancelled
  (CI capacity + signing), but their code is on main; the only
  feature batch in the range is #84 (313847f1).
- **Baseline scope (in):** select text → trigger icon/panel → translate
  → dictionary lookup follows the panel selection; region OCR →
  panel (OpenPanel) or copy-only; hotkeys (Ctrl+Alt+T translate,
  Ctrl+Shift+S OCR); tray menu + Settings open on launch. Batch A
  (v0.1.2) + the two OCR fixes below bring Windows to this baseline.
- **Mac #84 extras — deferred as advanced (not baseline):** Scan
  Barcodes/QR (Vision on mac; Windows.Media.Ocr has no barcode API —
  needs ZXing/ML Kit equivalent), Screenshot Copy (region capture
  straight to clipboard), Clipboard OCR (OCR an image already in the
  clipboard), panel top/bottom bars (Gap 7), menubar section headlines,
  CI build number in the header (Gap 13). Manh: advanced features
  come after baseline.
- **OCR must stay on the UI thread (`OcrFlowService.RunOcrAsync`):**
  every entry point starts on the UI thread (tray click, hotkey via
  the UI queue, panel button), but the flow used `ConfigureAwait(false)`
  on every await, so after the first await its continuations ran on
  thread-pool threads — and then it touched the panel, the toast and
  the clipboard, which throw off-thread. Fix: no `ConfigureAwait(false)`
  anywhere in `RunOcrAsync`; the heavy work already runs off-thread
  inside the providers. Found by code audit 2026-11-11; never live-tested
  before (v0.1 OCR testing stopped at the overlay trap).
- **First-run Tesseract data auto-download:** Tesseract is the default
  OCR provider for Vietnamese but its `*.traineddata` files are NOT
  bundled. On a fresh install the registry silently fell back to
  Windows OCR (English-only on most machines), so Vietnamese OCR came
  back as garbage or nothing. Fix: `RunOcrAsync` downloads the missing
  codes for the configured languages before resolving the provider
  (toast shows progress); a failed download falls back to Windows OCR
  with an explanatory toast. Pure part
  (`TesseractLanguageDataManager.MissingCodes`) is unit-tested; the
  download itself is live-tested.
- **Google GTX endpoint check (2026-11-11):** a curl from this Linux VM
  got Google's HTML "Sorry..." block page instead of JSON — datacenter
  IPs are blocked/rate-limited for the unofficial endpoint. NOT treated
  as breakage: the mac app uses the same endpoint and translates fine
  from Manh's network. Re-check from a real user network if translation
  ever fails on Windows.

## UI language: English base, Vietnamese later (2026-11-11, Manh)

- **Decision (Manh): English is the base UI language; Vietnamese comes
  later, then more languages if needed (planned next: zh-Hans Chinese
  Simplified, ko Korean).** Background: the app shipped Vietnamese-only
  because Muse set it without asking — Manh flagged it. The mac app
  already uses English as its base (`Localizable.xcstrings`,
  `sourceLanguage: en`, 319 strings, no translations yet), so Windows
  follows the same direction.
- **How it works (WinUI standard):** `Strings/en-US/Resources.resw`
  holds every user-visible string (105 keys); XAML reads them through
  `x:Uid` (resw name = `Uid.Property`) with the English literal kept in
  the markup as the fallback; code reads them through
  `TransPeek.Services.AppStrings.Get(key, fallback)` with the same
  English text as the fallback. A lookup never shows a blank: a missing
  key or a missing `.pri` falls back to English.
- **Adding a language later = adding one file:**
  `Strings/<bcp47>/Resources.resw` with the same keys. No code or XAML
  change. A manual language picker in Settings is deferred to the first
  translation (until then the app follows the system language, which is
  English on Manh's machine — and the base file is English anyway).
- **When to add languages (Manh, 2026-11-11): only once the app is
  final.** Adding Vietnamese / Chinese / Korean before the UI settles
  would mean re-translating every changed string after each UI change,
  so translations wait until the app is complete. Applies to mac too.
- **Core stays English-only literals:** `TransPeek.Core` has no resw
  (it is UI-framework-free and unit-tested on Linux). Its user-facing
  messages (`UpdateChecker`, `DictionaryLink.ValidationError`,
  `TesseractOcrProvider.Status`) are plain English literals. The WinUI
  layer owns localization.
- **Not localized (deliberately):** language endonyms in the language
  picker ("Tiếng Việt", "中文 (简体)", "한국어", … — a language is always
  shown in its own name), provider/dictionary proper nouns ("Google
  Translate", "Oxford"), developer comments, and test data.
- **Version:** 0.1.3.0. Verified by script before push: resw XML valid,
  no duplicate keys, every `x:Uid` property and every `AppStrings.Get`
  key exists in the resw, no key unreferenced, no Vietnamese left in UI
  string literals (comments + endonyms only).

## Tray menu mirrors the mac menubar (2026-11-11, Manh)

- **Decision (Manh): make the tray menu match the mac menubar design,
  full features per the menu — except Check for Updates (discuss later)
  and Smart Translation (deferred: Manh does not yet know how that
  feature behaves).** Menu now: header "TransPeek {version}" + section
  "Text Capture from Screen" (Manual Translation, Screenshot OCR,
  Clipboard OCR, Selection Translation, Clipboard Translation,
  Screenshot Copy) + section "Barcodes / QR Codes" (Scan Barcodes/QR
  Codes) + Settings… + Quit TransPeek. Icons from Segoe Fluent Icons
  (glyph codes verified against Microsoft's list).
- **Semantics mirror mac exactly:** Manual Translation = empty panel at
  screen center, focused for typing; Screenshot OCR = region → OCR →
  translate → panel (existing flow); Clipboard OCR = region → OCR →
  copy text + "Copied" toast, ignores the post-OCR mode setting;
  Selection Translation = current selection → panel; Clipboard
  Translation = clipboard text → panel; Screenshot Copy = region →
  the IMAGE to the clipboard (NOT OCR text — mac uses
  `screencapture -i -c`); Scan Barcodes/QR = region → ZXing.Net decode,
  URL opens in the browser, anything else copies with an 80-char toast
  preview, none found toasts "No barcode found".
- **Permissions Guide is skipped, deliberately:** Windows never asks
  for the Accessibility / Screen Recording grants the mac guide walks
  through — those permissions are mac-only, so there is nothing to
  guide the user to grant.
- **Bug fixed on the way:** `<Version>` in TransPeek.csproj was still
  0.1.0 while Package.appxmanifest said 0.1.3.0 — About showed 0.1.0
  (AppInfo.Version reads the assembly version). Bumped to 0.1.3; the
  tray header uses the same AppInfo.Version.
- **Tests:** BarcodePayload routing/preview are pure Core logic with
  xUnit tests (runs on Linux CI). The capture/decode flows are WinRT
  and need Manh's live test.

## Tray header shows the CI build number (2026-11-11, Manh)

- **Decision (Manh): the tray header shows the CI build number like
  the mac menubar** — "TransPeek 0.1.3 (36)", where 36 is the GitHub
  Actions run number of the build that produced the running app.
- **How:** `build.yml` stamps it at build time with
  `-p:Version=0.1.3.${{ github.run_number }}` on both the Build and
  the Publish steps, so the assembly version becomes 0.1.3.36.
  `AppInfo.CiBuildNumber` reads the 4th version part (0 for local
  builds, where the csproj `<Version>` has no 4th part — the header
  then shows no number, as before). The About page shows
  "Version 0.1.3 (build 36)" from the same value.
- **Local copy of build.yml is STALE** (predates the browser commit
  8d912bb that added paths-ignore + [build-app] gating + concurrency):
  never push the local file — workflow edits go through the browser
  on the GitHub-side file.

## Screenshot Copy: app capture first, system Snipping Tool as fallback (2026-11-11, Manh)

- **Decision (Manh, confirmed final 06:49): app capture is primary, the
  system tool is the fallback.** Screenshot Copy first tries the app's
  own region select + capture (same on Win10/Win11, proper
  cancel/error/toast). Only when that capture throws does it open the
  system Snipping Tool via the `ms-screenclip:` URI (Snip & Sketch on
  Win10, Snipping Tool on Win11) and toast that the screenshot will be
  copied to the clipboard — the app gets no completion callback from
  the system tool, so the toast states what will happen instead of
  claiming "Copied".
- **Why not system-first (Manh asked 06:44, rejected 06:49):** the
  reverse order is not a real fallback. Once the app launches
  `ms-screenclip:` it gets no signal back — it cannot tell whether the
  user snipped, cancelled, or has auto-copy-to-clipboard switched off
  — so it would never know when to step in with its own capture. The
  only detectable failure is the URI failing to launch at all
  (Snipping Tool uninstalled). A clipboard-watch workaround (wait for
  a new image in the clipboard, with a timeout) was considered and
  rejected as fragile: arbitrary timeout, false positives if the user
  copies something else meanwhile. The app's own capture is already
  built, deterministic, identical on Win10/Win11, gives proper
  cancel/error feedback, and shares the region overlay with the OCR
  flows.
- **Why not system-first (other reasons):** the system tool differs
  between Win10 and Win11, depends on a user setting (auto-copy to
  clipboard), and reports nothing back (no cancel/success signal).
  PowerToys was considered and ruled out as a capture backend —
  correction (2026-11-11, Manh pointed it out): PowerToys DOES have
  OCR, Text Extractor (Win+Shift+T, region select -> recognized text
  to the clipboard). It still cannot serve Screenshot Copy: it outputs
  TEXT only, never the image, and there is no URI or API for TransPeek
  to invoke it — the user triggers it by hotkey. PowerToys has no
  screenshot/image-capture tool of its own (the "capture" side is the
  Snipping Tool, which is already the fallback here).
- **Scope:** fallback applies to Screenshot Copy only. The OCR flows
  need the PNG bytes inside the app, so the system tool cannot feed
  them.
- **Shipped:** commit `c1b2eb9` (fallback) + `939eb73` `[build-app]`,
  run #40 SUCCESS, artifact 126 MB, app version 0.1.3.40.

## Startup crash 0xc000027b in Microsoft.UI.Xaml.dll (run #40, 2026-11-12)

- **Symptom (Manh, 12/11 15:25):** build #40 (0.1.3.40) shows the
  Settings panel, then exits at once. PowerShell prints no error;
  Task Manager shows the process appear and vanish.
- **Log (Event Viewer, pasted in Google Doc "LOG TransSpeek TEST"):**
  Application Error, Event 1000, 15:24:55 — faulting module
  `Microsoft.UI.Xaml.dll` 3.1.8.0, exception `0xc000027b`
  (STATUS_STOWED_EXCEPTION: a WinRT exception escaped through WinUI),
  fault offset `0x3a7515`. WER Event 1001 attributes the same crash to
  `combase.dll`, exception `80004002` (E_NOINTERFACE). **No ".NET
  Runtime" event and no stack trace anywhere** — the crash is a WinRT
  exception crossing the ABI boundary, not a managed exception with a
  message. The faulting module is WinUI itself, so the log alone does
  not say which app call threw.
- **Fix (first step): crash trap + crash log.** The app had NO
  `UnhandledException` handler, so any unhandled error killed it
  silently. `App` now subscribes to `Application.UnhandledException`
  (writes type/message/stack trace to
  `%LOCALAPPDATA%\TransPeek\crash.log` via `Services/CrashLog.cs` and
  marks it handled, so a stowed exception no longer kills the app),
  plus `AppDomain.UnhandledException` and
  `TaskScheduler.UnobservedTaskException` (log-only). Next run: if it
  crashes again, the log file names the exact throwing call; if the
  handler catches it, the app stays alive and the log still tells us
  what threw.
- **Reading the test log doc:** do NOT use the live browser for the
  "LOG TransSpeek TEST" Google Doc — `browser.open` on the edit URL
  fails ("browser version no longer supported") and a browser task
  takes ~2 min and returns a huge report. Use the export URL instead:
  `https://docs.google.com/document/d/<id>/export?format=txt` —
  one fast text fetch, full plain-text content, far fewer tokens.

## Startup crash persists in 0.1.3.42 — now CoreMessagingXP.dll (2026-11-12)

- **New log (Event Viewer, doc "LOG TransSpeek TEST", read via
  downloaded export + grep):** Manh ran 0.1.3.42 twice (16:32:29 and
  16:33:41), crashed twice the same way. Application Error, Event
  1000 — faulting module now `CoreMessagingXP.dll` 10.0.27108.1058
  (offset `0x93bd6`), exception still `0xc000027b`; WER still
  `combase.dll` + `80004002` (E_NOINTERFACE). (Build #40 faulted in
  `Microsoft.UI.Xaml.dll`; the module changed, the exception did not.)
- **What this rules out:** 0.1.3.42 contains BOTH the
  `UnhandledException` trap (commit `73b887d`) and the
  ShowSettings/toast try/catch (commit `f34798`) — and the app still
  died. A managed exception reaching the WinUI unhandled path would
  have been caught (and the app would have survived). So this crash
  never passes through managed code: it is a native WinRT/COM failure
  (E_NOINTERFACE stowed through the CoreMessaging pump). No
  `crash.log` content appears in the doc — check whether
  `%LOCALAPPDATA%\TransPeek\crash.log` exists at all: an empty/missing
  file confirms the fully-native crash.
- **Next step:** get the crash.log verdict from Manh. If missing:
  bisect by disabling startup suspects one at a time (tray icon
  ForceCreate, hotkey thread, selection monitor, hidden window, toast
  presenter calls) or analyze the WER minidump in the ReportArchive.
- **Correction:** run #42 is `f34798` (0.1.3.42), run #43 is the
  workflow commit `0a82b86` (0.1.3.43). An earlier note said #42 was
  0.1.3.41 — wrong, off by one.
- **Auto pre-release VERIFIED (same day):** run #43 created public
  pre-release `v0.1.3.43` with `TransPeek-Windows-0.1.3.43.zip`
  (132,278,940 B — Compress-Archive size, differs from the artifact
  zip of the same code). Download needs no login. Manh's manual
  pre-release `0.0.1` was NOT deleted by the cleanup step (its tag
  does not match `v0.1.3.<number>`). Note: 0.1.3.43 has identical app
  code to 0.1.3.42 (only build.yml changed), so it crashes the same
  way — no need to test it for the crash.
- **Reading the log doc (Manh's rule, 12/11):** download the export
  to a file and grep it — `curl -sL -A "<browser UA>"
  .../export?format=txt -o /tmp/transpeek-log.txt` (plain curl returns
  empty). Do NOT dump the whole doc into context and do NOT use the
  live browser.

## 0.1.3.49 test feedback (2026-11-12)

- **Quit**: `Application.Exit()` did not terminate the process on this
  machine (tray Quit left 0.1.3.47/.49 running, so the user could not
  tell the builds apart and feared overlapping instances). Quit now
  disposes the tray icon (no ghost icon) and calls `Environment.Exit(0)`.
- **Tray menu actions swallowed**: showing a window (Settings, panel)
  synchronously from a `MenuFlyoutItem.Click` is swallowed while the
  flyout is dismissing — the Settings item did nothing from the menu
  (left-click on the icon worked), same for the panel actions. Every
  menu action now runs on the next dispatcher tick, after the flyout
  has closed. Menu clicks also log to crash.log on failure.
- **Click-outside stale hide**: a hide enqueued by the click that opened
  the panel (tray menu mouse-down) ran after the panel re-showed and
  killed it. The monitor now captures a show-epoch at click time and
  only hides when the epoch is unchanged.
- **Ctrl+V in the panel**: `ShowWithoutActivation` sets `WS_EX_NOACTIVATE`
  permanently on the window style, so keyboard shortcuts never reach
  the source box (mouse paste worked, keyboard paste did not). A click
  into the panel now clears the flag and activates the window
  (`ActivateWindow`); Manual Translation activates on show. The panel
  still does not steal focus when it first appears.
- **Tray shortcut hints**: the two hotkey actions show their shortcut
  dimmed on the right (`KeyboardAcceleratorTextOverride`, refreshed on
  every menu open): Clipboard Translation = translate hotkey,
  Screenshot OCR = OCR hotkey.
- **Full shortcut set (DEFERRED, Manh 2026-11-12)**: when the app runs
  all tray-menu features, add a hotkey for every menu action in
  Settings. Currently only 2 shortcuts exist (translate + OCR).
- **Panel chrome**: top bar has the app name + slogan (from `AppInfo`);
  footer has the GitHub mark + GitHub link (repo URL) + dimmed build
  number. The mark is the official GitHub-Mark.png embedded as base64
  (`Assets/GitHubMark.cs`), same text-only trick as the app icon; the
  About page shows the same mark + GitHub + repo link.
- **Dictionary bar scrolling**: the horizontal ScrollViewer ignored the
  mouse wheel — wheel now scrolls horizontally; mouse drag pans like a
  touch swipe (plain click still opens the link).

## 0.1.3.51 test feedback (2026-11-12, evening)

Manh killed the stuck 47/49 instances (`taskkill /IM TransPeek.exe /F`)
and ran 51. New round of fixes (next build):

- **Tray DEAD in 51 — regression under investigation**: left-click on
  the icon does nothing, and NO right-click menu item responds
  (including Quit — the `Environment.Exit(0)` fix from 51 did not get
  to prove itself). Two suspects, both removed/neutralised: (1) the
  `menu.Opening` handler (refreshing shortcut hints on every open)
  — hints are now set once at construction and refreshed by
  `TrayIconService.RefreshAccelerators()` when Settings are saved;
  (2) the dispatcher-deferred Click — now falls back to a direct call
  if the dispatcher is null or `TryEnqueue` fails, and the try/catch
  moved into `RunMenuAction` (logged to crash.log). If 52 is still
  dead, read crash.log + count TransPeek.exe processes in Task
  Manager: a ghost icon (leftover from a taskkill'ed process, cleared
  by hovering over it) looks identical to the live one, and a new
  build's icon starts HIDDEN in the overflow (^) on Windows 11 — the
  menu header shows the build number ("TransPeek 0.1.3 (52)") so the
  live icon can be identified.
- **Full shortcut set — DONE (reverses the deferral above)**: one
  global hotkey per tray menu action, all in Settings > General, all
  `Control+Alt+letter` (free on Windows): Manual Translation M,
  Clipboard OCR C, Selection Translation S, Screenshot Copy P, Scan
  Barcodes B, Settings O; the originals stay (translate Ctrl+Alt+T,
  OCR Ctrl+Shift+S). No hotkey for Quit (accidental quit). Settings
  rejects a chord already used by another action (Win32 silently
  drops the duplicate registration); defaults covered by
  `AppSettingsHotkeyTests`.
- **Panel drag**: the title bar is the drag handle — `PointerPressed`
  calls `WindowHelpers.BeginDrag` (`SendMessage(WM_NCLBUTTONDOWN,
  HTCAPTION)`), after activating the window. The panel has no system
  title bar, so without this it cannot be moved at all.
- **Panel top bar**: app name centred, slogan on the line below.
  **Footer**: GitHub mark + GitHub centred, build number right.
  **Close (X) removed**: Esc + click-outside already close the
  panel (hint text now says "Esc: close").
- **Inline copy button removed** from the input row: the actions row
  already has Copy source. **Settings gear (E713)** added at the
  right end of the actions row — opens Settings (panel constructor
  takes an `openSettings` callback from App).
- **Dictionary bar scrollbar**: WinUI scrollbars overlay content, so
  the bar covered the bottom half of the links. Fixed with bottom
  padding (16px) on the scrollable content — the buttons sit fully
  above the bar.
- **About**: app icon (48px, decoded from the embedded ICO's largest
  frame via `EmbeddedIcon.Image()`) + "TransPeek" + slogan below;
  repo link text is now "Github.com/TranManhTuanLabs" (still opens
  the repo URL); the separate "Version … (build …)" line is gone;
  the release link is now "Release Ver 0.1.3 (Build CI #51)" and
  opens the releases LIST page (`AppInfo.ReleasesUrl`, replaces the
  per-tag `ReleaseNotesUrl`) so all releases are visible.
- **OCR still unverified**: Manh reports it "still the same". The
  51 overlay fix (15% dim + Win32 monitor bounds) was never actually
  exercised — the dead tray menu meant OCR could not be triggered
  from the tray. After the tray works, test OCR via the tray AND via
  Ctrl+Shift+S; if the overlay is still opaque grey, the next step
  is a screenshot-background overlay (capture first, show it behind
  the selection rect), because a WinUI window surface may be opaque
  regardless of the XAML background.

## 0.1.3.53 test feedback, round 2 (2026-11-12, evening)

- **Panel drag — no traditional title bar needed.** A plain click on
  the name/slogan moved the panel because `OnTitleBarPointerPressed`
  started the system drag on every press. Fixed with a 4px threshold:
  press records the start point, the drag starts only after hold +
  move past 4px. A borderless floating panel drags from its top bar
  exactly like a normal window drags from its title bar; min/max/
  close buttons are a separate UI choice (the panel closes via Esc /
  click-outside) and are not required to make dragging work.
- **Settings gear dead in 3 of 4 panel paths — root cause: mid-click
  activation.** The panel's root `PointerPressed` handler called
  `ActivateWindow` (style change + `window.Activate()`) between the
  mouse-down and the mouse-up, which swallowed the Button's Click.
  It worked only in Manual Translation, where the panel was already
  active. Fixed by activating on `PointerReleased` instead (via
  `AddHandler(..., handledEventsToo: true)` — the Button's Click
  fires during its own release handling, before the event bubbles
  to the root, so release-activation runs after the Click). The
  title-bar drag keeps its own activation on press (a drag is not a
  button click, nothing to swallow).
- **Settings opened behind the topmost panel.** The panel is always
  `HWND_TOPMOST`, so a Settings window opened from it was covered.
  `OnOpenSettings` now hides the panel first. Separately, tray
  left-click while Settings sat behind other windows did not focus
  it: `Activate()` alone does not restore a minimized window.
  `SettingsWindow.ShowSettings()` now restores if minimized
  (`IsIconic` + `SW_RESTORE`) and calls `SetForegroundWindow` before
  `Activate()` (the tray click is user input aimed at this app, so
  the foreground grant applies).
- **Dictionary bar scroll — `+=` handlers never fired.** A press on a
  link button is marked handled by the Button, so a plain `+=`
  handler on the parent ScrollViewer never saw it (same for the
  wheel: the ScrollViewer's own handling marks it handled first).
  Fixed with `AddHandler(..., handledEventsToo: true)` for wheel /
  press / move / release / cancel, and NO pointer capture (capturing
  to the ScrollViewer would steal the release from the Button and
  break plain clicks). A drag is recognised by movement alone; the
  link open is suppressed by checking `_dragMoved` in the Button's
  Click handler — the Click fires before EndDrag resets the flag, so
  setting `e.Handled` in EndDrag would be too late.
- **OCR grey — diagnostic, not a fix yet.** After a successful
  capture, the flow now saves the captured image to
  `%LOCALAPPDATA%\TransPeek\ocr-capture.png` and logs brightness
  mean/stddev to crash.log (`OcrCapture`). stddev ~0 = a flat grey
  capture (capture pipeline broken); a wide spread = the capture is
  fine and the grey is the overlay (next step then: a
  screenshot-background overlay). `CrashLog` gained a `WriteLine`
  (plain message) overload for diagnostics.
- **Tray right-click menu "useless" — not yet diagnosed.** Every menu
  Click is now logged to crash.log (`TrayMenu: Clicked '<key>'`),
  so the next test shows whether Click fires at all. Manh: after
  testing, send crash.log — if a click is logged but nothing
  happens, the action is the problem; if nothing is logged, the
  Click never fires (flyout/input problem).
- **OCR providers (decided, already implemented):** Windows itself
  gives screenshot copy (Snipping Tool, Win+Shift+S) and, with
  PowerToys, text copy (Text Extractor, Win+Shift+T) — both are
  Windows.Media.Ocr underneath, and Vietnamese needs the optional
  "Vietnamese OCR" language pack that most machines lack. TransPeek
  does NOT write its own OCR engine (multi-year ML work); it embeds
  Tesseract (open source) as the default — the engine ships with the
  app, language data downloads once on first run — and falls back to
  Windows OCR automatically when Tesseract fails. No PowerToys or
  language pack needed. Screenshot Copy (image to clipboard) and
  Screenshot OCR (text from screen) stay two separate features.

## 0.1.3.54 test feedback (2026-11-12, evening)

- **Dictionary bar scroll works now** (wheel + click-drag on links):
  the `AddHandler(handledEventsToo: true)` fix from round 2 is
  confirmed. No further action.
- **OCR capture IS a flat grey board — root cause found.** The
  OcrCapture diagnostic proved it: mean 222.2 stddev 2.9, mean 222.4
  stddev 4.2, mean 255.0 stddev 0.0. Mean 222 is exactly white +
  15% black dim + 13% white selection fill — the capture photographs
  the overlay window itself, not the desktop. Two causes: (a) the
  RegionSelectWindow renders OPAQUE (the Window's default background
  is solid, the semi-transparent dim just darkens that to grey — the
  user cannot see the desktop either, which is the 0.1.3.47 "grey
  frame, no content" report; he was picking regions blind), and (b)
  `Finish()` completes the selection task BEFORE `Close()`, so the
  capture starts while the overlay is still up. Fix (screenshot-
  background, the approach Snipping Tool / PowerToys use): App
  captures the display BEFORE showing the overlay, the overlay
  shows that screenshot as its background (so the user sees the real
  desktop dimmed), and the region is cropped out of the same
  screenshot (`ImageEncoding.CropPngAsync` + `RegionSelection`
  record carrying rect + screenshot + display bounds). The live
  region capture stays only as a fallback when the display capture
  fails. Applies to all three region flows (OCR, Screenshot Copy,
  Barcode scan — they share `SelectRegionAsync`).
- **Panel stuck visible (gear + Esc both dead) — root cause: the
  `_visible` guard.** HidePanel set `_visible = false` and then hid
  the window; when the hide left the window up, every later
  HidePanel was an early-return no-op. Fixed by always hiding (no
  early return) and using the WinUI-native `AppWindow.Hide()`
  instead of `ShowWindow(SW_HIDE)`.
- **Esc dead in the no-activate paths — root cause: no keyboard
  input.** The panel's Esc accelerator/KeyDown need a focused
  element, but ShowPanelNear never activates the window, so Esc
  goes to the user's app instead. Fixed with `EscapeKeyMonitor`
  (WH_KEYBOARD_LL, same shape as ClickOutsideMonitor): it sees Esc
  whatever window has focus, only while the panel is visible, and
  only observes the key (it never swallows it — the key still
  reaches the other app). Started/stopped with the click-outside
  monitor; the accelerator + KeyDown stay as backups.
- **Top bar drag only on the name text — root cause: no
  background.** A StackPanel with no Background is only
  hit-testable on the text glyphs, so a press on the empty area
  beside/below the text never reached the drag handler. Fixed with
  `Background="Transparent"` (+ explicit Stretch): the whole row,
  full width, is now the drag handle.
- **Tray right-click menu: no TrayMenu lines in the log Manh
  pasted** — only the OcrCapture lines. Still undiagnosed: Manh
  needs to click a menu item and check crash.log for
  `TrayMenu: Clicked '...'` (the log in the doc ends with the three
  OcrCapture lines from 22:18–22:19).

## Note for a later session (Manh, 2026-11-12 22:57 — do NOT do yet)

- **OCR overlay dim: REMOVE it entirely next session (Manh
  confirmed 22:59, pending his test report on 0.1.3.56 first — do
  NOT do yet).** Manh's suspicion: the "slight dim" (#26000000 over
  the screenshot) may be the problem — it may read as a solid
  colour fill rather than a dim (consistent with the opaque-window
  finding: over an opaque surface a semi-transparent brush is just
  a flat tint). No dim is needed to signal capture mode: the cursor
  already changes to a capture-drag shape while dragging. If the 56
  test still shows a grey frame, remove the dim (show the
  screenshot undimmed) before investigating anything else.

## Donate "Buy me a coffee" (Manh, 2026-11-12, mirrors TransPeek mac)

- **Account (author's own): BIDV — 2600786466 — TRAN MANH TUAN.**
  The QR card is the full official VietQR card
  (`src/TransPeek/Assets/DonateCard.png`, 600x732, black rounded
  corners fixed to white); its QR was verified to decode to this
  account before shipping (mac: same account and card at
  `Resources/DonateCard.png`). Use the full card, NOT a QR-only
  crop. International QR (for non-Vietnamese users) is a separate
  later task — NOT this round.
- **About > Support:** headline "Support TransPeek" + "If this app
  is useful to you, buy me a coffee to keep improving it." +
  "Show QR Code" (ContentDialog with the card at 280px wide — the
  QR inside is ~165px, scannable — plus the account line
  underneath) + "Copy Account Number" (copies 2600786466, button
  briefly shows "Copied"). The QR stays behind a button so About
  stays compact. UI language is English (settled policy).
- **README:** "## Support" before "## License", same sentence +
  the card image at width 300 + the bold account line.
- **Binary asset rule:** the connector cannot push binaries, so
  DonateCard.png goes up via the browser (its own commit) BEFORE
  the code commit that references it as `<Content>` — otherwise
  the code commit's CI build fails on the missing file. The app
  icon stays base64 in `Assets/EmbeddedIcon.cs`.

## OCR selection dim removed (Manh, 2026-11-13)

- **The `#26000000` dim Grid is GONE from `RegionSelectWindow`.**
  Manh's suspicion (2026-11-12): the light dim "fills color in"
  instead of dimming, so the user picks blind. No dim is needed to
  signal capture mode — the cursor changes to the capture shape
  while dragging. Kept: the screenshot background, the selection
  rectangle's light fill (`#22FFFFFF`, so the picked region stays
  visible), and the hint's dark chip (`#CC000000`).
- Done in the same commit as the donate-template update (batch rule),
  after the round-3 screenshot-background fix (build 58) — so build
  59 tests both changes at once; attribution between the two is
  traded for one test round, per Manh's call.

## Donate template = the standard one (Manh, 2026-11-13)

- **The standard template (EN + VI, both ending in "!") now applies
  to TransPeek-Windows too** (About + README), mirroring mac:
  "If this app is useful to you, you can support the project by
  buying me a coffee!" / "Nếu ứng dụng này hữu ích với bạn, bạn có
  thể ủng hộ dự án bằng một ly cà phê!" + the 3-line account block
  (Account / Holder / BIDV full name). The old one-line
  "BIDV — 2600786466 — TRAN MANH TUAN" (`DonateAccountLine`) is
  gone; `AppInfo.DonateAccountBlock` is the single source. SeekZip
  keeps its old text (Manh's call) — the template applies from the
  next repo onward, and TransPeek (mac + Windows) was updated to it.

## Pre-release zip name includes the CI run (Manh, 2026-11-13)

- **Chosen format: `TransPeek-Windows-0.1.3-CI<run>.zip`** (e.g.
  `TransPeek-Windows-0.1.3-CI60.zip`), picked from 7 options Manh
  listed. Why this one: "CI60" says outright that 60 is the CI run
  number — the old name hid it inside the version
  (`...-0.1.3.59.zip`), `-59` reads like a SemVer prerelease
  suffix, parentheses (options 1/4/5/6) are ugly in URLs and
  scripts, 5/6 repeat the number twice, and dropping the version
  (`TransPeek-Windows-59`) loses which app version a file is. A
  future stable release just drops the `-CI<run>` suffix.
- **Changed in `build.yml` only (commit `c4b68b6`, via browser —
  the connector cannot push workflow files):** the Zip step and the
  `ZIP=` variable (2 lines). The app version (`-p:Version=
  0.1.3.<run>`) and the tag (`v0.1.3.<run>`) are UNCHANGED, and so
  are the delete-older steps (they match tags/artifacts, not zip
  names). Signing is unaffected (it applies to the exe inside, not
  the zip name); Build/Test/Publish/Verify run before the Zip step,
  so they cannot be affected either.
- **Applies from the NEXT `[build-app]` run onward.** Build 59
  keeps its old name (`TransPeek-Windows-0.1.3.59.zip`) — the
  rename does not rebuild or re-upload it.

## Pre-release zip name: short form, like mac (Manh, 2026-11-13)

- **Final format: `TransPeek-Windows-<run>.zip`** (e.g.
  `TransPeek-Windows-60.zip`), mirroring mac's
  `TransPeek-macOS-<run>`. Supersedes the `...-0.1.3-CI<run>` form
  chosen 30 minutes earlier (commit `c4b68b6`): Manh's call, same
  reason as mac — the short name is instantly distinct from a
  future stable release file (`TransPeek-Windows-0.1.3.zip`), while
  the CI-suffixed name looks confusingly similar to it in the
  Downloads folder. The version still shows on the release page
  (tag `v0.1.3.<run>`) and inside the app (About: version + CI
  build). Changed in `build.yml` only (commit `8973ef0`, via
  browser): the Zip step + the `ZIP=` variable, 2 lines; version,
  tag and delete-older steps untouched. Applies from the next
  `[build-app]` run onward; build 59 keeps its old name.

## About version line template (Manh, 2026-11-13, mirrors mac option B)

- **The version line reads "Release 0.1.3 (Build CI #59)"** —
  "Release" without "Ver" ("Ver" is redundant: "Release" already
  means a version), and the build number only inside
  "(Build CI #...)". Local builds (no CI number) show just
  "Release 0.1.3". The line is already a link to the Releases page
  (OnReleaseNotesClick); there is no standalone "Build CI #..." line
  on Windows About, so nothing else to delete. The tray header
  ("TransPeek 0.1.3 (36)") is unchanged — that is the tray, not
  About.

## Test 59 feedback round (Manh, 2026-11-14) — 5 issues, one build

Manh tested build 59: OCR works with BOTH engines + QR scan works.
Remaining issues, all fixed in one [build-app] commit (debugging
exception: build on every fix while the app is broken):

1. **Floating Esc button on the capture screen.** The Cancel button
   ("✕ Cancel (Esc)", top-right) sat near the mouse and an accidental
   click exited the capture. FIX: button REMOVED (XAML + code-behind +
   resw key). Exits left: keyboard Esc (KeyDown + accelerator, grid
   keeps focus), right-click, Alt+F4. Hint now reads "Esc /
   right-click to exit".
2. **Floating thing in the translate panel on hover/select of
   input/output text.** Root cause: SelectionMonitor had NO own-window
   exclusion — selecting text inside TransPeek's own windows (panel,
   Settings) showed the floating trigger icon under the mouse. FIX:
   TryGetSelection skips the focused element when its process id is
   ours (FlaUI `element.Properties.ProcessId`, fail open on pid 0).
   (Could not find any element literally saying "Esc" in the panel —
   the trigger icon is the only floating element there; if it still
   appears, ask Manh for a screenshot.)
3. **Panel drag: press-and-hold did not move the panel; it only
   started following the mouse AFTER the click was released.**
   Root cause: the threshold + pointer-capture pattern —
   SendMessage(WM_NCLBUTTONDOWN) while a XAML element holds pointer
   capture does not engage the system move loop until capture is
   released. FIX: BeginDrag on PointerPressed directly (standard
   WinUI 3 pattern); a press without movement still does not move
   the window (the move loop ends on release). Threshold fields and
   Moved/Released handlers removed.
4. **About > Show QR: text only, NO card image.** Root cause:
   `Assets/DonateCard.png` was NEVER in the published zip (checked
   the 0.1.3.59 zip): `<Content Include>` alone does not copy to the
   publish folder. FIX: `CopyToOutputDirectory="PreserveNewest"` on
   the Content item + load the image from the file path
   (`AppContext.BaseDirectory/Assets/DonateCard.png`) with ms-appx
   as fallback (logs to crash.log when the file is missing). Verify
   by checking the next build's zip contains Assets/DonateCard.png.
   ALSO in About: removed the duplicate "GitHub" TextBlock next to
   the cat icon — cat + repo link only (Manh).
5. **Switching OCR Tesseract -> Windows OCR exits the app suddenly.**
   NOT identified from code: the switch itself only sets the setting
   + saves (OnSettingsSaved rebuilds translation providers only,
   never touches OCR), and the OCR flow catches provider exceptions
   into a toast. FIX (fallback, Manh's words: "nếu k sửa được phải
   có nút quit & reluanch"): **Restart TransPeek** — tray menu item
   (before Quit) + a button at the bottom of Settings > About.
   `App.Restart()` starts a new process of the current exe, then
   exits only when the start succeeded (a failed relaunch leaves
   the app running). Diagnostics added: `OcrProvider: Switched to
   '<id>'` in crash.log at switch time — the next crash.log (the
   "Application.UnhandledException" lines) is needed to find the
   real cause; ask Manh to paste it after the crash.
6. **Tray right-click menu still does not work (unresolved since
   build 53/54).** Diagnostics added: `menu.Opening` now logs
   "TrayMenu: Menu opening" (logging ONLY — the earlier Opening
   handler that refreshed accelerators was suspected of breaking
   the menu, so this one never touches the items). Next crash.log
   distinguishes: no "Menu opening" = menu never opens; opening
   but no "Clicked" = items don't fire. Ask Manh: does the menu
   appear at all on right-click?

## OCR-switch crash — ROOT CAUSE FOUND in the #59 log (Manh, 2026-11-14)

Manh pasted the Event Viewer log into the test doc ("LOG TransSpeek
TEST"). It shows the crash is NOT about OCR at all:

- .NET Runtime Event 1025, FailFast, several times on 2026-11-14
  (15:50, 16:06, ...), TransPeek.exe 0.1.3.59:
  "A callback was made on a garbage collected delegate of type
  'TransPeek!TransPeek.Services.GlobalHotkeyService+WndProc::Invoke'",
  stack: DestroyWindow -> DestroyWindow -> GlobalHotkeyService.MessageLoop().
- The OcrCapture lines in the same log all have HIGH stddev (25-60):
  the screenshot-background capture works, real content, not grey.
- NO "TrayMenu" lines in the doc at all (menu clicks never logged —
  still unknown whether the menu opens; the #63 "Menu opening" log
  will tell).

**Mechanism:** the hotkey message-only window class
("TransPeekHotkeyWindow") is registered ONCE per process
(RegisterClassEx fails if the name is taken; the class is never
unregistered), with the FIRST service's WndProc function pointer.
Every Settings save re-creates the service (OnSettingsSaved ->
StartHotkeys -> Dispose old + new service), but the class keeps
pointing at the FIRST service's delegate. The old service becomes
unreachable, the GC collects it AND its `_wndProc` field (the field
only kept it alive while the service was reachable) — then Windows
calls the class WndProc (hotkey message, or DestroyWindow on the
next dispose) and the runtime FailFasts the process. That is why the
app "exits suddenly" when switching the OCR engine: the switch is
just the first Settings save after launch, and any save would do it.
(The same bug family does NOT apply to EscapeKeyMonitor /
ClickOutsideMonitor: their Stop() calls UnhookWindowsHookEx before
the instance can be dropped, so no callback can arrive after
collection.)

**Fix (commit after #63):** the WndProc is now STATIC (a static
field lives for the process lifetime) and forwards to the CURRENT
service for that window handle, via a static
`ConcurrentDictionary<IntPtr, GlobalHotkeyService>` registered in
MessageLoop after CreateWindowEx and removed after DestroyWindow.
No API change; App.xaml.cs untouched. The Restart button added in
#63 stays as a general recovery path.

## Test 64 feedback (Manh, 2026-11-14) — round 2

Manh tested build 64 and reported 6 items. IMPORTANT: he still sees the
OCR Cancel button and the panel trigger icon — both were REMOVED in #63
(the button is gone from the XAML, the icon is excluded by process id).
A removed XAML element cannot appear, so he is almost certainly still
running #59 (or clicking the ghost tray icon of the crashed #59 instance
— a FailFast crash leaves a tray icon that does not respond until it is
hovered). Before re-testing, he must quit the old version (Task Manager
→ TransPeek.exe → End task if the tray Quit does not work) and check
About: the version line must read "Release 0.1.3 (Build CI #<new run>)".

1. **Panel drag STILL release-then-track in #64.** The press-version of
   the system move (SendMessage WM_NCLBUTTONDOWN on PointerPressed) did
   not fix it either — the old comment blamed pointer capture, but the
   press version had no capture and behaved the same, so the system move
   is simply unreliable for this no-activate window. FIX: manual drag —
   PointerPressed records the pointer + window position and captures the
   pointer, PointerMoved calls AppWindow.Move by the pointer delta
   (DIPs scaled by DpiScale to physical pixels), PointerReleased stops.
   It follows the mouse only while the button is held; a plain click
   (no move) generates no Moved events, so the window stays put.
2. **Settings gear shows Settings but does not hide the panel.** Cause:
   the panel root's PointerReleased handler (added in 0.1.3.53 to
   activate the panel after a click) runs AFTER the gear's Click — and
   the Click calls HidePanel first. So the handler re-activated (and
   re-showed) the panel right after HidePanel hid it. Click-outside and
   Esc hide without a pointer release, so they were never affected. FIX:
   the handler now skips activation when `_visible` is false.
3. **Settings left menu: icons.** NavigationViewItems were text-only.
   Added FontIcon (Segoe Fluent Icons, like Windows 11 Settings):
   General E713 (Settings), Translation E70F (Edit), Dictionary E8D2
   (Dictionary/book), OCR E8FE (Scan), About E946 (Info).
4. **About: bilingual slogan lines.** The English + Vietnamese support
   sentences were a full line apart (outer StackPanel Spacing 12).
   They are now wrapped in a StackPanel with Spacing 2, so the pair
   reads as one message.
5. **Hotkey boxes required typing the spec by hand.** Now: click a
   hotkey box, then press the combination — the box records it
   (KeyDown handler, e.Handled so nothing is typed). Modifiers alone
   show "Control+Alt+…"; a bare key shows "T (needs a modifier…)"
   (a modifier-less global hotkey would hijack normal typing); Esc
   cancels and restores the old text; a half-recorded "…" text is
   restored silently on LostFocus. The recorded spec goes through the
   existing TrySaveHotkey (format + duplicate check). A hint line above
   the boxes explains it.
6. **Float icon link.** The trigger icon is not an image file — it is
   XAML (TriggerIconWindow: 30x30 white circle, 1px #C8C8C8 border,
   "A" 13pt bold + "文" 10pt, #1A1A1A). A render at 1x/2x/4x was shared
   with Manh by link (a render with DejaVu/Noto fonts, NOT a
   screenshot — the real font on Windows is Segoe UI with a CJK
   fallback). Manh will propose a size/style change from it.

## Tray icon not responding (Manh, 2026-11-14)
Clicking the tray icon "shows nothing". Most likely the ghost icon of
the crashed #59 instance (see the note at the top of the Test 64
section). If #65's tray still does not respond after a clean launch,
send crash.log: #63/#64 log "TrayMenu: Menu opening" on right-click,
so the log tells whether the menu opens at all.

## Test 65 feedback (Manh, 2026-11-14) — round 3

Items 2, 3, 4, 5 (gear hides panel, Settings icons, About slogan,
hotkey recording) are DONE. Three things left:

1. **Panel drag: almost right, but jerky and fires for right/middle
   buttons too.** The manual AppWindow.Move drag is a hack — Manh is
   right, it must behave like the Settings window's title bar (native
   OS drag). FIX: the title bar is now a NATIVE caption region via
   InputNonClientPointerSource.SetRegionRects(NonClientRegionKind.Caption)
   — the OS moves the window itself: left button only, smooth, exactly
   like a real title bar. (The system move via SendMessage
   WM_NCLBUTTONDOWN never engaged for this no-activate window, and the
   manual move was jerky.) The title bar holds only text (the gear is
   in the header row below it), so the whole row is the region; it is
   recomputed on Loaded + TitleBar.SizeChanged. The manual drag code
   and WindowHelpers.BeginDrag are now unused (BeginDrag stays as a
   helper; it documents the failed approach).
2. **The "esc" icon still shows in the panel and in the OCR capture
   screen; hovering it exits the capture.** Two causes, both fixed:
   (a) the floating hint chip in the capture screen ("Drag to select
   the region to OCR — Esc / right-click to exit") sat top-center
   where the mouse works — REMOVED. Esc on the keyboard, right-click
   and Alt+F4 already cover the exit. (b) the trigger icon still
   appeared over our own windows: the UIA ProcessId exclusion in
   SelectionMonitor misses it when the focused element reports pid 0
   (fail open). ADDED a second guard in App.OnSelectionChanged: the
   icon is not shown when the selection point lies inside one of our
   visible windows (panel, Settings, trigger icon, capture overlay —
   App tracks the RegionSelectWindow in a field while it is open).
3. **Tray: left-click works, right-click menu opens but NO item does
   anything.** ROOT CAUSE (found by web search, H.NotifyIcon docs):
   the default ContextMenuMode builds a Win32 PopupMenu from the
   MenuFlyout and calls the items' COMMANDS — a Click handler is never
   invoked. Our items only had Click, so the menu rendered but every
   click went nowhere (and no "TrayMenu: Clicked" line ever appeared,
   which is why the log was empty). FIX: each MenuFlyoutItem now
   carries a RelayCommand (the same defer-to-next-dispatcher-tick
   pattern as before, so showing a window while the menu dismisses is
   still safe). The left-click already worked because
   LeftClickCommand is a Command too. The Opening log stays as a
   diagnostic. (SecondWindow mode was rejected — it is still preview.)

## CI #66 red — SetRegionRects takes RectInt32, not Rect (Manh, 2026-11-14)
`InputNonClientPointerSource.SetRegionRects` takes
`Windows.Graphics.RectInt32[]` (PHYSICAL pixels), not
`Windows.Foundation.Rect[]` (DIPs) — CS1503. The WMC9999 next to it
is a consequence of the C# error, not a separate error (same pattern
as runs #46/#52/#55). Fixed by scaling the title-bar bounds by
`WindowHelpers.DpiScale` into a RectInt32. (The CS8619 warning in
TesseractLanguageDataManager is pre-existing and does not fail the
build.)

## Float icon: mac source + size (Manh, 2026-11-14)
Mac source: `Sources/UI/TriggerIcon/TriggerIconPanel.swift`
(https://github.com/TranManhTuanLabs/TransPeek/blob/main/Sources/UI/TriggerIcon/TriggerIconPanel.swift).
Mac: 32 pt panel, 30 pt circle (inset 1 pt), 0.5 pt border, SF Symbol
"character.bubble" 14 pt medium (labelColor) — "speaker.wave.2.fill"
(controlAccentColor) while TTS plays; background controlBackgroundColor,
border separatorColor. The mac does NOT use text at all. Windows already
matches the size (32 px window, 30 px circle), so no size change was
needed. Manh 2026-11-14: keep the letter "A", drop the Chinese character
(文) — done in TriggerIconWindow.xaml. The full mac icon (SF Symbol
look) is NOT ported yet — later.

## Test 68 feedback — Enter, icon size, speaker on click (Manh, 2026-11-14)
- Kéo panel (native caption region): DONE — Manh xác nhận bản 68.
- Tray menu chuột phải: DONE — bấm món nào cũng chạy (RelayCommand fix #66).
- Icon lúc hiện lúc không khi select text: Manh test thêm, hiện đang hiện
  tốt — để kiểm tra sau, báo lại nếu lỗi thật. Không sửa gì lúc này.
- Enter ở ô input panel: BUG — Enter chỉ xuống dòng, không dịch lại.
  Nguyên nhân: với AcceptsReturn=True, TextBox tự nuốt Enter trong
  OnKeyDown của nó (chèn newline, đánh dấu handled) TRƯỚC khi handler
  KeyDown của app chạy, nên handler không bao giờ được gọi. Sửa:
  AcceptsReturn=False + Shift+Enter tự chèn "\n" tại vị trí con trỏ
  (OnSourceKeyDown), Enter thường = dịch lại.
- Size icon "bất thường": cửa sổ trigger icon được resize bằng
  AppWindow.MoveAndResize (PIXEL VẬT LÝ) với số 32, trong khi nội dung
  (vòng tròn 30) tính bằng DIP — trên màn hình có scale, vòng tròn tràn
  ra khỏi cửa sổ (bị cắt). Sửa: nhân 32 DIP với DpiScale ra pixel vật
  lý trong ShowAt. Chữ "A" lên 14pt (bằng symbol 14pt bên mac).
- Float icon click → hiện icon loa: Tapped đổi mark sang FontIcon
  speaker (Segoe MDL2 E7F5), ShowAt/HideIcon trả lại chữ "A". Đây mới
  là TRẠNG THÁI VISUAL — TTS audio chưa có (M2, sau khi mac ổn).
- Ảnh trong Google Doc (đã đọc được qua export docx, 3 ảnh): ảnh 1 là
  float icon "A"; ảnh 2/3 là nút "Esc" trắng nổi phía trên panel.
  Đã soát toàn bộ code: TransPeek KHÔNG có nút/pill "Esc" nào — chỉ
  còn dòng hint trong panel ("Enter: translate again • Esc: close")
  và chip hint màn chụp (đã bỏ ở #66). Kết luận: pill "Esc" đó KHÔNG
  phải của TransPeek, khả năng từ app/tool overlay khác trên máy Manh.
  Cần Manh kiểm chứng: thoát hẳn TransPeek (tray → Quit) xem pill
  còn hiện không, và cho biết đang chạy tool chụp màn hình/overlay nào.

## Provider text on its own line (Manh, 2026-11-14)
"via Google Translate" was inline on the language-picker row. Moved to
its own line below the pickers (Row 1 is now a vertical StackPanel:
pickers row + ProviderText under it). No other layout change.

## Exe file icon — queued for the next round (Manh, 2026-11-14)
Manh wants the published TransPeek.exe to carry the app icon so it is
easy to find in Explorer/Downloads. Currently the exe has no icon:
the csproj has no <ApplicationIcon> and the repo has no .ico file —
the icon only exists as base64 in src/TransPeek/Assets/EmbeddedIcon.cs
(multi-size ICO 16/32/48/256, converted from the mac TransPeek.png).
Next round: decode the base64 to Assets/TransPeek.ico and add
<ApplicationIcon>Assets\TransPeek.ico</ApplicationIcon> to the csproj.
NOTE: the connector cannot push binary files (.ico) — push the .ico
via the browser (like DonateCard.png), or push the csproj via the
connector and the .ico via the browser in one commit.

## Settings pane resize — queued for the next round (Manh, 2026-11-14)
Manh wants a draggable resize between the left nav pane and the right
content pane in the Settings window. Windows/WinUI ALLOWS it: the
NavigationView's left pane width is the settable OpenPaneLength
(currently unset — default). NavigationView has NO built-in drag
splitter, so the next round adds a custom thin drag handle at the
pane's right edge that updates OpenPaneLength while dragging. (The
Windows 11 Settings app keeps its pane fixed — a design choice, not
an OS limit.)

## Tray + hotkey renames (Manh, 2026-11-14)
- "Screenshot OCR" -> "OCR from Screen": it captures a screen region,
  OCRs it and shows the panel (original + translation).
- "Clipboard OCR" -> "OCR to clipboard": it captures a screen region,
  OCRs it and copies the recognised TEXT to the clipboard, panel-less
  (toast only). Note this is NOT the same as "Screenshot Copy", which
  copies the captured IMAGE (bitmap) to the clipboard — that name stays.
- Renamed in both places: tray menu (resw values + fallbacks in
  TrayIconService) and the hotkey labels (GeneralPage headers in resw +
  XAML fallbacks, and the display names in TrySaveHotkey/AllHotkeys —
  the duplicate error now reads "Already used by OCR from Screen.").
  Internal identifiers (resw keys, settings properties, hotkey ids,
  log labels) are unchanged.

## Test 70 feedback — provider above, Capture OCR label, speaker/click, mac bubble, Esc pill (Manh, 2026-11-14)
- "via Google Translate": moved ABOVE the language pickers (it was below
  them in 0.1.3.70, Manh wants it above). Row 1 is now [ProviderText,
  pickers row].
- Panel button "Capture OCR (Ctrl+Shift+S)" -> "Capture OCR": the
  hotkey in parentheses is dropped (resw Panel.CaptureOcr.Content + XAML
  fallback). The button still runs the same OCR flow.
- Click icon -> speaker, hover/click conflict (0.1.3.70 test: the speaker
  never appeared): ROOT CAUSE — the click raised OpenRequested, opening
  the panel hid the icon in the same tick, so the speaker was never
  visible; and hover (300 ms) and click both opened the panel, so only
  one function ever ran. Fixed by porting the mac model
  (TriggerIconController): click = listen mode — it cancels the hover
  timer, switches the mark to the speaker and does NOT open the panel;
  while speaking, hover is blocked (guard !isSpeaking); a second click
  turns the speaker back off. Hover (past the delay) still opens the
  translate panel. TTS audio is NOT built yet, so the click is only the
  speaker visual for now; the speaker auto-hides after the auto-hide
  duration (the mac keeps it until playback stops — here there is no
  playback, so a stuck speaker would be worse).
- Icon: the plain "A" in the circle still looked wrong, so the mac icon
  was ported (Manh's fallback): the mark is now the mac SF Symbol
  "character.bubble" drawn as a XAML Path (bubble outline + "A" inside),
  on the WHITE circle background as it is now (not the mac's
  controlBackgroundColor).
- "Esc" pill (floating white "Esc" above the panel, and in the OCR
  capture screen when moving the mouse): RE-CHECKED the doc link/images
  and the whole codebase — TransPeek has NO "Esc" button, tooltip or
  label anywhere. RegionSelectWindow.xaml is only BackgroundImage +
  SelectionRect; its code-behind has no mouse-move UI at all (only
  keyboard Esc + right-click to cancel). The panel has no Esc pill
  either (only the hint text line). Conclusion stands: the pill is NOT
  drawn by TransPeek — it must come from another app/tool overlay on
  Manh's machine. Possible: an old TransPeek build still running in the
  background (its capture screen still had the hint chip / Cancel
  button) — check Task Manager for a second TransPeek.exe and quit it.
  Decisive test for Manh: with TransPeek fully quit, move the mouse on
  the desktop — if the pill still appears, it is another app.

## "Esc" pill — exhaustive code search + window-list diagnostic (Manh, 2026-11-14)
- Manh insists the floating white "Esc" pill is from the app and asked
  to trace "Esc" in the code. FULL search (all .cs/.xaml/.resw):
  the ONLY "Esc" strings are comments (Esc keyboard handling) and two
  hint texts INSIDE windows — the panel hint "Enter: translate again •
  Esc: close • ..." (Row 3, inside the panel) and the Settings hotkey
  hint "Esc cancels." No "Esc" button, tooltip, label or pill exists.
  "Cancel": only comments — the Cancel button was removed in 0.1.3.63.
  "Hint": only the panel hint and Settings hints. ToolTips in the app:
  tray icon (long text), dictionary link names, gear button "Settings"
  — none is "Esc". The toast is a DARK pill at the bottom-right and no
  toast call ever passes "Esc". RegionSelectWindow has no mouse-move UI
  at all (only keyboard Esc + right-click to cancel). EscapeKeyMonitor
  is a bare WH_KEYBOARD_LL hook with no UI. The app has exactly 5
  windows (TriggerIcon, Panel, RegionSelect, Toast, Settings) — none
  renders a white "Esc" pill.
- Because the code alone cannot settle it, a DIAGNOSTIC was added
  (temporary, remove once the owner is known): Tray menu ->
  "Diagnose: list windows" enumerates every visible top-level window
  (HWND, title, rect, owning process) into
  %LOCALAPPDATA%\TransPeek\windows.log (WindowDiagnostics.cs +
  EnumWindows P/Invokes in NativeMethods.cs). When the pill is visible,
  run it and send windows.log — the pill's window will be the small
  (~40x28 px) one near the top-center, and its process name settles
  whether it is TransPeek.exe or another app (screenshot tool, Edge
  fullscreen hint, old TransPeek build still running, ...).

## Restore capture hint + revert float icon to plain "A" + shadow/size fix (Manh, 2026-11-14)
- Capture screen lost its instruction text (removed in 0.1.3.65): RESTORED
  as before — dark chip #CC000000, top-center, "Drag to select the
  region to OCR — Esc / right-click to exit" (resw RegionSelect.Hint.Text
  restored). This time with IsHitTestVisible="False" so the chip can
  never take mouse input — the pointer passes through to the grid, so
  the 0.1.3.65 bug (hovering the chip exited the capture) cannot return.
- Float icon: the mac bubble port (0.1.3.72, XAML Path bubble + "A")
  looked bad, so REVERTED to the plain "A" (14 pt, white circle) as in
  0.1.3.70. Shadow + off-size, both still present with the plain "A":
  (a) SHADOW — the window is a tool window (WS_EX_TOOLWINDOW, which
  gets no window-manager drop shadow), but MakeToolWindow only changed
  the extended style and never told the frame to re-evaluate, so the old
  shadow stayed. FIXED: SetWindowPos + SWP_FRAMECHANGED after the style
  change (added SWP_NOZORDER = 0x0004). (b) SIZE — the window sized
  itself to content; FIXED by setting Width/Height = 32 DIP in the
  constructor (ShowAt still resizes in physical pixels, 32 * DpiScale).
  Windows does NOT forbid small windows: the icon already shows at
  32 px, so there is no minimum-size rule in the way.
- CI #74 (diagnostic build, RECT fix) went GREEN; pre-release
  v0.1.3.74 verified. The "Diagnose: list windows" tray item is in
  that build — Manh still needs to run it while the Esc pill is visible
  and send windows.log.

## .exe icon + pin button (Manh, 2026-11-14)
- TransPeek.exe now has its own icon: Assets/TransPeek.ico decoded from
  EmbeddedIcon.IcoBase64 (multi-size 16/32/48/256, 109130 bytes) +
  <ApplicationIcon>Assets\TransPeek.ico</ApplicationIcon> in the csproj.
  The .ico is binary, so it was uploaded via the browser (connector
  cannot push binary) BEFORE the csproj change, so no commit references
  a missing file and CI never goes red in between.
- Pin button (next to the Settings gear in the panel footer, Segoe MDL2
  Pin E718, ToggleButton): while pinned, click-outside and Esc do NOT
  hide the panel — it stays fixed and visible until unpinned. The panel
  is already topmost, so pin is about not auto-hiding, not z-order.
  Explicit hides (Capture OCR button) still hide it; the pin state
  persists until the user toggles it off.
- Esc hint locations (Manh confirms): exactly 2 places — the panel
  (input/output area, the Row 3 hint "Enter: translate again • Esc:
  close • ...") and the OCR capture screen (restored hint chip). The
  floating white "Esc" PILL is a separate thing; the window-list
  diagnostic (build 74) did not catch it because Manh ran it while the
  panel/capture were closed — the pill only shows when one of them is
  open. Next step: run "Diagnose: list windows" WHILE the pill is
  visible (panel open), then send windows.log.
- CI #75 (hint restore + icon revert, 35429bb) was CANCELLED for an
  unknown reason — no newer push at the time (same as #71). Its changes
  are already in main, so the next build (#76) covers them.

## "Esc" pill — ROOT CAUSE FOUND (Manh's pin test, windows.log 22:10:38, 2026-11-14)
- The pin test worked: with the panel pinned open, "Diagnose: list
  windows" caught a TransPeek-owned window the earlier runs missed:
  `0xDA0D94 pid=36756 [TransPeek] rect=1584,797 46x41 title="PopupHost"`,
  sitting over the panel (440x480 "WinUI Desktop" at 1473,540) near the
  input/output area — exactly where Manh said the Esc hint appears.
- ROOT CAUSE: the "Esc" pill is WinUI's auto-generated key tip for a
  KeyboardAccelerator. Both the panel and the capture screen created
  `new KeyboardAccelerator { Key = VirtualKey.Escape }` on their root
  element; WinUI renders the accelerator's key as a floating "Esc" pill
  (a PopupHost window, 46x41 px) whenever the mouse hovers the element.
  That is why the pill appeared above the panel AND in the capture
  screen on mouse-move, and why no "Esc" button/tooltip/label exists in
  the code — the pill is generated by the framework, not by app code.
- FIX: removed the Esc KeyboardAccelerator from both windows
  (TranslatePanelWindow.xaml.cs, RegionSelectWindow.xaml.cs). Esc still
  closes both: the panel via KeyDown on the root + the low-level
  EscapeKeyMonitor hook, the capture screen via KeyDown on the focused
  grid (RootGrid is explicitly focused on Loaded/Activated). The
  accelerator was only a backup (it needs a focused element), so
  removing it loses nothing. The diagnostic ("Diagnose: list windows")
  stays in the app for now — it proved its worth here.

## Float icon: white square + shadow — ROOT CAUSE (build 78, Manh 2026-11-14)
- Build 78 (icon revert to "A" + MakeToolWindow/SWP_FRAMECHANGED shadow
  fix) did NOT fix it: Manh reports the icon is still a white square
  with a drop shadow, and the doc screenshot confirms it.
- ROOT CAUSE: WinUI 3 has NO built-in transparent window background.
  A "Transparent" XAML background renders as an opaque box (white in
  Light mode), so the 30 px white circle is invisible against its own
  window and the icon looks like a white square. And WS_EX_TOOLWINDOW +
  SWP_FRAMECHANGED does NOT remove the shadow in WinUI 3 — the
  shadow/outline comes from the frame bits (WS_DLGFRAME, which
  OverlappedPresenter.SetBorderAndTitleBar leaves behind) and the opaque
  background. The 0.1.3.75 comment claiming the shadow was "the stale
  frame" was wrong; corrected in WindowHelpers.
- FIX (Action-Wheel pattern — a WinUI 3 radial menu with real per-pixel
  alpha; all parts required, drop any one and the window is an opaque
  box): new Interop/TransparentWindow.cs — presenter attached FIRST,
  WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, RemoveWindowFrame (strip
  WS_CAPTION | WS_BORDER | WS_DLGFRAME | WS_THICKFRAME + edge bits,
  DWMWA_WINDOW_CORNER_PREFERENCE = DONOTROUND, DWMWA_BORDER_COLOR =
  COLOR_NONE), DwmEnableBlurBehindWindow(fEnable: true, null region),
  a custom SystemBackdrop holding a transparent composition brush
  (TransparentSystemBackdrop + WindowsSystemDispatcherQueueHelper),
  and the native client surface cleared to BLACK via a window subclass
  on WM_ERASEBKGND (WinUI clears it theme-white first; black + DWM
  alpha = fully transparent pixels). The brush + subclass delegate are
  rooted per-HWND so they cannot be GC'd. TriggerIconWindow now calls
  TransparentWindow.MakeTransparent on Loaded instead of
  MakeBorderlessTopmost + MakeToolWindow. Panel/toast stay opaque
  (they are cards; a shadow is fine there).

## OCR capture: crosshair (+) cursor (Manh 2026-11-14)
- The capture screen shows a screenshot, so the user needs a clear
  "drag to select" affordance: a + (crosshair) cursor, like Snipping
  Tool. The old XAML comment already claimed "the cursor changes to
  the capture shape while dragging" but no code ever set it.
- FIX: RootGrid.ProtectedCursor = InputSystemCursor.Create(
  InputSystemCursorShape.Cross) in the RegionSelectWindow constructor
  (ProtectedCursor is the WinUI 3 way; CoreWindow.PointerCursor does
  not reach secondary windows). Set on the grid so it covers the whole
  screen, including the hit-test-invisible hint chip.

## CI #80 RED — two compile errors in the transparency + cursor change (2026-11-14)
- CS0234 TransparentWindow.cs: `Windows.UI.Colors` does not exist in
  this project (net8.0-windows10.0.22000.0 + WinAppSDK 1.8). FIX: build
  the transparent color directly with `Color.FromArgb(0, 0, 0, 0)`
  (alias `using Color = Windows.UI.Color;`, the Action-Wheel pattern
  that also avoids the Microsoft.UI vs Windows.UI Colors ambiguity).
  `Windows.UI.Color` itself resolves fine (CreateColorBrush needs it);
  only the `Colors` static class does not.
- CS0122 RegionSelectWindow.xaml.cs: `UIElement.ProtectedCursor` is
  protected — a plain Grid cannot set it from outside. FIX: new
  Views/CrosshairGrid.cs, a Grid subclass that sets ProtectedCursor
  from inside (on Loaded — it cannot be set until the visual tree is
  loaded, winui-api#112). RegionSelectWindow.xaml now uses
  `<local:CrosshairGrid x:Name="RootGrid">` instead of a plain Grid.
- WMC9999 in the same build is a consequence of the two C# errors, not
  a separate error (same pattern as runs #46/#52/#55/#66/#73/#76/#77).
