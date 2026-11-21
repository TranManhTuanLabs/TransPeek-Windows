# Project State — TransPeek-Windows

> ẢNH CHỤP NHANH tình hình hiện tại — đọc file này là nắm được dự án đang ở đâu.
> Log thay đổi theo ngày: `CHANGELOG.md`. Việc đang làm / chờ: `TASK_QUEUE.md`.
> Lộ trình: `MASTER_PLAN.md`. Cập nhật mỗi khi có thay đổi lớn.

## 1. Tổng quan (22/11/2026 ~03:20)

- **Version:** 0.1.4 (pre-release tự động mỗi code push — cổng `[build-app]` đã bỏ 18/11).
  Stable 0.1.4 tạo thủ công sau.
- **Head trên GitHub (main):** `4aa959b5` (hướng an toàn cho phím tắt lúc đọc).
- **Build app mới nhất:** #238 (commit `4aa959b5`), CI XANH, pre-release `v0.1.4.238`
  (151 MB, đã verify + mục "What's new"). Link đã gửi Manh 02:20.
- **Nhánh `feature/fastread-speaker-style2` (PR #4, chưa gộp):** head `714d933`,
  CI #237 XANH, artifact `TransPeek-Windows-237-branch`. Link trang run đã gửi Manh 02:27.

## 2. Đang mở

- **PR #4 — Style 2 "Basic Reading" + nút loa ở Read mode:** CI nhánh xanh (#237),
  chờ Manh test bản nhánh rồi merge (có xung đột docs ở `AGENTS.md` + `DECISIONS.md`,
  gỡ lúc sync main vào nhánh). Xem `docs/FEATURES/FAST_READ.md`.
- **Bug che loa/Copy panel:** nguyên nhân thật ĐÃ tìm ra, fix #164 đã vào main (CI xanh)
  — chờ Manh xác nhận hết che trên bản mới. Chi tiết:
  `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §6. (Khi anh xác nhận thì viết lại case study
  ở `AGENTS.md` §3 theo nguyên nhân thật.)
- **Panel focus — click 1 không kích hoạt, click 2 mới kích hoạt; chuột ra ngoài mất
  viền/bóng:** chưa giải thích được, điều tra sau (test bản cũ `v0.1.4.211` không có
  auto-detect). Chi tiết: panel doc §11.7.

## 3. Chờ Manh

- Test bản main `v0.1.4.238` (cỡ icon 24pt + icon alternative + hướng an toàn phím)
  + bản nhánh #237 (Style 2 + loa, đã gỡ bắt Space toàn cục).
- Quyết định merge PR #4 sau khi test nhánh OK.
- Link từ điển — Manh tự sưu tập, gửi 1 thể; chỉ sửa khi Manh nói "bắt đầu fix".
- Lệnh bắt đầu 0.1.4: Velopack → Privacy Policy → SignPath → Store.
- Saved pre-release: chờ panel ổn (hết che loa) + Manh test OK thì save làm baseline
  (tag `0.1.4.<run>-saved`).

## 4. Baseline

- **Panel:** #153 làm chuẩn (xem panel doc §1); bug che loa fix #164 đã vào.
- **Saved pre-release:** `0.1.4.199-saved`, `0.1.4.200-saved`, `0.1.4.201-saved`
  (đều có zip); `0.1.3.185-saved` rỗng, chờ Manh upload zip.

## Liên kết

- [Log thay đổi](CHANGELOG.md)
- [Việc đang làm / chờ](TASK_QUEUE.md)
- [Lộ trình](MASTER_PLAN.md)
- [Panel UX (CHỐT)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
- [Quy tắc làm việc](AGENTS.md)
