# RSVP Speed Reading — Báo cáo nghiên cứu

**Ngày:** 16/11/2026 · **Phạm vi:** mẫu thứ tư (RSVP) trong đợt nghiên cứu TTS/Quick panel cho TransPeek
**Ngôn ngữ báo cáo:** Tiếng Việt · **Mục đích:** chỉ nghiên cứu, chưa triển khai

## Tóm tắt nhanh (đọc 1 phút)

| Câu hỏi | Kết luận |
|---|---|
| RSVP có build được native vào TransPeek không? | **CÓ — tích hợp được (Path 1).** Engine RSVP là logic thuần túy, nhỏ (vài trăm dòng), render bằng Text native. Không cần browser extension cho luồng chính. |
| Nền tảng | macOS: khả thi tốt · Windows: khả thi tốt (xem §6). |
| Luồng 1 icon 3 chức năng | Giữ mặc định hover = dịch, click = nghe (TTS). RSVP vào theo **thứ tự Manh CHỐT 17/11: (1)** Option-click Mac / Alt-click Windows → RSVP thẳng, **(2)** menu chuột phải trên icon float ("Đọc nhanh"), **(3)** nút "Đọc nhanh" trong panel dịch, **(4)** mục menubar/tray, **(5)** 1 phím tắt global duy nhất chỉ là fallback. Thêm Settings toggle để đổi click → RSVP cho người dùng RSVP nhiều. **Không dùng middle-click** (trackpad/laptop không có), **không long-press** (đụng thao tác kéo icon). |
| View đọc: tab hay panel riêng? | **GIỮ CẢ 2 PHƯƠNG ÁN, quyết ở lúc prototype (Manh 17/11).** Báo cáo khuyên **panel riêng** (cửa sổ riêng, layout riêng; tab 2/3/4 quá chật cho chữ RSVP lớn, dễ chuyển tab nhầm khi đang đọc). Phương án tab chỉ được nếu: panel dãn to mà đọc vẫn tập trung, không giật, không bị auto-hide phá — và khi đang đọc phải **ẩn HOÀN TOÀN chức năng panel dịch** (không chỉ ẩn giao diện mà vẫn bấm được), ẩn chrome dịch + hàng chấm, tắt swipe/lăn chuyển tab, tạm dừng auto-hide. |
| Phím tắt | Chỉ **1 phím tắt global** (mở RSVP từ selection/clipboard). Mọi phím điều khiển khi đọc (Space, ←/→, ↑/↓…) chỉ có tác dụng khi panel đọc đang focus — không chiếm phím tắt global. |
| Mobile (iOS/Android) | **Chưa làm.** Thị trường RSVP mobile đã đông và nặng subscription; TransPeek nên chứng minh RSVP trên desktop trước, mobile chỉ là phase sau (nếu có). |

## 0. RSVP là gì (nền tảng chung)

**RSVP = Rapid Serial Visual Presentation**: hiển thị **một từ (hoặc một cụm vài từ) tại một điểm cố định duy nhất** trên màn hình, thay từ theo tốc độ đặt trước (WPM — words per minute). Mắt không phải di chuyển dọc dòng, không phải quay lại đầu dòng mới — chỉ đứng yên một chỗ.

- **Mục tiêu:** loại bỏ *subvocalization* (thói quen "đọc thầm" từng từ trong đầu) — đây là yếu tố giới hạn tốc độ đọc. Khi từ hiện nhanh hơn tốc độ đọc thầm (~200–250 WPM ở người trưởng thành), não buộc phải nhận diện từ bằng mắt thay vì "nói" trong đầu.
- **Tốc độ thực tế:** người mới 300–400 WPM vẫn hiểu tốt; 400–600 WPM là vùng thoải mái phổ biến sau luyện tập; trên ~600–1000 WPM độ hiểu giảm rõ, đặc biệt với văn bản phức tạp. Các app quảng cáo "1800+ WPM" là mức luyện tập, không phải mức hiểu đầy đủ — nên xem với thái độ hoài nghi.
- **ORP (Optimal Recognition Point):** điểm nhận diện tối ưu — một chữ cái trong mỗi từ (thường là chữ thứ 2–4 tùy độ dài từ) được tô màu khác và luôn đặt đúng vào cùng một vị trí cố định trên màn hình. Mắt nhìn đúng vào ORP thì nhận diện từ nhanh nhất. Đây là ý tưởng của Spritz (nổi tiếng với "chữ đỏ") và được cả 3 mẫu dưới đây dùng theo cách riêng.
- **Các kỹ thuật đi kèm:** tạm dừng sau dấu câu (phẩy/câu/đoạn), từ dài hiện lâu hơn, tách từ dài, hiển thị ngữ cảnh (câu đang đọc) khi pause, thanh tiến trình + tua (seek).

---

## 1. SwiftRead — extension thương mại, đa chế độ (đã đọc source 6.11.2 + 5.9.2 từ 17/11)

**Extension:** SwiftRead — "read faster, learn more" · ID `ipikiaejjblmdopojhpejjmbedhlibno` · **v6.11.2+9023721d1, cập nhật 09/11/2026, 12,44 MiB** · Chrome + Edge.
**Trang Pro:** https://swiftread.com/pro · **Web app:** app.swiftread.com · **Có app iOS + Android.**

> ✅ **BỔ SUNG 17/11/2026 — ĐÃ ĐỌC SOURCE THẬT:** Manh gửi 2 file `.crx` (6.11.2.0 + 5.9.2.0, `~/workspace/user/files/`), đã giải nén và đọc source. Phần SwiftRead dưới đây đã được cập nhật theo source thật; những gì **không có trong `.crx`** (3 chế độ Focus/Flow/Spotlight, danh sách giọng, trích xuất toàn trang luồng mới) được ghi rõ là **nằm ở web app/server, không kiểm chứng được từ source**. Giới hạn mới: các bundle mới (`background/index.js`, `swiftread-menu/`, `page-buttons/`) đã minified nên phát hiện ở đó dựa trên tên định danh/chuỗi/URL; engine legacy (`legacy/app.js`, `listen.js`, `settings_store.js`, `focus_anchor.js`) là source readable nên các con số timing/ORP/settings là chắc chắn. (Xem thêm §13.)

### 1.1. Ba chế độ đọc (không chỉ RSVP thuần)

SwiftRead hiện tại không còn là RSVP một chế độ mà có **3 focus mode** — đây là điểm khác biệt lớn nhất so với jetzt/Sprint Reader:

| Chế độ | Cách hoạt động | Khi nào dùng |
|---|---|---|
| **Focus** | Hiện vài từ một lúc tại một điểm cố định (RSVP cổ điển) — mắt dừng di chuyển, tăng tốc được cao nhất | Đọc nhanh, luyện tốc độ |
| **Flow** | Giữ nguyên trang đầy đủ, một vệt highlight di chuyển qua text theo nhịp — vẫn thấy ngữ cảnh xung quanh | Đọc thật (hiểu sâu), không mất ngữ cảnh |
| **Spotlight** | Ẩn/mờ text xung quanh, chỉ nổi bật cụm từ đang đọc | Tập trung, giảm xao nhãng |

Người dùng đổi mode bất cứ lúc nào; font, theme, spacing, tốc độ đều chỉnh được. Đây là **bài học quan trọng cho TransPeek**: RSVP thuần (Focus) tuy nhanh nhưng mất ngữ cảnh — nhiều người dùng thật ra thích Flow/Spotlight hơn vì vẫn thấy câu/đoạn xung quanh. Nếu TransPeek làm RSVP, nên cân nhắc **thêm chế độ highlight chạy trên text đầy đủ** (như Flow) làm mode thứ hai, thay vì chỉ có RSVP một từ.

> ⚠️ **Caveat từ source thật (17/11):** 3 chế độ Focus/Flow/Spotlight **không tồn tại trong source của cả 2 bản `.crx`** (grep `spotlight`/`focusMode`/`readingMode` = 0 hit; chỉ có `highlightMode` là boolean bật/tắt màu highlight). Trong extension chỉ có **1 engine RSVP + các toggle hiển thị** (`contextDisplayStyle`: always/on-pause/never, `textScrollerEnabled`, context ±150 từ). Nếu 3 mode có thật thì chúng nằm ở **web app host `app.swiftread.com`** — không ship trong `.crx`, không kiểm chứng được. Vì vậy "thêm Flow/Spotlight" nên hiểu là **toggle hiển thị trên cùng engine**, không cần tách engine riêng.

### 1.2. TTS đi kèm (đọc + nghe đồng bộ)

- **TTS giọng tự nhiên, 90+ ngôn ngữ**, highlight từng từ đồng bộ với giọng đọc — ở cả 3 chế độ.
- Mô hình giá: **mọi tính năng đều có trong bản free, nhưng TTS và "premium pages" có hạn mức theo ngày**; SwiftRead PRO là subscription để bỏ hạn mức. (Trang `/pro` hiện tại chỉ là trang đăng nhập/tạo tài khoản + nhập key — không liệt kê chi tiết tính năng, nên chi tiết giá lấy từ listing Web Store.)

