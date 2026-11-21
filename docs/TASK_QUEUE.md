# Task Queue (bản nháp)

> Trạng thái: **bản nháp** — việc đang làm / chờ. Cập nhật dần khi xong hoặc có việc mới.
> Ghi rõ session + repo cho mọi mục nợ (quy tắc 17/11: tránh áp nhầm sang repo khác).

## ĐANG LÀM

- [ ] **Bug che loa/Copy panel** (Windows — TransPeek-Windows, #149→#159, fix #164 đã vào):
  Nguyên nhân thật ĐÃ TÌM RA (18/11): kéo NGANG cũng bị tính là kéo tay (khoá auto-fit)
  + `SettleResizedSize` chỉ biết co không biết dài → card tràn khỏi hàng `*`, hàng Actions
  đè che đáy card. Fix #164 (`bf74379`, CI xanh) đã vào main. **Chờ Manh xác nhận hết che
  trên bản mới** — xác nhận thì viết lại case study ở `docs/AGENTS.md` §3 theo nguyên nhân
  thật (hiện §3 vẫn theo kết luận cũ). Chi tiết: `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` (§6).
- [ ] **Doc UI/UX** (Windows — TransPeek-Windows): panel đã chốt + viết xong;
  các doc còn lại là bản nháp, cập nhật dần khi hoàn thiện hoặc Manh yêu cầu.
  `SETTINGS.md` và `SETTING_UX.md` CHƯA VIẾT.

## CHỜ MANH

- [ ] **Link từ điển** — Manh đang tự sưu tập, sẽ gửi 1 thể; CHỈ sửa khi Manh nói
  "bắt đầu fix". Chi tiết: `~/workspace/projects/moepeek-translator/dictionary-links-pending.md`.
- [ ] **Saved pre-release** — chờ panel ổn (hết che loa) + Manh test OK thì save bản đó
  làm baseline (tag `0.1.4.<run>-saved`, tiêu đề "(saved preview)"). Cần Manh tiếp quản
  trình duyệt để chọn file + Publish (trình duyệt không upload được zip 150MB).
- [ ] **0.1.4** — Velopack → Privacy Policy → SignPath → Store (thứ tự trong
  `MASTER_PLAN.md`). Chờ Manh ra lệnh mới triển khai.

## NỢ / LÀM SAU

- [x] Dòng hướng dẫn ở mục tải ngôn ngữ OCR (Windows — TransPeek-Windows, Manh 2026-11-21 08:35, làm ở đợt gộp commit sau, KHÔNG đẩy lẻ): Settings → OCR → mục "Tesseract language data", thêm 1 dòng hỏi ngôn ngữ user thường dùng/muốn OCR nhận diện + nhắc tải dữ liệu ngôn ngữ đó. Vị trí: ngay dưới header "Tesseract language data" (`OcrPage.xaml` dòng 31), trên `TesseractStatusText`. Câu gợi ý (UI tiếng Anh): "Which languages do you usually use OCR for? Tick them below and download — OCR can only recognize a language after its data is downloaded." Câu chữ Manh ĐÃ CHỐT 08:40 ("Ok câu đó") — làm ở đợt gộp sau. Đã so sánh với câu mac ngày 08:44, chọn giữ câu này cho Windows vì: Windows tick checkbox ở list "All Tesseract languages" bên DƯỚI + nút "Download selected" (không có mục "Recognition languages" như mac; ô ngôn ngữ trên Windows là textbox gõ tay, không tick được); auto-download cũng có trên Windows nhưng câu này vẫn đúng (dữ liệu phải có thì Tesseract mới đọc được). **ĐÃ LÀM 2026-11-21 ở đợt gộp cùng OCR auto-add + tray Reading + pin (chưa đẩy — chờ PR #3 gộp xong mới đẩy để khỏi xung đột DECISIONS.md).**
- [x] Pin nghiêng 30° trái (Windows — TransPeek-Windows, Manh 2026-11-21 09:59, làm ở đợt gộp sau, KHÔNG làm lẻ): pin ở hàng dots (`TranslatePanelWindow.xaml`, `PinButton`, FontIcon E718) hiện đang thẳng đứng (0°, chưa có RotateTransform) — thêm `RotateTransform Angle="-30"` (tâm giữa, `RenderTransformOrigin="0.5,0.5"`) cho nghiêng trái 30°. Mac parity: bên mac pin đang -45°, Manh vừa đổi sang -30° (commit `79869410`). **ĐÃ LÀM 2026-11-21 ở đợt gộp cùng OCR auto-add + tray Reading + dòng hướng dẫn OCR (chưa đẩy — chờ PR #3 gộp xong mới đẩy để khỏi xung đột DECISIONS.md).**
- [x] **30h qua (21–22/11, Windows — TransPeek-Windows):** cỡ icon float + mặc định 24pt + icon alternative + hướng an toàn phím (main `b94893f1` → `4aa959b5`, CI #233/#234/#235/#238 xanh); hover polish (`fa68941f`, CI #231); block-text PR #3 + đợt gộp 5 mục (`a6d8c106`); nhánh Style 2 + loa đã gỡ tracker (`7b912e63` + xoá file `714d933`, CI #237 xanh) — chờ Manh test rồi merge PR #4. Chi tiết: `CHANGELOG.md`.
- [ ] **Điều tra panel focus** (Windows — TransPeek-Windows, Manh quan sát test CI230 23:28 ngày 21/11): click 1 vào panel KHÔNG kích hoạt, click 2 cùng vị trí mới kích hoạt; chuột ra ngoài panel thì mất viền accent/bóng. Đã ghi chi tiết vào panel doc §11.7. Hướng điều tra: cú `PointerReleased` đầu có bị nuốt không; `SetForegroundWindow` lần đầu có bị foreground lockdown chặn không; test bản cũ không có auto-detect (`v0.1.4.211`) để chốt auto-detect có liên quan không.
- [ ] **Đổi tên file** `src/TransPeek/Views/LanguageComboBoxHelper.cs` → `LanguageSuggestBoxHelper.cs` (snapshot push không đổi tên được; làm khi có lần sửa qua trình duyệt; đã ghi `DECISIONS.md` mục CI #189).
- [ ] Icon float theo dark/light system.
- [ ] Nav pane 250 — Manh chưa phán quyết (nếu chật → về 320).
- [ ] "Restart TransPeek" tray — Manh thấy "hơi vô nghĩa" nhưng chưa bảo bỏ.
- [ ] Icon float lúc hiện lúc không — Manh test thêm.
- [ ] Slider cỡ panel trong Settings — Windows chưa có (chỉ có slider font);
  chờ Manh quyết có đồng bộ với mac không.
- [ ] Nút RSVP cạnh loa trong output — chưa chốt lại sau khi loa chuyển vào trong ô.

## ĐÃ ĐÓNG

- [x] SAPI / Service tự nhận engine TTS cài ngoài — Manh chốt KHÔNG làm (16/11 23:32).

## Liên kết

- [Trạng thái dự án](PROJECT_STATE.md)
- [Lộ trình](MASTER_PLAN.md)
- [Panel UX (bug che loa)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
