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

- **Mirror of mac Phase 1**: same 4 defaults (Oxford / Cambridge /
  Google Dictionary / YouGlish), same `{word}` / `{text}` templates,
  same validation rules, Icon+Text / Icon-only.
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