> 🔍 **Sửa theo source thật (17/11):** TTS **không phải Web Speech** (`speechSynthesis` = 0 hit) mà là **giọng cloud**: server tổng hợp audio + trả **timepoints từng từ**, extension phát bằng `HTMLAudioElement` và đồng bộ highlight bằng cách **poll `audio.currentTime` mỗi 50 ms** so với timepoints (`periodicCurrentTimeCheck`, `listen.js`). Danh sách giọng **fetch live từ server** (mỗi giọng có `id/languageCode/sampleURL`) nên con số "90+" **không kiểm chứng được từ source**. **Listen chỉ chạy khi `chunkSize == 1`**. Về giá: trong extension, TTS/PDF/ePub legacy bị **khóa nhị phân theo license PRO** (không license → tắt; server trả 403 → bắt upgrade) — **không có code/chuỗi hạn mức ngày nào**; chuỗi hạn mức duy nhất thấy được là `"Daily free PDF limit reached"` ở luồng save-PDF (server-side). Audio WPM mặc định **200** (khác WPM đọc visual 275), chỉnh bằng `playbackRate` với bước biến thiên tối đa 15%.
- Ghi chú cho TransPeek: SwiftRead chứng minh **RSVP + TTS là cặp đi chung tự nhiên** (nghe + nhìn cùng lúc giúp ghi nhớ) — đúng hướng "1 icon, hover = dịch, click = nghe" của TransPeek, chỉ thêm RSVP làm chức năng thứ ba.

### 1.3. Cách lấy text (text capture)

- Bấm icon toolbar (hoặc phím tắt tự đặt) → **"SwiftRead current page"** — mở trang hiện tại vào reader.
- **Chuột phải → "SwiftRead selected text"** (chỉ đọc phần đã bôi).
- **PDF:** đọc trực tiếp trong trình duyệt; miễn phí nếu mọi trang đều có text selectable và không lưu vào Library; trang scan hoặc lưu vào Library thì tốn "premium pages" (tính năng PRO).

> 🔍 **Sửa theo source thật (17/11):** legacy PDF reader **gated PRO toàn bộ** (không PRO → chuyển sang trang trả phí); trang scan = báo "không có selectable text", **không có OCR**; không có logic tính premium theo số trang trong extension (nếu có thì ở server). Luồng mới: extension chỉ stage blob PDF rồi mở web app host để lưu/đọc. ePub legacy cũng gated PRO toàn bộ. Kindle/Libby: **không thấy chuỗi PRO/premium nào** trong extension (đi chung cơ chế `external-reader-interactor`).
- **ePub, sách trên Kindle Cloud Reader, Libby** (tốn premium pages) — tức là SwiftRead tự nhúng vào các web reader sẵn có, không cần file.
- **Local files** (mở file local trong trình duyệt).
- Copy-paste text vào SwiftRead (bản cũ).
- **Đồng bộ đa thiết bị:** cùng một tài khoản dùng ở web app, extension và app iOS/Android — Library + tiến độ đọc đi theo tài khoản.

### 1.4. Panel đọc

- **Reader riêng (dedicated reader view)**, không phải overlay nhỏ trên trang: text được mở sang một view đọc riêng, chọn mode + tốc độ hoặc bật TTS.

> 🔍 **Tinh chỉnh theo source thật (17/11):** reader mặc định là **popup window của web app host** (`app.swiftread.com/extension/reader`, `chrome.windows.create` type `popup`, tái dùng qua `swiftread.readerWindowId`) — không phải overlay trong trang cũng không phải popup của extension. Reader legacy trong-extension (`legacy/app.html`) chỉ mở cho **user grandfathered** (gating theo `chrome.storage.sync`, cài mới từ 6.0.0 mặc định experience mới).
- Có nút trên trang ("on-page button") ở một số trang.
- Cài đặt: fonts, themes, spacing, reading speed — lưu trong trình duyệt.
- Chỉ chạy khi người dùng kích hoạt (click icon / phím tắt / menu chuột phải) — không tự chạy trên mọi trang.

### 1.5. Đánh giá riêng cho TransPeek

- **Điểm đáng học nhất:** (a) 3 chế độ Focus/Flow/Spotlight — RSVP thuần chỉ là 1 trong 3 *(caveat 17/11: 3 mode không có trong source `.crx`, nằm ở web app — trong extension chỉ có 1 engine + toggle hiển thị)*; (b) đồng bộ TTS + highlight (timepoints server + poll 50 ms, chunk = 1); (c) micro-pause hệ số nhân + context ±150 từ khi pause — 2 pattern rẻ mà hiệu quả, nên có ở v1 TransPeek. *(Sửa 17/11: "hạn mức ngày cho TTS" không có trong source — TTS legacy khóa theo license PRO.)*
- **Điểm không áp dụng:** đồng bộ tài khoản đa thiết bị (TransPeek là app desktop offline-first, không có tài khoản); đọc Kindle/Libby (chỉ làm được trong trình duyệt, native app không nhúng được web reader của Amazon/OverDrive).
- **Giấy phép:** SwiftRead là phần mềm thương mại, **closed-source** — chỉ học pattern (3 mode, flow vào reader riêng), không copy code hay UI y hệt.

---

## 2. jetzt — RSVP tối giản, mã nguồn mở (Apache 2.0)

**Repo:** https://github.com/ds300/jetzt · **Demo:** https://ds300.github.io/jetzt/demo.html · **v1.2.0, Manifest V2** (cũ, dùng `browser_action` + background page không persistent) · **Giấy phép Apache 2.0** (được tái dùng kèm ghi nhận bản quyền).
**Source đã đọc trực tiếp:** `src/jetzt/` — `modules/{preamble,helpers,config,themes,parse,exec,view,select,control,init}.js`, `background.js`, `chrome-config.js`, `jetzt.css`, `options/` (AngularJS), `demo.html`.

### 2.1. Cách vào (entry points)

| Cách vào | Chi tiết trong code |
|---|---|
| **Alt+S rồi click vào block text** | `control.js`: `Alt+S` khi reader chưa mở → `jetzt.select()` → chế độ chọn (select mode) |
| **Bôi đen text trước, rồi Alt+S** | `select.js`: nếu có selection thì đọc thẳng text đó |
| **Bấm icon toolbar** | `background.js`: `browserAction.onClicked` → chạy `jetzt.select()` |
| **Chuột phải → "Speed-read this with Jetzt"** | `background.js`: context menu, chỉ hiện khi có selection; dùng `data.selectionText` (vì `window.getSelection()` không chạy được trên PDF) |
| Bookmarklet | Có `bookmarklet.js` — chạy được cả khi không cài extension |

**Chế độ chọn block (select mode) — rất đáng học:** khi bật, di chuột qua trang thì block đang hover được **tô overlay đỏ mờ** (`view.addOverlay`, class `sr-overlay`, opacity 0.2) và con trỏ thành pointer; click thì đọc block đó. Mặc định nó chọn **cả nhóm anh em** (các `<p>`, `<h1-6>`, `<span>`… cùng parent `<div>/<article>/<section>/<ul>…`); giữ **Alt thì chỉ chọn đúng element đang hover**. Esc hủy. Đây là cách lấy "cả bài viết" nhanh mà không cần trích xuất article — đơn giản, không cần Readability.

### 2.2. Cơ chế RSVP trong code

**Parser (`modules/parse.js` — `Instructionator`):** text được biên dịch thành danh sách "instruction" — mỗi instruction = 1 token + 1 modifier + decorator + wrap trái/phải:

- **Modifier (hệ số nhân thời gian):** `normal` ×1 · `start_clause` ×1 · `end_clause` ×1,8 · `start_sentence` ×1,3 · `end_sentence` ×2,2 · `start_paragraph` ×2,0 · `end_paragraph` ×2,8 · `short_space` ×1,5 · `long_space` ×2,2. Tức là sau dấu chấm nghỉ gấp 2,2 lần, cuối đoạn nghỉ gấp 2,8 lần — **pause on punctuation được làm bằng hệ số nhân, không phải delay cố định**.
- **Từ dài:** dài > 13 ký tự (hoặc > 9 ký tự mà có gạch nối) thì **tách thành nhiều đoạn ≤ 8 ký tự**, đoạn giữa gắn dấu `-` (decorator) — ví dụ "antidisestablishmentarianism" thành "antidise-" + "stablish-" + "mentaria-" + "nism". Tách theo dấu gạch nối trước, nếu không có thì cắt đều.
- **Wrap (ngữ cảnh quanh từ):** dấu ngoặc kép `“”`/`«»`, ngoặc đơn `()`, tiêu đề (`#`), trích dẫn (`›`) được giữ lại hiển thị ở 2 bên từ — người đọc vẫn thấy mình đang ở trong ngoặc kép hay tiêu đề.
- **Trích dẫn trong văn bản:** tùy chọn "strip in-text citations" — regex loại `(Tác giả, 2020)` kiểu APA hay `[12]` kiểu IEEE khỏi luồng đọc.
- **Parse theo DOM:** duyệt cây DOM, `<h1-6>`/`<p>`/`<ul>`/`<ol>`/`<blockquote>`… được đánh dấu start/end paragraph; `<script>` và comment bị bỏ qua; node chỉ chứa inline thì lấy `textContent` thẳng.

**Executor (`modules/exec.js`):** vòng lặp `setTimeout` đệ quy:

- Thời gian cơ bản mỗi từ = `60000 / WPM` ms. **Từ dài hiện lâu hơn:** 6–7 ký tự ×1,2 · 8–9 ×1,4 · 10–11 ×1,8 · 12–13 ×2 (áp dụng khi modifier là `normal`; nếu đã có modifier câu/đoạn thì dùng modifier).
- **WPM mặc định 400, chỉnh được 100–1500** (`config.adjustWPM`, clamp 100–1500).
- Điều hướng: nhảy tới/lùi câu, tới/lùi đoạn (tìm instruction có modifier `start_sentence`/`start_paragraph` gần nhất; lùi câu: nếu đang ở < 5 từ đầu câu thì về câu trước).
- Thông báo: mỗi 5 từ cập nhật "Ns left" (ước lượng thời gian còn lại từ thời gian đã trôi); đọc xong hiện "N words in Ns".

