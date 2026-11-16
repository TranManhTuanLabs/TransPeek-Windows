# AGENTS.MD — TransPeek-Windows

> Quy tắc làm việc cho AI (Muse) trong repo này. Đọc trước khi làm việc.
> Bối cảnh tổng: `AI_CONTEXT.MD`. Trạng thái hiện tại: `PROJECT_STATE.MD`.
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
- Số đã chốt trong `UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD` là spec — đổi số cần Manh duyệt,
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
- **Doc workflow phải sync cập nhật mỗi khi thay đổi workflow (Manh 17/11):** sửa `.github/workflows/*` hay cấu hình test/build thì cập nhật `GIT-CI-BUILD-SETUP.md` + `BUILD-TEST-WORKFLOW-GITHUB-CI.md` trong cùng đợt, không dồn sang sau.

## 6. Về Manh

- Manh **không phải dân IT gốc, đang học, làm theo kiểu vibecode**. Giải thích bằng
  tiếng Việt đơn giản, ví dụ từ chính dự án, không dạy dồn dập. Vừa làm vừa dạy dần.
- Trả lời bằng tiếng Việt, ngắn, trực tiếp, cụ thể. Lần đầu nhắc thuật ngữ thì viết đủ,
  giữ nguyên từ kỹ thuật tiếng Anh (hash, certificate, ...), không dịch gượng.

## Liên kết

- [Bối cảnh AI](AI_CONTEXT.MD)
- [Trạng thái dự án](PROJECT_STATE.MD)
- [Bản đồ docs](STRUCTURE_MAP.md)
- [Panel UX (CHỐT)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.MD)
