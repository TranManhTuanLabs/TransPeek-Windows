# Research

> Thư mục chứa các báo cáo nghiên cứu (research) làm cơ sở cho quyết định của dự án.
> Mỗi nghiên cứu một thư mục riêng, có `report.md` (+ `notes/` nếu có).
> Kết luận đã chốt từ research thì ghi vào `DECISIONS.md` / doc tính năng liên quan —
> thư mục này giữ bằng chứng, không phải nơi ra quyết định.

## Quy ước

- Một nghiên cứu = một thư mục: `docs/RESEARCH/<ten-nghien-cuu>/report.md`.
- Ghi rõ ngày nghiên cứu, nguồn, và những gì **chưa xác minh**.
- Khi Manh chốt hướng từ research, cập nhật doc liên quan (vd `MASTER_PLAN.md`,
  `FEATURES/SPEECH_ENGINE.MD`) trong cùng đợt, và ghi link tới báo cáo.

## Nghiên cứu đã có

- **`transpeek-windows-packaging-signing/`** (15–16/11/2026) — Đóng gói / ký / Store /
  update / TTS nhanh cho bản Windows. Kết luận: Velopack cho 0.1.4; SignPath Foundation
  $0; Store cá nhân miễn phí; Edge TTS không có API công khai. Xem `MASTER_PLAN.md` §2–§3.
- **`tts-speechify-naturalreader/`** (16/11/2026) — TTS: 5 báo cáo
  (Speechify, NaturalReader, ENVI, RSVP, panel nhiều tab + break) + `recommendation.md`.
  Kết luận: tích hợp native phần lớn; Model B chỉ làm extension riêng; RSVP panel riêng.
  Xem `FEATURES/SPEECH_PLAYER.MD`. (Thư mục `src/` chứa source extension + audio mẫu
  ~92MB KHÔNG đưa vào repo — chỉ đưa 6 báo cáo .md; source gốc còn ở workspace.)
- **`transpeek-panel-sizing/`** (16/11/2026) — Panel macOS: hành vi co dãn
  (input / output / snap-back / khung). Báo cáo gốc viết cho mac, có đối chiếu Windows.
  Cơ sở cho thiết kế panel chung mac + Windows chốt 16/11; bản Windows đã chốt size
  theo `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD`.

## Liên kết

- [Lộ trình](../MASTER_PLAN.md)
- [Bản đồ docs](../STRUCTURE_MAP.md)
- [Quyết định đã chốt](../DECISIONS.md)
