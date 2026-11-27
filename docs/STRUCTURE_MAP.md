# Structure Map (bản nháp)

> Trạng thái: **bản nháp** — bản đồ repo + docs. Cập nhật dần khi thêm/sửa doc.

## 1. Repo

- **Repo:** `TranManhTuanLabs/TransPeek-Windows`, branch `main`, public.
- **Code:** `src/TransPeek/` (app WinUI 3), `src/TransPeek.Core/` (logic thuần),
  `tests/TransPeek.Tests/`.
- **Docs:** `docs/` (xem §2).

## 2. Bản đồ docs

| File | Nội dung | Trạng thái |
|---|---|---|
| `docs/TEST-BUILD-WORKFLOW-GITHUB/GIT-CI-BUILD-SETUP.md` | Git, CI test + build workflow, intervention log | Đang dùng |
| `docs/TEST-BUILD-WORKFLOW-GITHUB/BUILD-TEST-WORKFLOW-GITHUB-CI.md` | CI workflow chi tiết (đọc run đỏ, gotcha) | Đang dùng |
| `docs/DECISIONS.md` | Mọi quyết định đã chốt + lý do | Đang dùng |
| `docs/WINDOWS-v0.1-TESTING.md` | Checklist build + test live | Đang dùng |
| `docs/ARCHITECTURE.md` | Kiến trúc tổng | Bản nháp |
| `docs/MASTER_PLAN.md` | Lộ trình 0.1.4 | Bản nháp |
| `docs/STRUCTURE_MAP.md` | Doc này | Bản nháp |
| `docs/TASK_QUEUE.md` | Việc đang làm / chờ | Bản nháp |
| `docs/UI_DESIGN.md` | Chỉ mục UX | Bản nháp |
| `docs/FEATURES/FLOAT_ICON.md` | Tính năng icon float | Bản nháp |
| `docs/FEATURES/SPEECH_ENGINE.md` | Engine TTS | Bản nháp |
| `docs/FEATURES/SPEECH_PLAYER.md` | Player TTS + RSVP | Bản nháp |
| `docs/FEATURES/FAST_READ.md` | Fast Read (RSVP) — cửa sổ đọc riêng | Bản nháp |
| `docs/FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md` | TTS word-sync highlight (từ/câu theo giọng) | Bản nháp |
| `docs/FEATURES/ALTERNATIVE_ICON.md` | Icon alternative trong Fast Read (bôi đen → tra từ / nghe) | Bản nháp |
| `docs/FEATURES/OCR_TEXT_CAPTURE.md` | OCR | Bản nháp |
| `docs/FEATURES/SETTINGS.md` | Tính năng Settings | **CHƯA VIẾT** |
| `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` | **Panel UX — size đã CHỐT** | **CHỐT 17/11/2026 (#161)** |
| `docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md` | Icon float UX | Bản nháp |
| `docs/UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md` | Highlight: màu + hiện/ẩn 6 lớp (vạch/cụm/câu/từ) | Bản nháp |
| `docs/UI/UX_DESIGN/APP_UX_OVERVIEW.md` | Tổng quan UX | Bản nháp |
| `docs/UI/UX_DESIGN/SETTING_UX.md` | Settings UX | **CHƯA VIẾT** |
| `docs/UI/UX_DESIGN/MAIN_NAVIGATION_UX.md` | Menu tray + Settings | Bản nháp |
| `docs/AGENTS.md` | Quy tắc làm việc cho AI trong repo | Đang dùng |
| `docs/AI_CONTEXT.md` | Bối cảnh cho AI | Đang dùng |
| `docs/PROJECT_STATE.md` | Trạng thái dự án hiện tại | Đang dùng |
| `docs/CHANGELOG.md` | Log thay đổi theo ngày | Đang dùng |
| `docs/BUILD_FROM_ZERO.md` | Build app từ máy trắng | Đang dùng |
| `docs/RESEARCH/README.md` | Chỉ mục research | Đang dùng |
| `docs/RESEARCH/transpeek-windows-packaging-signing/report.md` | Research đóng gói/ký/Store | Đang dùng |

## 3. Cây docs

```
docs/
├── TEST-BUILD-WORKFLOW-GITHUB/   # doc workflow (setup + CI chi tiết)
│   ├── GIT-CI-BUILD-SETUP.md         # git, CI test + build workflow, intervention log
│   └── BUILD-TEST-WORKFLOW-GITHUB-CI.md  # CI workflow chi tiết
├── DECISIONS.md                  # mọi quyết định đã chốt + lý do
├── WINDOWS-v0.1-TESTING.md       # checklist build + test live
├── AGENTS.md                     # quy tắc làm việc cho AI
├── AI_CONTEXT.md                 # bối cảnh cho AI
├── PROJECT_STATE.md              # trạng thái dự án hiện tại
├── CHANGELOG.md                   # log thay đổi theo ngày
├── BUILD_FROM_ZERO.md            # build app từ máy trắng
├── RESEARCH/
│   ├── README.md                     # chỉ mục research
│   └── transpeek-windows-packaging-signing/  # đóng gói/ký/Store (Windows)
├── ARCHITECTURE.md               # kiến trúc tổng
├── MASTER_PLAN.md                # lộ trình 0.1.4
├── STRUCTURE_MAP.md              # doc này (bản đồ + cây docs)
├── TASK_QUEUE.md                 # việc đang làm / chờ
├── UI_DESIGN.md                  # chỉ mục UX
├── FEATURES/
│   ├── FLOAT_ICON.md             # tính năng icon float
│   ├── SPEECH_ENGINE.md          # engine TTS
│   ├── SPEECH_PLAYER.md          # player TTS + RSVP
│   ├── FAST_READ.md              # Fast Read (RSVP) — cửa sổ đọc riêng
│   ├── TTS_WORD_SYNC_HIGHLIGHT.md  # TTS word-sync highlight (từ/câu theo giọng)
│   ├── ALTERNATIVE_ICON.md       # icon alternative trong Fast Read
│   ├── OCR_TEXT_CAPTURE.md       # OCR
│   └── SETTINGS.md               # tính năng Settings (CHƯA VIẾT)
└── UI/
    └── UX_DESIGN/
        ├── FLOAT_POPUP_PANEL_UX.md   # panel UX — size đã CHỐT
        ├── FLOAT_ICON_UX_SPEC.md     # icon float UX
        ├── HIGHLIGHT_COLORS_UX_SPEC.md  # highlight: màu + hiện/ẩn 6 lớp
        ├── APP_UX_OVERVIEW.md        # tổng quan UX
        ├── MAIN_NAVIGATION_UX.md     # menu tray + Settings
        └── SETTING_UX.md             # Settings UX (CHƯA VIẾT)
```

## 4. Quy ước

- Doc trong `docs/` viết tiếng Việt (trừ `DECISIONS.md` viết tiếng Anh từ đầu — giữ nguyên).
- Panel UX là spec chốt: đổi số trong đó cần Manh duyệt, và cập nhật doc trong cùng đợt
  với code.

## Liên kết

- [Quy tắc làm việc](AGENTS.md)
- [Bối cảnh AI](AI_CONTEXT.md)
- [Trạng thái dự án](PROJECT_STATE.md)
- [Log thay đổi](CHANGELOG.md)
- [Panel UX (CHỐT)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
- [Highlight: màu + hiện/ẩn 6 lớp](UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md)
- [Chỉ mục UX](UI_DESIGN.md)
