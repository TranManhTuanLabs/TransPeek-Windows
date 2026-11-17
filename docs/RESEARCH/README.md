# Research

> Thư mục chứa các báo cáo nghiên cứu (research) làm cơ sở cho quyết định của dự án.
> Mỗi nghiên cứu một thư mục riêng, có `report.md` (+ `notes/` nếu có).
> Kết luận đã chốt từ research thì ghi vào `DECISIONS.md` / doc tính năng liên quan —
> thư mục này giữ bằng chứng, không phải nơi ra quyết định.
>
> **Research dùng chung chỉ ở repo mac** (Manh chốt 17/11): repo mac là nhà chính của
> research — [docs/RESEARCH/ trên repo mac](https://github.com/TranManhTuanLabs/TransPeek/tree/main/docs/RESEARCH)
> giữ mọi research dùng chung (TTS, panel-sizing, ...). Thư mục này chỉ giữ research
> **riêng Windows**. Không copy research dùng chung sang 2 repo — bản copy lệch nhau ngay.

## Quy ước

- Một nghiên cứu = một thư mục: `docs/RESEARCH/<ten-nghien-cuu>/report.md`.
- Ghi rõ ngày nghiên cứu, nguồn, và những gì **chưa xác minh**.
- Khi Manh chốt hướng từ research, cập nhật doc liên quan (vd `MASTER_PLAN.md`,
  `FEATURES/SPEECH_ENGINE.md`) trong cùng đợt, và ghi link tới báo cáo.

## Nghiên cứu đã có (riêng Windows)

- **`transpeek-windows-packaging-signing/`** (15–16/11/2026) — Đóng gói / ký / Store /
  update / TTS nhanh cho bản Windows. Kết luận: Velopack cho 0.1.4; SignPath Foundation
  $0; Store cá nhân miễn phí; Edge TTS không có API công khai. Xem `MASTER_PLAN.md` §2–§3.

Research dùng chung (TTS, panel-sizing) đã dọn khỏi repo Windows 17/11 — đọc ở
[docs/RESEARCH/ trên repo mac](https://github.com/TranManhTuanLabs/TransPeek/tree/main/docs/RESEARCH).

## Liên kết

- [Research dùng chung (repo mac)](https://github.com/TranManhTuanLabs/TransPeek/tree/main/docs/RESEARCH)
- [Lộ trình](../MASTER_PLAN.md)
- [Bản đồ docs](../STRUCTURE_MAP.md)
- [Quyết định đã chốt](../DECISIONS.md)
