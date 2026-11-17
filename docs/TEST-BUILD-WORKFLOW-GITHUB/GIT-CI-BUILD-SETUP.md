# Git, CI Test & Build Workflow Setup (TransPeek-Windows)

The complete setup and configuration of git, the CI test workflow, and the
app build workflow for this repo — plus the history of what was changed and
why, so a future maintainer (or another AI entering this repo) can
investigate a red run, fix a failure, or change the setup without
rediscovering everything.

Related docs:

- `BUILD-TEST-WORKFLOW-GITHUB-CI.md` — the CI workflow in detail (how
  to read a red run, caching, .NET/API gotchas).
- `../BUILD_FROM_ZERO.md` — build the app from a blank machine.
- `../WINDOWS-v0.1-TESTING.md` — build + live-test checklist.
- `../ARCHITECTURE.md` — architecture overview.

## Maintenance rule — update this doc on EVERY workflow change

**Whenever `.github/workflows/build.yml` (or any workflow/config) changes,
update this doc and `BUILD-TEST-WORKFLOW-GITHUB-CI.md` in the same
round** — same commit if possible, never deferred to a later batch
(Manh, 2026-11-17). A workflow change with no doc update leaves the next
investigator reading stale setup. The same rule applies to the mac repo
(its `GIT-CI-BUILD-SETUP.md`) and SeekZip.

## 1. Git setup

### Repo

- GitHub: `TranManhTuanLabs/TransPeek-Windows` (public).
- Default branch: `main`. Bigger features (e.g. Fast Read) are built on
  `feature/<name>` branches (see §3, branch standard) and merged via PR;
  small fixes land on `main` in batches; `main` stays clean and green.
- Local copies:
  - Manh's Windows PC (primary, live testing).
  - This VM: `~/workspace/projects/transpeek-windows/` (snapshot, no
    `.git` — sync record:
    `~/workspace/projects/transpeek-windows.muse-github-snapshot-sync.json`).

### Authentication

- Manh's PC: SSH with an ed25519 key. Verify with
  `ssh -T git@github.com` — it must print `Hi TranManhTuanLabs`. SSH is
  preferred over HTTPS.
- The Muse GitHub App (connector) does NOT have Workflows permission: any
  call touching `.github/workflows/*` returns 403. Stop after the first
  403; do not retry the same call. Edit workflow files from Manh's PC,
  or through the GitHub web editor (browser session) when Manh has
  explicitly allowed it. (Manh granted a fresh 7-day exception on
  2026-11-17, until ~2026-11-24, for TransPeek-Windows and the mac repo:
  Muse may push, edit workflows via the browser, and read CI logs via
  the browser without asking each time. Ask Manh again after it expires;
  it does not cover other repos. The previous exception expired
  ~2026-11-15.)
- The connector snapshot push (`push-files` with a paths file) only
  handles UTF-8 text files (max 2,000 files / 16 MiB total), no binary,
  and CANNOT delete files. Deleting a file needs the GitHub web UI
  (browser session) or local git.

### How pushes are done (and the rules)

- **Push related files in ONE commit.** Prepare everything locally first
  (all files written, diff re-read, names/parameters checked consistent),
  then push the whole change as one commit. Never push related files
  one-by-one through the browser web editor — each web edit is 1 commit,
  so the repo passes through intermediate states where the code does not
  fit together and CI goes red mid-batch.
- Approval cards for connector pushes expire after 30 minutes. An
  expired/denied card only blocks that push — the code is not lost;
  re-push and a new card appears.
- **Gate (milestone flow):** work locally → push one batch → CI green →
  Manh tests live on his Windows PC → ok / reports bugs → fix → only then
  move to the next milestone.
- **Commit message marker `[build-app]`:** the app artifact is only built
  when the commit message contains `[build-app]` (see §3). Only put
  `[build-app]` in a commit when Manh explicitly says "build-app". Do not
  decide it yourself, even if the change is visible in the app.
