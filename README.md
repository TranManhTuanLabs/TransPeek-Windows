# TransPeek for Windows

**Translate anywhere, peek instantly.** — Windows edition.

TransPeek is a floating translator: select text anywhere, hover the
trigger icon, and the translation appears in a small panel — without
leaving the app you are reading in. This is the Windows (WinUI 3 + C#)
edition, rewritten from scratch. The macOS edition (Swift) lives at
[TranManhTuanLabs/TransPeek](https://github.com/TranManhTuanLabs/TransPeek).

> The idea comes from [MoePeek](https://github.com/cosZone/MoePeek) by
> cosZone (Swift, macOS, AGPL-3.0). The Windows edition mirrors its
> design and behaviour but shares no code with it — it is a fresh C#
> implementation. See [LICENSE](LICENSE).

## v0.1 — what it does

- **Select → translate**: UI Automation `TextPattern.GetSelection`
  watches the text you select; a small `A 文` trigger icon appears
  nearby. Hover (~300 ms) or click it to open the float panel.
  Manual fallback: `Ctrl+Alt+T` copies the selection via the clipboard
  (Ctrl+C) and translates it — your clipboard is restored afterwards.
- **Flexible translation**: `ITranslationProvider` + Google Translate
  (free, no key) as the default, plus any custom OpenAI-compatible
  endpoint (OpenAI, DeepSeek, local Ollama/LM Studio…) with your own
  key. Settings pick a primary + fallback provider and enable/disable
  each one. Order: primary → fallback → other enabled → Google (free)
  as the safety net. The first provider's error is reported when all
  fail — never a silent failure.
- **Dictionary at the panel bottom**: Oxford, Cambridge, Google
  Dictionary, YouGlish by default (`{word}` / `{text}` templates).
  Add/edit/delete links in Settings; each opens in the default browser.
  Icon + Text or Icon-only; favicons load async (host favicon →
  Google s2 → DuckDuckGo) with a local cache.
- **Screenshot OCR**: `Ctrl+Shift+S` (changeable) → drag a region →
  OCR → translate → panel, with separate **[Copy gốc]** / **[Copy
  dịch]** buttons and auto-copy of the translation (default on), or
  **copy-only mode** (OCR → copy source text, toast "Đã copy").
  Engines: **Tesseract (default for Vietnamese)** — language data
  (`vie`/`eng`) downloads on demand from tessdata_best into
  `%LOCALAPPDATA%\TransPeek\tessdata` — and built-in **Windows OCR**
  as the fallback. Capture uses Windows.Graphics.Capture with a GDI
  BitBlt fallback.
- **Settings + tray icon**: Settings window (General / Translation /
  Dictionary / OCR / About), tray icon with menu (Settings, Translate
  selection, Capture & OCR, Quit), launch at login (registry Run key).
- **About + updates**: app name + running version/build, author line
  with GitHub icon + repo link, release-notes link for the running
  version, and a **Check for Updates** button (see below).

## What v0.1 does NOT have

- **No TTS.** No click-to-read, no EdgeTTS, no speaker button, no
  external TTS engines. TTS comes after the mac side is stable.
- **No Apple Translation.** It does not exist on Windows.
- **No auto-update.** **Check for Updates** (Settings → About) only
  *checks*: it calls the GitHub Releases API, compares the latest tag
  with the running version, and — when a newer release exists — shows
  a link to the release page. You download and install by hand. No
  updater library, no auto-download/install. Automatic download +
  install + relaunch (Velopack) comes at release time, when there is
  a real release pipeline.
- **No MSIX packaging yet.** v0.1 is unpackaged — run the exe
  directly. Packaged/MSIX comes at release time.

## Requirements

- Windows 10 1809+ / Windows 11, x64.
- No account, no key needed for the default (Google Translate free).

## Build & run (developer)

- Visual Studio 2022 (17.8+) with the **.NET desktop development**
  workload, .NET 8 SDK, Windows SDK 10.0.19041+.
- Open `TransPeek.sln`, build **Release | x64**, run
  `src\TransPeek\bin\x64\Release\net8.0-windows10.0.19041.0\TransPeek.exe`.
- Tests (pure logic, no Windows needed):
  `dotnet test tests\TransPeek.Tests\TransPeek.Tests.csproj`
- CI: GitHub Actions on `windows-latest` builds the solution and runs
  the tests. The workflow file (`.github/workflows/build.yml`) is NOT
  in this repo yet — the GitHub App cannot push workflow files (403).
  Create it on the GitHub web UI (copy from the
  [testing doc](docs/WINDOWS-v0.1-TESTING.md)).

## Layout

```
TransPeek.sln
src/
  TransPeek.Core/      net8.0, platform-neutral, no WinUI/WinRT:
                       providers, coordinator, dictionary links,
                       favicons, OCR interfaces + Tesseract,
                       settings model, update check
  TransPeek/           WinUI 3 app (net8.0-windows10.0.19041.0):
                       windows, tray, hotkeys, UIA monitor,
                       capture, Windows OCR, OCR flow
tests/
  TransPeek.Tests/     xUnit, Core logic only
docs/
  DECISIONS.md         every locked decision + its reason
  WINDOWS-v0.1-TESTING.md  build + live-test checklist
```

## Settings & data

- One JSON file: `%LOCALAPPDATA%\TransPeek\settings.json`
  (same setting names as the mac app where they overlap).
- Tesseract data: `%LOCALAPPDATA%\TransPeek\tessdata`
- Favicon cache: `%LOCALAPPDATA%\TransPeek\Favicons`

### Known debts (v0.1, documented — not hidden)

- **API keys are stored in plain text** in `settings.json`
  (per-user folder, but plain text). The mac app uses the Keychain;
  the Windows equivalent is Credential Locker (PasswordVault).
  Moving keys there is a post-v0.1 task.
- **No app icon yet** (only the embedded base64 tray icon). The real
  `.ico` + manifest logos come with packaging.
- **Region capture covers the display containing the cursor.**
  Multi-monitor capture of a region spanning displays is a follow-up.
- **Windows OCR Vietnamese** needs the "Vietnamese OCR" optional
  feature installed; Tesseract is the default precisely so Vietnamese
  works without it.

## License

AGPL-3.0 — see [LICENSE](LICENSE).