**Không có chunk size** — jetzt luôn hiện **1 từ/lần**, không có chế độ nhiều từ/slide (khác Sprint Reader).

### 2.3. Điểm cố định / ORP (focal point)

`modules/view.js` — `calculatePivot(word)`:

| Độ dài từ | Vị trí pivot (0-based) |
|---|---|
| 1 ký tự | 0 |
| 2–5 | 1 |
| 6–9 | 2 |
| 10–13 | 3 |
| ≥ 14 | 4 |

Từ được tách thành 3 span: `leftWord` + `pivotChar` (tô màu khác) + `rightWord`; cả cụm từ được **dịch ngang sao cho pivotChar nằm đúng giữa reticle** (đo bằng `offsetLeft`/`offsetWidth` thật sau khi render). Pivot màu đỏ `#E01000` (theme sáng) / xanh dương `#73b5ee` (theme tối).

**Reticle (dấu ngắm):** 2 vạch ngắn trên/dưới (cao 20px mỗi vạch, rộng 10px) tại vị trí cố định `left: 110px` trong khung từ — mắt luôn nhìn vào đúng điểm đó.

**Ngữ cảnh 2 bên:** `sr-wrap` trái/phải hiển thị từ trước/sau (mờ hơn, nền xám), giúp người đọc không bị "rớt" khỏi câu.

### 2.4. Panel đọc (view)

- **Full-screen overlay:** backdrop đen mờ toàn màn hình (`position: fixed`, opacity 0,86 ở theme tối / 0,07 ở theme sáng, transition 0,34s) + reader trượt từ trên xuống giữa màn hình (`top: -50%` → `50%`).
- **Kích thước:** khung từ rộng 400px, cao 100px, font từ 50px, bo góc 10px, đổ bóng; font mặc định monospace (`Menlo, Monaco, Consolas, monospace`) — monospace giúp độ rộng từ ổn định, pivot ít nhảy.
- **Thanh tiến trình:** dải 3px dưới đáy khung từ, độ dài = `percent × 4px` (border-left trick).
- **Hiển thị phụ:** WPM hiện tại (góc dưới phải khung), message "Ns left" / "N words in Ns" (góc trên phải, bật/tắt bằng `/` hoặc `?`).
- **Đóng:** click vào backdrop, hoặc Esc. Khi mở, reader tự giật focus (tabIndex + `onblur` refocus + `window.onfocus` refocus sau 100ms) để nhận phím tắt — đồng thời chặn scroll trang (lưu `scrollTop` rồi khôi phục).
- **Theme:** sáng/tối, chỉnh được từng màu (backdrop, nền, chữ, pivot, reticle, progress bar, wrap) + opacity backdrop; có theme editor + chia sẻ theme bằng JSON (paste theme).

### 2.5. Điều khiển & phím tắt (chỉ có tác dụng khi reader đang mở)

| Phím | Tác dụng |
|---|---|
| `Space` | Play / Pause |
| `↑` / `↓` | WPM +10 / −10 |
| `←` / `→` | Lùi / tới 1 câu (`Alt+←/→`: 1 đoạn) |
| `+` / `−` | Phóng to / thu nhỏ reader (scale 0–1, bước 0,1) |
| `0` | Đổi theme sáng/tối |
| `/` hoặc `?` | Ẩn/hiện message thống kê |
| `Esc` | Đóng reader |
| `Alt+S` | Mở chế độ chọn block |

Không có nút bấm trên giao diện — **mọi điều khiển đều bằng phím** (khi reader mở, `box.onkeyup/onkeypress` chặn event lan ra trang). Đây là thiết kế tối giản cực đoan: nhanh cho người quen, nhưng người mới không biết phím nào (phải mở trang Options → tab Keyboard để xem).

### 2.6. Đánh giá riêng cho TransPeek

- **Điểm đáng học nhất:** (a) parser `Instructionator` với modifier hệ số nhân — cách làm pause sau dấu câu **đơn giản mà hiệu quả**, dễ port sang Swift/C# (vài trăm dòng); (b) pivot/ORP bằng đo `offsetWidth` thật sau render — công thức pivot theo độ dài từ (0/1/2/3/4) dùng được ngay; (c) select mode với overlay đỏ + chọn cả nhóm anh em — cách lấy "cả bài" hay, rẻ hơn trích xuất article; (d) tách từ dài > 13 ký tự.
- **Điểm không nên học:** không có chunk size, không có nút bấm nào (chỉ phím tắt), không có seek/progress click được, không lưu vị trí đọc, Manifest V2 đã cũ.
- **Giấy phép:** Apache 2.0 — được tái dùng code (kèm giữ lại notice bản quyền). Parser và executor là phần tái dùng giá trị nhất.

---

## 3. Sprint Reader — RSVP đầy đủ tính năng nhất, mã nguồn mở (BSD)

**Repo:** https://github.com/anthonynosek/sprint-reader-chrome · **v3.0.3, Manifest V3** · **Giấy phép kiểu BSD 3-điều khoản** (tái dùng kèm giữ notice bản quyền; không dùng tên Sprint Reader/Anthony Nosek để quảng bá sản phẩm phái sinh nếu không có phép).
**Source đã đọc trực tiếp:** `src/sprint-reader/` — `src/{engine,reader,content,popup,background,background-worker,advanced,utility,facts}.js`, `reader.html`, `popup.html`, `manifest.json`, `lib/guess_language/`.

### 3.1. Cách vào (entry points)

| Cách vào | Chi tiết trong code |
|---|---|
| **Bôi đen → chuột phải → "Sprint read selected text"** | `background.js`: context menu cho `selection` và `page` ("Sprint read last saved selection") |
| **Phím tắt global `Ctrl+Shift+E`** (Mac: `Cmd+Shift+E`) | `manifest.json` → `commands.sprint_read_shortcut`; nếu chưa có selection thì hỏi content script lấy tọa độ chuột |
| **Bấm icon toolbar → popup → "Open reader"** | `popup.js` gửi message `openReaderFromPopup` |
| **Chế độ auto-select (tắt mặc định)** | `content.js`: bật trong Advanced (`madvHotkeySelectionEnabled`); `Ctrl+Shift` bật/tắt chế độ, di chuột thì element được **viền outline**, bấm **`E`** thì đọc text của element đang hover — tương tự select mode của jetzt nhưng kích hoạt bằng phím |
| **Nạp từ clipboard** | Nút/menu "Load clipboard content (C)" — `navigator.clipboard.readText()` (quyền `clipboardRead`) |

**Content script** (`content.js`) lắng nghe `mouseup` trên mọi trang, gửi selection về background mỗi lần bôi đen — nên khi mở reader thì text mới nhất luôn sẵn. Kèm phát hiện RTL bằng cách đo vị trí `getBoundingClientRect` của 2 span giống nhau (nếu span sau nằm bên trái span trước thì là RTL).

### 3.2. Cơ chế RSVP trong code (engine.js — phần "não" của extension)

**Ba thuật toán hiển thị (chọn trong Advanced):**

| # | Thuật toán | Cách tính thời gian mỗi slide |
|---|---|---|
| 0 | **Basic** (mặc định) | Mọi slide bằng nhau: `(60000 / WPM) × số từ trong slide`, tối thiểu `madvBasicMinimumSlideDuration` (mặc định 0ms) |
| 1 | **Word Length** | Thời gian ∝ độ dài từ: tổng thời gian chia đều theo tổng số ký tự → từ dài hiện lâu hơn |
| 2 | **Word Frequency** | Thời gian ∝ **lượng thông tin Shannon** của từ: `-log2(P(từ))` theo tần suất từ trong kho SUBTLEX-US (phụ đề phim, file `data/wordfrequency-en-US.js`). Từ quen thuộc (xác suất cao) hiện nhanh 40ms, từ hiếm hiện tới 300ms; **WPM không còn tác dụng ở thuật toán này**. Chỉ hỗ trợ tiếng Anh (en-US) và chunk size 1 |

**Tách text thành slide (2 pass):**

- *Pass 1:* chèn khoảng trắng sau dấu câu nếu thiếu (`"word.Word"` → `"word. Word"`); tách theo khoảng trắng; xử lý **từ có gạch nối** (tách hoặc nối lại nếu là từ bị ngắt dòng); **từ dài** (mặc định > 17 ký tự) thì tách bằng hyphenation theo pattern ngôn ngữ (thư viện Hypher, tách ở điểm ngắt âm tiết, tối thiểu 6 ký tự/slide); **từ viết tắt** (`U.S.A.`) nối lại bằng đệ quy; **số thập phân** (`1.20`) nối lại, không tách ở dấu chấm.
- *Pass 2:* bỏ slide rỗng.
- **Chunk size:** gộp N từ thành 1 slide (mặc định 1); chunk tự ngắt sớm khi gặp dấu phẩy/chấm/đoạn (nếu bật pause) để câu không bị cắt dở.
- **Pause sau dấu câu (delay cố định, khác jetzt):** phẩy `,`/`;`/`:` → 250ms · chấm `.`/`?`/`!` → 450ms · đoạn → 700ms (mặc định, chỉnh được từng cái). Ngoài ra có **word flicker**: làm trống màn hình trong X% cuối mỗi slide (mặc định tắt, 10%) — một số người thấy "nhấp nháy" giúp tách từ rõ hơn.
- Mỗi slide lưu đủ metadata: text, text gốc, pre/post delay, duration, WPM tính được, số từ, vị trí ORP, pixel offset tới ORP, số slide, có phải "con" của slide trước (từ dài bị tách) hay không.

