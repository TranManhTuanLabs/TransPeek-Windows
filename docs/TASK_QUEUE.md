# Task Queue (bản nháp)

> Trạng thái: **bản nháp** — việc đang làm / chờ. Cập nhật dần khi xong hoặc có việc mới.
> Ghi rõ session + repo cho mọi mục nợ (quy tắc 17/11: tránh áp nhầm sang repo khác).

## ĐANG LÀM

- [ ] **Bug che loa/Copy panel** (Windows — TransPeek-Windows, #149→#159, fix #164 đã vào):
  Fix #164 (`bf74379`, CI xanh) đã vào main nhưng **Manh xác nhận 22/11 VẪN CHE trên bản
  v0.1.4.238** — nguyên nhân 18/11 (kéo ngang khoá auto-fit + settle chỉ biết co) KHÔNG phải
  nguyên nhân cuối. Repro 22/11: mở lần đầu đã che; kéo nhỏ hết cỡ dừng ĐÚNG điểm che;
  dịch từ mới không kéo vẫn che; text rất dài không che, 3–6 dòng vẫn che.
  **Nguyên nhân thật ĐÃ XÁC NHẬN bằng số đo 23/11** (Manh test bản 240, máy công ty Dell
  Pro 24, 1920x1080, scale 100%): frame=16x16 (outer 420x554, client 404x538) — khung
  cửa sổ thừa (`WS_DLGFRAME` sót lại sau `SetBorderAndTitleBar` + `WS_THICKFRAME`) ăn mất
  16px chiều cao vùng hiển thị → ô output nhận 210 thay vì tối thiểu 218 → loa/copy bị
  che ~8px. Khớp ghi chú của Manh: 1 chữ bị che, 3 dòng không bị. **Đang sửa trên nhánh
  `fix/panel-strip-frame`** (tách `RemoveWindowFrame` thành `WindowHelpers.StripWindowFrame`
  dùng chung, panel gọi ở Loaded; log `[PanelSize]` GIỮ LẠI trên bản nhánh để test xác
  nhận frame=0). Chờ Manh test bản nhánh → OK thì gỡ log + gộp main. Case study
  `docs/AGENTS.md` §3 đã viết lại theo nguyên nhân thật (trên nhánh). Chi tiết:
  `docs/UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` (§6).
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
- [x] Basic Reading: kéo chuột đổi font size, thay wheel (Windows — TransPeek-Windows, Manh 2026-11-21 20:19, lẻ gộp phiên sửa sau, KHÔNG đẩy lẻ): click chuột vào text rồi kéo lên/xuống để đổi font size, cả Full + Lite (`Style2ReadingView` dùng chung 2 mặt). "Đổi sang" = THAY wheel bằng kéo, không giữ cả hai — gỡ `OnPageWheelChanged` đã đẩy ở commit `50bd04d0` (wheel 5pt/tick, clamp 40–450). Gợi ý triển khai: PointerPressed ghi Y bắt đầu + cỡ chữ bắt đầu (capture pointer), PointerMoved đổi cỡ theo delta Y (kéo lên = to lên), PointerReleased/CaptureLost kết thúc + lưu 1 lần; giữ dòng hiện tại ở giữa khi kéo (như bản wheel, animate: false). Lưu ý: chuột kéo trên ScrollViewer không tự cuộn (chỉ wheel/touch mới cuộn), nên kéo chuột không tranh cuộn trang. **ĐÃ LÀM 2026-11-21 ở đợt gộp cùng icon Maximize + fix CI #224 (1 commit, chờ CI mới).**
- [x] Nút Maximize ở 2 mặt Read: đổi icon gạch chéo 2 đầu mũi tên (Windows — TransPeek-Windows, Manh 2026-11-21 20:31, gộp phiên sửa sau, KHÔNG đẩy lẻ): nút Maximize hiện tại là ô vuông (glyph ChromeMaximize E922, trông như nút stop) — đổi sang gạch chéo có 2 đầu mũi tên (expand, như ⤢), cả Full window lẫn Lite (`FastReadWindow` + `CompactReadingView`, nút Maximize trong transport). Em CHỌN gạch chéo 2 đầu (không chọn 4 góc như icon scan: 4 góc dễ nhầm với icon scan mã vạch đã có trong app; gạch chéo là metaphor maximize/fullscreen quen thuộc, đọc rõ ở cỡ nhỏ). Tra glyph đúng trên Microsoft Learn trước khi code (không lấy tên từ trí nhớ). **ĐÃ LÀM 2026-11-21: E9E3 `ArrowExpand` (đã xem ảnh glyph thật trên trang Segoe Fluent Icons), ở đợt gộp cùng kéo font size + fix CI #224 (1 commit, chờ CI mới).**
- [x] **30h qua (21–22/11, Windows — TransPeek-Windows):** cỡ icon float + mặc định 24pt + icon alternative + hướng an toàn phím (main `b94893f1` → `4aa959b5`, CI #233/#234/#235/#238 xanh); hover polish (`fa68941f`, CI #231); block-text PR #3 + đợt gộp 5 mục (`a6d8c106`); nhánh Style 2 + loa đã gỡ tracker (`7b912e63` + xoá file `714d933`, CI #237 xanh) — **PR #4 ĐÃ GỘP 23/11 (merge commit `9b0369d`, CI #244 xanh, pre-release `v0.1.4.244`). Chi tiết: `CHANGELOG.md`.
- [ ] **Điều tra panel focus** (Windows — TransPeek-Windows, Manh quan sát test CI230 23:28 ngày 21/11): click 1 vào panel KHÔNG kích hoạt, click 2 cùng vị trí mới kích hoạt; chuột ra ngoài panel thì mất viền accent/bóng. Đã ghi chi tiết vào panel doc §11.7. Hướng điều tra: cú `PointerReleased` đầu có bị nuốt không; `SetForegroundWindow` lần đầu có bị foreground lockdown chặn không; test bản cũ không có auto-detect (`v0.1.4.211`) để chốt auto-detect có liên quan không.
- [ ] **Đổi tên file** `src/TransPeek/Views/LanguageComboBoxHelper.cs` → `LanguageSuggestBoxHelper.cs` (snapshot push không đổi tên được; làm khi có lần sửa qua trình duyệt; đã ghi `DECISIONS.md` mục CI #189).
- [ ] **Component chung cho phím/focus của read** (Windows — TransPeek-Windows, Manh 03:38: **làm SAU khi PR #4 gộp**): tránh phân mảnh như bên mac (Full tự lo focus, Lite tự lo focus, mỗi đường mở read tự nhớ kích hoạt). Thiết kế đã nghiên cứu xong (03:35, đọc code thật main): thêm 1 event `PlayingStarted` vào `RsvpReaderViewModel` (chung, chưa có event nào ngoài `PropertyChanged`), bắn khi `SetPlaying` chuyển dừng→phát; 3 host gán 1 lần lúc tạo (Full → foreground+Activate, panel → `ActivateWindow`, Rest → `Activate`) rồi gỡ các lệnh kích hoạt lẻ ở lúc bắt đầu phát (giữ click-kích hoạt). Lưu ý: `StartPanelReading` tạo view model mới mỗi lần nên panel phải gán lại callback cho instance mới; foreground lockdown vẫn có thể chặn kích hoạt từ timer. Thứ tự: sau PR #4 gộp để tránh xung đột file + kế thừa `PreviewKeyDown` của nhánh. Chi tiết nghiên cứu: `~/MEMORY.md` mục 2026-11-22 (component chung).
- [ ] **Loa ở cửa sổ nghỉ** (Windows — TransPeek-Windows, Manh 23/11) — thêm nút loa vào cửa sổ nghỉ (RestWindow) để nghe thay vì đọc lúc nghỉ mắt; hiện cửa sổ nghỉ không có loa (không nối TTS service). Làm sau khi PR #4 gộp (cần hạ tầng loa từ PR #4).
- [ ] **Loa nhớ trạng thái chung của read** (Windows — TransPeek-Windows, Manh 23/11, làm sau khi PR #4 gộp) — 1 trạng thái loa chung cho Full + Lite, lưu bền (mặc định lần đầu vẫn TẮT; bật ở mặt nào thì mặt kia nhớ theo); thay thế khung Style 2 (mặc định TẮT, không lưu bền) và khác quyết định mac 21/11 (mỗi mặt giữ riêng). Đề xuất: 1 setting `RsvpSoundOn`, đọc khi bắt đầu đọc, lưu khi bấm loa.
- [ ] **Gộp speed audio vào WPM** (Windows — TransPeek-Windows, Manh 23/11, làm sau, chưa có lịch) — 1 control tốc độ chung, không để 2 slider riêng (Speed 0.6x–4x + WPM); bảng quy đổi cần chốt lại khi làm (vd anh đưa: 100-0.1x, 200-1.5x).
- [ ] Icon float theo dark/light system.
- [ ] Nav pane 250 — Manh chưa phán quyết (nếu chật → về 320).
- [ ] "Restart TransPeek" tray — Manh thấy "hơi vô nghĩa" nhưng chưa bảo bỏ.
- [ ] Icon float lúc hiện lúc không — Manh test thêm.
- [ ] Slider cỡ panel trong Settings — Windows chưa có (chỉ có slider font);
  chờ Manh quyết có đồng bộ với mac không.
- [ ] Nút RSVP cạnh loa trong output — chưa chốt lại sau khi loa chuyển vào trong ô.
- [ ] Panel: click 1 không kích hoạt, click 2 mới kích hoạt + chuột ra ngoài mất viền accent (Windows — TransPeek-Windows, Manh test CI230 2026-11-21 23:28, việc điều tra phiên sau): panel mở lên không có viền màu 1px ở đỉnh + bóng nhạt (đúng thiết kế NOACTIVATE); nhưng click 1 vào panel (topbar/bottom/input/output/action đều thử) KHÔNG kích hoạt, click 2 đúng/gần vị trí click 1 mới kích hoạt; chuột ra ngoài panel thì bóng nhạt lại, mất viền. Nghi ngờ liên quan tới Space play/pause không ăn khi chưa click vào panel (workaround cũ `GlobalSpaceTracker` ở `ff33cce5` đã GỠ 2026-11-22 theo hướng an toàn — panel tự kích hoạt khi bài đọc panel-right bắt đầu). Chưa rõ có từ auto-detect block text (PR #3) hay đã có từ trước. Khi điều tra: test bản cũ không có auto-detect (vd v0.1.4.211) để chốt; rồi kiểm tra cú `PointerReleased` đầu có bị nuốt không, `SetForegroundWindow` lần đầu có bị chặn không. Chi tiết: `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §11.7.

## ĐÃ ĐÓNG

- [x] SAPI / Service tự nhận engine TTS cài ngoài — Manh chốt KHÔNG làm (16/11 23:32).

## Liên kết

- [Trạng thái dự án](PROJECT_STATE.md)
- [Lộ trình](MASTER_PLAN.md)
- [Panel UX (bug che loa)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
