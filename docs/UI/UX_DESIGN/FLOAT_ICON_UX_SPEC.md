# Float Icon — UX Spec (bản nháp)

> Trạng thái: **bản nháp** — ghi lại những gì đã chốt, chờ Manh duyệt rồi mới thành spec chốt
> như panel (`FLOAT_POPUP_PANEL_UX.md`).

## 1. Hình dạng (đã chốt)

- Cửa sổ **32×32**, vòng tròn trắng **30px**, viền `#C8C8C8`, chữ **"A" 14pt** thường,
  màu xanh `#0078D4`.
- Trong suốt thật: `TransparentWindow.cs` + `SetWindowRgn` cắt tròn; tắt viền DWM
  (`DWMWA_COLOR_NONE`). **KHÔNG có bubble** như mac.
- Icon theo dark/light system: **CHƯA LÀM** — vòng tròn trắng + chữ "A" chưa đổi theo
  system, làm sau.

## 2. Hành vi (đã chốt)

- **Mặc định: bôi chọn text → icon hiện** gần chỗ chọn (theo dõi selection tự động,
  có độ trễ hover chỉnh được trong Settings, mặc định 500ms — chọn được 300/500/800/1000).
- **Hover icon → luồng dịch:** mở panel dịch.
- **Click icon → luồng TTS nghe:** phát tiếng của text đã chọn (glyph đổi thành loa E767
  lúc đang phát; bấm lần nữa để dừng).
- **Option-click (Mac) / Alt-click (Windows) → đọc nhanh (RSVP) thẳng** — đã chốt
  luồng vào RSVP 17/11, xem `FEATURES/SPEECH_PLAYER.md`.
- **Chuột phải icon → menu** có mục "Đọc nhanh".
- Icon thỉnh thoảng lúc hiện lúc không khi select text — **đang chờ Manh test thêm**
  trên các bản mới (nợ phiên).

## 3. Vị trí

- Hiện gần selection; panel mở gần icon (x, y truyền vào `ShowPanelNear`).
- Chưa chốt chi tiết offset / tránh mép màn hình — bổ sung sau khi đo thực tế.

## 4. Tham chiếu code

- `TriggerIconWindow` (cửa sổ icon), `TransparentWindow.cs`, `SetWindowRgn` cắt tròn.
- Click phát tiếng: `_iconSpeech` trong `App.xaml.cs` + `SystemSpeechService.DetectLanguage` (VI/EN).

## Liên kết

- [Panel UX (CHỐT)](FLOAT_POPUP_PANEL_UX.md)
- [Tổng quan UX](APP_UX_OVERVIEW.md)
- [Tính năng Float Icon](../../FEATURES/FLOAT_ICON.md)
- [Điều hướng chính](MAIN_NAVIGATION_UX.md)