**Vòng lặp phát (playSlideShow):** đệ quy `setTimeout` 3 pha mỗi slide — **pre-delay → hiện từ → post-delay**; khi pause thì hiện câu ngữ cảnh (xem §3.4). Hết text thì nút Play thành **Restart**. Có đếm ngược auto-start (3-2-1, mặc định 2 giây nếu bật), tùy chọn tự đóng cửa sổ khi đọc xong.

### 3.3. Điểm cố định / ORP

- **Vị trí ORP theo độ dài từ:** 1 ký tự → 1 · 2–4 → 2 · 5–9 → 3 · ≥ 10 → 4 (hàm `assignOptimalLetterPosition`, 1-based).
- **Căn pixel chính xác:** đo bằng `<canvas>` `measureText` với đúng font/size đang dùng — tính khoảng cách từ mép trái từ tới **giữa chữ ORP**, rồi đặt `padding-left` cho container sao cho ORP luôn rơi vào cùng một tọa độ X (mặc định 30% chiều rộng cửa sổ, `madvOptimisedPositionLeftMarginPercent = 30`).
- **Tô màu chữ ORP:** mặc định cam `#FF8C00`, chọn màu bằng color picker; chỉ áp dụng khi chunk size = 1 (dùng jQuery lettering tách từng chữ thành span).
- **4 kiểu đặt text** (`textPosition`): 0 = căn giữa cửa sổ · 1 = căn trái · 2 = optimal positioning (mặc định) · 3 = optimal + **static focal** (chèn ký tự `⋅` U+22C5 ngay tại vị trí ORP, ký tự này vẫn hiện cả lúc slide trống giữa các từ — mắt luôn có điểm bám).
- **Focal guide:** 2 vạch dọc ngắn trên/dưới từ tại đúng tọa độ X của ORP (màu = màu highlight), ẩn khi chunk > 1 hoặc khi tắt trong Advanced.

### 3.4. Panel đọc (reader.html — cửa sổ riêng, 6 tab)

**Cửa sổ riêng** (`chrome.windows.create`, type `popup`): mặc định **70% chiều rộng × 53% chiều cao màn hình**, tối thiểu 880×550, vị trí góc dưới-phải; kích thước được lưu lại khi resize; chỉ có **1 cửa sổ reader** — mở lại thì focus cửa sổ cũ + refresh text mới.

| Tab | Nội dung |
|---|---|
| **Home** | Vùng đọc chính: từ hiện ở giữa (font mặc định 110px, Arial), focal guide, thanh tiến trình click được để seek, thời gian còn lại, status line (ngôn ngữ, WPM, chunk size, LTR/RTL), câu ngữ cảnh khi pause |
| **Content** | Toàn bộ text đã chọn (để đối chiếu / đọc lại theo cách thường) |
| **Statistics** | Tổng số từ/slide, thời gian slide min/max/trung bình, tổng thời gian (có/không tính pause), WPM thực tế, chunk size + box "Did you know?" (fact xoay vòng, `facts.js`) |
| **Settings** | Font, cỡ chữ, WPM, chunk size, color scheme (17 scheme: Modern Blue mặc định, Black, Charcoal, High Contrast…), auto-start + số giây đếm ngược, tự đóng khi xong, RTL, hiện thời gian còn lại |
| **Advanced** | Thuật toán (3 loại), pause sau phẩy/chấm/đoạn (bật/tắt + ms), word flicker, vị trí text (4 kiểu), highlight chữ ORP + màu, nút "More..." mở trang cài đặt nâng cao (~30 tùy chọn `madv*`) |
| **Shortcuts** | Bảng phím tắt + hướng dẫn 2 cách dùng |

**Câu ngữ cảnh khi pause (`displaySentence`):** khi dừng, dưới vùng từ hiện câu đang đọc (khoảng 20 từ trước + 20 từ sau), **từ đang dừng được tô đậm + tô màu** — giúp người đọc biết mình đang ở đâu trong câu, đọc lại ngữ cảnh rồi mới tiếp tục. Có tùy chọn tự ẩn sau N giây, viền trên, vị trí lệch %. Đây là tính năng **nên có** trong bản TransPeek (rẻ, hiệu quả cao cho độ hiểu).

**Lưu vị trí đọc:** khi pause, vị trí slide được lưu kèm text (`selectedText + delimiter + wordIndex`); mở lại text cũ thì tiếp tục từ chỗ đã dừng. Lịch sử giữ **3 text gần nhất** (mới nhất + 2 history) — chọn trong menu Play.

**Tooltip trạng thái:** hover vào status line hiện tooltip (qTip): "Slide N of M, Word: …, Timing: Pre Xms – Word Yms – Post Zms".

### 3.5. Điều khiển & phím tắt

**Trong cửa sổ reader:**

| Phím / thao tác | Tác dụng |
|---|---|
| `Space` | Play / Pause |
| `A` / `S` | Lùi / tới 1 slide |
| `K` / `L` | Lùi / tới nhiều slide (mặc định 10 slide) |
| `Z` / `X` | WPM −50 / +50 (bước chỉnh được) |
| `R` | Reset về đầu |
| `C` | Nạp nội dung clipboard |
| `V` | Nạp selection hiện tại |
| `Q` | Đóng cửa sổ reader |
| Click thanh tiến trình | Seek tới % tương ứng |
| Nút Play + menu dropdown | Play/Pause, Reset, Step ±, Load selection/history/clipboard |

**Global (duy nhất 1 phím):** `Ctrl+Shift+E` (Mac `Cmd+Shift+E`) mở reader từ selection hiện tại.

**Ngôn ngữ:** tự động phát hiện bằng thư viện `guess_language` (kèm data pattern nhiều ngôn ngữ); tự nhận RTL (Arabic/Hebrew…) + đảo ngược số trong slide RTL để hiển thị đúng; tiếng Anh luôn ép LTR.

### 3.6. Đánh giá riêng cho TransPeek

- **Điểm đáng học nhất:** (a) 3 thuật toán timing — Basic đủ dùng cho v1, Word Length rẻ và hay, Word Frequency chỉ nên làm sau (cần kho tần suất từ, chỉ có en-US); (b) câu ngữ cảnh khi pause + lưu vị trí đọc + 3 lịch sử text; (c) thanh tiến trình click-to-seek; (d) auto-select bằng hover + `E` (ý tưởng cho "chọn block" không cần chuột phải); (e) static focal `⋅` + focal guide — điểm bám mắt luôn hiện.
- **Điểm không nên học:** quá nhiều cài đặt (~30 tùy chọn `madv*` + 17 color scheme + 6 tab) — TransPeek nên giữ **ít tùy chọn, mặc định tốt**; thuật toán Word Frequency gắn cứng dữ liệu en-US; code jQuery/Bootstrap cũ, không port trực tiếp được (chỉ port ý tưởng + công thức).
- **Giấy phép:** kiểu BSD 3-điều khoản — được tái dùng kèm giữ notice; không dùng tên Sprint Reader để quảng bá phái sinh.

---

## 4. Bảng so sánh 3 mẫu

| Tiêu chí | SwiftRead | jetzt | Sprint Reader |
|---|---|---|---|
| Loại | Thương mại, closed-source | Open source, Apache 2.0 | Open source, kiểu BSD |
| Phiên bản khảo sát | 6.11.2 (09/11/2026) | 1.2.0 (Manifest V2, cũ) | 3.0.3 (Manifest V3) |
| Chế độ đọc | Focus (RSVP) + Flow (highlight chạy) + Spotlight — *tên gọi ở web app; trong source `.crx` chỉ có 1 engine RSVP + toggle hiển thị (context on-pause/always/never, text scroller)* | RSVP thuần (1 từ) | RSVP thuần (1–N từ/slide) |
| ORP | Pivot = ký tự giữa `floor(n/2)` (không bảng theo độ dài), 1 chữ cam `#f6861f` trên nền `#272727`, **không reticle/guide**; căn bằng đo kích thước thật của ký tự highlight | Pivot 0/1/2/3/4 + reticle | ORP 1/2/3/4 + căn pixel bằng canvas + static focal `⋅` + focal guide |
| WPM | Mặc định **275**, max cứng **2000**, bước 25 (audio WPM riêng mặc định 200) | 100–1500, mặc định 400, bước ±10 | Mặc định 400, bước ±50 (chỉnh được), quảng cáo tới 1800+ |
| Chunk size | 1–6 từ, mặc định 1, gộp cơ học; delay tính theo chunk, WPM hiệu dụng hiển thị = `wpm × chunkSize` | Không có (luôn 1 từ) | Có (1–N từ/slide) |
| Pause dấu câu | **Hệ số nhân** (micro-pause): số 0.5, dấu câu 0.9, kết câu 1.1, từ dài 0.5, đoạn mới 1.1 — slider 0.1–3, cộng dồn, tắt khi `chunk×wpm ≤ 100`; trừ 30 ms thực thi mỗi slide | Hệ số nhân ×1,8/×2,2/×2,8 | Delay cố định: phẩy 250ms, chấm 450ms, đoạn 700ms |
| Tách từ dài | Từ dài > `8×chunkSize` thì hiện lâu hơn (hệ số 0.5), **không tách từ** trong engine legacy | > 13 ký tự → tách ≤ 8 ký tự | > 17 ký tự → tách theo âm tiết (Hypher) |
| Lấy text | Trang hiện tại (Readability, legacy), selection, PDF, ePub, Kindle/Libby, file local, paste tay; entry: Alt+V mở, Alt+B lưu library, menu overlay khi bấm toolbar, nút floating theo loại trang, context menu (kể cả "Start SwiftRead here") | Selection, click block (select mode), context menu, bookmarklet | Selection, context menu, shortcut, clipboard, hover auto-select + `E` |
| PDF | Legacy reader **gated PRO toàn bộ**; scan = báo không có selectable text, **không OCR**; luồng mới stage blob rồi mở web app (hạn mức PDF theo ngày nằm ở server) | Chỉ qua selection | Không (chỉ selection) |
| TTS | Có, giọng **cloud** (danh sách fetch live từ server, "90+" không kiểm chứng được từ source), highlight từng từ bằng **timepoints server + poll 50 ms**, chỉ khi chunk = 1 | Không | Không |
| Panel | **Popup window của web app host** (`app.swiftread.com/extension/reader`, mặc định); reader legacy trong-extension chỉ cho user grandfathered | Full-screen overlay | Cửa sổ popup riêng, 6 tab |
| Tiến trình/seek | Slider seek theo index từ + text scroller (cuộn chuột để seek) + J/L nhảy theo giây | Thanh 3px (không click) | Thanh click-to-seek + thời gian còn lại + tooltip slide |
| Ngữ cảnh khi pause | Context **±150 từ** quanh chunk hiện tại, mặc định chỉ hiện khi pause (always/on-pause/never), chunk active tô đậm | Từ trước/sau 2 bên (wrap) | Câu đầy đủ, từ hiện tại tô đậm |
| Lưu vị trí / lịch sử | Library đồng bộ theo tài khoản (Supabase + server riêng, save cần đăng nhập); vị trí đọc legacy **chỉ trong phiên** (in-memory, không persist), reader mới resume qua anchor + scroll report | Không | Lưu vị trí + 3 text gần nhất |
| Phím tắt global | **Alt+V** mở đọc + **Alt+B** lưu library (2 shortcut riêng); trong reader: Space, J/L, `;` reset, Q thoát, `=`/`-` WPM, `]`/`[` chunk | Alt+S | Ctrl+Shift+E duy nhất |
| Mobile | iOS + Android (đồng bộ) | Không | Không |
| Giá | Free + PRO subscription; trong extension TTS/PDF/ePub legacy **khóa theo license PRO** (không phải hạn mức ngày thấy được trong code); chỉ chuỗi `"Daily free PDF limit reached"` là ở luồng save-PDF (server) | Miễn phí | Miễn phí |

