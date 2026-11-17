# Architecture (bản nháp)

> Trạng thái: **bản nháp** — kiến trúc tổng, cập nhật dần. Quyết định chi tiết:
> `docs/DECISIONS.md`.

## 1. Stack

- **WinUI 3 + C#, Windows App SDK 1.8.12 (.NET 8)** — native Windows UI.
- Target `net8.0-windows10.0.22000.0`, unpackaged (`WindowsPackageType None`),
  self-contained, publish `win-x64`.
- Windows App SDK 1.8.12 hết servicing từ 24/09/2026 — nâng lên 2.x trước khi nộp Store.
- `ImplicitUsings` bật ở cả 3 project, LangVersion 12.

## 2. Cấu trúc repo

```
src/
  TransPeek/          # app WinUI 3 (Views, Services, Assets, FastRead/)
  TransPeek.Core/     # logic thuần (Settings, Translation, Ocr, Dictionary, FastRead/)
tests/
  TransPeek.Tests/    # chỉ test logic Core trên net8.0
docs/
  DECISIONS.md        # mọi quyết định đã chốt + lý do
  WINDOWS-v0.1-TESTING.md
  FEATURES/           # mô tả từng tính năng
  UI/UX_DESIGN/       # spec UX (panel đã CHỐT)
  ARCHITECTURE.md     # doc này
  MASTER_PLAN.md      # lộ trình
  STRUCTURE_MAP.md    # bản đồ repo + docs
  TASK_QUEUE.md       # việc đang làm / chờ
  UI_DESIGN.md        # chỉ mục UX
```

Tính năng mới sống trong thư mục riêng của nó (quy tắc tách module ở §5):
`FastRead/` là tính năng đầu tiên theo cấu trúc này (engine ở Core, window
ở app, cùng tên thư mục để 2 repo song song).

## 3. Thành phần chính

- **App.xaml.cs** — composition root; `App.Settings` static không bao giờ null
  (khởi tạo `AppSettings.Default()`, gán lại bằng `SettingsStore.Load()` ở startup).
- **Cửa sổ:** `TriggerIconWindow`, `TranslatePanelWindow`, `RegionSelectWindow`,
  `ToastWindow`, `SettingsWindow`.
- **Translation:** `ITranslationProvider.TranslateAsync` →
  `TranslationProviderResult(Text, DetectedSourceLanguage)`; `TranslationCoordinator` →
  `TranslationResult(Text, ProviderName, DetectedSourceLanguage)`. Google Translate,
  retry 429 tối đa 3 lần backoff 1s/2s/4s, KHÔNG fallback provider.
- **TTS:** `SystemSpeechService` (`Windows.Media.SpeechSynthesis`) — xem
  `FEATURES/SPEECH_ENGINE.md`.
- **OCR:** Windows OCR + Tesseract — xem `FEATURES/OCR_TEXT_CAPTURE.md`.
- **Settings:** `AppSettings` (JSON, `%LOCALAPPDATA%\TransPeek\settings.json`),
  `SettingsStore.Load/Save`, migration `SettingsVersion`.
- **Hotkey:** `GlobalHotkeyService` (WndProc static + ConcurrentDictionary).

## 4. Build / CI

- `build.yml`: chỉ build app + up artifact khi commit có `[build-app]`;
  docs-only (`docs/**`, `*.md`) không trigger CI. Pre-release tự động mỗi build
  `[build-app]` (tag `v0.1.3.<run>`, chỉ giữ mới nhất). Stable sau này là 0.1.4,
  tạo thủ công.
- Bundle Tesseract eng+vie tải ở bước CI (fail nếu file < 1MB).

## 5. Quyết định kiến trúc

### Tách module: mỗi tính năng 1 thư mục, không nhét vào code dịch (Manh 2026-11-17)

- Tính năng mới (tomato/focus/rest-eyes, Fast Read/RSVP, ...) viết riêng
  file/module — không nhét vào code chính/code dịch. Lý do: tránh conflict
  và tránh làm vỡ panel dịch (đã qua quá nhiều vòng sửa).
- Cấu trúc song song mac + Windows, 1 thư mục/tính năng, UI + logic nằm
  chung trong đó (không tách quá nhỏ — 1 file cho 1 hàm thì khó tìm):
  `FastRead/` (engine RSVP + cửa sổ đọc + view), `BreakTimer/` (timer
  Pomodoro/focus/20-20-20 + tab, làm sau), `Shortcuts/` (1 view dùng chung
  cho nút "?" nhúng + panel shortcut trong khung multi-tab, không copy-paste
  2 bản — TODO refactor).
- Khung tab giữ mỏng: 1 tab = 1 view + 1 tên, không gọi thẳng code dịch.
- Chỉ chạm code chung ở chỗ nối mỏng (nút, menubar/tray, Alt+click, toggle
  Settings). Cái dùng chung thật (icon float, hiện/ẩn panel, Settings,
  provider dịch) thì giữ chung, không nhân bản.
- Áp dụng đầu tiên: `FastRead/` trên nhánh `feature/fast-read-rsvp`.
  Ghi đầy đủ hơn ở `DECISIONS.md` (mục cùng tên).

## Liên kết

- [Quyết định đã chốt](DECISIONS.md)
- [Lộ trình](MASTER_PLAN.md)
- [Bản đồ docs](STRUCTURE_MAP.md)
- [Bối cảnh AI](AI_CONTEXT.md)
- [Trạng thái dự án](PROJECT_STATE.md)
