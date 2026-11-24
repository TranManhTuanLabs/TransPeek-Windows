# Changelog — TransPeek-Windows

> Log thay đổi theo ngày, mới nhất lên đầu. Mỗi mục: ngày + commit + CI + thay đổi ngắn.
> Quyết định chi tiết: `DECISIONS.md`. Trạng thái hiện tại (ảnh chụp nhanh):
> `PROJECT_STATE.md`.

## 2026-11-24

- **nhánh `fix/panel-strip-frame` — TRẢ KHUNG + SỬA CÔNG THỨC — MANH TEST #250 OK 24/11:** Manh test bản nhánh #250 (CI xanh, commit `76fbe04`): **loa HẾT CHE** — log `[PanelSize]` 24/11 09:41: outer=420x570, client=404x554, **frame=16x16**, minH=570, minParts=(fixedH=304 cardMin=218 cardMargin=8 **frameH=16** minH=570) — công thức đúng: 304+218+8+24+16=570, card thấy được = 554−304−8−24 = **218 đúng thiết kế**. Bo tròn 4 góc về lại. Kéo thu nhỏ hết cỡ dừng đúng điểm không che. **Pin VẪN MỜ — giả thuyết "gỡ khung làm mờ pin" BỊ BÁC** (khung đã trả lại, frame 16, mà pin vẫn mờ) — pin là việc riêng, điều tra sau, không chặn gộp. Gốc: minH tính số đo trong (client) nhưng đặt làm chiều cao ngoài, thiếu đúng khung; sửa: gỡ `StripWindowFrame` ở panel, card về lại 218, cộng khung đo live (`FrameHeightDips`) vào `ComputeMinPanelHeight`.
- **nhánh `fix/panel-strip-frame` — gỡ khung CHƯA ĐỦ + tăng min card 218→232 (chưa có số CI):** Manh test bản nhánh #241: frame chỉ 16x16 → 14x14 (client 406x540), loa vẫn che — `StripWindowFrame` chỉ gỡ được 2px, 14px còn lại không nằm ở style cửa sổ. Đã sync main vào nhánh (merge `9dc6f59`, PR #6 draft — PR #5 sync ngược đã đóng vì nút gỡ xung đột sẽ commit lên main). Thử tiếp theo hướng Manh chỉ: `MinOutputCardHeight` 218 → 232 (= 218 + 14 frame đo được) + XAML `MinHeight` đổi theo; log `[PanelSize]` giữ lại để xác nhận số mới.
- **nhánh `fix/panel-strip-frame` — sửa bug che loa theo số đo (chưa có số CI):** Manh test
  bản 240, log `[PanelSize]` đo được frame=16x16 (outer 420x554, client 404x538) — nguyên
  nhân thật đã rõ: chiều cao tính cho vùng hiển thị nhưng đặt theo kích thước ngoài, panel
  thiếu 16px, ô output nhận 210 thay vì tối thiểu 218 → loa/copy bị che ~8px. Sửa: tách
  `RemoveWindowFrame` thành `WindowHelpers.StripWindowFrame` dùng chung (chỉ gỡ khung,
  panel vẫn là card mờ đục) + panel gọi ở Loaded; log chẩn đoán GIỮ LẠI trên bản nhánh để
  test xác nhận frame=0. Chờ Manh test bản nhánh.

## 2026-11-23

