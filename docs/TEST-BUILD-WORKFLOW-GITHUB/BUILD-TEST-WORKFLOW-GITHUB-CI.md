# CI Notes (TransPeek-Windows)

How the `Build` workflow (`.github/workflows/build.yml`) is set up and how
to read a red run. Written so the next person (or future us) does not have
to rediscover any of this.

> **Maintenance rule:** update this doc on EVERY workflow change, in the
> same round as the change (Manh, 2026-11-17). See
> `GIT-CI-BUILD-SETUP.md` §4 for the intervention log.

## Workflow at a glance

- Trigger: push to `main`, PRs, `workflow_dispatch`.
- Runner: `windows-latest`. One job (`build`), timeout 30 minutes.
- Concurrency group `build-${{ github.event.pull_request.number || github.ref }}` with `cancel-in-progress`: a new
  push cancels the run that is still going. Push related files in ONE commit
  so CI only ever sees a complete version.
- Steps: Checkout → Setup .NET (`8.0.x`) → Restore → Build (Release,
  `-p:Version=0.1.3.<run>`) → Test → Publish self-contained win-x64 →
  Download bundled Tesseract language data (eng+vie) → Verify publish has
  WinUI resources (`.pri` + ≥12 `.xbf`) → Upload artifact
  `TransPeek-Windows-v0.1` (7-day retention) + delete older artifacts,
  keeping only the newest → Zip → Create pre-release `v0.1.3.<run number>`
  (direct download link on the Releases page, keeps only the newest) →
  Delete older pre-releases. All steps from Publish on are gated on
  `[build-app]` in the commit message.

## When CI runs, and when the app is built

To avoid wasting CI runs:

- **Docs-only changes do not trigger CI at all.** The `push` and
  `pull_request` triggers have `paths-ignore` for `docs/**` and `**.md`.
  Editing a doc costs zero CI runs.
- **Tests run on every code commit**, but the app artifact is only built
  when the commit message contains `[build-app]`. The Publish / Tesseract
  / Verify / Upload / Delete artifacts / Zip / Create pre-release /
  Delete pre-releases steps have
  `if: contains(github.event.head_commit.message, '[build-app]')`.
  A plain code commit runs tests only; add `[build-app]` to the message
  when a testable app build is actually needed. Only add it when Manh
  explicitly says "build-app". (Same pattern as the mac repo and SeekZip.)

## The artifact and the pre-release

- The artifact `TransPeek-Windows-v0.1` (Actions tab, 7-day retention,
  only the newest kept) is a CI build artefact — it expires and notifies
  nobody. The pre-release `v0.1.3.<run>` (Releases page, zip
  `TransPeek-Windows-<run>.zip`, ~150 MB) is the direct download link.
  A real stable release (0.1.4) is created manually later; there is no
  `release.yml` in this repo yet.
- The version stamp `-p:Version=0.1.3.<run>` makes the run number the
  build number shown in About as "Release 0.1.3 (Build CI #N)".
- **The CI app is unsigned** — `build.yml` has no signing step.
  SmartScreen warns on first launch; that is expected until 0.1.4
  (SignPath for the GitHub build, Store signing for the Store build).
- **Saved pre-release:** a known-good build is saved as tag
  `0.1.3.<run>-saved` (no "v"), title "(saved preview)" — only after
  Manh tests that build OK. See `GIT-CI-BUILD-SETUP.md` §3.

## Reading a red run

1. Open the run, job `build`, and read the failing STEP — the step name
   tells you where it broke (Restore / Build / Test / Publish /
   Tesseract download / Verify WinUI resources / pre-release).
2. **A run cancelled by a newer push is not a failure.** The concurrency
   group cancels the older run; that is expected. Only a step that
   actually failed is a real red run.
3. **A red "Download bundled Tesseract language data" step** usually
   means the download was < 1 MB (network flake or a bad file from
   `tessdata_best`) — re-run before touching code.
4. **A red "Verify publish has WinUI resources" step** means the publish
   output is missing `TransPeek.pri`/`resources.pri` or has fewer than
   12 `.xbf` files — the build itself is broken, not the check.
5. **A red "Build" step with a compiler error** — read the error text
   first; the common self-inflicted ones are in "Known gotchas" below.
6. If the web viewer hangs on a huge log, download the log archive
   ("Download log archive") instead of fighting the viewer. Do NOT read
   CI logs via the GitHub API — it is blocked (403); use the browser.

## Known gotchas (learned the hard way)

- **A local function parameter must NOT use `_` twice** — it causes
  CS0100 (CI #113/#114 went red). A lambda `(_, _)` for an event
  handler is fine; a named local function with two `_` parameters is
  not.
- **Removing a `using`? Grep every remaining use first.** A leftover
  reference to the removed namespace goes red (CI #138/#139).
- **Check API/enum/glyph/URI names against Microsoft Learn FIRST**
  (WinUI, Windows App SDK, .NET, Segoe Fluent Icons, `ms-settings`) —
  never from memory (WPF/UWP names differ) and never from a third-party
  page. The horizontal resize cursor cost three red runs (#82–#84):
  `SizeWe` (memory, wrong — that is WPF) → `SizeEastWest`
  (third-party page, wrong) → `SizeWestEast` (Microsoft Learn, correct).
- **A local function name must not collide with a type/property in the
  same scope** — the compiler error points at the call site, not the
  declaration; check the declaration first.
- **The GitHub API is rate-limited (60/hr, unauthenticated, shared VM
  egress IP).** A tiny (~280 byte) JSON response means rate limited,
  not "no data". The list-runs key is `workflow_runs`, not `runs`.
  Poll CI status sparsely (5–10 min, only while a run is running).
- **Tests only cover Core logic on `net8.0`.** A green Test step says
  nothing about the WinUI panel, the tray icon, or OCR on a real
  machine — those need a `[build-app]` run + Manh's live test.
- **The browser cannot upload a 150 MB zip, and the connector cannot
  create releases.** A saved pre-release needs Manh to take over the
  browser to pick the file and press Publish.
- **Version "0.1.3" in `build.yml` is the marketing version.** Bump it
  in the workflow when the app version bumps, or the stamp lies.

## Liên kết

- [Setup + intervention log](GIT-CI-BUILD-SETUP.md)
- [Build từ máy trắng](../BUILD_FROM_ZERO.md)
- [Checklist build + test live](../WINDOWS-v0.1-TESTING.md)
- [Quy tắc làm việc](../AGENTS.md)
- [Bản đồ docs](../STRUCTURE_MAP.md)
