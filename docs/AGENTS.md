# AGENTS.md — TransPeek-Windows

> Quy tắc làm việc cho AI (Muse) trong repo này. Đọc trước khi làm việc.
> Bối cảnh tổng: `AI_CONTEXT.md`. Trạng thái hiện tại: `PROJECT_STATE.md`.
> Bản đồ docs: `STRUCTURE_MAP.md`.

## 1. Commit và đẩy code

- **Đẩy các file liên quan GỘP 1 COMMIT**, không đẩy từng file qua web editor
  (mỗi lần sửa web là 1 commit, repo đi qua trạng thái dở dang, CI đỏ giữa chừng).
- **KHÔNG đẩy commit `[build-app]` mới khi run trước còn đang chạy**
  (2 run chồng nhau làm bước Create/Delete pre-release đua nhau xoá bản mới hơn).
- **Commit chỉ sửa docs (`docs/**`, `*.md`) KHÔNG trigger CI** (do `paths-ignore`).
  Muốn chạy lại build phải có thay đổi ở file code.
- **Local phải đồng bộ mới nhất theo main trước khi đẩy** — tránh dùng code cũ.
- Sau mỗi lần đẩy, **tự theo dõi run CI mới nhất tới khi kết luận**, rồi báo kết quả.

## 2. Theo dõi CI

- Xem trạng thái xanh/đỏ qua **API GitHub** (poll thưa 5–10 phút/lần, chỉ lúc run đang
  chạy, xong là dừng). **Đọc log CI qua API bị chặn (403) — dùng thẳng trình duyệt.**
- Khi CI bị huỷ do push mới đè lên (concurrency cancel) thì **không cần báo**;
  chỉ báo CI xanh (kèm link tải) hoặc CI đỏ thật.
- **Báo nhanh khi run kết luận**, đừng để Manh phải hỏi "sao build lâu thế".

## 3. Sửa UI — khung trước, sửa sau

- **Trước khi sửa UI: lên khung layout trước, Manh duyệt khung, rồi mới sửa dần theo khung.**
  Không đẩy bản mò từng lỗi vặt bắt Manh test từng bản.
- **Bài học chuỗi #150→#159 (bug che loa):** nâng min output card 150→160→170→180→
  210→218 mà không tìm nguyên nhân gốc — không lần nào hết. Đừng lặp lại:
  **điều tra nguyên nhân trước (cần bằng chứng: ảnh/video đo được), rồi mới sửa.**
- **Chuỗi #149→#161 — kết thúc (17/11):** #160 sửa BÊN TRONG card (loa sang hàng Auto
  riêng) vẫn không hết, vì chỗ bị bóp là Ở PANEL (hàng `*`), nằm trên card — sửa sai
  tầng thì không bao giờ hết. **Nguyên nhân thật do Manh tìm ra bằng quan sát**
  (kéo rộng → hint 1 dòng, hết che; kéo hẹp → hint xuống 2 dòng, che lại): hint
  `TextWrapping="Wrap"` (~103 ký tự) đổi chiều cao theo chiều rộng, còn
  `ComputeMinPanelHeight()` đọc `ActualHeight` của layout lần trước (comment trong code
  còn ghi sai là "kéo thì chiều cao không đổi"). Fix #161: rút ngắn hint còn 51 ký tự,
  vừa 1 dòng ở rộng tối thiểu 400. Bài học: (1) có bằng chứng rồi vẫn phải hỏi
  **sửa đúng tầng chưa**; (2) quan sát hành vi của user (kéo rộng/hẹp) tìm ra nguyên
  nhân mà đọc code không thấy — **hỏi user "nó thay đổi theo cái gì" trước khi đoán**;
  (3) comment trong code cũng có thể sai — tin bằng chứng, không tin comment.
  Chi tiết kỹ thuật đầy đủ ở `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` §6.
- Số đã chốt trong `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md` là spec — đổi số cần Manh duyệt,
  và cập nhật doc trong cùng đợt.
- **Native trước, tự chế sau:** tính năng nào control native không có thì **báo Manh
  1 câu trước** rồi mới làm custom.

## 4. Tra tên API / enum / glyph

- **Mở thẳng tài liệu chính chủ Microsoft Learn TRƯỚC khi viết code.**
  Không dùng tên nhớ từ WPF/UWP, không tin trang thứ ba.