**Kết luận so sánh:** jetzt là **bản tham chiếu kỹ thuật gọn nhất** (parser + executor + pivot, vài trăm dòng, dễ port); Sprint Reader là **bản tham chiếu tính năng đầy đủ nhất** (3 thuật toán, pause, chunk, seek, ngữ cảnh, lịch sử); SwiftRead là **bản tham chiếu sản phẩm** (3 mode, TTS đồng bộ, đa thiết bị) — nhưng closed-source, chỉ học được ở mức ý tưởng. **Bổ sung 17/11 từ source thật:** 3 mode không có trong `.crx` (nằm ở web app); cái học được chắc chắn nhất từ SwiftRead là engine legacy gọn nhẹ (micro-pause hệ số nhân, pivot `floor(n/2)`, context ±150 từ khi pause, TTS cloud + timepoints) — xem §1.

---

## 5. RSVP cho tiếng Việt — lưu ý riêng

TransPeek là app học tiếng Anh (EN↔VI) nên cần lưu ý:

- **Từ tiếng Việt ngắn, đa số đơn âm tiết** ("tôi", "đi", "học") — ORP pivot theo độ dài từ vẫn dùng được (từ 2–4 ký tự → pivot 2), nhưng lợi ích ORP ít hơn tiếng Anh vì từ đã ngắn.
- **Chunk size 2–3 từ có thể hợp hơn chunk 1** cho tiếng Việt (cụm "học tiếng Anh" đọc như một đơn vị nghĩa) — nên để chunk size chỉnh được, mặc định 1 cho tiếng Anh, gợi ý 2 cho tiếng Việt (hoặc để người dùng tự chọn).
- **Tách từ dài ít quan trọng** với tiếng Việt (hiếm từ > 13 ký tự); với tiếng Anh thì cần (như jetzt/Sprint Reader).
- **Dấu thanh không ảnh hưởng timing** — nhưng font render tiếng Việt phải đúng (font hệ thống mac/Windows đều render tốt; tránh font monospace thiếu glyph tiếng Việt — jetzt dùng monospace mặc định, nếu port thì nên mặc định font hệ thống thay vì monospace).
- **RSVP đọc song ngữ:** nếu TransPeek cho đọc cả câu gốc + bản dịch, có thể đọc xen kẽ (1 câu EN rồi 1 câu VI) — chưa mẫu nào làm, là ý tưởng riêng đáng thử ở phase sau, không làm ở v1.

---

## 6. Khả thi tích hợp native vào TransPeek (integrate vs separate)

### 6.1. Kết luận chung: TÍCH HỢP ĐƯỢC (Path 1) — không cần extension riêng cho RSVP

| Thành phần | Độ khó native | Ghi chú |
|---|---|---|
| Engine RSVP (tokenize, timing, ORP, pause) | **Dễ** | Logic thuần túy, không phụ thuộc DOM. Port parser jetzt (~250 dòng JS) + công thức pivot sang Swift/C# trong ~1 buổi. Sprint Reader engine phức tạp hơn (~1400 dòng) nhưng v1 chỉ cần bản Basic + pause. |
| Render 1 từ tại điểm cố định | **Dễ** | SwiftUI `Text` + `AttributedString` (tô màu pivot); WinUI 3 `TextBlock` + `Run`. Căn pixel ORP: CoreText (`CTLine`) trên mac / `TextBlock.Measure` trên Windows thay cho `canvas.measureText`. |
| Timing WPM | **Dễ** | `DispatchSourceTimer` / `Timer` (mac), `DispatcherTimer` (WinUI 3). Độ chính xác ms là đủ (jetzt/Sprint Reader cũng chỉ dùng `setTimeout`). |
| Panel đọc (cửa sổ riêng) | **Dễ** | `NSPanel` floating (mac) / `AppWindow` + `OverlappedPresenter` (WinUI 3). Cả hai đều làm được cửa sổ riêng, nhớ kích thước, topmost khi đọc. |
| Điều khiển khi đọc (Space, ←/→, ↑/↓) | **Dễ** | Phím tắt **nội bộ panel** (chỉ active khi panel focus) — `NSWindow.keyDown` / WinUI `KeyboardAccelerator`. Không cần phím global. |
| Lấy text từ selection | **Đã có sẵn** | TransPeek đã có pipeline lấy selection (mac: Accessibility; Windows: UI Automation/clipboard). RSVP dùng chung luồng này — không viết lại. |
| Lấy text cả trang (kiểu SwiftRead "current page") | **Trung bình** | mac: duyệt AX tree của trình duyệt (Chrome/Safari expose AX tree, nhưng web app phức tạp thì cây AX dài, chậm); Windows: UIA tree tương tự. Chỉ lấy được text, không lấy được cấu trúc bài (heading/đoạn) tốt như DOM. **Khuyến nghị: không làm ở v1** — selection + clipboard + file là đủ. |
| Đọc PDF | **mac: dễ · Windows: trung bình** | mac: PDFKit trích text native (`PDFDocument`), có sẵn. Windows: `Windows.Data.Pdf.PdfDocument` (WinRT) trích được text từng trang — dùng được từ WinUI 3, nhưng API thô hơn PDFKit. PDF scan (ảnh) thì cả hai đều cần OCR — TransPeek đã có Vision OCR (mac) / Windows OCR + Tesseract (Windows), tái dùng được. |
| Đọc ePub | **Dễ** | ePub = ZIP + XHTML — giải nén + parse, code chung được cho cả 2 nền tảng. |

**2 pattern học được từ source thật SwiftRead (17/11, nên có ở v1 TransPeek — rẻ, hiệu quả):** (i) **micro-pause hệ số nhân** (số 0.5, dấu câu 0.9, kết câu 1.1, từ dài 0.5, đoạn 1.1; cộng dồn vào `delay = 60000/wpm`, tắt khi `chunk×wpm ≤ 100`) + trừ 30 ms thực thi mỗi slide; (ii) **ngữ cảnh ±150 từ khi pause + text scroller seek** (cuộn chuột để tua) — giải quyết "mất ngữ cảnh" ngay trong view đọc riêng, không cần chế độ Flow riêng.
| Kindle Cloud Reader / Libby | **Không làm được native** | Đây là web app của Amazon/OverDrive — chỉ extension trong trình duyệt mới nhúng được. **Bỏ qua** (không phải nhu cầu cốt lõi của TransPeek). |
| Đồng bộ đa thiết bị / tài khoản | **Không làm** | TransPeek không có tài khoản; lưu vị trí đọc local là đủ (như Sprint Reader). |

### 6.2. macOS (Swift/SwiftUI + AppKit)

- **Khả thi tốt.** Engine port thẳng sang Swift. Panel đọc: `NSPanel` với `level = .floating`, style riêng (không dùng chung panel dịch). ORP: `AttributedString` tô màu pivot; căn pixel bằng CoreText. WPM timer: `DispatchSourceTimer` cho chính xác. Phím tắt nội bộ: override `keyDown` trên panel. Lấy text: tái dùng pipeline selection hiện có (AX) + clipboard (`NSPasteboard`) + PDFKit cho PDF. Không cần quyền mới ngoài những quyền TransPeek đã cần (Accessibility cho selection).
- **Lưu ý:** panel đọc cần nhận focus bàn phím để Space/←/→ hoạt động — dùng `NSPanel` có thể thành key window (`becomesKeyOnlyIfNeeded` không đủ; cần panel thật sự key). Khi panel key thì app gốc mất focus — chấp nhận được vì đang ở chế độ đọc tập trung (giống jetzt chiếm focus).

