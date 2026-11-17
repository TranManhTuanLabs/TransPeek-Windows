# Speech Player (bản nháp)

> Trạng thái: **bản nháp** — mô tả player TTS (nút loa, auto-play, đọc nhanh RSVP).

## 1. Nút loa

- **Loa panel** (ô nguồn + ô dịch): toggle — đang phát bấm lại = Stop.
  Tooltip "Listen to source text" / "Listen to translation".
- **Loa icon float**: click = phát text đã chọn; bấm lần nữa để dừng.
- Nút loa ở output đã chuyển **vào trong ô text** như input (#157) — overlay góc
  dưới-phải, Margin `0,0,6,8`. Xem bug che loa trong
  `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD` (§6).

## 2. Auto-play

- Tab TTS Speech: Auto-play source, Auto-play translation (nguồn trước, dịch sau
  qua event `SpeechEnded`). Nối vào `ShowResult` → `AutoPlayIfEnabled()`.

## 3. Đọc nhanh (RSVP) — đã chốt luồng vào 17/11, CHƯA LÀM

- (1) Alt-click (Windows) / Option-click (Mac) → RSVP thẳng.
- (2) Chuột phải icon → menu "Đọc nhanh".
- (3) Nút "Đọc nhanh" trong panel dịch.
- (4) Menubar/tray. (5) Shortcut global chỉ là fallback.
- + Settings toggle đổi click → RSVP cho người dùng RSVP là chính.
- Vị trí view đọc: **giữ cả 2 phương án** (tab dãn / panel riêng), quyết ở lúc prototype.
  Nếu làm tab phải **ẩn HOÀN TOÀN chức năng panel dịch** khi đang đọc.
- Engine RSVP (tham khảo SwiftRead): WPM mặc định 275, max 2000, chunk 1–6,
  ORP pivot = ký tự giữa `floor(n/2)`, chữ cam `#f6861f`.

## Liên kết

- [Speech Engine](SPEECH_ENGINE.MD)
- [Float Icon](FLOAT_ICON.MD)
- [Panel UX (CHỐT)](../UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD)
