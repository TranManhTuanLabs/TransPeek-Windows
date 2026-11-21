# AGENTS.md — TransPeek-Windows

> Quy tắc làm việc cho AI (Muse) trong repo này. Đọc trước khi làm việc.
> Bối cảnh tổng: `AI_CONTEXT.md`. Trạng thái hiện tại: `PROJECT_STATE.md`.
> Bản đồ docs: `STRUCTURE_MAP.md`.

## 1. Commit và đẩy code

- **Đẩy các file liên quan GỘP 1 COMMIT**, không đẩy từng file qua web editor
  (mỗi lần sửa web là 1 commit, repo đi qua trạng thái dở dang, CI đỏ giữa chừng).
- **KHÔNG đẩy commit mới khi run trước còn đang chạy**
  (2 run chồng nhau làm bước Create/Delete pre-release đua nhau xoá bản mới hơn).
- **Commit chỉ sửa docs (`docs/**`, `*.md`) KHÔNG trigger CI** (do `paths-ignore`).
  Muốn chạy lại build phải có thay đổi ở file code.
- **Local phải đồng bộ mới nhất theo main trước khi đẩy** — tránh dùng code cũ.
- **Bản làm việc local có thể thiếu file sau merge qua trình duyệt** (bài học
  2026-11-21: bản main thiếu đúng 4 file của PR #3, bản nhánh này thiếu 2
  file + 6 file stale — merge qua trình duyệt không qua snapshot push nên
  local không có): trước khi làm việc, đối chiếu cây file local với nhánh
  qua API (`git trees ... recursive=1`), khôi phục file thiếu/stale từ raw
  ghim commit rồi mới sửa; kiểm tra hash file sẽ sửa so với nhánh để khỏi
  đẩy code cũ đè code mới.
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
- **Native trước, tự chế sau (Manh 2026-11-19):** khi cần 1 tính năng/control,
  CHECK NATIVE TRƯỚC — native có sẵn và hỗ trợ ngon (đủ dùng, đúng chuẩn hệ
  thống) thì dùng native; chỉ tự chế khi native KHÔNG có hoặc KHÔNG hỗ trợ đủ
  tốt. Tự chế thì **báo Manh 1 câu trước** rồi mới làm (native không có tính
  năng đó, muốn làm phải tự chế, anh có đồng ý không). Ví dụ: dropdown tìm
  kiếm 249 ngôn ngữ — AutoSuggestBox là control native sinh ra để gõ-tìm,
  dùng luôn, không moi TextBox trong template ComboBox ra tự chế.

## 4. Tra tên API / enum / glyph

- **Mở thẳng tài liệu chính chủ Microsoft Learn TRƯỚC khi viết code.**
  Không dùng tên nhớ từ WPF/UWP, không tin trang thứ ba.
- Bài học: con trỏ resize ngang sai 3 lần (`SizeWe` → `SizeEastWest` → đúng
  `SizeWestEast`), CI đỏ #82–#84. Cùng lỗi lặp lại 18/11: `BaselineAlignment`
  trên `Run` là thuộc tính chỉ có ở WPF, WinUI không có — dùng là vỡ build
  #178 (WMC0011); A+ vẽ bằng Grid chứa 2 TextBlock riêng thay vì 2 Run.
- **Glyph mới đã kiểm chứng trên Microsoft Learn 2026-11-20
  (feature/focus-timer-decor):** Clock **E917** (header thẻ Focus & Rest —
  mac dùng "timer", WinUI không có glyph "Timer"), Heart **EB51** (mục
  Focus — mac "heart"), Walk **E805** (mục Rest eyes — mac "figure.walk"),
  Cafe **EC32** (màn countdown Rest window — mac "cup.and.saucer.fill"),
  Ringer **EA8F** (chuông banner — mac "bell.fill").
- **Glyph mới đã kiểm chứng trên Microsoft Learn 2026-11-21
  (feature/fastread-speaker-style2):** Volume **E767** (loa bật tiếng —
  mac "speaker.wave.2.fill"), Mute **E74F** (loa tắt tiếng — mac
  "speaker.slash.fill").
