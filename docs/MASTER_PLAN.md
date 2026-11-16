# Master Plan (bản nháp)

> Trạng thái: **bản nháp** — lộ trình tổng, cập nhật dần khi Manh chốt từng bước.

## 1. Version

- Hiện tại: **0.1.3** (pre-release tự động mỗi build `[build-app]`).
- **Stable sau: 0.1.4**, tạo thủ công khi đạt baseline (TTS xong).
- Velopack bắt buộc đổi versioning sang SemVer 2 (`0.1.4`).

## 2. Lộ trình 0.1.4 (thứ tự đã chốt)

1. **Velopack + tag SemVer `0.1.4` + CI**, giữ ZIP portable — CHƯA LÀM.
2. **Nút loa giọng hệ thống** — ĐÃ LÀM (tab TTS Speech, #135–#140).
3. **Privacy Policy** — CHƯA LÀM (soạn → Manh duyệt → host → Expander trong About
   → URL điền Partner Center). Bắt buộc vì app gửi text sang Google Translate.
4. **Nộp đơn SignPath Foundation** song song ($0, cert Sectigo OV) — CHƯA LÀM.
5. **Đăng ký Store miễn phí + nộp tay lần đầu + nối job MSIX** — CHƯA LÀM
   (Store live từ 0.1.5 hoặc 0.1.4 nếu kịp; nâng SDK 1.8 → 2.x trước).

## 3. Phát hành: 2 hướng, chung 1 git flow

- **1 codebase, 1 bump, 1 tag → CI ra Velopack Setup.exe (GitHub Release) +
  `.msixupload` cùng release.** Binary tự thích ứng lúc chạy (packaged → Store sở hữu
  update; unpackaged → Velopack). Nộp Store: lần đầu nộp TAY trên Partner Center;
  sau đó tự động qua Store Submission API.
- Ký SignPath chỉ áp cho bản GitHub.

## 4. Việc đang mở (xem chi tiết `TASK_QUEUE.MD`)

- Bug che loa/Copy panel — ĐANG MỞ (#149→#159).
- Doc UI/UX — panel đã chốt, còn lại đang viết dần.
- Link từ điển — chờ Manh gửi 1 thể, chỉ sửa khi Manh nói "bắt đầu fix".
- Icon float theo dark/light — làm sau.

## Liên kết

- [Việc đang làm / chờ](TASK_QUEUE.MD)
- [Trạng thái dự án](PROJECT_STATE.MD)
- [Kiến trúc](ARCHITECTURE.MD)
- [Chỉ mục UX](UI_DESIGN.MD)
