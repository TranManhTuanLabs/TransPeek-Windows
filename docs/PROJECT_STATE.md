# Project State — TransPeek-Windows

> Trạng thái dự án tại thời điểm ghi. Cập nhật mỗi khi có thay đổi lớn.
> Việc đang làm / chờ: `TASK_QUEUE.md`. Lộ trình: `MASTER_PLAN.md`.

## 1. Tổng quan (17/11/2026 ~05:00)

- **Version:** 0.1.3 (pre-release tự động). Stable sau: 0.1.4 (tạo thủ công).
- **Head trên GitHub:** `ba2b9af` (commit docs: cấu trúc docs + panel UX chốt).
- **Build app mới nhất:** #159 (commit `93bbd8e`), CI XANH, release `v0.1.3.159`
  đã verify 150.4MB. Link đã gửi Manh lúc 04:31.

## 2. Đang mở

- **Bug che loa/Copy panel (#149→#159, vẫn MỞ):** tra 1–5 line bị che loa/Copy ở mép
  dưới card; từ 6 line trở lên không bị. Nguyên nhân thật CHƯA RA (giả thuyết ~8px
  cố định đã bị bác bởi dữ kiện 3-line cũng bị). Đang chờ Manh xác nhận đang test
  bản nào + video/ảnh so sánh 3-line (bị) vs 6-line (không bị). KHÔNG đẩy thêm bản mò.
  Chi tiết: `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` (§6).

## 3. Đã xong gần đây

- **Docs (17/11 ~04:50–05:00):** cấu trúc docs đầy đủ + panel UX chốt size +
  cross-link + docs tree; `AGENTS.md`, `AI_CONTEXT.md`, `PROJECT_STATE.md`.
  Cùng cấu trúc đã gửi sang repo mac (`TranManhTuanLabs/TransPeek`), riêng panel UX
  mac là placeholder vì chưa chốt size.
- **Panel baseline #153** làm chuẩn (420×450, min 400×450, max 600/800, cap 260).
- **Tab TTS Speech** (giọng hệ thống, 0.5x–2x, auto-play) — xong (#135–#140).
- **Tesseract tải thêm ngôn ngữ** (catalog 123) — xong (#141/#142).
- **SAPI: ĐÃ ĐÓNG** — Manh chốt không làm (16/11 23:32).

## 4. Chờ Manh

- Test #159 + gửi bằng chứng 3-line vs 6-line (bug che loa).
- Link từ điển — Manh tự sưu tập, gửi 1 thể; chỉ sửa khi Manh nói "bắt đầu fix".
- Lệnh bắt đầu 0.1.4: Velopack → Privacy Policy → SignPath → Store.
- Saved pre-release: chờ panel ổn + Manh test OK.

## 5. Baseline

- **Panel:** #153 làm chuẩn (xem `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §1).
- **Saved pre-release:** `0.1.3.132-saved` vẫn còn (tiêu đề "(saved preview)").

## Liên kết

- [Việc đang làm / chờ](TASK_QUEUE.md)
- [Lộ trình](MASTER_PLAN.md)
- [Panel UX (CHỐT)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
- [Quy tắc làm việc](AGENTS.md)
