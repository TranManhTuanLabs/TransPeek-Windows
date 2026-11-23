# Changelog — TransPeek-Windows

> Log thay đổi theo ngày, mới nhất lên đầu. Mỗi mục: ngày + commit + CI + thay đổi ngắn.
> Quyết định chi tiết: `DECISIONS.md`. Trạng thái hiện tại (ảnh chụp nhanh):
> `PROJECT_STATE.md`.

## 2026-11-23

- **nhánh `fix/panel-strip-frame` — sửa bug che loa theo số đo (chưa có số CI):** Manh test
  bản 240, log `[PanelSize]` đo được frame=16x16 (outer 420x554, client 404x538) — nguyên
  nhân thật đã rõ: chiều cao tính cho vùng hiển thị nhưng đặt theo kích thước ngoài, panel
  thiếu 16px, ô output nhận 210 thay vì tối thiểu 218 → loa/copy bị che ~8px. Sửa: tách
  `RemoveWindowFrame` thành `WindowHelpers.StripWindowFrame` dùng chung (chỉ gỡ khung,
  panel vẫn là card mờ đục) + panel gọi ở Loaded; log chẩn đoán GIỮ LẠI trên bản nhánh để
  test xác nhận frame=0. Chờ Manh test bản nhánh.

## 2026-11-22

- **main — "Read in:" + thứ tự reminder (theo mac b0e314f0 + 46c8e357 + 2cb39c10, CI #240
  XANH, pre-release `v0.1.4.240`):** panel right demo thêm tiêu đề "Read in:" + 2 nút "FastRead Lite" / "FastRead
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