- Bài học: con trỏ resize ngang sai 3 lần (`SizeWe` → `SizeEastWest` → đúng
  `SizeWestEast`), CI đỏ #82–#84.

## 5. Quy tắc chung

- **KHÔNG hardcode** — mọi thứ không nên fix cứng trừ khi thật sự cần; phải có tính
  mở rộng hoặc tự nhận diện.
- GitHub App/connector **không sửa được `.github/workflows/*` (403)**, không xoá được
  file, không tạo được release. Workflow sửa từ Mac rồi push, hoặc qua trình duyệt
  khi Manh cho quyền.
- GitHub web: mặc định giả vờ như **chưa đăng nhập**; chỉ khi Manh nói rõ cho quyền
  thì mới đăng nhập web và thao tác. (Ngoại lệ TransPeek-Windows: được tự đọc log CI,
  sửa code, đẩy commit, re-run tới khi có base chạy được như macOS.)
- **Khi ghi việc nợ phiên, ghi rõ session/chat nào và repo nào** (vd "Windows —
  TransPeek-Windows"), tránh chat khác áp nhầm sang repo sai.
- Manh viết sai chính tả thì **tự correct cho đúng** khi đưa vào app; để ý cả chữ
  hoa/thường. NGOẠI LỆ: slogan "Translate anywhere, Peek instantly!" — chỉ hoa T và P.
- Manh dùng từ kỹ thuật / tiếng Anh sai thì **báo hoặc sửa cho đúng ngay lúc đó**.
- Quyết định sản phẩm phải hỏi Manh trước, trừ khi anh liệt kê phương án và bảo Muse chọn.
- **Doc workflow phải sync cập nhật mỗi khi thay đổi workflow (Manh 17/11):** sửa `.github/workflows/*` hay cấu hình test/build thì cập nhật `TEST-BUILD-WORKFLOW-GITHUB/GIT-CI-BUILD-SETUP.md` + `TEST-BUILD-WORKFLOW-GITHUB/BUILD-TEST-WORKFLOW-GITHUB-CI.md` trong cùng đợt, không dồn sang sau.

## 6. Tách module tính năng (mỗi tính năng 1 thư mục)

- **Tính năng mới (tomato/focus/rest-eyes/Fast Read/RSVP...) viết riêng
  file/module — KHÔNG nhét vào code chính/code dịch.** Lý do (Manh 17/11
  13:08): tránh conflict và tránh làm vỡ panel dịch (đã qua quá nhiều vòng sửa).
- **Cấu trúc song song mac + Windows** (1 thư mục/tính năng, UI + logic nằm
  chung trong đó; không tách quá nhỏ — 1 file cho 1 hàm thì khó tìm):
  `FastRead/` (engine RSVP + cửa sổ đọc + view), `BreakTimer/` (timer
  Pomodoro/focus/20-20-20 + tab, làm sau), `Shortcuts/` (1 view dùng chung
  cho nút "?" nhúng + panel shortcut trong khung multi-tab, không copy-paste
  2 bản — TODO refactor, chưa làm).
- **Khung tab giữ mỏng:** 1 tab = 1 view + 1 tên, không gọi thẳng code dịch.
- **Chỉ chạm code chung ở chỗ nối mỏng:** nút, menubar/tray, Alt+click,
  toggle Settings. Cái dùng chung thật (icon float, hiện/ẩn panel, Settings,
  provider dịch) thì giữ chung, không nhân bản.
- Windows hiện tại: `FastRead/` đã làm trên nhánh `feature/fast-read-rsvp`
  (engine `src/TransPeek.Core/FastRead/` + window `src/TransPeek/FastRead/`);
  chi tiết ở `FEATURES/FAST_READ.md`.

## 7. Về Manh

- Manh **không phải dân IT gốc, đang học, làm theo kiểu vibecode**. Giải thích bằng
  tiếng Việt đơn giản, ví dụ từ chính dự án, không dạy dồn dập. Vừa làm vừa dạy dần.
- Trả lời bằng tiếng Việt, ngắn, trực tiếp, cụ thể. Lần đầu nhắc thuật ngữ thì viết đủ,
  giữ nguyên từ kỹ thuật tiếng Anh (hash, certificate, ...), không dịch gượng.

## Liên kết

- [Bối cảnh AI](AI_CONTEXT.md)
- [Trạng thái dự án](PROJECT_STATE.md)
- [Bản đồ docs](STRUCTURE_MAP.md)
- [Panel UX (CHỐT)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