- **Do NOT push a new `[build-app]` commit while the previous run is
  still running.** Two runs racing each other make the Create/Delete
  pre-release steps delete the newer build (see §4, 2026-11-16).

## 2. CI test workflow (`build.yml`)

### At a glance

- File: `.github/workflows/build.yml`. Runner: `windows-latest`. One job
  (`build`), timeout 30 minutes.
- Triggers: push to `main` and `feature/**`, PRs to `main`,
  `workflow_dispatch`.
- `paths-ignore`: `docs/**`, `**.md` (both push and PR) — a docs-only
  push runs NO CI at all.
- Concurrency group
  `build-${{ github.event.pull_request.number || github.ref }}` with
  `cancel-in-progress: true` — a new push cancels the run still going.
  A run cancelled this way is expected behaviour, not a failure — do not
  report it (see §4).
- Permissions: `contents: write`, `actions: write` (`contents: write` is
  needed to create/delete pre-releases; `actions: write` to delete old
  artifacts).
- Steps: Checkout → Setup .NET (`8.0.x`) → Restore → Build → Test →
  Publish (only `[build-app]`) → Download bundled Tesseract language
  data (only `[build-app]`) → Verify publish has WinUI resources (only
  `[build-app]`) → Upload artifact (only `[build-app]`; name
  `TransPeek-Windows-v0.1` on main, `TransPeek-Windows-<run>-branch` on
  feature branches) → Delete older artifacts (only `[build-app]` AND only
  on main) → Zip published app (only `[build-app]`) → Create pre-release
  (only `[build-app]` AND only on main) → Delete older pre-releases (only
  `[build-app]` AND only on main).

### How tests run

- `dotnet test tests/TransPeek.Tests/TransPeek.Tests.csproj -c Release
  --no-build` — tests only the pure Core logic on `net8.0` (no Windows
  UI). UI/Windows-only behaviour is verified by CI build + live test on
  Manh's PC.
- Typical run time: ~3–3.5 minutes for a `[build-app]` run.

### Diagnostics: what reaches the CI log and what does NOT

- **Reading CI status via the GitHub API is allowed** — poll sparsely
  (5–10 minutes apart, only while a run is running, then stop). The
  list-runs response key is `workflow_runs`, NOT `runs`.
- **Reading CI logs via the API is blocked (403)** — do NOT poll it; use
  the browser to read a run's log when diagnosis is needed. Re-check
  once a week whether the API block is gone.
- **Rate limit (unauthenticated API, 60 requests/hour on the VM's shared
  egress IP) only affects this VM, NOT Manh's GitHub account.** A
  rate-limited response is a tiny (~280 byte) JSON message, not the real
  data — if a `curl` result looks empty or ~280 bytes, it is rate
  limited, not missing data.
- **Verify a pre-release asset without the API:** `HEAD` the expected
  download URL
  (`https://github.com/TranManhTuanLabs/TransPeek-Windows/releases/download/<tag>/TransPeek-Windows-<run>.zip`)
  — HTTP 200 + `Content-Length` ~150 MB confirms the zip is attached.
  The release page text does not list assets.

## 3. Build workflow (app artifact + pre-release)

### Build steps (all gated on `[build-app]`)

- **Build:** `dotnet build TransPeek.sln -c Release --no-restore
  -p:Version=0.1.3.${{ github.run_number }}`. The run number becomes the
  version shown in About as "Release 0.1.3 (Build CI #N)". The "0.1.3"
  in the version is the current marketing version — update it in
  `build.yml` when the app version bumps (stable 0.1.4 comes later).
- **Publish:** `dotnet publish src/TransPeek/TransPeek.csproj -c Release
  -r win-x64 --self-contained -o publish -p:Version=0.1.3.${{
  github.run_number }}`. Self-contained, so the zip runs on a Windows
  10/11 machine with nothing installed.