- **Loa cửa sổ Nghỉ + loa nhớ trạng thái chung (Manh, mac parity):** cửa sổ Nghỉ có nút loa (cả 2 loại nhắc Pomodoro + nghỉ mắt đều qua `ShowRest` nên 1 chỗ nối đủ cả 2; service riêng, dispose khi đóng cửa sổ). Trạng thái loa CHUNG cho Full + Lite + Rest, lưu bền ở setting mới `RsvpSoundOn` (mặc định lần đầu TẮT; bấm loa ở mặt nào ghi ngay, mặt khác nhớ theo; read mới đọc trạng thái lần cuối; không sync live). Bấm loa khi đang tạm dừng chỉ đổi chế độ, vẫn cần bấm Play mới nói.
- **Space đọc lại khi đọc xong (Manh, mac parity):** Space khi đã đọc xong thì đọc lại từ đầu (trước đây không làm gì để nút share/Done hoạt động); cả 2 Style, Full + panel Lite. Nút share/Done vẫn bấm được bằng Enter + chuột. Ngoại lệ: cửa sổ Nghỉ — đọc xong thì hiện đếm ngược, phím đọc (Space / ←/→ / R) không làm gì để khỏi đọc lại sau lưng đếm ngược.
- **PR #4 MERGED — main `9b0369d` (CI #244 XANH, pre-release `v0.1.4.244`):** Fast Read Style 2 (Basic Reading) + speaker in Read mode — continuous phrase-by-phrase speech synced with focus, voice panel with Speed slider (0.6x–4x), safer keys (Space only when the read surface is focused), drag-to-resize font in Basic Reading, solid orange reminder banner, blue Done border, Maximize E9E3, "FastRead Full Window" naming.
- **3 quyết định mới về loa read (Manh, docs-only):** (1) thêm loa vào cửa sổ nghỉ; (2) loa nhớ trạng thái chung Full + Lite, lưu bền (thay khung Style 2 "mặc định TẮT, không lưu bền"); (3) sau này gộp speed audio vào WPM thành 1 control chung. Ghi vào `DECISIONS.md` + `FAST_READ.md` + `FOCUS_REST.md` + `TASK_QUEUE.md`. (Docs mac đã đẩy cùng ngày, commit `b4aa6042`.)
- **Đổi tên đích nhắc nhở (theo mac f08a9a3f):** "Run reminder in:" đổi "FastRead Full Window" → "FastRead Lite Window" (cửa sổ Nghỉ hiển thị view Lite, không phải cửa sổ Full thật). Chỉ đổi nhãn hiển thị; key/value/default của setting không đổi. "Run FastRead in:" và nút "Read in:" giữ nguyên tên cũ. (Dọn luôn 2 bullet trùng lặp trong `FOCUS_REST.md` từ đợt gộp PR #4.)

## 2026-11-22

- **main — "Read in:" + thứ tự reminder (theo mac b0e314f0 + 46c8e357 + 2cb39c10, chưa có
  số CI):** panel right demo thêm tiêu đề "Read in:" + 2 nút "FastRead Lite" / "FastRead
  Full Window" (đọc ngay, bỏ qua setting); radio thành "FastRead Full Window
  (recommended)"; "Run reminder in:" đổi thứ tự Lite lên trên.
- **main — bản chẩn đoán bug che loa (chưa có số CI):** log kích thước panel
  (outer/client/min/baseline) vào `windows.log` mỗi lần fit + submenu tray Diagnose
  (List windows / Open windows.log); nghi phạm khung cửa sổ thừa (`WS_DLGFRAME`).
- **v0.1.4.238 — main `4aa959b5` (CI #238 XANH):** hướng an toàn cho phím tắt lúc đọc —
  panel tự kích hoạt khi bài đọc panel-right bắt đầu (kể cả đường reminder), Space ăn
  ngay không cần click trước; đảo ngược quyết định cũ "reminder reads do NOT activate".
  (Bản `v0.1.4.235` bị keep-latest xoá khi 238 xong.)
- **v0.1.4.235 — main `27babd10` (CI #235 XANH):** icon alternative trong Fast Read
  (bôi đen text → icon A trong ô chat, rê thì dịch, bấm thì nghe bằng TTS riêng) +
  đổi mặc định cỡ icon float 26 → 24pt (đợt `bc5f20de`, CI #234 XANH).
- **Nhánh `feature/fastread-speaker-style2` — `714d933` (CI #237 XANH):** gỡ
  `GlobalSpaceTracker` (bắt Space toàn cục) + xoá file; panel tự kích hoạt khi đọc
  panel-right bắt đầu (đợt `7b912e63`).
- **main `bc5f20de` (CI #234 XANH):** "Float icon size" mặc định 26 → 24pt, picker thêm
  18/20/22, nhãn thêm "recommend 20-24pt"; "Auto-show" giữ 24pt, nhãn "recommend 20, 22pt".
- **main `b94893f1` (CI #233 XANH):** 2 picker cỡ icon float trong Settings → General
  (bôi chọn mặc định 26pt lúc đó, auto-show 24pt); chữ co theo vòng tròn.
- **main `fa68941f` (CI #231 XANH):** hover polish — icon hover nhỏ 24px, chỉ hiện khi
  chuột dừng đủ lâu (`HoverBlockDelayMs` mặc định 400ms, picker trong Settings → General).

## 2026-11-21

- **PR #3 merged (`217e04d5`):** block-text hover — rê chuột lên đoạn văn thì hiện icon
  (Model A, hit-test 1 điểm qua UIA, dwell 400ms, mặc định BẬT).
- **main `a6d8c106`:** đợt gộp 5 mục — tray có section "Reading" (Fast Read +
  Focus & Rest), OCR tải xong tự thêm ngôn ngữ, pin nghiêng -30°, dòng hướng dẫn OCR,
  số 249/123 lên README.
- **Nhánh speaker-style2:** Style 2 "Basic Reading" + nút loa ở Read mode (đọc liền mạch
  theo cụm-câu, phương án 1), fix CI #224 (CS0246), kéo chuột đổi font size, nút Maximize
  E9E3, fix test CI #228, fix CI230 (Full wheel cuộn trang Style 2) — CI #232 XANH, chờ
  Manh test rồi merge PR #4.
- **PR #2 merged (`0f8fdc7`):** Focus Timer + panel decor (Focus & Rest, banner nhắc nghỉ,
  pin sang hàng dots).

## 2026-11-18

- **Fix #164 — bug che loa/Copy (`bf74379`, CI XANH):** nguyên nhân thật — kéo NGANG cũng
  bị tính là kéo tay (khoá auto-fit) + `SettleResizedSize` chỉ biết co không biết dài,
  card tràn khỏi hàng `*` nên hàng Actions đè che đáy card. Fix: dài panel tới chiều cao
  cần (kẹp min/max), min card 238 → 218. **Chờ Manh xác nhận hết che.**

## Liên kết

- [Trạng thái dự án](PROJECT_STATE.md)
- [Quyết định](DECISIONS.md)
- [Việc đang làm / chờ](TASK_QUEUE.md)