### 6.3. Windows (WinUI 3 + C#)

- **Khả thi tốt.** Engine port sang C# tương tự. Panel đọc: cửa sổ WinUI 3 riêng (`AppWindow`, `OverlappedPresenter`, có thể `IsAlwaysOnTop` khi đọc). ORP: `TextBlock` với `Run` tô màu; đo pixel bằng `Measure`/`ActualWidth`. Timer: `DispatcherTimer` (độ phân giải ~15ms mặc định, đủ cho WPM ≤ 1000; nếu cần chính xác hơn dùng `System.Threading.Timer` + dispatch về UI). Phím tắt nội bộ: `KeyboardAccelerator` trên window. Lấy text: tái dùng pipeline selection hiện có (UI Automation/clipboard) + `Windows.Data.Pdf` cho PDF.
- **Lưu ý:** WinUI 3 window cần xử lý focus/activation cẩn thận để phím tắt không rơi vào app đang đọc; và `DispatcherTimer` ở tốc độ cao (WPM > 600, mỗi từ < 100ms) vẫn ổn vì chỉ đổi `Text` của 1 control.

### 6.4. Verdict tích hợp

**RSVP nên tích hợp vào app native (Path 1), không cần extension riêng.** Lý do: engine nhỏ, render native dễ, text capture tái dùng pipeline selection đã có của TransPeek, và RSVP đọc được text từ **mọi app** (Word, PDF, trình duyệt…) — điều extension trình duyệt không làm được. Extension chỉ hơn ở đúng 1 điểm: lấy "cả trang" tự động trong trình duyệt (SwiftRead "current page", Kindle/Libby) — nhưng đó không phải luồng chính của TransPeek (luồng chính là selection), và có thể làm sau bằng extension riêng nếu thật sự cần (xem recommendation.md tổng).

---

## 7. Luồng 1 icon 3 chức năng (câu hỏi UX chính)

Hiện trạng TransPeek: **1 float icon, 2 chức năng** — hover = dịch, click = nghe (TTS). Thêm RSVP thành chức năng thứ ba. Đánh giá từng phương án:

### 7.1. Các phương án

| Phương án | Cách dùng | Ưu | Nhược | Verdict |
|---|---|---|---|---|
| **A. Chuột phải / middle-click trên icon** | Chuột phải → menu (Dịch / Nghe / Đọc nhanh); middle-click → RSVP thẳng | Nhanh, 1 thao tác, không đụng hover/click hiện tại | **Middle-click không tồn tại trên trackpad Mac và đa số laptop Windows** (touchpad 2 ngón = chuột phải; không có nút giữa). Chuột phải trên Mac = chạm 2 ngón hoặc Control-click — dùng được nhưng **ít người biết** | Chuột phải: **dùng làm đường phụ**. Middle-click: **loại** |
| **B. Settings toggle: tắt TTS → click đi thẳng RSVP** | Toggle "Click icon = Nghe / Đọc nhanh" trong Settings | 1 click tới chức năng mình dùng nhiều nhất; không thêm thao tác nào | Người dùng phải vào Settings để đổi; chức năng còn lại phải đi đường khác | **Dùng** — làm toggle, mặc định click = Nghe |
| **C. Chooser nhỏ khi click** | Click icon → hiện menu nhỏ 3 lựa chọn (Dịch / Nghe / Đọc nhanh) | Rõ ràng, khám phá được, không cần nhớ phím/chuột | **Thêm 1 click vào cả 2 luồng chính** (dịch + nghe) — phá đúng thứ TransPeek đang tối ưu (nhanh) | **Loại** làm mặc định; chỉ cân nhắc nếu sau này có > 3 chức năng |
| **D. Long-press (nhấn giữ) icon** | Giữ ~500ms → RSVP; click thường → nghe | Không thêm UI | **Đụng thao tác kéo icon** (float icon vốn kéo được để di chuyển); trên Mac trackpad long-press/force-click dễ kích hoạt nhầm; khó phát hiện (không có gợi ý trực quan) | **Loại** |
| **E. Option-click (Mac) / Alt-click** | Option+click icon → RSVP | Chuẩn macOS (Option-click = hành động thay thế, vd Option-click Wi-Fi) | Chỉ có trên Mac; người dùng Windows không có tương đương tự nhiên; vẫn cần nhớ phím | **Đường phụ trên Mac** (ghi trong Help), không phải luồng chính |
| **F. Menubar/tray + phím tắt global** | Menu menubar (mac) / tray (Windows) có mục "Đọc nhanh (RSVP)"; 1 phím tắt global mở RSVP từ selection/clipboard | Chuẩn hệ điều hành, khám phá được, không đụng icon float; phím tắt nhanh cho power user | Thêm 1 mục menu (menubar đã dài — xem panel-tabs-break-report); phím tắt global phải tránh đụng app khác | **Dùng làm luồng chính cho RSVP** |
| **G. Nút "Đọc nhanh" trong panel dịch** | Trong panel dịch (tab 1) có nút nhỏ "Đọc nhanh" / biểu tượng ▶▶ — bấm thì mở panel RSVP với đúng text đang dịch | Rất khám phá được: người dùng đang xem bản dịch thì thấy ngay; dùng chung text đã có, không cần bôi lại | Thêm 1 nút vào panel dịch (panel đang tối giản) | **Dùng** — nút nhỏ, đặt ở hàng action của panel dịch |

### 7.2. Bất đối xứng Mac vs Windows — phân tích kỹ

**Windows:** chuột vật lý có đủ 3 nút (trái/phải/giữa) nên về lý thuyết middle-click khả thi — nhưng **đa số laptop Windows dùng touchpad**, và touchpad Windows không có middle-click (chạm 3 ngón là thao tác hệ thống; click vùng giữa touchpad không tồn tại). Vì vậy **ngay cả trên Windows cũng không được dựa vào middle-click**.

**Mac:** trackpad là thiết bị chính — **không có middle-click**; chuột phải = chạm 2 ngón hoặc Control-click (ít khám phá được hơn chuột phải Windows); **gesture 3 ngón đã bị hệ thống chiếm** (Mission Control, App Exposé, Look Up & data detectors) nên không dùng được cho TransPeek. **Force Click (nhấn mạnh)** cũng là thao tác hệ thống (tra từ điển/Quick Look) — không nên chiếm. Còn lại: click thường, Option-click, Control-click (chuột phải), và menubar + phím tắt.

**Kết luận:** không có luồng click nào dùng chung được cho cả 3 chức năng trên cả 2 nền tảng mà vẫn nhanh + khám phá được. Vì vậy **luồng mặc định nên giống nhau ở mức khái niệm, khác nhau ở mức thao tác**:

### 7.3. Luồng đề xuất (mặc định, dùng chung 2 nền tảng)

```
Float icon (1 icon duy nhất)
├── Hover ────────────→ panel Dịch (như hiện tại)
├── Click ────────────→ Nghe TTS (mặc định; đổi được trong Settings → RSVP)
├── Chuột phải ───────→ menu nhỏ: Dịch / Nghe / Đọc nhanh (RSVP) / ─── / Settings
│                        (Mac: chạm 2 ngón hoặc Control-click)
├── Option-click (chỉ Mac) → Đọc nhanh (RSVP) thẳng
└── Menubar/tray ──────→ menu có "Đọc nhanh (RSVP)" + phím tắt global
    Phím tắt global ───→ mở RSVP từ selection/clipboard (1 phím duy nhất, xem §9)
    Panel dịch ────────→ nút "Đọc nhanh" mở RSVP với text đang dịch
```

**Lý do chọn:**
1. **Không phá 2 luồng chính:** hover = dịch, click = nghe giữ nguyên — người dùng hiện tại không phải học lại.
2. **Ít click nhất cho người dùng RSVP thường xuyên:** bật toggle trong Settings thì click = RSVP (1 click); không thì phím tắt global (1 phím) hoặc chuột phải → chọn (2 thao tác).
3. **Không kích hoạt nhầm:** không long-press, không middle-click, không gesture 3 ngón — mọi đường vào RSVP đều là thao tác có chủ đích.
4. **Khám phá được:** menu chuột phải + nút trong panel dịch + mục menubar/tray là 3 chỗ người dùng có thể tự tìm thấy, không cần đọc hướng dẫn.
5. **Khác biệt theo nền tảng là tối thiểu:** chỉ thêm Option-click trên Mac (ghi trong Help, không quảng bá mạnh); luồng chính (phím tắt + menu + nút) giống hệt 2 bên.

**Thứ tự ưu tiên khi triển khai — MANH CHỐT 17/11 (thay thứ tự đề xuất trên):** (1) Option-click Mac / Alt-click Windows → RSVP thẳng → (2) menu chuột phải trên icon ("Đọc nhanh") → (3) nút "Đọc nhanh" trong panel dịch → (4) menubar/tray → (5) shortcut global chỉ là fallback; + Settings toggle đổi click → RSVP cho người dùng RSVP là chính.

---

## 8. Panel đọc + entry points

### 8.1. Panel đọc là panel RIÊNG — khẳng định

RSVP cần: chữ **rất lớn** (50–110px), một điểm cố định duy nhất, nền tối/mờ tập trung, thanh tiến trình, câu ngữ cảnh khi pause, và **chế độ "đang đọc" chiếm toàn bộ sự chú ý**. Không có cách nào nhét tất cả vào panel dịch (nhỏ, ~400px, tối giản) mà vẫn đọc được. Cả 3 mẫu đều dùng view riêng: SwiftRead = reader view riêng; jetzt = full-screen overlay; Sprint Reader = cửa sổ popup riêng 6 tab.

