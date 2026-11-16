# AI Context — TransPeek-Windows

> Bối cảnh cho AI làm việc trong repo này. Quy tắc làm việc: `AGENTS.MD`.
> Trạng thái hiện tại: `PROJECT_STATE.MD`. Bản đồ docs: `STRUCTURE_MAP.md`.

## 1. App là gì

- **TransPeek-Windows**: app dịch / tra từ / nghe (TTS) / OCR cho Windows 11,
  bản native Windows của TransPeek (fork từ MoePeek, macOS).
- **Định hướng (Manh chốt):** (1) Dịch/tra từ là chính, OCR hỗ trợ; (2) TTS là tính
  năng chính để NGHE text thay vì đọc bằng mắt. Cả 2 chung 1 float icon:
  **HOVER → dịch; CLICK → nghe**.
- Repo: `TranManhTuanLabs/TransPeek-Windows`, branch `main`, public.
- Version hiện tại: **0.1.3** (pre-release tự động); stable sau là **0.1.4**.

## 2. Stack

- **WinUI 3 + C#, Windows App SDK 1.8.12 (.NET 8)**, target
  `net8.0-windows10.0.22000.0`, unpackaged, self-contained, publish `win-x64`.
- `src/TransPeek/` (app), `src/TransPeek.Core/` (logic thuần),
  `tests/TransPeek.Tests/` (chỉ test logic Core).
- Ngôn ngữ UI gốc English; tiếng Việt thêm sau khi app hoàn thiện.

## 3. Quyết định lớn đã chốt (chi tiết: `DECISIONS.md`)

- **Dịch:** Google Translate; retry 429 tối đa 3 lần (backoff 1s/2s/4s),
  KHÔNG fallback provider.
- **TTS:** chỉ giọng hệ thống Windows (`Windows.Media.SpeechSynthesis`) cho 0.1.4.
  KHÔNG SAPI, KHÔNG Edge, KHÔNG Azure giai đoạn này.
- **OCR:** Tesseract (vie + eng nhúng sẵn, zip ~150MB; 123 ngôn ngữ tải thêm trong app)
  + Windows OCR (không nhúng/tải ngầm được, chỉ mở Settings cho user tự cài).
- **Panel:** xem spec chốt `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD` — mở mặc định
  420×450, min 400×450, max rộng 600 / cao 800, cap output 260, min card 218,
  auto-fit + snap-back, mỗi lần mở về mặc định.
- **Phát hành:** 1 git flow, 1 version → Velopack (GitHub) + MSIX (Store); Privacy Policy
  bắt buộc; SignPath Foundation ($0) để ký bản GitHub.
- **CI:** chỉ build app khi commit có `[build-app]`; docs-only không trigger CI;
  pre-release tự động, chỉ giữ mới nhất; stable 0.1.4 tạo thủ công.

## 4. File quan trọng

- Panel: `src/TransPeek/Views/TranslatePanelWindow.xaml(.cs)` (hằng số size ~dòng 50–121).
- Settings: `src/TransPeek.Core/Settings/AppSettings.cs` (`PanelWidth=420`,
  `PanelHeight=450`, migration `SettingsVersion`).
- TTS: `src/TransPeek/Services/SystemSpeechService.cs`.
- OCR: `src/TransPeek.Core/Ocr/TesseractLanguageCatalog.cs`.
- CI: `.github/workflows/build.yml`.

## 5. Đường dẫn trên máy Manh

- Crash log `%LOCALAPPDATA%\TransPeek\crash.log`, windows log
  `%LOCALAPPDATA%\TransPeek\windows.log`, settings
  `%LOCALAPPDATA%\TransPeek\settings.json`, tessdata
  `%LOCALAPPDATA%\TransPeek\tessdata`, ảnh OCR
  `%LOCALAPPDATA%\TransPeek\ocr-capture.png`.
- Lệnh tắt app: `taskkill /F /IM TransPeek.exe`. Nhắc Manh thoát hẳn bản cũ
  trước khi mở bản mới (tránh test nhầm bản cũ còn chạy ở tray).

## 6. Người dùng

- **Manh**: nói tiếng Việt; không phải dân IT gốc, đang học, vibecode bằng AI.
  Muốn AI vừa làm vừa dạy dần, chỉnh từ kỹ thuật sai thành đúng ngay lúc nói.
  Quyết nhanh, muốn làm đầu-cuối, gộp 2–3 sửa vào 1 commit + 1 vòng test.

## Liên kết

- [Quy tắc làm việc](AGENTS.MD)
- [Trạng thái dự án](PROJECT_STATE.MD)
- [Bản đồ docs](STRUCTURE_MAP.md)
- [Kiến trúc](ARCHITECTURE.MD)
- [Quyết định đã chốt](DECISIONS.md)
