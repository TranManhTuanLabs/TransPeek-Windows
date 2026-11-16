# Task Queue (bản nháp)

> Trạng thái: **bản nháp** — việc đang làm / chờ. Cập nhật dần khi xong hoặc có việc mới.
> Ghi rõ session + repo cho mọi mục nợ (quy tắc 17/11: tránh áp nhầm sang repo khác).

## ĐANG LÀM

- [ ] **Bug che loa/Copy panel** (Windows — TransPeek-Windows, #149→#159, vẫn MỞ):
  tra 1–5 line bị che, từ 6 line trở lên không bị. Nguyên nhân thật CHƯA RA.
  Đang chờ video/ảnh so sánh 3-line (bị) vs 6-line (không bị) từ Manh.
  KHÔNG đẩy thêm bản mò. Chi tiết: `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD` (§6).
- [ ] **Doc UI/UX** (Windows — TransPeek-Windows): panel đã chốt + viết xong;
  các doc còn lại là bản nháp, cập nhật dần khi hoàn thiện hoặc Manh yêu cầu.
  `SETTINGS.MD` và `SETTING_UX.MD` CHƯA VIẾT.

## CHỜ MANH

- [ ] **Link từ điển** — Manh đang tự sưu tập, sẽ gửi 1 thể; CHỈ sửa khi Manh nói
  "bắt đầu fix". Chi tiết: `~/workspace/projects/moepeek-translator/dictionary-links-pending.md`.
- [ ] **Saved pre-release** — chờ panel ổn (hết che loa) + Manh test OK thì save bản đó
  làm baseline (tag `0.1.3.<run>-saved`, tiêu đề "(saved preview)"). Cần Manh tiếp quản
  trình duyệt để chọn file + Publish (trình duyệt không upload được zip 150MB).
- [ ] **0.1.4** — Velopack → Privacy Policy → SignPath → Store (thứ tự trong
  `MASTER_PLAN.md`). Chờ Manh ra lệnh mới triển khai.

## NỢ / LÀM SAU

- [ ] Icon float theo dark/light system.
- [ ] Nav pane 250 — Manh chưa phán quyết (nếu chật → về 320).
- [ ] "Restart TransPeek" tray — Manh thấy "hơi vô nghĩa" nhưng chưa bảo bỏ.
- [ ] Icon float lúc hiện lúc không — Manh test thêm.
- [ ] Slider cỡ panel trong Settings — Windows chưa có (chỉ có slider font);
  chờ Manh quyết có đồng bộ với mac không.
- [ ] Nút RSVP cạnh loa trong output — chưa chốt lại sau khi loa chuyển vào trong ô.

## ĐÃ ĐÓNG

- [x] SAPI / Service tự nhận engine TTS cài ngoài — Manh chốt KHÔNG làm (16/11 23:32).