**Đề xuất cho TransPeek:** panel RSVP là **cửa sổ riêng** (như Sprint Reader), kích thước mặc định ~70% chiều rộng × ~55% chiều cao màn hình, tối thiểu 880×550, nhớ kích thước sau resize, có thể để topmost khi đang đọc (tùy chọn). Layout: vùng từ lớn ở giữa + thanh tiến trình click-to-seek + hàng điều khiển gọn (Play/Pause, WPM, chunk, mode) + câu ngữ cảnh khi pause (mặc định hiện). Đóng bằng Esc / Q / nút đóng.

### 8.2. Entry points — đánh giá

| Entry point | Đánh giá | Verdict |
|---|---|---|
| **Menubar (mac) / tray (Windows)** — mục "Đọc nhanh (RSVP)" | Chuẩn hệ điều hành; mở được cả khi không có float icon trên màn hình; nhưng menubar đã dài (xem panel-tabs-break-report) — thêm 1 mục là chấp nhận được, không thêm submenu | **Có** |
| **Phím tắt global** — mở RSVP từ selection/clipboard | Nhanh nhất cho người dùng thường xuyên; chỉ cần **1 phím** (xem §9) | **Có (luồng chính)** |
| **Menu chuột phải trên float icon** | Khám phá được, không tốn phím tắt | **Có** |
| **Nút "Đọc nhanh" trong panel dịch** | Dùng chung text đang dịch, không cần bôi lại; khám phá tốt nhất | **Có** |
| **Tự động mở khi bôi đen dài** (vd > 500 từ thì hiện gợi ý RSVP) | Dễ gây phiền; TransPeek vốn tránh tự động hiện panel | **Không** — chỉ mở khi người dùng chủ động gọi |
| **Đọc cả trang (kiểu SwiftRead)** | Cần trích xuất article trong trình duyệt — không làm được tốt bằng native (xem §6.1) | **Không ở v1** |

**Nguồn text khi mở RSVP (theo thứ tự ưu tiên):** (1) text đang bôi (selection) → (2) text trong panel dịch (nếu mở từ nút trong panel) → (3) clipboard (nếu không có selection, hỏi người dùng hoặc tự lấy) → (4) file (PDF/ePub, mở từ menubar/tray "Open file…", phase sau).

---

## 9. Vị trí view đọc: tab 2/3/4 của panel hay panel riêng?

| Tiêu chí | Tab 2/3/4 trong panel dịch | Panel riêng |
|---|---|---|
| **Kích thước chữ** | Panel dịch ~400px rộng → chữ RSVP tối đa ~30–36px, quá nhỏ so với chuẩn 50–110px; ORP pivot khó thấy | Chữ 50–110px thoải mái, đúng chuẩn |
| **Tập trung khi đọc** | Vẫn thấy tab dịch + các nút khác → xao nhãng; không có backdrop mờ | Toàn màn hình/cửa sổ riêng, backdrop mờ, chỉ có từ đang đọc |
| **Chỗ cho điều khiển** | Không đủ chỗ cho WPM slider + chunk + mode + seek bar trong 1 tab nhỏ | Đủ chỗ cho hàng điều khiển đầy đủ + câu ngữ cảnh |
| **Chuyển qua lại Dịch ↔ Đọc** | Tiện: swipe/click dot là sang tab | Mất 1 thao tác mở cửa sổ mới — nhưng **đang đọc thì không nên chuyển tab liên tục**; khi cần tra từ thì pause rồi hover từ trong câu ngữ cảnh (xem dưới) |
| **Chuyển tab nhầm khi đang đọc** | **Nguy cơ cao:** swipe 2 ngón (Mac) / scroll wheel trên dot (Windows) là gesture của chính panel tab — đang đọc mà lỡ tay là văng khỏi view đọc, mất vị trí + mất tập trung | Không có tab nên không có nguy cơ này |
| **Nhất quán với panel-tab concept** | Hợp với ý tưởng "1 panel nhiều tab" (panel-tabs-break-report) | Đi ngược concept, thêm 1 cửa sổ nữa vào app |
| **Kỹ thuật** | Phải resize panel khi sang tab đọc (panel dịch nhỏ → panel đọc lớn) — resize giữa chừng gây giật, khó giữ vị trí | Cửa sổ độc lập, không ảnh hưởng panel dịch |

**Khuyến nghị của báo cáo: PANEL RIÊNG.** Lý do quyết định: RSVP là **trạng thái đọc tập trung** (immersive), không phải một "tab thông tin" — nó cần không gian, cần loại bỏ xao nhãng, và cần bảo vệ khỏi chuyển tab nhầm. Việc resize panel dịch thành panel đọc lớn giữa chừng là trải nghiệm tệ hơn hẳn mở cửa sổ riêng. Cả 3 mẫu đều chọn view riêng, không mẫu nào nhét RSVP vào tab nhỏ.

> **Cập nhật — Manh 17/11: GIỮ CẢ 2 PHƯƠNG ÁN, quyết ở lúc prototype.** Nếu bản tab dãn đọc vẫn tập trung, không giật, không bị auto-hide phá thì làm tab; không thì chuyển panel riêng. Điều kiện bắt buộc nếu làm tab: **ẩn HOÀN TOÀN chức năng panel dịch khi đang đọc** (không chỉ ẩn giao diện mà vẫn bấm được — tab đọc tách riêng, click vào không chạm panel dịch), ẩn hết chrome dịch + hàng chấm, tắt swipe/lăn chuyển tab lúc đang đọc, tạm dừng auto-hide, cân nhắc căn giữa. Lý do giữ 2 hướng: tách ra tránh lag / lỗi đè chéo lên luồng dịch. Bằng chứng mới từ SwiftRead càng ủng hộ panel riêng: cả reader mới (popup web app) lẫn reader legacy đều là **cửa sổ riêng**, không có bản tab nào trong extension — nhưng vì "mode" chỉ là toggle hiển thị trên cùng engine, bản tab dãn (nếu làm) không cần engine riêng.

**Thỏa hiệp (nếu Manh vẫn muốn giữ concept multi-tab):** tab 2/3 của panel dịch có thể đặt **nút/mục "Đọc nhanh"** (launcher — bấm thì mở panel RSVP riêng), chứ không đặt **bản thân view đọc** trong tab. Như vậy concept multi-tab vẫn có chỗ cho RSVP mà không phải hy sinh trải nghiệm đọc.

**Ý tưởng thêm (phase sau):** khi pause RSVP, cho phép **hover vào từ trong câu ngữ cảnh để dịch từ đó** (dùng chung engine dịch của TransPeek) — đây là điểm nối tự nhiên giữa "đọc nhanh" và "học tiếng Anh", và là thứ không mẫu nào có.

---

## 10. Shortcut budget (ngân sách phím tắt)

### 10.1. Vấn đề

RSVP cần nhiều thao tác: play/pause, tăng/giảm tốc độ, lùi/tới 1 slide, lùi/tới nhiều slide, reset, nạp clipboard, nạp selection, đóng. Nếu mỗi thao tác là 1 phím tắt **global** thì: (a) đụng phím tắt của app khác (Ctrl+Shift+E, Space… đều đã có chủ); (b) TransPeek đã có danh sách phím tắt dài (dịch, TTS, OCR, panel…); (c) người dùng không nhớ nổi.

### 10.2. Đánh giá 3 phương án

| Phương án | Ưu | Nhược | Verdict |
|---|---|---|---|
| **A. Tách RSVP thành app/extension riêng** | Không tốn phím tắt của TransPeek; mỗi app có bộ phím riêng | **Chia đôi sản phẩm:** trùng pipeline lấy selection, trùng TTS, người dùng phải cài 2 app; mất lợi thế "1 icon 3 chức năng"; chi phí bảo trì 2 codebase | **Loại** |
| **B. 1 phím tắt global + điều khiển nội bộ panel** | Chỉ tốn **1 phím global**; mọi phím khi đọc chỉ active khi panel đọc đang focus (giống jetzt + Sprint Reader) nên **không đụng app khác**; khám phá được qua bảng Shortcuts trong panel | Người dùng phải mở panel trước mới dùng được các phím điều khiển (nhưng đó vốn là lúc cần chúng) | **Chọn** |
| **C. Menubar/tray menu thay phím tắt** | Không tốn phím nào; khám phá tốt | Chậm (mở menu → tìm mục → click) — không hợp với thao tác "đang đọc, cần pause ngay / tăng tốc ngay" | **Bổ sung, không thay thế:** menu có đủ mục (để khám phá + dùng chuột), nhưng phím nội bộ panel vẫn là đường chính khi đang đọc |

### 10.3. Bộ phím đề xuất

**Global (1 phím duy nhất, chỉnh được trong Settings):**
- Mở RSVP từ selection/clipboard — mặc định gợi ý `Ctrl+Shift+R` (Windows) / `Cmd+Shift+R` (Mac). *(Cần kiểm tra đụng độ: `Cmd+Shift+R` trên Mac là "reload" trong nhiều trình duyệt — nhưng vì là phím global của TransPeek thì chỉ kích hoạt khi TransPeek bắt được; vẫn nên để người dùng đổi được trong Settings. Nếu Manh muốn an toàn hơn: `Ctrl+Option+R` / `Ctrl+Alt+R`.)*

**Nội bộ panel đọc (chỉ active khi panel focus — học từ jetzt + Sprint Reader):**