- **Bài học CI #193–#196 (2026-11-19):** `Window` trong WinUI 3 KHÔNG có
  `IsLoaded` — chỉ `FrameworkElement` mới có. **Bài học CI #198:** biến
  vòng lặp không được trùng tên với biến đang sống ở scope ngoài (CS0136).
- **Bài học CI #202 (2026-11-20):** `DispatcherTimer` thuộc namespace
  `Microsoft.UI.Xaml`, KHÔNG phải `Microsoft.UI.Dispatching` (đã xác
  minh trên Microsoft Learn). File nào dùng DispatcherTimer mà chỉ có
  `using Microsoft.UI.Dispatching;` thì build lỗi CS0246.
- **Bài học Space (2026-11-21):** `KeyDown` (bubble) tới parent CHỈ SAU
  KHI nút đang focus đã xử lý Space — muốn chặn Space trước nút thì
  dùng `PreviewKeyDown` (tunnel, đã xác minh trên Microsoft Learn:
  `UIElement` có `PreviewKeyDownEvent`). Dùng cho phím Play/Pause khi
  đang đọc (panel / Rest window / Full window đều đổi sang Preview).
- **Bài học CI #215 (2026-11-21):** `TextBlock` trong WinUI KHÔNG có
  `Background` (chỉ WPF mới có — bẫy WPF→WinUI như `BaselineAlignment`
  ở CI #178); muốn tô nền dòng chữ thì bọc TextBlock trong `Border` và
  tô `Border.Background`. **`DispatcherTimer` KHÔNG có thuộc tính
  `DispatcherQueue`** — muốn marshal event về UI thread thì capture
  `DispatcherQueue.GetForCurrentThread()` ở constructor (view model
  luôn được tạo trên UI thread). 2 lỗi này làm kiểu
  `Style2ReadingView` không biên dịch được nên XAML gọi nó cũng lỗi
  theo (WMC0001 + WMC9999 chỉ là hệ quả).
- **Bài học CI #224 (2026-11-21):** record/model dùng chung giữa Core
  và app PHẢI khai ở top-level namespace, không lồng trong `static
  class` — lồng trong class thì file khác không dùng được tên trần
  (CS0246: `RsvpPhrase` lồng trong `RsvpEngine`, view model không thấy);
  WMC0001 + WMC9999 lại chỉ là hệ quả (mẫu CI #215).
- **Glyph đã kiểm chứng trên Microsoft Learn 2026-11-21
  (feature/fastread-speaker-style2):** ArrowExpand **E9E3** (gạch
  chéo 2 đầu mũi tên — nút Maximize, thay ô vuông E922 trông như
  stop; đã xem ảnh glyph thật trên trang Segoe Fluent Icons).
- **Bài học drag chuột (2026-11-21):** `IsLeftButtonPressed` ĐÚNG
  cả với touch (touch chạm mặt cũng là "primary action") — muốn
  chỉ chuột thì gate bằng `PointerDeviceType.Mouse`
  (`Microsoft.UI.Input.PointerDeviceType`, đã xác minh trên
  Microsoft Learn). Kéo đổi số thì áp live với `save: false`,
  lưu 1 lần lúc thả (mẫu `SetContextSizeRatio`).
- **Bài học Space toàn cục (2026-11-21):** `GetAsyncKeyState` trả
  bit thấp (0x0001) = phím đã bấm từ lần gọi trước — bắt được cú
  bấm nhanh lọt giữa 2 lần poll; bit cao (0x8000) = phím đang giữ.
  Dùng cả 2 (cạnh xuống HOẶC bấm-nhả giữa 2 tick) thì 1 lần bấm
  chỉ kích hoạt 1 lần, giữ phím không lặp lại. Nhớ xả (flush) bit
  thấp khi bắt đầu poll, không thì phím bấm trước lúc start cũng
  kích hoạt 1 lần. `VK_SPACE` = 0x20 (Microsoft Learn, Virtual-Key
  Codes); `GetForegroundWindow()` cho biết cửa sổ nào đang focus —
  poll phải bỏ qua khi chính cửa sổ mình đang focus (đường
  `PreviewKeyDown` đã xử lý Space rồi, làm nữa là bấm 1 lần mà
  toggle 2 lần).

## 5. Quy tắc chung

- **Ngắn, hiểu rõ thì làm luôn; dài, chưa rõ, việc nhiều thì hỏi lại** (Manh 18/11/2026, áp dụng mọi repo): (1) trình khung cho anh duyệt trước khi làm; (2) xong rồi thì tóm tắt những gì đã làm.
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

- Tính năng mới viết riêng thư mục/module, không nhét vào code chính/code
  dịch. Chi tiết + lý do: xem `ARCHITECTURE.md` §5 (nguyên tắc kiến trúc).

## 7. Về Manh

- Manh **không phải dân IT gốc, đang học, làm theo kiểu vibecode**. Giải thích bằng
  tiếng Việt đơn giản, ví dụ từ chính dự án, không dạy dồn dập. Vừa làm vừa dạy dần.
- Trả lời bằng tiếng Việt, ngắn, trực tiếp, cụ thể. Lần đầu nhắc thuật ngữ thì viết đủ,
  giữ nguyên từ kỹ thuật tiếng Anh (hash, certificate, ...), không dịch gượng.

## 8. Chọn phương án triển khai — đọc mã nguồn thật trước (Manh 2026-11-19)

- **Trước khi làm gì, đọc mã nguồn THẬT của dự án rồi mới chọn phương án
  triển khai tốt nhất.** Phương án phải dựa trên code đang chạy trong repo
  (cách code hiện tại viết, control/API nào đang dùng, hành vi thật của nó),
  không dựa trên trí nhớ về "thường thì làm thế này".
- **Không phụ thuộc hoàn toàn vào trí nhớ.** Trí nhớ chỉ là gợi ý.
  **Tra lại nhanh cho chắc** (mở file code, mở tài liệu chính chủ) với mọi
  thứ sẽ đưa vào code; **tra kỹ khi cần** (logic phức tạp, tên API/enum,
  hành vi quyết định chọn phương án nào, thứ đã từng sai).
- Dạng tổng quát của §4 (tra tên API/enum qua Microsoft Learn) và quy tắc
  tra mã/cú pháp từ nguồn chính thống.
- Bài học CI #189 (19/11): chọn ComboBox editable + `TextChanged` để lọc
  live 249 ngôn ngữ — từ trí nhớ kiểu WPF, không tra WinUI trước. WinUI
  ComboBox KHÔNG có `TextChanged` (chỉ có `TextSubmitted` khi Enter/mất
  focus), build đỏ. Tra mã nguồn/API thật trước thì đã chọn AutoSuggestBox
  ngay từ đầu.

## Cái đang ngon, ổn định thì để nguyên, tìm đường khác nối (Manh 21/11/2026)

- **Kinh nghiệm làm:** một phần của hệ thống đang chạy tốt, đã test kỹ thì
  KHÔNG sửa trực tiếp vào nó để thêm tính năng mới — sửa vào rất dễ làm vỡ
  logic đang ngon. Nối đường riêng: làm phần mới tách ra, rồi kích hoạt /
  dùng lại hành vi cũ qua callback có sẵn. Ví dụ (mac): icon giả trong Read
  mode — không sửa logic icon float hiện tại, làm icon giả y hệt, hover/click
  thì kích hoạt trigger vào icon hiện tại.
- Ghi đầy đủ hơn ở `ARCHITECTURE.md` §5 (nguyên tắc kiến trúc).

## Liên kết

- [Bối cảnh AI](AI_CONTEXT.md)
- [Trạng thái dự án](PROJECT_STATE.md)
- [Bản đồ docs](STRUCTURE_MAP.md)
- [Panel UX (CHỐT)](UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