- **Download bundled Tesseract language data (eng+vie):** downloads
  `eng.traineddata` (~15 MB) and `vie.traineddata` (~12 MB) from
  `tesseract-ocr/tessdata_best` into `publish/tessdata` (PowerShell).
  **Fails the build if either file is < 1 MB.** eng+vie ship with the
  app (Manh, 2026-11-15) so OCR works out of the box — that is why the
  zip is ~150 MB.
- **Verify publish has WinUI resources:** fails the build if
  `TransPeek.pri`/`resources.pri` is missing or there are fewer than 12
  `.xbf` files (one per XAML file).
- **Upload artifact:** name `TransPeek-Windows-v0.1` on main,
  `TransPeek-Windows-<run>-branch` on feature branches (the name decides
  the downloaded file: `TransPeek-Windows-<run>-branch.zip`), 7-day
  retention.
- **Delete older artifacts:** keeps only the newest
  `TransPeek-Windows-v0.1` artifact (`continue-on-error: true`) — main
  only.
- **Zip published app:** `Compress-Archive` to
  `TransPeek-Windows-<run>.zip`.
- **Create pre-release:** tag `v0.1.3.<run number>`, prerelease, title
  `TransPeek Windows 0.1.3 (CI#<run>) (preview)`, notes "Preview build
  from CI run <run>. Not a stable release." If the tag already exists
  (a re-run), uploads the zip to it with `--clobber` instead of
  creating a new release — main only.
- **Delete older pre-releases:** deletes every pre-release whose tag
  matches `^v0\.1\.3\.[0-9]+$` except the newest (`continue-on-error:
  true`). The regex only matches the 4-part preview tags, never the
  stable `v0.1.4` later — main only.

### Feature branches (branch standard, Manh 2026-11-17)

- A `feature/**` push runs the same Build + Test steps. With
  `[build-app]` it also publishes and uploads an artifact — but never
  creates a pre-release and never deletes older artifacts/pre-releases
  (those three steps are main-only), so a branch build cannot delete
  main's newest pre-release.
- The branch artifact is named `TransPeek-Windows-<run>-branch`
  (downloaded as `TransPeek-Windows-<run>-branch.zip`); main's artifact
  stays `TransPeek-Windows-v0.1`. The run number in the branch name
  keeps branch builds distinct.
- The workflow runs from the file in the branch being pushed — a branch
  only builds with the new setup if it contains the new `build.yml`.
  Create the branch from latest `main` after the workflow change (or
  edit the workflow on the branch).
- This standard applies to the current branch
  (`feature/fast-read-rsvp`) AND every future feature branch. Verify it
  with one `[build-app]` push on the branch: CI runs and the `-branch`
  artifact is downloadable from the run page; no pre-release appears
  and nothing of main's is deleted.

### No signing in CI

