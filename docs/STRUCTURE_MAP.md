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
| `docs/DECISIONS.md` | Mọi quyết định đã chốt + lý do | Đang dùng |
| `docs/WINDOWS-v0.1-TESTING.md` | Checklist build + test live | Đang dùng |
| `docs/ARCHITECTURE.MD` | Kiến trúc tổng | Bản nháp |
| `docs/MASTER_PLAN.md` | Lộ trình 0.1.4 | Bản nháp |
| `docs/STRUCTURE_MAP.md` | Doc này | Bản nháp |
| `docs/TASK_QUEUE.MD` | Việc đang làm / chờ | Bản nháp |
| `docs/UI_DESIGN.MD` | Chỉ mục UX | Bản nháp |
| `docs/FEATURES/FLOAT_ICON.MD` | Tính năng icon float | Bản nháp |
| `docs/FEATURES/SPEECH_ENGINE.MD` | Engine TTS | Bản nháp |
| `docs/FEATURES/SPEECH_PLAYER.MD` | Player TTS + RSVP | Bản nháp |
| `docs/FEATURES/OCR_TEXT_CAPTURE.MD` | OCR | Bản nháp |
| `docs/FEATURES/SETTINGS.MD` | Tính năng Settings | **CHƯA VIẾT** |
| `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD` | **Panel UX — size đã CHỐT** | **CHỐT 17/11/2026** |
| `docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.MD` | Icon float UX | Bản nháp |
| `docs/UI/UX_DESIGN/APP_UX_OVERVIEW.MD` | Tổng quan UX | Bản nháp |
| `docs/UI/UX_DESIGN/SETTING_UX.MD` | Settings UX | **CHƯA VIẾT** |
| `docs/UI/UX_DESIGN/MAIN_NAVIGATION_UX.MD` | Menu tray + Settings | Bản nháp |
| `docs/AGENTS.MD` | Quy tắc làm việc cho AI trong repo | Đang dùng |
| `docs/AI_CONTEXT.MD` | Bối cảnh cho AI | Đang dùng |
| `docs/PROJECT_STATE.MD` | Trạng thái dự án hiện tại | Đang dùng |

## 3. Cây docs

```
docs/
├── DECISIONS.md                  # mọi quyết định đã chốt + lý do
├── WINDOWS-v0.1-TESTING.md       # checklist build + test live
├── AGENTS.MD                     # quy tắc làm việc cho AI
├── AI_CONTEXT.MD                 # bối cảnh cho AI
├── PROJECT_STATE.MD              # trạng thái dự án hiện tại
├── ARCHITECTURE.MD               # kiến trúc tổng
├── MASTER_PLAN.md                # lộ trình 0.1.4
├── STRUCTURE_MAP.md              # doc này (bản đồ + cây docs)
├── TASK_QUEUE.MD                 # việc đang làm / chờ
├── UI_DESIGN.MD                  # chỉ mục UX
├── FEATURES/
│   ├── FLOAT_ICON.MD             # tính năng icon float
│   ├── SPEECH_ENGINE.MD          # engine TTS
│   ├── SPEECH_PLAYER.MD          # player TTS + RSVP
│   ├── OCR_TEXT_CAPTURE.MD       # OCR
│   └── SETTINGS.MD               # tính năng Settings (CHƯA VIẾT)
└── UI/
    └── UX_DESIGN/
        ├── FLOAT_POPUP_PANEL_UX.MD   # panel UX — size đã CHỐT
        ├── FLOAT_ICON_UX_SPEC.MD     # icon float UX
        ├── APP_UX_OVERVIEW.MD        # tổng quan UX
        ├── MAIN_NAVIGATION_UX.MD     # menu tray + Settings
        └── SETTING_UX.MD             # Settings UX (CHƯA VIẾT)
```

## 4. Quy ước

- Doc trong `docs/` viết tiếng Việt (trừ `DECISIONS.md` viết tiếng Anh từ đầu — giữ nguyên).
- Panel UX là spec chốt: đổi số trong đó cần Manh duyệt, và cập nhật doc trong cùng đợt
  với code.

## Liên kết

- [Quy tắc làm việc](AGENTS.MD)
- [Bối cảnh AI](AI_CONTEXT.MD)
- [Trạng thái dự án](PROJECT_STATE.MD)
- [Panel UX (CHỐT)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD)
- [Chỉ mục UX](UI_DESIGN.MD)
