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
- **Version:** 0.1.4.0. Verified by script before push: resw XML valid,
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
  the mac menubar** — "TransPeek 0.1.4 (36)", where 36 is the GitHub
  Actions run number of the build that produced the running app.
- **How:** `build.yml` stamps it at build time with
  `-p:Version=0.1.4.${{ github.run_number }}` on both the Build and
  the Publish steps, so the assembly version becomes 0.1.4.36.
  `AppInfo.CiBuildNumber` reads the 4th version part (0 for local
  builds, where the csproj `<Version>` has no 4th part — the header
  then shows no number, as before). The About page shows
  "Version 0.1.4 (build 36)" from the same value.
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

## Settings pane resize + speaker glyph + tray order (Manh 2026-11-14, build 81 test)
- Build 81 confirmed good: the float icon is now a round white circle
  with just "A" (no square, no shadow) — the transparency fix works.
- Settings pane resize: NavigationView exposes OpenPaneLength but has
  no built-in grip. New Views/ResizeHandle.cs (a Grid subclass that sets
  ProtectedCursor to SizeWe from inside — ProtectedCursor is protected,
  same pattern as CrosshairGrid) overlays an 8 px strip centered on the
  pane's right edge (Margin.Left = OpenPaneLength - 4, positioned in
  code on Loaded and on every drag). Dragging updates OpenPaneLength
  1:1 from the press point, clamped to 180–480. Session-only for now:
  the Settings window is recreated on every open, so the width resets;
  persist it in App.Settings if Manh asks.
- Speaker glyph (click = listen mode): E7F5 "Speakers" replaced by E992
  "Volume0" — a plain speaker cone with NO sound waves, like the
  Windows 11 system speaker (Manh: waves not needed). E767 "Volume" is
  the system icon WITH waves; E74F is Mute (speaker with X). If E992
  ever renders blank, fall back to E767.
