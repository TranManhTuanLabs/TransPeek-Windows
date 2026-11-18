# Float Icon — UX Spec (bản nháp)

> Trạng thái: **bản nháp** — ghi lại những gì đã chốt, chờ Manh duyệt rồi mới thành spec chốt
> như panel (`FLOAT_POPUP_PANEL_UX.md`). Chi tiết đầy đủ theo mac:
> `docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md` trên repo TransPeek (commit `0200788`,
> §3, §5, §6 — Alt thay Option, `GetAsyncKeyState(VK_MENU)` thay `NSEvent.modifierFlags`).

## 1. Hình dạng (đã chốt)

- Cửa sổ **32×32**, vòng tròn trắng **30px**, viền `#C8C8C8`.
- **Style B (Manh 2026-11-18): icon luôn hiện việc mà click tiếp theo sẽ làm** —
  có 3 mark, mỗi lúc chỉ 1 cái hiện (`UpdateGlyph` trong `TriggerIconWindow`):
  - **BubbleMark:** bong bóng nói (Segoe Fluent Icons Message **E8BD**, đã kiểm chứng
    trên Microsoft Learn 18/11), xanh `#0078D4` — click tiếp theo sẽ Speak.
  - **APlusMark:** mark Fast Read tự vẽ — chữ "A" đậm 15pt + dấu "+" đậm 8pt
    superscript ở góc trên-phải của A, màu vàng nghệ **#C7800D** (rgb 0.78/0.50/0.05)
    để đọc được cả nền sáng/tối — click tiếp theo sẽ Fast Read.
  - **SpeakerMark:** loa **E767** Volume — lúc đang phát tiếng.
- Chữ "A" 14pt cũ (Manh 2026-11-14) đã bỏ; bubble Path tự vẽ (0.1.3.72) từng bị
  revert vì xấu, nhưng glyph font thì sạch nên dùng glyph.
- Trong suốt thật: `TransparentWindow.cs` + `SetWindowRgn` cắt tròn; tắt viền DWM
  (`DWMWA_COLOR_NONE`).
- Icon theo dark/light system: **CHƯA LÀM** — vòng tròn trắng chưa đổi theo
  system, làm sau.

## 2. Hành vi (đã chốt)

- **Mặc định: bôi chọn text → icon hiện** gần chỗ chọn (theo dõi selection tự động,
  có độ trễ hover chỉnh được trong Settings, mặc định 500ms — chọn được 300/500/800/1000).
- **Hover icon → luồng dịch:** mở panel dịch.
- **Click icon → hành động đã chọn trong Settings → General → "Click icon":**
  **Speak** (mặc định — nghe đọc to) hoặc **Fast Read** (mở Window Read).
- **Alt + Click → hành động CÒN LẠI** (Click = Speak thì Alt+Click mở Fast Read;
  Click = Fast Read thì Alt+Click nghe).
- **Bảng glyph (Style B) — icon hiện việc click tiếp theo sẽ làm:**

  | Click setting | Không giữ Alt | Giữ Alt | Đang phát tiếng |
  | --- | --- | --- | --- |
  | Speak (mặc định) | bubble (xanh) | **A+** (nghệ) | loa |
  | Fast Read | **A+** (nghệ) | bubble (xanh) | loa |
- **Linger A+ 2.5s:** sau bất kỳ lần bấm Read nào (click thường, Alt+Click, menu
  chuột phải) icon hiện A+ trong **2.5s** để xác nhận đã bấm Read, rồi mới ẩn —
  trong 2.5s đó bỏ qua click thêm. App không ẩn icon sớm (`IsReadLingerActive`).
- **Alt đọc từ trạng thái phím GLOBAL (mac §5 — lỗi vừa dính bên mac, build #235):**
  KHÔNG dùng handler chỉ nghe trong cửa sổ TransPeek (phím đi tới app khác nơi
  user đang chọn chữ, handler local không thấy). Poll `GetAsyncKeyState(VK_MENU)`
  mỗi **0.1s** khi icon hiện; chỉ tác động khi trạng thái đổi thật (không reset
  auto-hide vô cớ); đọc trạng thái ban đầu lúc icon hiện; kiểm tra lại khi
  chuột vào icon (safety net). Trong lúc giữ Alt thì auto-hide tạm dừng, thả
  Alt ra thì auto-hide chạy lại từ đầu (5s mới).
- **Auto-hide:** mặc định **5s** (đổi từ 3s ngày 2026-11-18, giống mac). Tạm dừng
  khi: chuột đang trên icon; đang phát tiếng; đang giữ Alt; đang trong linger 2.5s.
- **Chuột phải icon → menu** có mục "Fast Read".
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