- `build.yml` has NO signing step — the CI app is unsigned. SmartScreen
  warns on first launch ("unidentified developer"); Manh's flow is
  "More info" → "Run anyway". Signing for 0.1.4 is planned separately:
  SignPath Foundation ($0, certificate in SignPath's name) for the
  GitHub build, Microsoft Store signing for the Store build — see
  `../RESEARCH/transpeek-windows-packaging-signing/report.md`.
  (Do not confuse this with the mac repo, whose CI signs with a
  self-signed certificate.)

### Artifact ≠ Release

- The `TransPeek-Windows-v0.1` artifact is a CI build artefact (7-day
  retention, only the newest kept, no user notification). The
  pre-release `v0.1.3.<run>` is the direct download link (Releases
  page). A real stable release (0.1.4) is created manually later —
  there is no `release.yml` in this repo yet.
- **Saved pre-release (baseline):** a known-good build is saved as a
  separate pre-release whose tag has NO "v" — `0.1.3.<run>-saved`
  (e.g. `0.1.3.132-saved`), title "TransPeek Windows 0.1.3 (CI#<run>)
  (saved preview)". The missing "v" keeps it out of the auto-delete
  regex. A saved pre-release is only made AFTER Manh tests that build
  OK. Saving needs Manh to take over the browser (the browser cannot
  upload a 150 MB zip; the connector cannot create releases; `gh` is
  not logged in).

## 4. Intervention log (what was changed, when, why)

Chronological, so a future investigator can see the cause and the fix for
each recurring problem. Code bugs are NOT logged here — only workflow/CI
setup changes.

- **2026-11-08 — `[build-app]` gating + docs `paths-ignore`.**
  Problem: every push built the app (wasting runners) and docs-only
  pushes ran CI. Fix: publish/zip/upload/delete/pre-release steps gated
  on `[build-app]` in the commit message; `paths-ignore` for `docs/**`
  + `**.md` on push and PR. Same pattern as the mac repo (`test.yml`)
  and SeekZip (`ci.yml`). Rule going forward: only add `[build-app]`
  when Manh explicitly says so.
- **2026-11-11 — Debug rule (Manh).** When a new log arrives,
  investigate the cause from the log FIRST; if investigation finds
  nothing, try the next fix, one per run, no jumping ahead. While the
  app is unstable, build on every fix (commit with `[build-app]`).
- **2026-11-15 — Tesseract eng+vie bundled in the publish output.**
  OCR now works out of the box (no in-app download for eng/vie); the
  step fails the build if a downloaded file is < 1 MB, so a broken
  download cannot ship silently. This is why the zip is ~150 MB.
- **2026-11-16 — CI tracking rules (Manh).** CI status via the GitHub
  API is allowed (sparse polling, 5–10 min, only while a run is
  running); CI log reads via the API are blocked (403) — use the
  browser. A run cancelled by a newer push (concurrency) is NOT
  reported; only report CI green (with download link) or a real red
  run. Report fast when a run concludes — do not make Manh ask "why is
  the build taking so long".
- **2026-11-16 — Do not stack `[build-app]` runs.** Two runs overlapping
  race in the Create/Delete pre-release steps and the newer build can be
  deleted (one green run had no download because of this). Never push a
  new `[build-app]` commit while the previous run is still running.
- **2026-11-16 — Saved pre-release convention (Manh).** Tag has NO "v"
  (`0.1.3.<run>-saved`); title uses "(saved preview)", never "— saved
  copy".
- **2026-11-17 — Workflow docs created + sync rule (Manh).** This doc
  and `BUILD-TEST-WORKFLOW-GITHUB-CI.md` created (following the mac
  template). **Rule: update these docs on EVERY workflow change, in the
  same round — never deferred.**
- **2026-11-17 — Feature branches can build + `-branch` artifact names
  (Manh 09:40/11:25/11:29).** `build.yml` (commit `f1d595b`, via browser
  — the connector cannot edit workflows): push trigger `[main,
  'feature/**']` (PR trigger unchanged); Delete older artifacts /
  Create pre-release / Delete older pre-releases now also require
  `github.ref == 'refs/heads/main'`; Upload artifact name is
  `TransPeek-Windows-v0.1` on main, `TransPeek-Windows-<run>-branch` on
  feature branches. Branch standard: applies to the current branch
  (`feature/fast-read-rsvp`) and all future branches; a branch must
  contain the new `build.yml` (create from latest `main`); verify with
  one `[build-app]` push on the branch. The edit itself triggered a
  test-only run (#168, no `[build-app]`).

## Liên kết

- [CI workflow chi tiết](BUILD-TEST-WORKFLOW-GITHUB-CI.md)
- [Build từ máy trắng](../BUILD_FROM_ZERO.md)
- [Checklist build + test live](../WINDOWS-v0.1-TESTING.md)
- [Quy tắc làm việc](../AGENTS.md)
- [Bản đồ docs](../STRUCTURE_MAP.md)