| Phím | Tác dụng |
|---|---|
| `Space` | Play / Pause |
| `←` / `→` | Lùi / tới 1 slide (`Shift+←/→`: 1 câu; `Alt+←/→`: 1 đoạn — như jetzt) |
| `↑` / `↓` | WPM +10 / −10 (như jetzt; Sprint Reader dùng Z/X ±50 — chọn ↑/↓ vì trực quan hơn) |
| `A` / `S` | Lùi / tới 1 slide (dự phòng cho người quen Sprint Reader; có thể bỏ nếu thừa) |
| `K` / `L` | Lùi / tới 10 slide |
| `R` | Reset về đầu |
| `C` | Nạp clipboard |
| `V` | Nạp selection hiện tại |
| `+` / `−` | Phóng to / thu nhỏ chữ |
| `0` | Đổi theme sáng/tối |
| `Esc` hoặc `Q` | Đóng panel đọc |

**Nguyên tắc:** phím nội bộ panel **không đăng ký global** — hệ điều hành/app khác không bao giờ thấy chúng. Chỉ 1 phím global duy nhất được đăng ký, và người dùng đổi được. Bảng phím tắt hiển thị trong chính panel đọc (tab Shortcuts kiểu Sprint Reader, hoặc dòng hint dưới panel).

---

## 11. Hướng mobile (iOS/Android) — ngắn

### 11.1. Mobile RSVP hiện tại làm gì tốt

Thị trường mobile RSVP đã đông, chia 2 trường phái:

| Trường phái | Đại diện | Điểm mạnh |
|---|---|---|
| RSVP thuần (1 từ) | **Spreeder** (iOS/web, có khóa học + thư viện), **Reedy** (Android, gọn nhẹ), **Spritz** (iOS/Android, engine "chữ đỏ" ORP) | Tốc độ cao, luyện tập có bài bản, thư viện sách |
| Guided highlight (highlight chạy trên text đầy đủ) | **Outread** (iOS/iPad/Mac, Flash Mode + Guided Highlight, đọc báo), **Éist** (RSVP + nghe audiobook offline) | Giữ ngữ cảnh, hợp đọc báo/bài dài hơn là RSVP thuần |
| Đa năng (đọc + nghe + đồng bộ) | **SwiftRead** (iOS/Android, đồng bộ Library + tiến độ với extension/web) | Một tài khoản, đọc tiếp ở mọi thiết bị |

Mô hình chung của mobile: **đọc trong app** (thư viện sách/bài trong app, hoặc share từ app khác vào), không có float icon; mua bằng subscription hoặc trả 1 lần (vd Spreeder $67 lifetime, Outread có Outread+).

### 11.2. TransPeek lên mobile thì thay đổi gì

- **Lấy text:** không có float icon, không có selection cross-app. Cách lấy text trên mobile là **share sheet** (Share → TransPeek), **clipboard**, hoặc **mở file trong app** (PDF/ePub) — tức là luồng "đọc trong app" chứ không phải "đọc text của app khác".
- **Panel đọc:** thành **full-screen** (đúng sở trường của RSVP — màn hình điện thoại vốn đã là 1 điểm cố định).
- **Điều khiển:** chạm để pause, vuốt để đổi tốc độ/tới-lui — thay cho phím tắt.
- **Dịch + TTS:** vẫn làm được (share text vào app → dịch; TTS bằng giọng hệ thống iOS/Android), nhưng **mất lợi thế lớn nhất của TransPeek desktop** là float icon hover/click ngay trên text của app khác.
- **Chi phí:** 2 codebase mới (SwiftUI iOS + Kotlin/Android, hoặc 1 codebase cross-platform) + tài khoản/đồng bộ nếu muốn theo mô hình SwiftRead + App Store review + subscription.

### 11.3. Khuyến nghị: CHƯA LÀM MOBILE

- **Không làm mobile ở phase hiện tại.** Lý do: (1) thị trường RSVP mobile đã đông và nặng subscription — TransPeek vào sau khó có lợi thế riêng; (2) lợi thế cốt lõi của TransPeek (float icon trên text của app khác) **không tồn tại trên mobile** — lên mobile là làm lại sản phẩm khác, không phải mở rộng sản phẩm hiện tại; (3) desktop (mac + Windows) còn chưa xong RSVP — nên chứng minh RSVP được dùng trên desktop trước.
- **Nếu sau này làm:** làm **sau khi RSVP desktop được dùng thật** (có người dùng phản hồi), bắt đầu bằng **iOS trước** (người dùng TransPeek hiện tại dùng Mac/iPhone nhiều hơn), theo mô hình SwiftRead: share sheet + clipboard + thư viện + đồng bộ tiến độ — nhưng đó là quyết định của phase sau, không lên kế hoạch chi tiết bây giờ.

---

## 12. Khuyến nghị tổng hợp (cho recommendation.md)

1. **Tích hợp RSVP vào app native (Path 1)** — macOS và Windows đều khả thi tốt; engine nhỏ, render native dễ, tái dùng pipeline selection + OCR + TTS đã có. Không cần extension riêng cho RSVP.
2. **Phạm vi v1 (gọn):** engine Basic (timing đều + pause sau dấu câu kiểu jetzt: hệ số nhân ×1,8/×2,2/×2,8) + chunk size 1–3 + ORP pivot + panel riêng + seek bar + câu ngữ cảnh khi pause + lưu vị trí. **Chưa làm:** thuật toán Word Frequency (cần kho tần suất en-US), đọc cả trang tự động, Kindle/Libby.
3. **Phase sau (nếu v1 được dùng):** thêm chế độ Flow (highlight chạy trên text đầy đủ, học SwiftRead) — vì nhiều người đọc thật thích giữ ngữ cảnh hơn RSVP thuần; và hover-từ-trong-câu-ngữ-cảnh để dịch (nối RSVP với học tiếng Anh).
4. **Luồng icon (Manh CHỐT 17/11):** giữ hover = dịch, click = nghe; RSVP vào theo thứ tự **(1) Option-click Mac / Alt-click Windows → (2) chuột phải icon → (3) nút "Đọc nhanh" trong panel dịch → (4) menubar/tray → (5) shortcut global chỉ là fallback**; thêm Settings toggle đổi click → RSVP. **Không middle-click, không long-press, không chooser mỗi lần click.**
5. **View đọc: GIỮ CẢ 2 PHƯƠNG ÁN (Manh 17/11), quyết ở lúc prototype** — báo cáo khuyên panel riêng; tab dãn chỉ được nếu đọc tập trung, không giật, không bị auto-hide phá + ẩn hoàn toàn chức năng panel dịch khi đang đọc.
6. **Phím tắt:** 1 global duy nhất (mở RSVP) + toàn bộ phím điều khiển chỉ active khi panel đọc focus.
7. **Mobile: chưa làm.** Chứng minh RSVP trên desktop trước.

## 13. Nguồn & giới hạn nghiên cứu

**Nguồn đã đọc trực tiếp (source code):**
- jetzt — clone https://github.com/ds300/jetzt (`src/jetzt/`, Apache 2.0): `modules/parse.js`, `exec.js`, `view.js`, `control.js`, `config.js`, `select.js`, `init.js`, `background.js`, `jetzt.css`, `manifest.json`, `options/templates/*.html`, `demo.html`.
- Sprint Reader — clone https://github.com/anthonynosek/sprint-reader-chrome (`src/sprint-reader/`, kiểu BSD): `src/engine.js` (1403 dòng), `src/reader.js` (1945 dòng), `src/content.js`, `src/background.js`, `src/popup.js`, `src/advanced.js`, `src/utility.js`, `reader.html`, `manifest.json`, `README.md`.

**Nguồn đã đọc trực tiếp (source code) — BỔ SUNG 17/11:**
- SwiftRead — 2 file `.crx` do Manh gửi (6.11.2.0, 13 MB + 5.9.2.0, 12 MB): đã giải nén và đọc source. Engine legacy readable (`legacy/app.js`, `listen.js`, `settings_store.js`, `focus_anchor.js`); các bundle mới (`background/index.js`, `swiftread-menu/`, `page-buttons/`) đã minified — phát hiện ở đó dựa trên tên định danh/chuỗi/URL. 477/588 file của 5.9.2 giống hệt (md5) trong 6.11.2 — engine RSVP không rewrite giữa 2 bản; đổi lớn là cấu trúc + reader mới (popup web app host).

**Nguồn web (không có source):**
- SwiftRead — listing Chrome Web Store (đọc 16/11/2026, v6.11.2+9023721d1, cập nhật 09/11/2026), https://swiftread.com/pro, bài Tech Tip (Univ. of Michigan) và các bài tổng hợp extension đọc nhanh.
- Mobile RSVP — các bài so sánh 2026 (hookedtobooks, eist.app, readkinetic, themindcompany).

**Giới hạn / chưa xác minh được:**
- **~~Không tải được file `.crx` của SwiftRead~~ — ĐÃ GIẢI QUYẾT 17/11:** Manh gửi 2 file `.crx` (6.11.2.0 + 5.9.2.0), đã đọc source và bổ sung toàn bộ §1/§4/§6/§7/§9/§12 (xem các ghi chú "theo source thật (17/11)"). Những gì **không có trong `.crx`** — 3 chế độ Focus/Flow/Spotlight, danh sách giọng TTS, trích xuất toàn trang luồng mới, vị trí/history của reader mới — nằm ở **web app/server `app.swiftread.com`**, không kiểm chứng được từ source. Sprint Reader vẫn chỉ có repo GitHub công khai (đã clone và đọc source đầy đủ).
- **Không cài extension để quan sát live** trong đợt này (chỉ đọc source + trang web) — các con số WPM, kích thước panel, phím tắt đều lấy từ source code hoặc listing chính thức, không phải từ quan sát live.
- **Số WPM "1800+" của Sprint Reader và "3x" của SwiftRead** là claim quảng cáo của nhà phát triển; độ hiểu thực tế ở tốc độ cao thấp hơn nhiều (xem §0).