- Tray menu order: "Diagnose: list windows" moved ABOVE "Restart
  TransPeek" (Manh). Diagnose was temporary (remove once the Esc
  pill's owner was known) but Manh keeps it — it found the PopupHost.
  Restart stays for now below Diagnose; Manh says it feels a bit
  meaningless (the hotkey crash it worked around is fixed in #64), so
  remove it entirely if he says so.

## Panel polish: pin spacing + via Google Translate back below pickers (Manh 2026-11-14)
- Pin button: it sat 12 px from the Settings gear (StackPanel Spacing 4
  + SettingsButton Margin 8,0,0,0). Manh wants it tight against the
  gear — the 8 px left margin is removed, so the gap is just the
  StackPanel's 4 px spacing.
- "via Google Translate" (ProviderText) moves back BELOW the language
  pickers (it was above them since 0.1.3.72). Where it sat, a blank
  line stays open — Manh wants the app name + slogan further from the
  pickers for a cleaner look. The blank line is a TextBlock holding a
  non-breaking space (U+00A0) so it keeps a full line's height and is
  never trimmed away; a Margin would do the same job, the blank line
  is the literal thing Manh asked for.

## CI #83 RED — InputSystemCursorShape.SizeWe does not exist (2026-11-14)
- CS0117 ResizeHandle.cs: the horizontal-resize shape is NOT SizeWe
  in WinUI 3. SizeWe / SizeWE is the WPF name (System.Windows.Input
  CursorType). WinUI 3 (Microsoft.UI.Input.InputSystemCursorShape)
  uses the full words: SizeEastWest (horizontal), SizeNorthSouth
  (vertical), SizeNortheastSouthwest, SizeNorthwestSoutheast.
  FIX: SizeWe -> SizeEastWest in ResizeHandle.cs. (Cross, used by
  CrosshairGrid, exists in both — that is why CI #81 was green.)
- WMC9999 in the same build is a consequence of the C# error, not a
  separate error (same pattern as runs #80 and earlier).

## CI #84 RED — SizeEastWest also does not exist; the real name is SizeWestEast (2026-11-14)
- CS0117 ResizeHandle.cs(24,95) again, same as #83: the SizeEastWest
  "fix" in 6afb8e6 came from a third-party site (copyprogramming) and
  is wrong too. The authoritative Microsoft Learn API reference for
  InputSystemCursorShape lists the horizontal-resize shape as
  SizeWestEast (value 10, "a dual arrow pointing left and right,
  typically used for horizontal sizing") — West first, then East.
  FIX: SizeEastWest -> SizeWestEast. Lesson: for enum member names,
  read the Microsoft Learn API reference, not a third-party roundup.
  (Full member list, WinAppSDK: Arrow, Cross, Hand, Help, IBeam,
  SizeAll, SizeNortheastSouthwest, SizeNorthSouth,
  SizeNorthwestSoutheast, SizeWestEast, UniversalNo, UpArrow, Wait,
  Pin, Person, AppStarting. No SizeWe, no SizeEastWest.)
- Note: CI #82 (8f30734, speaker E992 + Settings resize + tray order)
  also FAILED — so the speaker E992 change has not reached any green
  build yet; the latest working pre-release is still v0.1.3.81 (old
  E7F5 speaker). #82's failure was the SizeWe error too (ResizeHandle
  was added in that commit).

## Settings resize handle floats as a stray stripe when pane is collapsed (Manh 2026-11-14, 0.1.3.85 test)
- The drag works, but the built-in NavigationView toggle collapses the
  pane to icons-only (CompactPaneLength, 48 px) while the handle stays
  positioned by OpenPaneLength — so it floats in the content as a
  vertical stripe at the old dragged position.
- FIX: UpdatePaneResizeHandle() now hides the handle (Visibility
  Collapsed) while IsPaneOpen is false and shows it again on expand.
  It watches IsPaneOpen via RegisterPropertyChangedCallback on
  NavigationView.IsPaneOpenProperty (there is no dedicated
  pane-toggle event to hook). OpenPaneLength is preserved across
  collapse/expand, so the dragged width is not lost when the pane
  comes back.

## Panel tagline font: 11 -> 12 pt (Manh 2026-11-14)
- The tagline (slogan) at 11 pt read too small next to the 13 pt app
  name. Manh wants it exactly 1 size smaller than the main font, not
  2 — so 12 pt. (Mac uses a 2 pt gap, 17/15, but Manh wants 1 pt on
  Windows.) The other small texts (provider line 12 pt, hint 11 pt,
  footer 11 pt) are unchanged.
- Font-size setting (Manh asks, for later): easy to add. The panel
  fonts live in a few XAML spots + code-behind already reads
  App.Settings, so a slider in Settings -> General plus applying it
  on panel load follows the existing pattern (mac already has a Text
  Size slider 12-24, default 17). Not built yet — only when Manh asks.

## Panel top-bar font size setting (Manh 2026-11-14)
- Rule (Manh): the tagline (slogan) is always exactly 1 pt smaller
  than the app-name font. The hardcoded 11 pt tagline read too small
  next to the 13 pt name, so 12 pt was the interim fix; now it is a
  setting instead of a magic number.
- Setting: AppSettings.PopupTopBarFontSize (double, default 13 — the
  current app-name size, so the look is unchanged). Same setting NAME
  as mac (popupTopBarFontSize) for cross-platform comparability; mac
  default is 17 and mac uses a 2 pt gap, Windows uses a 1 pt gap.
- UI: Settings -> General, Slider 12-24 (mac parity) with a value
  label "13 pt — app name; tagline 12 pt" so the -1 rule is visible.
  Saves immediately like every other setting (Save() + SettingsSaved).
- Apply: TranslatePanelWindow.RefreshTopBarFontSize() sets
  AppNameText.FontSize = size and TaglineText.FontSize =
  max(8, size - 1). Called in the panel constructor and from
  App.OnSettingsSaved, so a change applies live even while the panel
  is open. The XAML FontSize values (13/12) stay as the design-time
  fallback matching the default.

## Top-bar font size buttons in the panel (Manh 2026-11-14)
- The Font Size slider lives in Settings -> General, but Manh also
  wants to adjust it right in the panel. Two small buttons sit in the
  actions row, before Pin: "A-" (smaller) and "A+" (larger). They step
  the SAME setting (PopupTopBarFontSize) by 1 pt, clamped to the 12-24
  slider range, save immediately, and re-apply live — so Settings
  shows the new value next time it opens. The tagline stays 1 pt
  smaller (RefreshTopBarFontSize). The smaller "A-" / bolder "A+" hint
  at the direction; tooltips say "Smaller/Larger top-bar font".

## Font size lives in Settings -> Appearance, NOT in the panel (Manh 2026-11-14)
- Correction: Muse first put A- / A+ buttons in the panel (commit
  3b4642a, CI #89 green). Manh: the panel must have NO font buttons
  at all. Reverted the buttons (XAML + handlers) in the next commit.
- The right home is Settings -> Appearance (new NavigationView section,
  icon Personalize E771 per Microsoft Learn Segoe Fluent Icons), with
  the hierarchy Manh sketched:
    Appearance
      Text Size
        Popup Panel
          App name & Slogan Font Size [slider 12-24]
  The slider sets PopupTopBarFontSize (default 13, app name); the
  tagline stays 1 pt smaller (max 8 pt floor). Same setting name as
  mac (popupTopBarFontSize); mac default 17, gap 2 pt.
- The slider moved out of GeneralPage into the new AppearancePage
  (same save-immediately pattern, same value label). The panel still
  applies the size via RefreshTopBarFontSize() — only the buttons are
  gone, the setting and the apply logic stay.

## Settings pane resize: custom handle REMOVED, native NavigationView only (Manh 2026-11-14)
- The custom ResizeHandle (drag strip + ResizeHandle.cs + drag logic
  in SettingsWindow) caused repeated trouble: a stray vertical stripe
  when the pane collapsed (0.1.3.85), then in 0.1.3.89 the General tab
  showed empty and resize stopped working. Manh: use native, do not
  custom-build it.
- NavigationView has NO built-in drag-to-resize (Microsoft Learn:
  OpenPaneLength is a property set in code; the pane is toggled with
  the built-in button). So "native" means: fixed pane width (default
  OpenPaneLength 320), expand/collapse via the built-in toggle only.
  The whole custom handle is gone — XAML element, drag handlers,
  UpdatePaneResizeHandle, the property-changed callback, and
  ResizeHandle.cs itself (deleted).
- General tab: in 0.1.3.90 the Font Size slider had already moved out
  of GeneralPage into AppearancePage, so General is back to its plain
  pre-slider content. If General is still empty in the new build, the
  cause is elsewhere and needs a fresh look, not another custom fix.

## Speaker icon: E992 (no waves) -> E767 (with waves) for balance (Manh 2026-11-14)
- The float icon's speaker state used E992 "Volume0" (plain cone, no
  waves — Manh's first pick). In the 30 px circle it renders off-center:
  the cone sits left with empty space on the right where the waves
  would be, so it looks unbalanced next to the centered "A". The
  FontIcon itself IS centered (same Grid, same Center alignment as the
  "A"); the imbalance is in the glyph's own side bearings, not the
  layout — shifting it by hand would be guesswork.
- Manh prefers the waves in for balance: E767 "Volume" is the Windows
  11 system speaker WITH waves, and the waves fill the right side so
  the glyph reads centered in the circle. (E992 stays documented as
  the no-waves variant; E74F is Mute, not this.)

## Settings nav pane width 280 (Manh 2026-11-15, try-out)
- OpenPaneLength set to 280 (WinUI default is 320) so Manh can try a
  narrower left pane. Native NavigationView, still no drag resize —
  this is a fixed width, not a handle. Revert to 320 (remove the
  attribute) if 280 feels cramped.

## Appearance is a SECTION inside General, not a separate tab (Manh 2026-11-15)
- Correction: Muse first made Appearance a separate NavigationView
  tab (after General). Manh: no — like mac's Sections in
  GeneralSettingsView, Appearance is a section INSIDE the General
  page. The separate tab + AppearancePage are removed.
- General page now carries the section, in Manh's hierarchy:
    Appearance
      Text Size
        Popup Panel
          App name & Slogan Font Size [slider 12-24]
  (Same setting PopupTopBarFontSize, default 13, tagline -1 pt.)
  Placed after the auto-hide setting, with the other panel settings.

## Float icon missed on first launch — Settings excluded from the own-window guard (Manh 2026-11-15)
- Symptom: the trigger icon often missed on first launch; it only
  started recognizing selections after Settings was closed/minimized
  or reopened. It worked before (pre-0.1.3.65).
- Cause: IsPointInsideOwnWindow (added 0.1.3.65) suppresses the icon
  whenever the selection point falls inside ANY own window's rect.
  But Settings opens on launch (960x640, centered) as a NORMAL
  window, and the rect check cannot tell "behind" from "on top" — so
  a selection in another app, inside that big centered rect, was
  swallowed. Closing/minimizing Settings cleared the guard, which is
  exactly the workaround Manh found.
- Fix: drop Settings from the guard list. The panel, trigger icon and
  capture overlay are topmost/fullscreen, so a point inside their
  rect really is on top of them. Selecting text inside Settings is
  already skipped by the monitor's process-id check (only the pid-0
  edge case slips through, and an icon over Settings is a minor
  annoyance, not a missed core feature).
- Note: the two AppearancePage delete commits (c6fd51c, 58a8dd5) had
  no [build-app] marker, and run #95 (the Appearance-in-General
  commit) was cancelled by them — so no pre-release existed for the
  Appearance-in-General change until this commit's build.

## General tab blank since the font slider — ValueChanged during InitializeComponent (Manh 2026-11-15)
- Symptom: the General tab showed nothing at all, starting when the
  font slider was added (build #89). Removing the resize handle did
  NOT bring it back — the handle was a red herring.
- Cause: Slider.ValueChanged fires during InitializeComponent:
  setting Minimum=12 coerces the default Value 0 up to 12, and the
  handler touched FontSizeValueText, which does not exist yet (it
  comes after the Slider in XAML). The NullReferenceException escaped
  the page constructor, so the frame navigation failed and General
  showed blank. Other controls are safe: their handlers only touch
  themselves or App.Settings, never a later-named control.
- Fix: OnFontSizeChanged returns early while _loading or while
  FontSizeValueText is null. OnNavigatedTo sets the value and the
  label directly, so nothing is lost. Rule for new controls: a
  ValueChanged/SelectionChanged handler must never touch another
  named control that comes later in the XAML — guard it or reorder.

## Panel: provider card + language bar above output + speaker decor (Manh 2026-11-15)
- Mac reference (checked 2026-11-15): the panel window EXPANDS to fit
  content up to a max height, then the result ScrollView scrolls
  (PopupPanelSizing.frameToFit); the input also grows up to a max
  with a draggable divider. Each provider is a ProviderResultCard:
  a header row (chevron + provider icon + display name + status,
  click toggles expand) over a body with the result text and a
  Speak + Copy row on the right below it. The input (SourceInputView)
  mirrors it: Speak + Copy on the right below the source text.
- Windows, this round: output is one card per provider ("Google
  Translate" header + chevron expand, body = read-only TextBox so
  words stay selectable). The language pickers moved ABOVE the
  output (input -> language bar -> results, like mac's
  LanguageBarView); the old header under the title (blank line +
  pickers + "via Google Translate") is gone — the provider name now
  heads the card, so "via google" is removed.
- Speaker icons (decor first): E767 Volume, same glyph as the float
  icon's speaker state. Input: bottom-right of the input area (in
  the hint row, right-aligned). Output: bottom-right of the card
  body. TTS audio is NOT wired yet — the buttons do nothing on
  click; speaking comes after the mac side is stable.
- Window resize-to-fit (mac frameToFit) is NOT in this round: the
  panel stays 440x480 and the card fills the remaining space, the
  text scrolling when long. Collapsing the card shrinks it to its
  header (border stops stretching) but the window does not shrink
  yet — that comes with the resize work if Manh wants it.

## Main Text Size slider + top-bar default 17/16 (Manh 2026-11-15)

- The main "Text Size" slider was MISSING: under Appearance > Text
  Size there was only a header with no slider under it (the only
  slider was App name & Slogan in the Popup Panel subsection). Added
  it as the slider itself (mac LabeledContent style): a Slider with
  Header "Text Size" + a value label showing the current size.
- It controls the panel CONTENT font size (source + translation
  text) — a new setting PopupFontSize (double, default 14, mac
  popupFontSize, same name, same default 14, slider 12-24). Applied
  by TranslatePanelWindow.RefreshPopupFontSize(), called on open and
  from App.OnSettingsSaved, next to RefreshTopBarFontSize(). Like mac,
  dragging it changes the main size and the label shows the current
  size ("14 pt").
- App name & Slogan Font Size default changed 13 -> 17 (Manh: "set
  mặc định 17, slogan 16"). The 1 pt Windows gap stays (tagline 16),
  mac uses a 2 pt gap (17/15). The label shows both ("17 pt — app
  name; tagline 16 pt") so the -1 rule is visible in Settings.
  Existing installs that already saved 13 keep 13 until the slider
  is dragged once (a saved value beats the new default).

## Window resize-to-fit like mac frameToFit (Manh 2026-11-15)

- The output looked small in the fixed 440x480 window: the card sat
  in a `*` row and the text just scrolled in whatever space was
  left. Now the window grows/shrinks to fit its content, like mac's
  PopupPanelSizing.frameToFit and like the input box grows as you
  type (MaxHeight 120).
- All outer Grid rows are Auto now (the output card row was `*`);
  the translation box is capped by MaxHeight 260, so a long result
  scrolls instead of growing the window without bound.
- TranslatePanelWindow.ResizeToFitContent() measures the root Grid
  with infinite height, clamps the result to [260, work-area height
  - 16], and shifts the window up if the bottom would run
  off-screen. RequestResizeToFit() defers it via the dispatcher so
  layout has finished before measuring. Called from ShowPanelNear,
  ShowInputMode, ShowResult, SetProviderCardExpanded, and when the
  source box changes height (OnSourceTextBoxSizeChanged) — the box
  width is fixed and its height is content-driven, so a window
  resize never changes it back (no loop). It only resizes when the
  height actually changed, so a resize never re-triggers itself.
  The initial size stays 480 px (PanelHeight) until the deferred
  resize runs right after showing.

## General page order: hotkeys first, Launch with Watch selection (Manh 2026-11-15)

- Hotkeys moved to the TOP of Settings -> General (they were buried
  below Appearance): the hint + all 8 hotkey boxes now come first,
  since they are the most-used settings.
- "Launch with Windows" moved up next to "Watch selection
  automatically" (it was at the bottom of the page): both are
  app-behaviour toggles, so they sit together — Watch selection
  toggle + hint, then Launch toggle + Run-key note + error text.
- Pure XAML reorder (no code-behind change): control names are
  unchanged, so OnNavigatedTo and the handlers still find them. The
  slider ValueChanged guard still holds — each value label still
  comes right after its slider in XAML, so a ValueChanged firing
  during InitializeComponent still sees a null label and returns
  early (the General-blank bug, 2026-11-15).

## Input speaker inside the input box, like the output speaker (Manh 2026-11-15)

- The input speaker was in the hint row BELOW the input box, so it
  did not look like the output speaker, which sits inside the output
  card. Manh (on build 100): "loa ô input chưa giống ô output —
  nằm luôn bên trong input".
- Moved SpeakSourceButton into the input cell itself (Grid.Row 1,
  same cell as SourceTextBox, after it in XAML so it paints on top),
  anchored bottom-right with a small margin — it overlays the
  TextBox's bottom-right corner and stays there as the input grows
  ("luôn bên trong input"). The hint row is now just the hint
  TextBlock. Same glyph (E767 Volume) as the output speaker; still
  decor only, TTS not wired yet.

## Speaker icons bigger (Manh 2026-11-15)

- Both panel speaker icons (input + output) were a bit small at
  FontSize 12 / Padding 6,2. Bumped to FontSize 16 / Padding 8,4 —
  the glyph is now a touch larger than the 14 pt content text, like
  mac's Speak button (icon a bit larger than the text). Manh: "cho
  to hơn chút hơi bé (có thể là padding hay gì đó tuỳ em)".

## Output expand covering the bottom — no fix needed (Manh 2026-11-15)

- Manh first reported the expanding output "che luôn cả bottom"
  (covered the bottom rows), then re-tested and said it works on
  the 2nd try ("không che bottom"). Likely the first open measured
  before layout settled (ResizeToFitContent runs deferred via the
  dispatcher; the very first measure can lag one frame). No code
  change — keep watching; if it recurs on first open, the fix is to
  re-run the fit after the first layout pass.

## Panel: Capture OCR to the language row, Copy next to the speakers (Manh 2026-11-15)

- Capture OCR moved from the Actions row to the language picker
  row, at the right (pickers left, OCR right) — same row as the
  language selection.
- Copy buttons moved to the mac position (Speak + Copy together on
  the right, Speak first, Copy second): the source Copy (renamed
  from "Copy source" to just "Copy", resw updated too) sits next to
  the input speaker inside the input box; the translation Copy sits
  next to the output speaker in the card's speaker row. Both Copy
  buttons are now labelled just "Copy" (input copies the source,
  output copies the translation — like mac, where each side has its
  own "Copy").
- The translation Copy is two buttons toggling by mode
  (CopyButton in normal mode, CopyTranslationButton in OCR mode,
  UpdateCopyButtons) — both now live in the output speaker row, so
  only one is visible at a time. The Actions row keeps only Pin +
  Settings. Pure XAML move: names and Click handlers unchanged, so
  no code-behind change was needed.

## Settings nav pane width 250 (Manh 2026-11-15)

- OpenPaneLength 280 -> 250 (WinUI default is 320). Manh: "set
  panel trái Settings rộng 250 (hẹp hơn mặc định 320)". The 280 was
  a try-out from 2026-11-15; 250 is narrower still. If it feels
  cramped, drop OpenPaneLength entirely to return to the 320
  default.

## Text Size: size on the same line + live resize (Manh 2026-11-15)

- Manh: "Text Size: (hiện current size cùng line)" and "vuốt không
  thấy thay đổi size trong" — dragging Text Size showed no change
  inside the panel. Two fixes, both in this round:
- (1) The size now shows ON THE SAME LINE as the label, like mac's
  LabeledContent: the Slider Header itself is set in code to
  "Text Size: 14 pt" (main) and "App name & Slogan Font Size:
  17 pt — slogan 16 pt" (top bar, -1 rule visible). The separate
  value TextBlock below each slider is gone, and the sliders no
  longer use x:Uid Headers (resw entries removed).
- (2) RefreshPopupFontSize() and RefreshTopBarFontSize() now call
  RequestResizeToFit(), so the window grows/shrinks to fit the new
  text size — dragging the slider shows the change live instead of
  the bigger text being clipped by a fixed-height window. (The
  panel may be hidden behind Settings while dragging; the size
  applies when it is next shown, via the constructor refresh.)
- Guard change: _loading now starts true (field initializer). The
  old null-check guard relied on the value label sitting AFTER its
  slider in XAML; with the size in the Header there is no later
  control to check, so without this the ValueChanged firing during
  InitializeComponent (Minimum coerces 0 -> 12) would overwrite the
  saved size with 12 on every page construction. OnNavigatedTo
  still sets it false at the end.

## Esc on the OCR capture screen — low-level hook, not an accelerator (Manh 2026-11-15)

- Manh: pressing Esc on the capture screen did nothing, while
  right-click exited. The code already had a KeyDown handler for Esc
  (grid focused on show), but a tool window may never get keyboard
  focus, so KeyDown was dead — the same reason the panel needed its
  hook (0.1.3.54). Right-click worked because pointer input needs
  no focus.
- Fix: RegionSelectWindow now uses the same EscapeKeyMonitor (low-level
  WH_KEYBOARD_LL hook) the panel already uses — it sees Esc whatever
  window has focus. Started in SelectRegionAsync, stopped in Finish().
  The KeyDown handler stays as a backup; double Esc (hook + KeyDown)
  is safe because Finish() is exactly-once.
- This does NOT add a KeyboardAccelerator, so it cannot bring back
  the "Esc" key-tip pill (PopupHost 46x41) that the accelerator drew
  in 0.1.3.79. The hook is only installed while the capture is open
  and only intercepts Esc then; it always calls the next hook.

## Rename Text Size + new Setting Text Size (Manh 2026-11-15)

- Manh: keep the panel content slider as-is, but rename it — "Text
  Size" is now "Source & Translation Text Size" (the source + target
  text in the panel, mac popupFontSize, default 14). Name chosen by
  Muse (Manh: "tuỳ em chọn").
- New "Setting Text Size" slider (General -> Appearance -> Settings
  Window): the Settings window's OWN text size (mac appTextSize,
  new setting AppTextSize, default 14, slider 12-24). Main Settings
  text is this size; description/hint text is 1 pt smaller (the -1
  rule, shown in the Header: "Setting Text Size: 14 pt —
  description 13 pt", like the top-bar slogan).
- Applied by SettingsWindow.RefreshSettingsFontSize(): FontSize on the
  NavigationView (inherited by toggle/combo/textbox/slider labels
  and the nav pane), plus a visual-tree walk fixing TextBlocks that
  set an explicit size — brand titles (>= 20 pt, About "TransPeek"
  24 pt) untouched; grey/red description text -> size - 1;
  semibold/bold headers -> size + 2; other text -> size. Base size
  is captured once in TextBlock.Tag (free on these TextBlocks) and
  sizes are always set from the setting, so repeated calls never
  compound or stick. Called on show, on page navigation
  (ContentFrame.Navigated), and from App.OnSettingsSaved.

## Tray menu: "OCR to Clipboards" (Manh 2026-11-15)

- Manh: the Windows spelling is "OCR to Clipboards" (plural, capital
  C) — the tray menu item was "OCR to clipboard" (singular, lowercase).
  Renamed in resw (Tray.Menu.ClipboardOcr) + the TrayIconService
  fallback, and the matching hotkey label in Settings ("Hotkey for OCR
  to Clipboards") for consistency. The mac forward already said
  "OCR to Clipboards", so both sides now match.

## CI #110 red: FontWeights needs Microsoft.UI.Text (2026-11-15)

- Run #110 (162da8c) failed at Build: CS0103 "The name
  'FontWeights' does not exist in the current context" in
  SettingsWindow.xaml.cs:147 (the WMC9999 was a consequence, as
  usual). Cause: RefreshSettingsFontSize used FontWeights.SemiBold /
  Bold but the file had no `using Microsoft.UI.Text;`. The namespace
  is proven by the repo itself — TrayIconService.cs already has
  `using Microsoft.UI.Text;` and DictionaryBar.xaml.cs uses the
  fully-qualified Microsoft.UI.Text.FontWeights.Bold. Fix: add the
  using to SettingsWindow.xaml.cs (1 line).

## Setting Text Size: default 15, "(default: X pt)", all tabs (Manh 2026-11-15)

- Default AppTextSize 14 -> 15 (description 14, the -1 rule). The
  three font slider labels now show the default next to the current
  value so it is visible at a glance: "Source & Translation Text
  Size: N pt (default: 14 pt)", "App name & Slogan Font Size: N pt
  — slogan M pt (default: 17 pt)", "Setting Text Size: N pt —
  description M pt (default: 15 pt)". Note: a value already saved in
  settings.json wins over the new default until the slider is moved
  once (same as the earlier PopupTopBarFontSize 13 -> 17 change).
- Bug: Setting Text Size only adjusted the General tab. Cause: the
  TextBlock walk in RefreshSettingsFontSize ran synchronously from
  ContentFrame.Navigated, when the new page was not in the visual
  tree yet, so the walk found nothing — only General (present at
  show time) ever got sized. Fix: NavView.FontSize is still set
  immediately (inheritance), but the walk is deferred with
  this.DispatcherQueue.TryEnqueue so it runs after navigation/layout
  completes and sees the new page's tree. Idempotent, so queued
  repeats from a fast slider drag are harmless (FIFO order, last
  wins). Uses this.DispatcherQueue (Window instance property) to
  avoid the CS0176 ambiguity with the Microsoft.UI.Dispatching
  type (cf. TranslatePanelWindow).

## Panel: slogan spaced exactly 1 line from the input (Manh 2026-11-15)

- The TitleBar's bottom margin was a fixed 6 px, so the slogan sat
  too close to the input. Now the margin is set to the tagline
  (slogan) font size in RefreshTopBarFontSize, so the gap is exactly
  1 line and scales with the top-bar font size (default 16 px).
  XAML default margin updated to 16 to match.

## Setting Text Size still only 1 tab: Navigated + enqueue ran too early (Manh 2026-11-15)

- Investigation (Manh: "điều tra đi xem gắn đủ hết chưa"): all 5
  pages DO have TextBlocks with explicit FontSize (General 16,
  About 14, Ocr 6, Translation 4, Dictionary 4) and the Navigated
  hook IS wired (ContentFrame.Navigated -> RefreshSettingsFontSize).
  So the setting was "attached" everywhere — the walk just never
  saw the other pages' trees. Root cause: Navigated fires before
  the new page is in the visual tree, and a single dispatcher
  enqueue can still run before the page's tree exists, so the walk
  found nothing and only General (present at show time) ever got
  sized. The #112 enqueue fix did not cure it.
- Fix: walk a fresh page only once it is Loaded (guaranteed in the
  visual tree). RefreshSettingsFontSize now sets FontSize on BOTH
  NavView and ContentFrame (the Frame is the Page's direct parent,
  so inheritance reaches the page even if the NavigationView
  template ever blocks it), then ApplyToCurrentPage walks the page
  immediately if IsLoaded, else attaches a one-time Loaded handler
  that reads the LIVE setting at fire time (so a slider drag while
  the page was loading still lands the newest value). Navigated,
  ShowSettings and OnSettingsSaved all go through this one path.

## Appearance regrouped: Popup Panel holds the panel sliders, no "Settings Window" (Manh 2026-11-15)

- "Source & Translation Text Size" moved under the "Popup Panel"
  sub-header (it controls the translate panel's text, so it belongs
  there, next to "App name & Slogan Font Size"). "Setting Text Size"
  now sits directly under "Appearance" — the "Settings Window"
  sub-header is removed, because Appearance IS the Settings window's
  appearance and the extra header was redundant. The orphaned resw
  entry Settings.General.SettingsWindow.Text is removed too. XAML
  order only; no slider logic or setting changed.

## CI #113/#114 red: local function cannot have two '_' parameters (CS0100, 2026-11-15)

- Both runs failed at Build with CS0100 "The parameter name '_' is
  a duplicate" in SettingsWindow.xaml.cs:152 — the one-time Loaded
  handler was declared `void OnLoaded(object _, RoutedEventArgs _)`.
  A lambda CAN have two '_' parameters (discards), but a named local
  function cannot: its parameters must have distinct names. (The
  WMC9999 is the usual consequence.) Fix: rename to
  `void OnLoaded(object sender, RoutedEventArgs e)`. Lesson: use
  distinct names in any named function; '_' twice is only legal in
  lambdas.

## Setting Text Size moved BEFORE the Popup Panel header (Manh 2026-11-15)

- After the regroup, Setting Text Size sat after the two panel
  sliders, with no header between them — so it LOOKED like a third
  Popup Panel item even though it was meant as an Appearance-level
  item (Manh: "nó đang nằm trong popup panel"). Fix: move the
  Setting Text Size slider up to sit directly under the Appearance
  header, FIRST, before the Popup Panel sub-header. New order:
  Appearance -> Setting Text Size -> Popup Panel -> Source &
  Translation Text Size -> App name & Slogan Font Size. XAML order
  only; no slider logic or setting changed.

## Third line under the slogan: "Hover icon to translate, Click to listen" (Manh 2026-11-15, mirrors mac)

- Three lines, top to bottom: (1) app name "TransPeek", (2) slogan
  "Translate anywhere, Peek instantly!" — Manh intentionally
  capitalises only T and P (start of each clause), never every word;
  (3) new hint "Hover icon to translate, Click to listen" — only
  "Hover" and "Click" capitalised (start of each clause), "listen"
  lowercase like "translate". The hint is a few pt smaller than the
  slogan and dimmer (tertiary): panel = app name - 4 pt (13 at the
  17 default), About = AppTextSize - 4 (11 at the 15 default).
- Panel: HintText added to the TitleBar under TaglineText, text from
  AppInfo.Hint, size set in RefreshTopBarFontSize. The 1-line gap to
  the input is now measured from the hint (TitleBar bottom margin =
  hint size), since the hint is the last line.
- About: HintText added under the slogan (x:Uid Settings.About.Hint,
  resw entry added); the Settings font-size walk sizes it by Name
  (size - 4) because the normal grey-description rule (size - 1)
  would make it the same size as the slogan.
- Slogan itself changed from "Translate anywhere, peek instantly."
  to "Translate anywhere, Peek instantly!" (capital P, "!") — the
  pending change from 2026-11-15, now applied in AppInfo.Tagline,
  the About resw entry, and README.md. README (English only on
  Windows) adds the hint line under the slogan.
- Standing rule (Manh 2026-11-15): from now on, if Manh misspells
  something, correct the spelling when putting it into the app —
  never apply the misspelling as-is; watch capitalisation too.
  The slogan's T/P capitalisation above is the exception (as Manh
  specified).

## DPI: panel/toast cut off at 150% scale — MoveAndResize takes physical pixels (Manh 2026-11-15)

- Lenovo T460S, 1920x1080, scale 150%: panel bottom cut off (last row
  half-visible), toast after saving settings showed text but 2/3 cut
  off. At 100% scale everything was normal.
- Cause: `AppWindow.MoveAndResize` takes PHYSICAL pixels (Microsoft
  Learn, "Manage app windows": "AppWindow.Resize and
  AppWindow.ResizeClient take sizes in physical pixels"), but the panel
  (`ResizeToFitContent`, `ShowPanelNear`, `ShowInputMode`) and the toast
  (`ToastWindow.Show`, fixed 320x40) passed LOGICAL pixels (DIPs)
  straight in, without multiplying by the DPI factor. At 150% the
  window was 1.5x too small, so content was clipped. The float icon
  (`TriggerIconWindow.ShowAt`) and the caption drag region
  (`UpdateCaptionRegion`) already scaled correctly — only the panel and
  toast did not.
- Fix: multiply every logical size/offset by `WindowHelpers.DpiScale`
  (GetDpiForWindow/96) before `MoveAndResize`. At 100% the factor is
  1.0, so behaviour there is unchanged — the fix cannot break the
  working case. 150% is Microsoft's recommended default for 14" 1080p
  laptops, so this is not an edge case.
- Forwarded to mac as a precaution (Manh only has one 13" M1 Mac and
  cannot test every resolution/scale). Mac checked and is NOT affected:
  AppKit uses points (logical) throughout — NSWindow.setFrame,
  NSScreen.frame/visibleFrame, NSEvent.mouseLocation, SwiftUI
  measurement are all points, the system converts point to pixel, and
  no code uses backingScaleFactor or pixel sizes (grep = 0).

## Float icon: SetWindowRgn circle (Manh 2026-11-15)

- On the Lenovo T460S the transparent icon window showed a grey
  square around the white circle — DWM blur-behind fails there
  (Transparency effects off, or old Intel HD 520 driver). Manh chose
  ONLY SetWindowRgn (cut the window into a circle); no
  AdvancedEffectsEnabled check and no "turn on Transparency effects"
  prompt.
- `SetWindowRgn` (winuser.h, User32.dll) + `CreateEllipticRgn`
  (wingdi.h, Gdi32.dll) — both verified against Microsoft Learn. The
  region is in PHYSICAL pixels, so its diameter is 32 x DpiScale (same
  w/h as the window in ShowAt). Applied in ShowAt on every show, so a
  DPI change is picked up. On success the OS owns the region — only
  DeleteObject it when SetWindowRgn fails.

## Tray renames: "Translate Selection" + "Translate Clipboard", "OCR to Clipboard" pair (Manh 2026-11-15, mac parity)

- "Selection Translation" -> "Translate Selection", "Clipboard
  Translation" -> "Translate Clipboard" (verb-first reads as an
  action; "Clipboard Translation" was ambiguous — translate FROM the
  clipboard, or translate then copy INTO it).
- "OCR from Screen" -> "OCR Screen Translate"; "OCR to Clipboards" ->
  "OCR to Clipboard" (singular — a machine has ONE clipboard;
  "Clipboards" is wrong). New item right under it: "OCR to Clipboard -
  Translated" — OCR a region, translate, copy the TRANSLATION to the
  clipboard, no panel (waits up to 30 s, opens the panel with the
  source + error on failure). Glyph E774 Globe (Segoe Fluent Icons —
  E76E is Emoji2, not Globe). Same renames applied to the hotkey
  labels in Settings -> General.
- DROPPED as redundant: "Translate to Clipboard" / "Translate
  Selection to Clipboard" — a translated copy is already covered by
  "OCR to Clipboard - Translated" (OCR) or copying from the panel
  after "Translate Selection".

## Google dictionary link: "{text}+meaning" (Manh 2026-11-15) + panel hint "word"

- Google default link changed from `define+{text}` to `{text}+meaning`
  — the way Manh used to search when learning English. Both show the
  definition card; "meaning" reads more naturally and adds web
  results. MigrateDefaults upgrades saved links (old URL -> new, and
  "Google Dictionary" -> "Google" in the same pass); the old-defaults
  list used for the Cambridge Vi append now carries the new URL.
  Tests updated (migration asserts the new URL; UrlFor fixture uses
  the new template).
- Panel hint: "Select text in the panel..." -> "Select a word in the
  panel to look it up in the dictionary" ("look up a word" already
  covers phrases/idioms; "text" was vague, "sentence" is not what a
  dictionary looks up).

## Google 429: retry with backoff, no fallback provider (Manh 2026-11-15)

- Cause found in the company PC log ("LOG TransSpeek TEST"): Google
  Translate returns HTTP 429 (rate limit) on the first request after
  launch; restarting / retrying 1-2 times gets through (limit relaxes
  over time/IP).
- Fix: GoogleTranslateProvider retries the SAME provider up to 3
  times, backoff 1s -> 2s -> 4s, instead of failing at once. The panel
  keeps showing "Translating…" during the retries (mac parity). After
  the retries, a clear message replaces the raw 429 body:
  "Google Translate is rate-limiting requests. Try again in a few
  minutes." (TranslationException.RateLimited, StatusCode 429).
- NO fallback provider on 429 (Manh): the coordinator rethrows a 429
  immediately instead of trying the next provider. TranslationException
  gained a StatusCode property (set by ApiError/RateLimited) so the
  coordinator can tell a 429 from any other failure. Test:
  TranslateAsync_RateLimited_DoesNotFallBack.

## Tesseract eng+vie bundled with the app (Manh 2026-11-15)

- Manh: "Nhúng nhé". eng (15,400,601 byte, 14.7 MB) + vie
  (12,435,550 byte, 11.9 MB) from tessdata_best, ~27 MB total — the
  app goes from ~133 MB to ~160 MB. OCR must work out of the box, no
  first-run download, no "OCR doesn't work yet" gap for a new user.
- CI (`build.yml`, step after Publish) downloads eng+vie from
  tessdata_best into `publish/tessdata/` (next to the exe). The step
  fails the build if either file is under 1 MB (a 404 HTML page would
  be a few KB) — the bundle must never silently ship a broken file.
- Provider: `TesseractLanguageDataManager` gained
  `BundledTessdataDirectory` (tessdata next to the exe, via
  `AppContext.BaseDirectory`), `BundledCodes()` and `AvailableCodes()`
  (bundled OR local). `DownloadAsync` prefers the bundled copy (local
  file copy, no network) and only downloads from tessdata_best when
  the app does not bundle that language. The engine still reads from
  ONE directory (`TessdataDirectory` = %LOCALAPPDATA%) — a bundled
  file is copied there on first use, so a mixed bundle/local split
  can never confuse the engine.
- `IsInstalled` / `InstalledCodes` stay local-only (the engine's
  missing check and the copy/download decision must see the local
  dir). Status display uses `AvailableCodes()` instead, so Settings
  → OCR and the provider status read "Ready (eng, vie)" on first run
  instead of "not downloaded" — `TesseractOcrProvider.Status`,
  `SupportedLanguages`, `IsEngineAvailable`, and
  `OcrPage.RefreshTesseractStatus` all use it.
- Extra languages still download on demand to the local dir (the
  all-languages picker is a separate session, after the 2026-11-16
  token reset). The old "never bundled" comment is removed.

## Panel: user-resizable by dragging the edges (Manh 2026-11-15, mac parity)

- Manh asked (via the mac chat) whether the panel should be responsive
  automatically or user-resizable, for big screens. Mac conclusion:
  NO auto-responsive (a popup should stay compact — auto-growing would
  cover the user's work, and translation content does not need a big
  panel); instead let the user drag it bigger when they want. Mac
  already had it; Windows did not, so it was added here to match.
- Mac reference (PopupPanel / PopupView / PopupPanelController):
  default 450x350, min 280x200, max 800x800; a resize grip at the
  bottom-right (custom DragGesture); the size persists
  (popupDefaultWidth/Height); auto-fit (frameToFit) keeps the current
  width and never goes below the persisted height (baselineHeight);
  after a manual resize, auto-fit is suppressed for the rest of that
  translation (manualResizeGeneration == translationGeneration), and
  the height is only persisted when it moved more than 8 pt.
- Windows implementation — NATIVE borders, not a custom drag grip:
  `OverlappedPresenter.IsResizable = true` (only the panel;
  `WindowHelpers.MakeBorderlessTopmost` gained a `resizable`
  parameter, default false, so the trigger icon / toast / region
  window stay non-resizable) plus `InputNonClientPointerSource.
  SetRegionRects` with `NonClientRegionKind.BottomBorder` and
  `RightBorder` (Microsoft Learn, Windows App SDK: the enum has the
  four borders + Caption/Passthrough, no corner values — the
  bottom-right corner is where the two strips overlap and the OS
  treats it as a diagonal resize). The OS shows the resize cursor
  and handles the drag itself, in physical pixels, so it is correct
  at 150% with no manual logical/physical conversion (the DPI bug
  class that cut the panel off at 150%). A custom grip was considered
  and rejected: it would need its own pointer-drag math (the exact
  DPI conversion just fixed) and a hover cursor via the protected
  `UIElement.ProtectedCursor` (not settable from Window code-behind).
  The strips are 8 physical px along the bottom/right edges, inside
  the root Grid's 12 px padding, so they never cover interactive
  content. A small "↘" glyph at the bottom-right corner (IsHitTest-
  Visible=False, decorative) hints that the edge drags.
- Size tracking: `AppWindow.Changed` + `AppWindowChangedEventArgs.
  DidSizeChange` (Microsoft Learn, "Manage app windows"). A user
  border-drag is told apart from our own programmatic MoveAndResize
  (show + auto-fit) by the size itself: a programmatic resize sets
  exactly `_lastProgrammaticSize`, recorded before every
  MoveAndResize; any other size is a user drag. On a user drag,
  `_manualResizeGeneration = _epoch` suppresses auto-fit for the rest
  of that show (mac generation rule), and the size is persisted
  300 ms after the last size change (debounced — Changed fires on
  every step of a drag). Width is always saved; height only when it
  moved more than 8 pt from the saved value (mac rule). The window
  itself is clamped to the limits when the drag ends (the OS does not
  enforce min/max on a borderless window).
- Limits: min 400x260, max 800x800, further clamped to the display
  work area. The limits are chosen for the WINDOWS panel, not copied
  from mac (Manh 2026-11-15 — mac's 450x350 / 280x200 / 800x800 are
  reference only): min width 400 fits the language row (Source 128 +
  Swap + Target 128 + Capture OCR, ~390); min height 260 is the same
  floor auto-fit uses, because the Windows content cannot fit in
  less; max 800x800 is roomy on a big screen.
- Settings: `PanelWidth` (default 440) / `PanelHeight` (default 480)
  in `AppSettings` (mac names popupDefaultWidth/Height). Show
  (ShowPanelNear / ShowInputMode) opens at the persisted size.
  Auto-fit (`ResizeToFitContent`) never changes the width (it
  measures at the current width, mac frameToFit keeps
  currentFrame.width) and never goes below the persisted
  PanelHeight (mac baselineHeight) — so a panel the user dragged
  tall stays tall, and it still grows when the content needs more.
  A new show resets the suppression, so auto-fit runs fresh again.

## Icon "A": regular weight, sea blue; two new hotkeys; hotkey order follows the tray menu (Manh 2026-11-15)

- Icon: the float icon's letter "A" is now REGULAR weight (was Bold)
  and sea blue `#0078D4` (the Windows accent blue — the mac glyph went
  `.systemBlue` the same day). The white circle and its grey border are
  unchanged, and the icon still does NOT follow system dark/light —
  that is planned for later (Manh 2026-11-15). The speaker mark (the
  click = listen/TTS state) stays near-black; only the "A" changed.
- Two new hotkey settings (Settings -> General, in the hotkey list):
  "OCR to Clipboard - Translated" defaults to Control+Alt+R
  (tRanslated — T is taken by Translate) and "Translate Clipboard"
  defaults to Control+Alt+L (cLipboard — C is taken by OCR to
  Clipboard). Both are registered globally in `App.StartHotkeys` and
  call the same handlers as their tray menu items
  (`RunSilentOcrTranslatedAsync`, `TranslateClipboardAsync`). The
  duplicate check in `GeneralPage` (`AllHotkeys`) covers all ten.
- Hotkey order in Settings now follows the tray menu (Manh 2026-11-15):
  Hotkey to open the translate panel, Manual Translation, OCR Screen
  Translate, OCR to Clipboard, OCR to Clipboard - Translated, Translate
  Selection, Translate Clipboard, Screenshot Copy, Scan Barcodes / QR
  Codes, Settings. (Before: OCR sat second and Manual third.)
- BUG FIX in the same round: the tray menu's "Translate Clipboard"
  item showed the hint "Ctrl+Alt+T" — the "open the translate panel"
  chord, which does NOT trigger Translate Clipboard (it translates the
  current selection instead). It now shows the real Translate Clipboard
  chord (Control+Alt+L). "OCR to Clipboard - Translated" also shows
  its chord in the tray menu now (Control+Alt+R).
- Tests: `AppSettingsHotkeyTests` covers all ten defaults (distinct +
  expected values).

## Vertical resize: full Windows approach — snap back for short text, output grows for long text (Manh 2026-11-15)

- Problem: dragging the panel taller left useless white space below the
  footer (all 8 root rows were Auto and the translation box was capped
  at MaxHeight 260, so no row grew). Manh chose the full Windows
  approach over a plain mac-style snap-back: short text snaps back on
  release, long text grows the output so more of it is visible.
- XAML (`TranslatePanelWindow.xaml`): the output-card row is now `*`
  (was Auto) and named `OutputCardRow`. The card stretches with the
  window, so the translation fills the extra height. Collapsed, the row
  goes back to Auto in code — a `*` row would keep its share of the
  window height and leave empty space below the header, and
  VerticalAlignment.Top alone does not prevent that (the row itself
  still takes the space).
- `PersistResizedSize` (on release, 300 ms debounce): reads the
  translation box's ScrollViewer — ExtentHeight (full text height) vs
  ViewportHeight (visible height). The window height needed to show all
  the text is the current height plus (full - visible), because the card
  row is `*` and the box row inside it is `*`, so the box grows 1:1
  with the window. If the drag left the window taller than needed by
  more than 8 pt (mac rule), it snaps back to the needed height and the
  box cap resets to 260. Otherwise the dragged height is kept and the
  box cap is raised to the full text height, so the output grows past
  260 and shows more text. Cannot measure (card collapsed / ScrollViewer
  not ready): keep the dragged height, previous behaviour.
- `ResizeToFitContent` (auto-fit): resets the box cap to 260 and forces
  layout before measuring, so the measure and the ScrollViewer read are
  both with the cap. The baseline is no longer always
  max(PanelHeight, desired): if the full text fits in the capped box
  (ExtentHeight <= 260) the panel shrinks to the content height, so a
  new show with short text after a tall manual resize opens compact
  again instead of carrying the tall height (and its white space)
  forward. If the text is long, the PanelHeight floor stays, so a manual
  tall resize sticks — and the box cap is then raised to the full text
  height so the output fills the tall window instead of leaving the
  extra height empty.
- Helper: `FindDescendant<T>` walks the visual tree to reach the
  ScrollViewer inside the TextBox (GetTemplateChild is protected, so the
  Window cannot call it on the TextBox directly); `TryGetTranslationTextHeights`
  returns false when the card is collapsed or the ScrollViewer is not
  ready, and callers fall back instead of guessing.
- Mac: asked the mac chat the same day whether mac can do this natively
  (snap back for short text, output grows for long text) so both sides
  behave the same on a vertical drag; waiting on their answer.

## General settings: "Keyboard shortcuts" header; Launch with Windows first (Manh 2026-11-15)

- The hotkeys at the top of General now have a section header,
  "Keyboard shortcuts", in the same style as Appearance (16 pt,
  SemiBold) — the page reads as sections instead of a bare list at the
  top. The name follows Windows: "Keyboard shortcuts" is the term
  Windows Settings uses (resw `Settings.General.KeyboardShortcuts`).
  The individual boxes keep their "Hotkey to/for ..." labels.
- Behaviour group order changed: Launch with Windows now sits FIRST,
  then Watch selection automatically, then how long to hover, then
  trigger icon hides after. (Before: Watch selection was first and
  Launch sat under it.) Each toggle moved with its own hint/note/error
  text. No code change — XAML order only, the code-behind addresses
  the toggles by name.

## Hotkey labels match the tray menu exactly, no examples (Manh 2026-11-15)

- The ten hotkey boxes in Settings -> General now use exactly the tray
  menu item names as their Header, with no "Hotkey to/for ..." prefix
  and no "(e.g. ...)" example — the box itself already shows the
  current combination, so the example was redundant. The names come
  from TrayIconService (the fallbacks, which are what the tray shows):
  "Open translate panel", "Manual Translation", "OCR Screen Translate",
  "OCR to Clipboard", "OCR to Clipboard - Translated", "Translate
  Selection", "Translate Clipboard", "Screenshot Copy", "Scan
  Barcodes/QR Codes" (no spaces around the slash, as in the tray),
  "Settings…" (with the ellipsis, as in the tray). Both the XAML Header
  fallback and the resw values were updated (resw wins at runtime).
- Note: "Open translate panel" has no tray menu item (the tray has no
  open-panel entry — the panel opens from selection/hover/hotkey), so
  that one is the plain action name in the same concise style, not a
  tray name. Manh can rename it if he wants a different one.

## Hotkey hint moved below the last hotkey box; shortcuts stay at the top (Manh 2026-11-15)

- The how-to hint ("Click a hotkey box, then press the key combination
  — no typing needed. Esc cancels.") moved from the top of the Keyboard
  shortcuts section to below the last hotkey box (Settings…), as the
  section's footer note. It was sitting at the top where it read as
  attached to the wrong group.
- The whole Keyboard shortcuts section STAYS at the top of General and
  is NOT moved below Appearance / Popup Panel (Manh asked whether to
  move it, and whether proper UX/UI rules demand it). The answer is to
  keep it: the most-used settings go first, and the hotkeys are the
  most-used settings on this page (decided 2026-11-15 for the same
  reason). Opening Settings and seeing the shortcut list at once — so
  the user can check a shortcut he forgot without scrolling — is
  recognition over recall, a core UX principle, not a shortcut around
  the rules. Appearance (font sizes) is set-and-forget, so it belongs
  lower; the "settings are stored in ..." line stays the page footer.

## Resize grip: three diagonal lines like mac, not a "↘" arrow (Manh 2026-11-15)

- The bottom-right resize indicator is now three diagonal lines (a
  12x12 Canvas, 3 lines 3 px apart, 1 px wide, tertiary colour), drawn
  exactly like mac's ResizeGripView (PopupView.swift: Canvas, 3 lines,
  spacing 3, lineWidth 1, secondary at 0.4 opacity). It replaces the
  single "↘" arrow, which read as a plain arrow rather than a resize
  grip. Decorative only (IsHitTestVisible=False) — the actual drag is
  still the OS native resize strips (BottomBorder + RightBorder), which
  show the resize cursor there; the grip just signals that the corner
  drags diagonally. It sits in the root Grid's 12 px padding, below the
  footer, covering no interactive content.

## Hotkey hint back at the TOP; Run key note is about Launch, not shortcuts (Manh 2026-11-15)

- The how-to hint ("Click a hotkey box, then press the key combination
  ...") goes back to the TOP of the Keyboard shortcuts list, right under
  the header — this corrects the previous move that put it below the
  last box. The hint introduces the list; it is not a footer note.
- The "Writes to the Windows Run key (HKCU\...). No admin rights
  needed." line STAYS under the Launch with Windows toggle. It is NOT
  about keyboard shortcuts: the Windows Run key is the registry
  location (HKCU\Software\Microsoft\Windows\CurrentVersion\Run) where
  Windows records which apps start automatically at login, so the note
  explains what the Launch toggle does. The word "Run" here is the
  registry key's name, not "run a shortcut". It belongs with Launch.

## Release race: pushing twice in quick succession lost the #129 pre-release (Manh 2026-11-15)

- Run #129 (commit f6e9ef6, hotkey hint back at top) completed SUCCESS,
  but no v0.1.3.129 pre-release exists. The only release is v0.1.3.128
  (asset TransPeek-Windows-128.zip — a #128 build, hint BELOW the last
  box, the wrong position), even though its tag points at f6e9ef6.
  Cause: #129 was pushed while #128 was still running, and the two
  runs' "Create pre-release" / "Delete older pre-releases" steps raced
  (the delete step keeps only the newest by createdAt; with two runs
  creating and deleting at the same time the newer release got removed).
  The run logs cannot be read via the public API (403, needs admin).
- Lesson: do NOT push a new [build-app] commit while the previous run
  is still in progress — wait for it to finish (or be cancelled) first,
  or the release can be lost. This push re-triggers a clean run (#130)
  with no concurrent run, so v0.1.3.130 is created properly.

## Resize grip flush against the corner; build text clears it (Manh 2026-11-16)

- The 3-line resize grip now sits FLUSH against the window's
  bottom-right corner: the Canvas margin is -11 on the right and
  bottom, cancelling the root Grid's 12 px padding, so the 12x12 grip
  ends 1 px from the edge — inside the 8 px native resize strip. Before,
  it sat 12 px in from the corner (inside the padding), so hovering the
  lines did not show the resize cursor; the user had to travel a further
  12 px to the corner before the cursor changed. Now the cursor turns
  to resize as soon as the mouse reaches the lines.
- The footer build/version text (FooterBuildText) gets a 16 px right
  margin so it stops clear of the grip. Before, the grip sat at the
  bottom of the footer row and overlapped the right end of the version
  text ("góc chồng version" in a company-doc screenshot). The grip is
  still decorative only (IsHitTestVisible=False); the OS strips do the
  dragging.

## Build/version text aligned to the right edge, under the Settings icon (Manh 2026-11-16)

- The footer build/version text (FooterBuildText) has NO right margin
  any more — its right edge lines up with the Settings icon in the
  Actions row above it and with the output card's right edge, all at
  the content edge. This reverses the 16 px right margin added with the
  flush grip, which had pushed the version too far left; Manh wants it
  tight to the right, aligned down from the Settings icon (the company
  doc screenshot showed the version sitting too far in from the edge).
  The 1 px corner touch with the flush grip is negligible.

## Feature modules: one folder per feature, never inside the translate code (Manh 2026-11-17)

- New features (tomato/focus/rest-eyes, Fast Read/RSVP, ...) live in their
  own folder/module — never stuffed into the main/translate code. Reason:
  to avoid conflicts and to stop breaking the translate panel, which has
  already been through too many fix rounds.
- Same structure on mac + Windows, one folder per feature, UI + logic
  together in it (not split too small — one file per function is hard to
  find): `FastRead/` (RSVP engine + reader window + view), `BreakTimer/`
  (Pomodoro/focus/20-20-20 timer + tab, later), `Shortcuts/` (one shortcut
  table shared by the "?" embed and the multi-tab shortcut panel, no
  copy-paste of two versions — TODO refactor).
- The tab frame stays thin: one tab = one view + one name, never calling
  translation code directly. Only the thin joints touch shared code
  (button, menubar/tray, Alt+click, Settings toggle). Things truly shared
  (float icon, panel show/hide, Settings, translation provider) stay
  shared, never duplicated.
- Windows first application: `FastRead/` on branch `feature/fast-read-rsvp`
  (engine `src/TransPeek.Core/FastRead/`, window `src/TransPeek/FastRead/`);
  rule also recorded in `AGENTS.md` §6 and `ARCHITECTURE.md` §2.

## Google Translate: full 249 languages + searchable picker + About share (2026-11-19, Manh)

- **Full language list:** `LanguageCode.Supported` now has all 249 languages Google Translate supports (was 14), for both source and target. Google's free endpoint translates server-side — the app only sends the language code, so 250 entries cost a few KB and the app does not get bigger. Verified against Wikipedia's Google Translate list (249, counted by script), Google Cloud's NMT table, and the `google-translate-api-x` language file.
- **"pt" alias:** Google's 249 list only `pt-BR` and `pt-PT` — plain `pt` is NOT in it. Kept `pt` as a 250th entry anyway: it is a valid code the endpoint accepts, and settings saved before 0.1.4 may already use it. `pt-BR` / `pt-PT` are in the list too.
- **`ForGoogle` fix (same round):** the old code cut every code at `-` (`Split('-')[0]`), which turned Dari (`fa-AF`) into Persian (`fa`) and dropped scripts (`crh-Latn`, `ms-Arab`, ...). Codes now pass through unchanged except `zh-Hans`→`zh-CN` and `zh-Hant`→`zh-TW`.
- **Popular first:** the 20 most-used languages are pinned at the top of the picker (Manh's pick from 14/20/30/50): the 14 the app already had + Hindi, Indonesian, Turkish, Dutch, Polish, Ukrainian — then the full list A–Z by English name.
- **Searchable picker (AutoSuggestBox, since CI #189):** the language
  pickers are AutoSuggestBox — the native control built for type-to-search
  (Microsoft Learn: "Use an AutoSuggestBox to provide a list of
  suggestions for a user to select from as they type"). Typing filters
  the list live (native name, English name, or code); focusing the box
  shows the full list again. The suggestion list height is capped
  (`MaxSuggestionListHeight` 320) so it scrolls inside instead of
  expanding to 249 rows. One shared helper (`LanguageSuggestBoxHelper`,
  used by Settings + the translate panel) — no copy-paste of the filter
  logic. Leaving without a pick restores the saved language; Enter with
  an exact name/code match picks it. The first version used an editable
  ComboBox + `TextChanged` — but WinUI ComboBox HAS no `TextChanged`
  event (CS1061, CI #189); it only has `TextSubmitted` (Enter/focus-loss),
  so it cannot filter live. The helper file still named
  `LanguageComboBoxHelper.cs` — rename it on the next browser-side edit
  (snapshot push cannot rename files).
- **About share row (Manh):** Settings > About has the same row of share buttons as the Fast Read completion window on mac (Facebook / Threads / X / Reddit / LinkedIn / Mail), sharing the app tagline + the Windows repo link. URLs live in one place (`SharePlatform`, mirroring mac `RSVPSharePlatform`); the row is built by `ShareRowBuilder` (mirroring mac `ShareRowView`). Facebook and LinkedIn share the link only (Facebook removed the `quote` param). Mail glyph is E715 (verified on Microsoft Learn — E8A5 is Document).
- **Alt+click fix (Manh):** when Click icon = Fast Read, Alt+click opened Read instead of speaking. Cause: `Tapped` fires on pointer release and Alt released just before pointer-up was missed. Fix: the click now reads Alt from the pointer event at press time (`PointerPressed` + `KeyModifiers.Menu`, mirroring mac `handleIconClick` reading `event.modifierFlags`); the 0.1s `GetAsyncKeyState` poll + pointer events drive only the preview glyph.
- **Version 0.1.4:** bumped to match mac (`TransPeek.csproj` `<Version>`, `Package.appxmanifest`, `build.yml` version stamp + pre-release tag/title + delete regex, workflow docs). The `0.1.3.<run>-saved` releases stay valid (the delete regex only matches `v0.1.3.*`).
- **Quality note:** the ~69 languages added by Google in June 2024 are mostly low-resource languages; their translation quality is lower than the big languages. Google publishes no per-language quality table.

## Share row on panel left (2026-11-19, Manh)

- **Why:** the panel is where users spend the most time; Settings is rarely opened — share must be on the panel, not just About (Manh 2026-11-19 04:15, mac d414700).
- **Placement (Manh 04:25–04:33, kept):** at the end of panel left's content, right after the shortcuts table — label "Share TransPeek" (caption, secondary) + the same 6-icon row, both centered. No new card, no Spacer pushing it to the bottom (the space left when the 2 cards collapse is reserved for a future focus timer).
- **Alternatives considered and rejected:** (a) next to the cat icon in the bottom bar — too cramped at 420pt (6 icons ~218pt + GitHub ~65pt + version ~120pt) and the 28pt share icons dwarf the 16pt cat; (b) a fixed line above the cat icon (panel left only) — always visible and clean, but needs auto-fit changes + ~50pt fixed extra height — complex.
- **Windows adaptation:** mac puts the row at the end of the left panel's single ScrollView; Windows has no such ScrollView (each card scrolls itself), so the row is a third Auto row in the left tab's Grid — it takes ~50pt from the shortcuts card, never from the panel height, so auto-fit needs no change.
- **About cross-check (same round):** the Windows About share row (0.1.4) matches mac's ShareRowView exactly — same 6 badges (28px, spacing 10, same brand colors and adaptive Threads/X/Mail), same tagline text + repo link, Facebook/LinkedIn link-only. Mail uses FontIcon E715 (mac: SF envelope.fill).
- **Future idea (NOT implemented):** later, hide the cat + GitHub link on panel left and put share in that exact spot (middle of the bottom bar) — share becomes the main call-to-action on panel left. Note: the bottom bar is shared by all 3 tabs, so that change must know which tab is showing and hide only on panel left.

## CI #189 red: ComboBox has no TextChanged — AutoSuggestBox instead (2026-11-19)

- Run #189 (head 41acb70) failed at Build in 22s: CS1061 in
  `LanguageComboBoxHelper.cs:49` — `ComboBox` has no definition for
  `TextChanged`. The WMC9999 was a consequence, as usual. Cause: the
  helper was written from memory (WPF-era assumption) without checking
  Microsoft Learn. FIX: the picker is now an AutoSuggestBox
  (`LanguageSuggestBoxHelper`): `TextChanged` with
  `Reason == UserInput` filters live, `SuggestionChosen` commits,
  `QuerySubmitted` handles Enter (exact name/code match picks, else
  restore), `GotFocus` shows the full list, `LostFocus` restores the
  saved language. All API names verified on the Microsoft Learn
  AutoSuggestBox class page first (`MaxSuggestionListHeight`,
  `IsSuggestionListOpen`, `TextMemberPath`, `UpdateTextOnSelect`,
  `Header`, `ChosenSuggestion`, `QueryText`).
- The switch also fixed a latent double-subscribe wart: Settings
  rebuilt the helper on every navigation, subscribing the same box
  twice. Helpers are now created once (`??=`) and `Refresh`ed after.
- Lesson (same family as #188): WinUI ComboBox only has
  `TextSubmitted` (fires on Enter/focus-loss, only for text not
  matching an entry) — it cannot do live filtering. For a searchable
  long list, AutoSuggestBox is the native control; do not reach for
  template-part digging (custom, fragile) to force ComboBox to do it.
- **CI #190 follow-up (same commit round):** the first AutoSuggestBox
  version failed with CS1061 at `LanguageComboBoxHelper.cs:108` —
  `AutoSuggestBoxTextChangedEventArgs` has NO `QueryText` (verified on
  Microsoft Learn: that args class has only `Reason`; `QueryText`
  belongs to `AutoSuggestBoxQuerySubmittedEventArgs` only). The Learn
  sentence "the event args QueryText property contains the text" was
  about QuerySubmitted, and I applied it to TextChanged. Fix: read
  the text from the control itself (`sender.Text`) in the TextChanged
  handler. Lesson: when one sentence in the docs covers several
  events, check WHICH args class it belongs to — do not carry a
  property across event-args classes.

## CI #191 test feedback batch (2026-11-19)

Manh test bản 0.1.4.191: Google 249 ngôn ngữ ngon. 4 việc còn lại làm gộp
1 commit sau khi duyệt khung:

- **Window Read — wheel về cuộn, kéo để đổi cỡ:** wheel trên vùng đọc
  (chữ focus + context) về lại cuộn context (trước đây wheel đổi cỡ chữ,
  trùng với cuộn); kéo giữ left-click lên/xuống trên vùng context đổi
  Context size % (±5% mỗi 24 px, kẹp 25–100%, lưu khi thả chuột). Chỉ kéo
  dọc hẳn mới đổi cỡ (≥12 px, dọc nhiều hơn ngang) — kéo ngang là bôi
  chọn text (context đã bật `IsTextSelectionEnabled` để tra từ). Kéo lăn
  chuột trên thanh progress vẫn bước từng từ. Focus size chỉ đổi bằng
  picker dưới đáy (không còn wheel).
- **Panel — wheel chuyển tab trên topbar:** hàng hint gắn handler XAML
  (trước đây thiếu); hàng tên app/slogan ĐÃ có handler XAML nhưng không
  chạy — nguyên nhân: hàng này là vùng caption native (kéo di chuyển
  panel) nên hệ thống nuốt mất event wheel, XAML không bao giờ nhận
  được. Sửa bằng cách bắt `WM_MOUSEWHEEL` trong window subclass
  (`MinSizeSubclassProc`, đã có sẵn cho min-size), kiểm tra con trỏ có
  trong hình chữ nhật TitleBar không rồi chuyển tab, dùng chung throttle
  0.4s với các vùng XAML. Sau này có thể đưa 3 chấm lên dưới hint cho
  đồng bộ mac (chưa làm — chỉ ghi lại).
- **Icon — A thường (không bold), bong bóng làm dự phòng:** icon thường
  về chữ A 15pt không bold màu nghệ (như trước Style B); bong bóng E8BD
  giữ nguyên trong code (`BubbleMark`, luôn ẩn) + ghi doc làm mẫu style
  dự phòng.
- **Alt+click — nguyên nhân thật (CI #191):** handler click viết
  `altHeld || clickReads` (OR) thay vì XOR — Click=Read thì Alt+click
  lúc nào cũng vào Read, dù preview đã dùng XOR nên icon đổi đúng mà
  click thì không. Sửa 1 dòng thành `altHeld != clickReads`. Preview Alt
  cũng đổi: giữ Alt + Click=Read thì icon hiện loa E767 thay vì bong
  bóng. Bài học: khi preview và hành động dùng 2 công thức khác nhau
  (preview XOR, click OR) thì icon nói một đằng, click làm một nẻo —
  phải dùng cùng 1 công thức cho cả 2.

## CI #188 red: Microsoft.UI.Color does not exist (2026-11-19)

- Run #188 (head 5e9734b) failed at Build in 16s: CS0234 in
  `ShareRowBuilder.cs:116` — the type `Color` does not exist in the
  namespace `Microsoft.UI`. The WMC9999 was a consequence, as usual.
  FIX: the Color struct is `Windows.UI.Color` — only the parameter type
  changed (`Microsoft.UI.Color` → `Windows.UI.Color`); `Microsoft.UI.Colors`
  (static class, used in SettingsWindow) and `Microsoft.UI.ColorHelper`
  (used in FastReadWindow/TranslatePanelWindow) DO resolve in this project.
- Runs #186/#187 failed the same way, not concurrency-cancelled: the
  same broken file was in all three commits (1c3a8fa introduced it), and
  the build fails in ~16s — each run concluded as failure before the
  next push could cancel it.
- Lesson: `Microsoft.UI` has `Colors` and `ColorHelper`, but the `Color`
  struct itself lives in `Windows.UI` — check the namespace per type,
  not per family (same family as the CI #80 `Windows.UI.Colors` trap,
  but the opposite direction).

## Đợt sửa 2026-11-19 (fade tab, chuột bàn tay About, share panel left) — chờ CI

- **Fade đổi tab 0.15s ease-out (như mac):** `ShowTab` fade-in nội dung
  tab mới bằng `Storyboard` + `DoubleAnimation` trên `Opacity`
  (`Microsoft.UI.Xaml.Media.Animation`), chỉ fade vào, tab cũ ẩn luôn.
  Fade đang chạy thì dừng + trả hết opacity về 1 trước khi fade mới,
  nên lật tab nhanh không để lại element mờ dở; bỏ qua fade khi cửa sổ
  chưa load (lần mở đầu tiên) để không kẹt opacity 0.
- **About: nút bấm được hiện chuột bàn tay:** WinUI 3 không có thuộc
  tính Cursor public — `UIElement.ProtectedCursor` là `protected`
  (Microsoft Learn), chỉ set được từ trong subclass của control, nên
  làm 2 subclass nhỏ (`HandCursorButton`, `HandCursorHyperlinkButton`
  trong `src/TransPeek/Views/CursorButtons.cs`) tự set hand cursor khi
  load (không set được trước khi visual tree load). Dùng cho 7 nút/link
  ở About (Repo, ReleaseNotes, CheckUpdates, DownloadUpdate, ShowQR,
  CopyAccount, Restart) + nút share trong `ShareRowBuilder` (About và
  panel left cùng dùng — cả 2 đều bấm được nên tay cầm là đúng).
- **Panel left: share vào cuối nội dung cuộn (giống mac):**
  `ShortcutsTabPanel` từ Grid 3 hàng (How / shortcuts / share cố
  định) thành 1 `ScrollViewer` chung chứa How card + shortcuts +
  share; `ShortcutsCardBody` từ `ScrollViewer` thành `StackPanel`
  (chỉ còn cuộn ngoài). Hàng share cố định cũ lấy ~50pt từ card
  shortcuts, tốn chỗ của timer focus/tomato/rest-eyes sau này.
  Không đổi code-behind: `ShortcutsCardBody.Visibility` (dòng duy
  nhất dùng nó) vẫn chạy trên StackPanel; `BuildShortcutRows` vẫn
  dựng vào `ShortcutsTabRowsPanel`.
- **Window Read wheel (Manh 19:55 — KHÔNG sửa code):** anh mô tả ô
  outside text ở "bản CI trước" là hover + scroll chuột giữa tăng
  giảm font size — đó là hành vi của bản 191. Bản 192 (`1ffcbb7`)
  đã làm đúng như anh yêu cầu: wheel trên ô outside text = cuộn
  chữ (native ScrollViewer + forward từ WordArea), bấm giữ
  left-click kéo lên/xuống = to/nhỏ outside text. Đã kiểm chứng
  code main: không còn handler wheel nào đổi cỡ chữ. Anh test lại
  trên bản 192 (nhớ thoát hẳn bản 191 ở tray trước).

## Settings defaults + Auto-hide dropdown + AI/TTS notes (2026-11-19)

- **Mặc định trong nhãn (Manh 22:26, sau test 200):** Hover delay
  giờ ghi "(default: 500 ms)"; Auto-hide giờ ghi "(default:
  5 seconds)". Giá trị mặc định vẫn như cũ (500ms / 5s).
- **Auto-hide thành ComboBox (Manh 22:26):** `AutoHideBox`
  (NumberBox 1–30s) → `AutoHideCombo` (dropdown giống Click icon),
  các mức 1 / 2 / 3 / 5 / 10 / 15 / 30 giây. Tên giữ nguyên
  "Trigger icon hides after". Load/save theo đúng mẫu của
  HoverDelayCombo (switch + Tag); giá trị cũ ngoài danh sách (vd 7s)
  về mặc định 5s khi mở trang. Chỉ còn tham chiếu trong GeneralPage.
- **AI API dịch — ĐÃ HỖ TRỢ, không phải coming soon:** tab
  Translations đã có sẵn: Primary/Fallback provider, bật/tắt từng
  provider, thêm endpoint OpenAI-compatible (nút Add OpenAI / Add
  DeepSeek + Name / Base URL / Model / API key) — endpoint thêm vào
  xuất hiện ngay trong danh sách provider. Thêm ghi chú đầu tab:
  "AI translation is supported via OpenAI-compatible APIs — add your
  endpoint below with your own API key (OpenAI, DeepSeek, or any
  compatible service)." (BYOK; key đang lưu plain text, nợ Credential
  Locker như đã ghi trong tab.)
- **TTS Speech:** thêm ghi chú dưới Service: "TTS Speech API for
  natural voices is coming soon." ("comming" của anh sửa thành
  "coming".) Hiện chỉ có giọng hệ thống.

## Hint rút ngắn + icon A xanh + chốt style Window Read (2026-11-19)

- **Hint rút ngắn (Manh 21:55):** bỏ "Scroll context" + "Drag ↑/↓
  size" khỏi hint trong vùng đọc — hint còn "Space play/pause • ←/→
  step • ↑/↓ speed • Wheel ↑/↓ focus size". Các thao tác cuộn/kéo vẫn
  ghi đầy đủ trong `FAST_READ.md` (đoạn Wheel + drag), không mất.
- **Icon A xanh (Manh 21:55):** Click=Listen (Speak) thì icon lúc
  select text là chữ A nghệ — đổi `PlainAMark` sang xanh dương
  `#0078D4` (cùng màu xanh với ô chat E8BD), KHÔNG bold
  (`FontWeight="Normal"` ghi rõ trong XAML). Đây chính là A thường
  Style 1 theo thiết kế ("blue" = màu của chính chữ A, Manh 18:06).
  A+ (Fast Read) giữ nguyên đậm + nghệ. Đồng thời ghi luôn phần làm rõ
  "blue" vào `FLOAT_ICON.md` — phần này sửa local từ 19:00 mà push
  bị hết hạn thẻ duyệt, tới nay mới đẩy được.
- **Chốt style Window Read (Manh 21:55):** thêm mục "Settled style"
  vào `FAST_READ.md` — khoá UX/UI/hành vi: scroll (wheel), chuột
  (kéo), phím tắt, vị trí các thành phần. Đây là 1 style thiết kế của
  Windows, sau này có thể thay đổi — đổi giống mac hoặc thêm style
  khác. Khác mac đáng chú ý: subtitle Windows giữ hậu tố "— RSVP"
  (mac bỏ 2026-11-19); version nằm cạnh Reading time (mac: góc phải
  trên, trái nút Settings); pivot ghim giữa vùng đọc (mac: 35% chiều
  rộng). Sửa luôn dòng Status stale trong doc (v1 đã lên main từ
  0.1.4, không còn "Not merged") + câu "(Speak = bubble E8BD...)"
  trong mục A+ linger (bubble chỉ còn là backup).

## CI đỏ #198 — biến `i` trùng trong RsvpEngine (2026-11-19) — sửa ngay

- **CI run #198 (commit `b52f61a8`) ĐỎ, bước Build:** `RsvpEngine.cs(126,26)` CS0136 — `A local or parameter named 'i' cannot be declared in this scope because that name is used in an enclosing local scope`. Lỗi từ code highlight ở `c8eb77bb`: vòng `for (var i = 0; i < pivotWordIndex; i++)` nằm trong `while` mà ngoài đã có `var i = 0` (chỉ số từ trong đoạn) — C# cấm khai báo trùng tên với biến ở scope ngoài. Lỗi IsLoaded đã hết (không còn xuất hiện ở #198).
- **Sửa:** đổi biến vòng lặp thành `j` (`for (var j = 0; j < pivotWordIndex; j++)`). Bài học: đặt tên biến vòng lặp phải kiểm tra biến cùng tên đang sống ở scope ngoài — nhất là khi chèn code vào giữa hàm có sẵn.

## CI đỏ #193–#196 — `IsLoaded` không có trên Window (2026-11-19) — sửa ngay

- **CI đỏ thật (cron 20:30 báo #195, đã kiểm chứng qua API):** #193
  (`270485c`), #194 (`a54e02e6`), #195 (`d0d567a6`), #196
  (`3af01b52`) đều FAILURE ở bước Build, cùng 1 lỗi:
  `TranslatePanelWindow.xaml.cs(1374,14)` CS0103 — `The name
  'IsLoaded' does not exist in the current context` (+ WMC9999 hệ
  quả). Lỗi từ code fade đổi tab ở `270485c`: em viết `if
  (!IsLoaded) return;` trong `FadeInTabContent` — `Window` trong
  WinUI 3 KHÔNG có `IsLoaded` (chỉ `FrameworkElement` mới có;
  `Window` không phải `FrameworkElement`). Run #197 (commit highlight
  `c8eb77bb`) cũng chứa lỗi này nên sẽ đỏ theo.
- **Sửa:** hỏi `Content` (RootGrid, là `FrameworkElement`): `if
  (Content is not FrameworkElement root || !root.IsLoaded) return;`
  — trước lần hiện đầu thì skip fade như ý ban đầu, sau khi load thì
  fade bình thường. Bài học: tra Microsoft Learn trước khi dùng
  property — `IsLoaded` chỉ có trên `FrameworkElement`, không có trên
  `Window` (cùng họ lỗi tra tên API #188–#190: dùng tên theo trí nhớ
  mà không kiểm chứng).

## Highlight focus mất khi chunk 2+ từ — sửa theo SwiftRead/mac (2026-11-19)

- **Bug (Manh 20:29):** chọn Words 2–3 trở lên thì mất highlight nghệ
  ở focus. Nguyên nhân: `RsvpEngine.Build` đặt `PivotCharIndex = -1`
  cho slide nhiều từ (kiểu Sprint Reader — "multi-word slide has no
  single pivot"), nên `ShowSlide` đi nhánh else, cả chunk nằm trong
  `LeftRun`, không còn ký tự pivot nghệ.
- **Cách SwiftRead làm (đối chiếu thẳng mã mac `RSVPEngine.swift`
  `buildSlides` + `RSVPView.orpLine`):** từ giữa chunk (`floor(n/2)`)
  mang pivot — ký tự giữa của từ đó, tính thành offset trong chuỗi
  nối; view tách chuỗi tại offset đó: trước (màu thường) + ký tự
  pivot (cam) + sau (màu thường), ghim pivot tại điểm cố định.
  Highlight không bao giờ mất, chunk 1–6 đều có.
- **Sửa Windows y hệt mac:** engine tính `pivotWordIndex =
  chunk.Count / 2`, `pivot = offset(từ giữa) +
  PivotCharIndex(từ giữa)` (offset = tổng độ dài các từ trước + 1
  khoảng trắng mỗi từ); `ShowSlide` + `AlignPivot` không đổi (đã làm
  việc trên chuỗi nối + pivot bất kỳ). Test cũ
  `Build_ChunkLargerThanOne_HasNoPivot` (assert -1) đổi thành
  `Build_ChunkLargerThanOne_PivotOnMiddleWord` ("one two" → 5, ký tự
  'w' của "two"; "three" → 2; chunk 3 → 5).

## Version vào Window Read + mô tả panel right giống mac (2026-11-19)

- **Version (Manh 20:23, chọn "Cạnh Reading time (bên trái)"):** thêm
  `VersionText` vào hàng trên của vùng đọc, ngay sau "Reading time"
  (StackPanel ngang, cách 8px), cùng cỡ 12pt xám. Định dạng giống
  footer panel: `"{Version} (build {CiBuildNumber})"` (vd "0.1.4
  (build 192)"), chỉ hiện version khi không có số build. Đặt trong
  constructor `FastReadWindow` (giống `FooterBuildText` ở panel).
- **Mô tả panel right (Manh 20:23, "mô tả panel right, để như bên
  mac"):** đối chiếu thẳng mac `PopupView.fastReadContent` — Windows
  đổi từ 2 dòng "Fast Read" + "Coming soon" thành y hệt mac: tiêu đề
  "Fast Read — coming soon" (16pt Semibold) + dòng mô tả "Fast
  reading (RSVP) will live here once the Fast Read window is ready."
  (12pt xám, căn giữa, tự xuống dòng, lề ngang 16). Icon giữ E736
  ReadingMode — Segoe không có glyph `book.pages` của mac, E736 là
  glyph gần nhất (đã kiểm chứng Learn 17/11). Khoảng cách StackPanel
  4 → 8 cho giống VStack spacing 8 của mac.

## Khôi phục wheel focus text = đổi focus size (2026-11-19) — sửa mục trên

- Manh (20:08): "bản 191 anh thích chỗ wheel chuột ở focus text nó
  đổi font size, bản 192 đã mất". **Sửa lại hiểu lầm ở mục trên:**
  phản hồi 16:52 "wheel về cuộn context, chuyển đổi cỡ sang kéo"
  chỉ nói về ô context (wheel context → cuộn, đổi cỡ context → kéo),
  KHÔNG nói bỏ wheel đổi focus size ở vùng focus text — nhưng đợt
  `1ffcbb7` đã đổi luôn wheel vùng focus text thành forward-scroll,
  nên 192 mất hẳn wheel đổi cỡ chữ. Manh thích wheel 191 ở focus
  text, xin khôi phục.
- **Khôi phục y hệt 191** (`OnFocusWheelChanged`, commit `afc0d67`):
  wheel lên +2pt, xuống −2pt, kẹp 40–450, `ApplyFocusSize` + đồng bộ
  picker về option gần nhất, `e.Handled = true`. Giữ nguyên hành vi
  192 ở ô context: wheel = cuộn chữ, kéo left-click lên/xuống = đổi
  context size. Hint đổi thành "… • Wheel ↑/↓ focus size • Scroll
  context • Drag ↑/↓ size". Muốn cuộn context thì hover vào ô
  context (không cuộn được từ vùng focus text nữa — như 191).
- **Bước wheel tăng 2pt → 5pt/tick (Manh 20:15, "lăn hơi nhiều"):**
  2pt/tick đổi từ 110 lên 200 mất ~45 nấc; 5pt/tick còn ~18 nấc,
  mỗi nấc vẫn thấy rõ (~4,5% ở cỡ 110) mà không nhảy vọt qua các
  option picker (cách nhau ~8pt ở cỡ nhỏ). **Around text giữ y 191:**
  đã kiểm chứng code — `ApplyFocusSize` → `ShowSlide` →
  `UpdateContext()` tính lại `ContextTextBlock.FontSize =
  WordTextBlock.FontSize × ratio`, nên wheel đổi focus size thì
  around text to/nhỏ theo cùng tỉ lệ (ratio không đổi); không cần
  sửa gì thêm cho ý này.

## Port Focus-Rest + panel decor từ mac (2026-11-20, nhánh feature/focus-timer-decor)

- **Nguồn:** mac nhánh `feature/focus-timer-decor` @ `54717ca` (chưa merge
  lúc port — gồm các sửa 07:54: 4 picker top bar cả khi size bé, pin
  dịch phải 6px; và 08:11: Maximize kẹp vào work area). Khung trình
  Manh 07:26, Manh duyệt 08:26 (3 mục) + "Duyệt làm" 08:31.
- **Dùng chung view model (Manh duyệt theo đề xuất của em):** tách
  `RsvpReaderViewModel` từ `FastReadWindow.xaml.cs` (refactor giữ
  nguyên hành vi/UI cửa sổ — XAML không đổi); panel right có file XAML
  riêng chỉ chứa vị trí nút (`PanelFastReadView` + `CompactReadingView`),
  cùng xài chung view model. KHÔNG copy 2 bộ, KHÔNG tham chiếu trực
  tiếp Window (Window độc lập, không nhét vào panel được). Đúng y mac
  (RSVPViewModel chung, PanelFastReadView file riêng); khác duy nhất:
  Windows phải tách view model ra trước vì trước giờ chưa có.
- **Pin chuyển chỗ (Manh "ok tạm vậy đi"):** Pin từ Actions row (chỉ
  center) sang hàng dots (mép trái, Margin 10) dùng chung cả 3 panel
  như mac; nút Pin ở Actions row bỏ đi.
- **Thông báo hệ thống — Manh prefer KHÔNG làm lúc này** (sợ mâu thuẫn
  chữ ký/Store sau này; banner + chấm cam đủ, dừng đọc thì nhắc tự
  hiện). **Nợ: làm toast khi có bản cài/Store** (app có định danh
  thật). Xem `FOCUS_REST.md`.
- **Mặc định (Manh chốt 07:25, áp cả 2 repo):** Focus "Repeat
  automatically" BẬT; rest-eyes TẮT.
- **Luật xung đột (Manh 08:04, y hệt mac, commit `bad6610`):** đang đọc
  mà tới giờ nghỉ thì KHÔNG cướp màn hình — chỉ banner (cả 3 tab) +
  chấm cam; dừng đọc (đóng window / kết thúc đọc ở panel / tắt panel)
  thì nhắc tự hiện.
- **Engine ở Core vì test chỉ reference Core:** `FocusTimerEngine`
  thuần (không UI) ở `src/TransPeek.Core/FocusTimer/` + 14 test xUnit;
  store/view model ở app project (cần DispatcherTimer + WinUI) nên
  không test unit được — kiểm chứng bằng Manh test live bản branch.
- **Khác mac có chủ đích:** wheel panel KHÔNG cap cỡ chữ (Manh 07:02,
  vuốt tự do); ↑/↓ panel ±50 WPM (window ±10); panel không có sentence
  step (window cũng không có); Rest window cố định 560×400 không
  resize được (mac: min 480×320, resize được) — cửa sổ nghỉ tạm thời
  không cần resize, và top bar lite reader cần đủ rộng.

## CI #202 đỏ — `DispatcherTimer` thuộc `Microsoft.UI.Xaml` (2026-11-20)

- **Nguyên nhân:** `FocusTimerStore.cs` + `RsvpReaderViewModel.cs`
  chỉ có `using Microsoft.UI.Dispatching;` mà `DispatcherTimer` thuộc
  `Microsoft.UI.Xaml` (đã xác minh trên Microsoft Learn — trang
  "Microsoft.UI.Xaml Namespace" liệt kê DispatcherTimer). 2 lỗi
  CS0246 DispatcherTimer + 28 lỗi Fact (file test thiếu
  `using Xunit;`) + WMC9999 hệ quả. RestWindow không lỗi vì có sẵn
  cả 2 using — đó là manh mối chẩn đoán.
- **Sửa ở `3246918e`:** thêm `using Xunit;` (test) + đổi using sang
  `Microsoft.UI.Xaml` (2 file). CI #203 xanh.

## Đợt 2026-11-20 — nhãn + completion + Style 1/2 + Auto start + panel right (nhánh feature/focus-timer-decor, gộp vào nhánh trước PR — Manh duyệt 16:00)

- **Nhãn "Run FastRead in:" theo mac mới:** "Right Panel" →
  "FastRead Lite"; "New Fast Read Window (Recommended)" → "New
  FastRead Window (recommended)". Chỉ đổi nhãn; setting + giá trị +
  mặc định không đổi. "Run reminder in:" (nhắc nghỉ, panel left) là
  setting RIÊNG, không đổi.
- **Window Read — chúc mừng + share khi đọc xong (mac §14.5):**
  view model đếm thời gian đọc THỰC (banked qua các đoạn playing) +
  `CompletionText` / `ShareText`; đọc xong hiện câu chúc mừng + hàng
  share TRÊN thanh progress. "Reading time" ở top-left vẫn là ước
  lượng, không đổi.
- **Style 1 / Style 2 cho context text (Window Read):** Style 1
  (mặc định) = theme FastRead Lite — book page, wheel đổi focus size
  KHÔNG cap; Style 2 = theme hiện tại của Window Read — đoạn văn
  cuộn, wheel kẹp 40–450, kéo đổi context ratio. Chỉ hiển thị +
  tương tác với focus text / around text theo style; nút khác giữ
  nguyên. Chọn trong Settings popover của Window Read (setting
  `RsvpContextStyle`). Reset KHÔNG reset style (như color scheme).
- **"Auto start focus" (Manh 2026-11-20, 09:47, mac 615f102):**
  toggle dưới "Repeat automatically", mặc định TẮT; bật thì mỗi lần
  mở app timer Focus tự Start (chỉ từ idle, chỉ 1 lần lúc launch).
- **Panel right đồng bộ mac:** tiêu đề "Fast Read Lite" (bỏ "coming
  soon"); "Run FastRead in:" căn LEFT; Space = play/pause (nguyên
  nhân thật: phím đã gán nhưng panel NOACTIVATE nên không nhận phím
  — đọc do user bấm thì panel tự kích hoạt ở `ShowPanelForReading`,
  đọc do nhắc nghỉ thì KHÔNG); đọc xong hiện chúc mừng + share ở
  full-size; theme lite là thiết kế chốt.
- **Banner nhắc nghỉ — tăng fill nền 0.12 → 0.25 (Manh 2026-11-20
  16:02):** nền cũ quá xuyên, chữ banner khó đọc trên text đang đọc
  phía sau; mac giữ 0.12.
- **KHÔNG có phím tắt ←/→ chuyển tab (Manh 2026-11-20 11:30):**
  "nút shortcut" anh nói là phím tắt bàn phím, KHÔNG phải nút
  chevron. Windows chưa từng gán ←/→ chuyển tab (kiểm chứng grep —
  Left/Right chỉ ở bước slide + nhận diện modifier ở Settings) nên
  không có gì phải revert; 2 nút chevron ‹ › ở hàng dots GIỮ NGUYÊN.
- **2 logic "Run FastRead in:" ĐÃ CÓ SẴN (kiểm tra 2026-11-20 12:57):**
  nút Fast Read panel center + click icon A+ đều qua `RouteFastRead`
  duy nhất — không cần add code. Docs kỹ thuật ở panel spec §10.

## Settings gọn lại — bỏ hotkey OCR trùng, Click icon đổi tên + dời lên, picker inline, cửa sổ Settings hẹp hơn (2026-11-20, nhánh feature/focus-timer-decor, gộp vào nhánh trước PR)

- **OCR: bỏ ô "Hotkey to capture a region" (Manh 2026-11-20):** trùng với ô "OCR Screen Translate" ở General — cùng setting `OcrHotkey`. Xoá cả XAML (`OcrHotkeyBox` + `HotkeyErrorText`) lẫn code-behind (`OnHotkeyLostFocus` + dòng load) ở `OcrPage`; setting vẫn sửa được ở General.
- **General — "Click icon" → "Click icon for:" + dời lên 1 vị trí (Manh 2026-11-20):** Manh chốt tên "for" (đề xuất "to" trước đó không dùng). Thứ tự mới: Click icon for → Hover delay → Auto-hide.
- **General — Hover delay + Auto-hide: nhãn trái, picker phải cùng hàng, mô tả ở dưới (Manh 2026-11-20):** bỏ `Header` của ComboBox; nhãn là TextBlock riêng (`Hover delay (default: 500 ms)` / `Auto-hide icon (default: 5 s)`), picker `MinWidth 120` ở cột phải; mô tả xám 12pt ở dưới. Chuỗi nhãn/mô tả chuyển sang `.resw` (`.Text` + `*Hint.Text`) theo quy ước hiện có; 2 key `.Header` cũ bỏ. Lưu ý: giá trị `.resw` cũ khác XAML (resw thắng lúc chạy — Hover delay hiện "How long to hover before the panel opens", Auto-hide hiện "Trigger icon hides after (seconds)"), nên khi đổi nhãn phải sửa cả `.resw`, không chỉ XAML.
- **Cửa sổ Settings hẹp hơn 40 px (Manh 2026-11-20):** `SettingsWindow.ShowSettings` width 960 → 920, cao giữ 640 (comment ở `App.xaml.cs` cũng sửa theo).

## Panel left + nhãn delay đổi sang giây (2026-11-20, nhánh feature/focus-timer-decor, gộp vào nhánh trước PR)

- **Nút "Start eye reminders" căn giữa (Manh 2026-11-20 17:02):** `EyeRestButton` từ `HorizontalAlignment="Left"` → `"Center"` (nút duy nhất của mục Rest eyes, giống hàng nút Focus).
- **"Run reminder in:" — picker xuống dòng riêng (Manh 2026-11-20 17:02):** bỏ Grid chung hàng [nhãn | picker phải]; thành StackPanel dọc: nhãn trên, picker dưới full-width. Lựa chọn giữ nguyên "New Fast Read Window" / "Right Panel" (đây là setting đích nhắc nghỉ, khác "Run FastRead in:").
- **Nhãn delay đổi sang giây (Manh 2026-11-20 17:03):** cả 2 delay dùng cùng đơn vị giây cho dễ đọc — "Hover delay (default: 0.5s)", "Auto-hide icon (default: 5s)"; các lựa chọn Hover delay cũng đổi theo ("0.3s" / "0.5s" / "0.8s" / "1s"). **Chỉ đổi cách hiển thị — giá trị không đổi** (mặc định vẫn 500 ms = 0.5 s; code dùng `Tag` số ms nên không động). Nhãn sửa cả XAML lẫn `.resw`.

## Center panel: viền nhẹ cho nút Capture OCR + Fast Read (2026-11-20, nhánh feature/focus-timer-decor, gộp vào nhánh trước PR)

- **Viền highlight NHẸ cho 2 nút panel center (Manh 2026-11-20 17:05–17:06):** chỉ viền, nền giữ mặc định — màu không chiếm focus. Capture OCR viền **xanh blue `#0078D4` 40%** (`#660078D4`, cùng màu xanh với icon A thường); Fast Read viền **nghệ `#C7800D` 40%** (`#66C7800D`, cùng màu nghệ với A+). `BorderThickness="1"` cả 2.

## Nhãn "Run reminder in:" + icon float translate-first (2026-11-20 18:09, nhánh feature/focus-timer-decor, gộp vào nhánh trước PR)

- **"Run reminder in:" đổi nhãn theo "FastRead Lite" (Manh 2026-11-20 18:09, giống mac):** "New Fast Read Window" → **"New FastRead Window"**, "Right Panel" → **"FastRead Lite"**. Chỉ đổi nhãn hiển thị — `Tag` (`NewWindow` / `RightPanel`) + giá trị + mặc định (NewWindow) không đổi; KHÔNG thêm "(recommended)" (đó là gợi ý riêng của "Run FastRead in:"). Docs `FOCUS_REST.md` đổi theo.
- **Icon loa màu xanh blue (Manh 2026-11-20 18:09):** `SpeakerMark` E767 từ `#1A1A1A` (gần đen) sang **`#0078D4`** — loa là highlight, cùng màu xanh với A thường.
- **Icon float chính về đúng thiết kế — translate-first (Manh 2026-11-20 18:09):** ý tưởng "translate-first" trong `FLOAT_ICON.md` §3 (ghi từ 19/11, lúc đó CHƯA làm) nay Manh duyệt làm. Icon bình thường LUÔN là **A thường xanh** (hover = translate là việc chính), bất kể Click là Speak hay Fast Read. `UpdateGlyph` đổi 1 dòng: `showRead = linger || (!speaking && altHeld && !clickReads)` — A+ chỉ hiện khi giữ Alt lúc Click=Speak (Alt+click đọc) hoặc 2.5s linger sau Read; giữ Alt lúc Click=FastRead thì hiện loa (Alt+click nghe). Đang nói thì loa; còn lại A thường. Hành vi click/Alt+click KHÔNG đổi (XOR `altHeld != clickReads` giữ nguyên) — chỉ đổi glyph hiển thị lúc bình thường.

## Panel lite: màn đếm ngược cafe sau khi đọc nhắc nghỉ (2026-11-20 18:19, nhánh feature/focus-timer-decor, gộp vào nhánh trước PR)

- **Nhắc nghỉ đọc xong ở panel lite thì hiện màn đếm ngược ở giữa (Manh 2026-11-20 18:19):** trước đây chỉ có dòng nhỏ "Rest — mm:ss" + Done ở dưới reader; nay giống cửa sổ Rest — reader ẩn, hiện màn giữa: icon cafe EC32 32pt + tiêu đề + số đếm ngược lớn 56pt Consolas Bold + "Back to Fast Read Lite when the timer ends." + nút Done; hết giờ về lại demo. Tiêu đề đi theo nhắc ("Take a break" / "Rest your eyes") — `FocusPanelRightRequest` thêm `Title`, `FocusTimerStore.PanelReminderTitle`, `StartPanelReading`/`RequestPanelReading` nhận thêm `title`. Chỉ áp cho đọc nhắc nghỉ — đọc thường xong vẫn hiện chúc mừng + share như cũ. Phím Space trong lúc đếm ngược không tới reader nữa (không khởi động lại bài đã đọc xong).

## Release/pre-release: mục "What's new" liệt kê tính năng mới nổi bật (2026-11-21, Manh 08:32)

- **Quy tắc:** từ nay, mô tả của mọi release và pre-release phải có thêm mục tính năng mới nổi bật **ngay dưới phần chữ hiện tại** (mục Download / dòng mô tả cố định) — liệt kê tính năng mới nổi bật cho user biết, vd OCR, block-text detect. Viết bằng tiếng Anh (repo công khai, UI/docs tiếng Anh).
- **Cơ chế Windows:** workflow `build.yml` tạo pre-release tự động với mô tả cố định 1 dòng ("Preview build from CI run N. Not a stable release.") — workflow KHÔNG biết tính năng nào mới nên không tự sinh được mục này. **Muse tự thêm tay sau mỗi lần CI main xanh** (cùng lúc kiểm tra CI + gửi link tải): sửa mô tả release qua trình duyệt, thêm mục `## What's new` ngay dưới dòng cố định, liệt kê tính năng nổi bật của run đó.
- **Đã áp luôn cho pre-release v0.1.4.213** (run #213, commit tray Focus & Rest): mô tả giờ là dòng cố định + `## What's new` với 1 mục (tray menu "Focus & Rest").

## Tray menu: mục "Focus & Rest" mở thẳng panel trái (2026-11-21, main)

- **"Focus & Rest" (Manh 2026-11-21 00:34, làm TRÊN MAIN):** chuột phải icon tray → thêm mục "Focus & Rest", bấm vào mở panel ở tab trái (Shortcuts — thẻ Focus & Rest nằm ở đó), tại vị trí chuột như các mục khác. Tên em chọn "Focus & Rest" (trong 3 tên anh đưa: Focus & Rest / Focus-Rest / Focus Rest) — giống tên card ở panel trái và giống mục menubar mac (mac cũng chọn "Focus & Rest").
- **Vị trí:** ngay sau "Fast Read" (cùng nhóm đọc/tập trung; mac đặt sau "Reading", trước "Barcodes / QR Codes"). Glyph E917 Clock (Segoe Fluent Icons — không có glyph "Timer").
- **Code:** `TrayIconService` thêm tham số `Action focusRest` + mục menu `Tray.Menu.FocusRest` (nhãn trong `.resw`, fallback "Focus & Rest"); `App.xaml.cs` truyền `focusRest: () => _panel?.ShowLeftPanel()`; `TranslatePanelWindow.ShowLeftPanel()` public mới (hiện panel nếu đang ẩn → `ShowTab(PanelTab.Shortcuts)` → `ActivateWindow`, vì user bấm từ tray là chủ động gọi panel, giống `ShowPanelForReading`).

## Liên kết

- [Bối cảnh AI](AI_CONTEXT.md)
- [Kiến trúc](ARCHITECTURE.md)
- [Bản đồ docs](STRUCTURE_MAP.md)
