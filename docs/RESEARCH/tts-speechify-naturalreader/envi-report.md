# Báo cáo nghiên cứu: ENVI — Từ điển Anh Việt (Chrome extension 1.1.6.0)

- **Mẫu nghiên cứu:** `ENVI 1.1.6.0` — file `.crx` đã giải nén sẵn tại `~/workspace/research_notes/tts-speechify-naturalreader-20261116/src/envi/`
- **Ngày nghiên cứu:** 16/11/2026
- **Phương pháp:** đọc trực tiếp mã nguồn đã giải nén (manifest, content script, background service worker, popup/options, dictionary app). Các file JS đều là bundle webpack đã minify (1 dòng), đã đọc bằng cách trích xuất theo từ khóa và theo module. **Không cài extension lên trình duyệt để chạy thử** — mọi kết luận dưới đây đều suy ra từ mã nguồn; những chỗ chỉ suy luận mà chưa kiểm chứng trực tiếp sẽ ghi rõ "chưa kiểm chứng live".
- **Mục đích:** làm mẫu cho tính năng **"Quick panel / instantly translate"** của TransPeek — một panel nhỏ, nhẹ, chỉ hiện bản dịch ngắn, thay cho panel dịch đầy đủ khi bật trong Settings.

---

## 0. Tóm tắt nhanh (đọc 1 phút)

ENVI thực chất là **hai hệ thống trong một extension**:

| Hệ thống | File chính | Vai trò |
|---|---|---|
| **(A) Tooltip dịch nhanh khi di chuột** | `contentScript.js` (~140 KB) | Hover vào từ/câu → hiện tooltip nhỏ chỉ chứa bản dịch. Đây chính là mẫu cho "Quick panel" của TransPeek. |
| **(B) Từ điển đầy đủ (web app nhúng vào trang)** | `popup2.js` (~2,5 MB, Vue 2 + Vuex) | Bôi đen từ → hiện icon nhỏ → click icon → mở panel từ điển 400–450 px với nghĩa, phiên âm, ví dụ, động từ bất quy tắc, tab dịch/phân tích, nút phát âm. |
| **(C) Background service worker** | `background.js` (~750 KB) | Gọi API dịch (Google/Bing/Papago), phát âm `chrome.tts`, lưu lịch sử 20 mục, context menu, omnibox, chuyển hướng PDF, OCR. |
| **(D) Popup/Options** | `popup.js` (~850 KB, Vue 2 + Vuetify), `popup.html` (350×600 px) | Giao diện cài đặt + lịch sử tra cứu. |

**Kết luận quan trọng nhất cho TransPeek:** ENVI chứng minh mẫu "tooltip chỉ hiện bản dịch ngắn" hoạt động tốt và rất nhẹ — nhưng nó **không dùng icon trung gian** cho tooltip (hover → tooltip hiện thẳng, không qua icon), và **không có Model B** (không tự nhận diện block text rồi hiển thị sẵn icon). Icon chỉ xuất hiện trong hệ thống (B), sau khi người dùng **bôi đen** văn bản — tức là biến thể của Model A (icon xuất hiện sau một hành động của người dùng, không phải tự hiển thị sẵn).

---

## 1. Panel dịch nhanh (tooltip) trông như thế nào

### 1.1. Bản chất: một Bootstrap tooltip, không phải panel tự vẽ

ENVI không tự vẽ panel. Nó dùng **Bootstrap 4 Tooltip + Popper.js** (đã bundle sẵn trong `contentScript.js`):

- Một `<div id="mttContainer">` duy nhất được chèn vào `<body>`, làm "neo" cho tooltip:
  - `position: fixed; left: 0; top: 0; width: 1000px; margin-left: -500px; z-index: 100000200; pointer-events: none; background: transparent`.
  - Container này **di chuyển theo chuột**: mỗi lần `mousemove`, code đặt `transform: translate(<x>px, <y>px)` cho container — tức điểm neo của tooltip luôn bám đúng vị trí con trỏ.
  - Tooltip được cấu hình `placement: "top", container: "#mttContainer", trigger: "manual"` — nghĩa là bong bóng hiện **phía trên con trỏ**, và chỉ hiện/ẩn khi code gọi `tooltip("show")` / `tooltip("hide")`, không tự hiện theo hover của Bootstrap.
- Nội dung tooltip được gán qua thuộc tính `data-original-title` = **chỉ có bản dịch** (xem §6). Không có tiêu đề, không có nút, không có từ gốc.

### 1.2. Kích thước và giao diện mặc định

CSS của tooltip được sinh động từ Settings (hàm `N()` trong `contentScript.js`), giá trị mặc định:

| Thuộc tính | Mặc định | Ghi chú |
|---|---|---|
| Cỡ chữ | `14 px` | Chỉnh được trong Settings (Tooltip Font Size) |
| Chiều rộng tối đa | `200 px` | Chỉnh được (Tooltip Width); chữ dài tự xuống dòng |
| Căn lề | `center` | Có thể chọn left/right/justify |
| Nền | `#e6f9ff` (xanh rất nhạt) | Chỉnh được (Tooltip Background Color) |
| Chữ | `#000` | Chỉnh được (Tooltip Font Color) |
| Hiệu ứng | `backdrop-filter: blur(15px)` | Làm mờ nền phía sau tooltip — tạo cảm giác "kính mờ", hiện đại |
| Bo góc | `.25rem` (~4 px) | Kiểu Bootstrap |
| Mũi tên | màu `#b3ecff` | Mũi tên chỉ xuống vị trí con trỏ |
| `pointer-events` | `none` trên toàn bộ tooltip | **Rất quan trọng:** tooltip không bao giờ chặn click — chuột "xuyên qua" tooltip, click vẫn rơi vào trang web bên dưới |

### 1.3. Tooltip chỉ hiện đúng một thứ: bản dịch ngắn

Tooltip **không** hiện:

- từ gốc / câu gốc,
- phiên âm (IPA),
- từ loại (noun/verb…),
- ví dụ,
- nút phát âm, nút copy, nút mở rộng.

Tất cả những thứ đó thuộc về panel từ điển đầy đủ (hệ thống B, §7). Sự "nhẹ" của tooltip đến từ việc **cố tình lược bỏ mọi thứ trừ bản dịch** — đúng triết lý mà TransPeek cần cho Quick panel: người dùng đang đọc, chỉ cần nghĩa ngay lập tức, không cần chi tiết.

---

## 2. Khi nào tooltip xuất hiện

### 2.1. Ba chế độ kích hoạt (Settings → "Translate When")

| Giá trị | Nghĩa | Mặc định |
|---|---|---|
| `mouseover` | Chỉ dịch khi di chuột qua chữ | |
| `select` | Chỉ dịch khi bôi đen | |
| `mouseoverselect` | Cả hai | **Mặc định** |

Ngoài ra còn hai "khóa giữ" (hold key) trong Settings — `keyDownTooltip` (giữ một phím để bật tooltip) và `keyDownTTS` (giữ một phím để phát âm) — mặc định đều là `null` (tắt). Khi người dùng gán phím giữ cho tooltip thì tooltip tự tắt (`useTooltip = "false"`), tức hai cơ chế loại trừ nhau.

### 2.2. Luồng hover — polling mỗi 700 ms, không phải event tức thì

Đây là chi tiết kỹ thuật quan trọng nhất của ENVI:

1. `mousemove` chỉ ghi lại tọa độ chuột (`u`, `c`) và phần tử dưới chuột (`f`), đồng thời di chuyển `#mttContainer` theo chuột. Nó **không** dịch ngay.
2. Một `setInterval` chạy mỗi **700 ms** kiểm tra: tab đang hiển thị (`document.visibilityState === "visible"`), chuột đã thực sự di chuyển (cờ `p` — xem dưới), chế độ có chứa `mouseover`, và không có văn bản đang được bôi đen (`v` rỗng — khi đang bôi đen thì ưu tiên luồng select).
3. Mỗi lần tick, code dùng **`document.caretRangeFromPoint(x, y)`** để lấy vị trí ký tự đúng dưới con trỏ, rồi mở rộng Range theo chế độ "Dịch theo" (`detectType`, §8), kiểm tra bounding rect của Range có chứa điểm chuột không, rồi mới gọi hàm dịch `E(text, "mouseover")`.
4. Hàm `E()` lọc đầu vào (xem §2.4), gửi message `{type: "translate"}` sang background, nhận bản dịch rồi gọi `tooltip("show")`.

Hệ quả về cảm nhận tốc độ:

- Độ trễ hover → tooltip rơi vào khoảng **0–700 ms** (trung bình ~350 ms) cộng thời gian gọi API dịch (Google/Bing). Không phải tức thì, nhưng đủ nhanh để cảm giác "di chuột là có nghĩa".
- Polling 700 ms là một sự đánh đổi có chủ ý: rẻ hơn nhiều so với việc dịch ngay mỗi `mousemove` (sẽ spam API), và đơn giản hơn việc dùng `mouseover` event + debounce (vì `mouseover` bắn liên tục khi đi qua các node con).

### 2.3. Cờ "chuột đã di chuyển" — chống tooltip tự hiện khi tải trang

Tooltip chỉ bắt đầu hoạt động sau khi chuột di chuyển đủ xa: mỗi `mousemove`, nếu tổng độ lệch `|dx| + |dy| > 3` px thì bộ đếm `h` tăng; khi `h` đạt 2 (tức di chuyển lần thứ hai) cờ `p` mới bật thành `true`. Nhờ đó, tooltip không tự bật khi trang vừa tải xong mà chuột đang đứng yên trên một từ.

### 2.4. Luồng bôi đen (select)

- Module `3497` trong `contentScript.js` lắng nghe `selectionchange` (debounce 300 ms) và `mouseup`, rồi tự phát ra một custom event `selectionEnd` kèm `selectedText`.
- Content script lắng nghe `selectionEnd`: nếu chế độ chứa `select` thì gán `v = selectedText` và gọi `E(v, "select")`. Tooltip hiện ở vị trí chuột hiện tại (container vẫn bám theo `mousemove` gần nhất).
- Khi có văn bản được bôi đen (`v` khác rỗng), vòng polling hover tạm dừng — tránh tooltip hover đè lên tooltip của vùng chọn.

### 2.5. Bộ lọc đầu vào — những gì ENVI từ chối dịch

Trước khi gọi API, hàm `E()` chuẩn hóa và loại bỏ:

- Gộp khoảng trắng, trim hai đầu; **dài hơn 1000 ký tự → bỏ** (không dịch cả đoạn dài bằng tooltip).
- **Là URL** (module `8079`: chuỗi dạng `scheme://…` có tên miền hợp lệ) → bỏ.
- **Chỉ toàn ký tự đặc biệt / số / ký hiệu tiền tệ** (regex kiểm tra "có ít nhất một ký tự không phải ký hiệu") → bỏ.
- Ngôn ngữ nguồn nằm trong `langExcludeList` (danh sách loại trừ trong Settings) → trả về chuỗi rỗng, tooltip không hiện.
- Nếu ngôn ngữ đích trùng ngôn ngữ nguồn và không đặt ngôn ngữ dịch ngược (`translateReverseTarget`) → cũng trả rỗng (không hiện tooltip dịch "cùng ngôn ngữ").

---

## 3. Tooltip định vị như thế nào (vị trí, mép màn hình, cuộn trang)

### 3.1. Neo theo chuột, bong bóng ở phía trên

Như §1.1: container 1000 px (căn giữa bằng `margin-left: -500px`) được `translate` tới đúng tọa độ chuột. Popper.js đặt bong bóng tooltip **phía trên** điểm neo (`placement: "top"`). Vì điểm neo là chuột, tooltip luôn "lơ lửng" ngay trên từ đang đọc — người dùng không cần rời mắt khỏi dòng chữ.

### 3.2. Xử lý mép màn hình

- **Mép trên:** Popper.js (cấu hình mặc định của Bootstrap 4 tooltip) tự **lật** tooltip xuống dưới khi không đủ chỗ phía trên. Đây là hành vi mặc định của Popper, không có code riêng của ENVI can thiệp — **chưa kiểm chứng live** nhưng suy ra từ việc ENVI dùng đúng cấu hình mặc định của Bootstrap Tooltip (`placement: "top"` mà không tắt `flip`).
- **Mép trái/phải:** chiều rộng tối đa chỉ 200 px (mặc định) và container neo rộng 1000 px căn giữa, nên Popper có đủ "đất" để căn bong bóng không tràn màn hình trong hầu hết trường hợp. Người dùng có thể tăng Tooltip Width trong Settings — khi đó nguy cơ tràn mép tăng, nhưng không thấy code xử lý riêng.
- **Mép dưới:** tooltip ở trên chuột nên hiếm khi chạm mép dưới, trừ khi Popper đã lật xuống dưới.

### 3.3. Theo dõi cuộn trang (scroll)

- **Không có listener `scroll` nào cho tooltip** (tìm trong toàn bộ phần code chính của `contentScript.js` không thấy `scroll`).
- Hệ quả: khi người dùng cuộn trang mà không di chuột, tooltip đứng yên ở vị trí viewport cũ trong tối đa 700 ms, tới tick polling tiếp theo — lúc đó `caretRangeFromPoint` với tọa độ chuột cũ sẽ trả về text khác hoặc rỗng, và tooltip sẽ cập nhật/ẩn. Thực tế cảm nhận: tooltip "dính" theo trang một nhịp ngắn rồi biến mất. Đây là một điểm yếu nhỏ của thiết kế polling, nhưng chấp nhận được vì tooltip có `pointer-events: none` nên không gây hại.
- Ngược lại, **panel từ điển đầy đủ** dùng `position: absolute` theo tọa độ trang (`pageX/pageY`), nên nó cuộn cùng nội dung trang — khác với tooltip dùng `position: fixed` theo viewport.

---

## 4. Tooltip tắt như thế nào

Hàm ẩn tooltip là `C()` — chỉ hai việc: `tooltip("hide")` và reset biến `d` (text đang hiển thị). Nó được gọi trong các trường hợp sau:

| Tình huống | Cơ chế |
|---|---|
| Di chuột sang chỗ không có chữ / sang từ khác | Tick polling 700 ms gọi `E("")` hoặc `E(từ mới)` → `C()` ẩn tooltip cũ trước khi hiện tooltip mới |
| Bôi đen vùng khác / bỏ chọn | Luồng `selectionEnd` với text rỗng → `C()` |
| Tab mất focus / chuyển cửa sổ | Listener `blur` → `C()` + dừng TTS |
| Nhấn `Ctrl+F` hoặc `Ctrl+A` | Listener `keydown` → `C(false)` (ẩn nhưng không reset cờ) — tránh tooltip che ô tìm kiếm |
| Rời trang / tải lại | `beforeunload` → `C()` + dừng TTS |
| Tắt tooltip trong Settings | Listener `chrome.storage.onChanged` → vẽ lại CSS; tooltip đang hiện sẽ bị dispose khi container bị xóa |
| Văn bản không dịch được (URL, ký hiệu, quá dài, ngôn ngữ bị loại trừ) | `E()` trả rỗng → `C()` |

**Những gì KHÔNG có:**

- **Không có phím `Esc` để tắt** — không tìm thấy listener `keydown` nào xử lý Escape cho tooltip.
- **Không có timeout tự tắt** — tooltip ở lại màn hình chừng nào chuột còn đứng trên cùng một từ/câu (mỗi tick polling đều gọi lại `E()` với cùng text; vì `d` đã bằng text đó nên tooltip không nhấp nháy).
- **Không có "click ra ngoài để tắt"** — không cần, vì tooltip có `pointer-events: none`: click "xuyên qua" tooltip xuống trang web; đồng thời click thường làm mất focus/selection nên tooltip cũng biến mất theo luồng select.
- **Không có nút đóng** — tooltip quá nhỏ nên không cần.

Nhận xét: cơ chế tắt của ENVI gần như hoàn toàn **thụ động** (ẩn khi ngữ cảnh thay đổi), không có thao tác tắt chủ động nào ngoài việc di chuột đi chỗ khác. Điều này phù hợp với triết lý "nhẹ": người dùng không bao giờ phải "quản lý" tooltip.

---

## 5. Cảm giác nhanh/nhẹ — tooltip lược bỏ những gì so với panel đầy đủ

| Khía cạnh | Tooltip dịch nhanh (A) | Panel từ điển đầy đủ (B) |
|---|---|---|
| Nội dung | Chỉ bản dịch (1 chuỗi text) | Từ, phiên âm, từ loại, nghĩa, ví dụ, từ liên quan, động từ bất quy tắc, nút loa, link bình luận, tab Dịch/Phân tích |
| Kích thước | Tối đa 200 px rộng, cao theo 1–3 dòng | 400–450 px rộng, cao tới ~360 px, có tab |
| Thao tác mở | Hover (tự động, không click) hoặc bôi đen | Bôi đen → click icon (2 bước) |
| API gọi | 1 lần dịch / 1 lần hover (có cache theo text `d`) | 1 lần tra từ điển + có thể thêm 1 lần dịch + lịch sử |
| Tương tác | Không tương tác được (`pointer-events: none`) | Đầy đủ: click tab, click từ trong ví dụ để tra tiếp, nút loa, nút đóng |
| Đóng | Tự động khi di chuột | Nút ×, hoặc bôi đen chỗ khác |
| Độ trễ cảm nhận | ~0–700 ms + 1 RTT API | ~100 ms sau click icon + 1 RTT API |

**Bài học cho TransPeek:** sự "nhẹ" không đến từ việc tối ưu code, mà đến từ **quyết định sản phẩm**: tooltip chỉ trả lời đúng một câu hỏi ("câu này nghĩa là gì?") và từ chối mọi thứ khác. Mọi tính năng phụ (phát âm, ví dụ, lịch sử) đều bị đẩy sang panel đầy đủ. Quick panel của TransPeek nên giữ đúng ranh giới này: chỉ hiện bản dịch ngắn, không thêm nút loa/copy/mở rộng vào trong Quick panel — nếu cần thì để người dùng click vào Quick panel để mở panel đầy đủ.

---

## 6. Tra cứu từ điển hoạt động như thế nào

### 6.1. Hai tầng tra cứu khác nhau

ENVI có **hai** cơ chế tra cứu riêng biệt — cần phân biệt rõ:

1. **Dịch nhanh (tooltip):** dịch máy toàn bộ chuỗi (từ/câu/khối) qua Google/Bing/Papago — không phải tra từ điển. Không có khái niệm "từ không có trong từ điển".
2. **Tra từ điển (panel đầy đủ):** chỉ nhận **một từ/cụm ngắn**, tra vào từ điển Anh–Việt của `jpdictionary.com`.

### 6.2. Tra từ điển — luồng chi tiết (trong `popup2.js`)

1. **Kiểm tra đầu vào:** từ phải khớp regex `/^[a-zA-Z\-' ]+$/` (chỉ chữ Latin, dấu gạch nối, nháy đơn, khoảng trắng) và **≤ 55 ký tự**. Từ có dấu tiếng Việt (à, á, đ, ơ, ư…) bị từ chối ngay — panel từ điển này chỉ phục vụ tra từ **tiếng Anh**.
2. **Chuẩn hóa:** lowercase, trim; xuống dòng được gộp thành khoảng trắng.
3. **Lemmatize (tìm dạng gốc):** dùng thư viện `only_lemmas` để suy ra dạng gốc của từ (vd "running" → "run"). Nếu tra từ gốc không có kết quả thì thử dạng lemma.
4. **Fallback động từ/tính từ bất quy tắc:** hai danh sách nhúng sẵn trong bundle (`irregularVerb`, `irregularAdj`, tách bằng `#`) được quét bằng regex `\b<từ>\b`; nếu khớp thì lấy dạng gốc trong danh sách (vd "went" → "go") rồi tra lại.
5. **Gọi API từ điển:** `POST https://en.jpdictionary.com/api/search/e` với body là chuỗi JSON đã mã hóa. Response gồm các trường `def`, `mean[]` (mỗi nghĩa có `m` = meaning, `e` = example, `v` = …) — **nội dung bị xáo trộn** (hàm `f()` trong bundle: thay ký tự theo bảng đổi chỗ + đảo cặp ký tự + reverse có điều kiện theo độ dài). Đây là biện pháp chống copy dữ liệu từ điển phía server.
6. **Cache:** kết quả tra từ được cache trong Vuex store (`wordHistory`); tra lại từ đã tra chỉ mất ~25 ms (setTimeout giả lập), không gọi mạng.
7. **Hiển thị:** tab "word" hiện từ, phiên âm, từ loại (POS), các nghĩa + ví dụ (từ trong ví dụ được bọc `<span>` để click là tra tiếp từ đó), lemmas ("Xem thêm"), động từ bất quy tắc tìm được, link "Tra nhanh trên từ điển ENVI" / "Xem bình luận" mở tab `envi.jpdictionary.com/?word=…`.

### 6.3. Dịch máy — ba nhà cung cấp (trong `background.js`)

| Vendor | Endpoint | Cách gọi | Ghi chú |
|---|---|---|---|
| **Google** (mặc định) | `translate.googleapis.com/translate_a/t` | GET với tham số `client=te_lib`, `tk` (token tính bằng thuật toán `k()` — bản sao thuật toán TKK cũ của Google Translate web) | Endpoint không chính thức, không cần API key; dễ bị Google chặn/đổi (rủi ro 429) |
| **Bing** | `bing.com/ttranslatev3` | POST; trước đó phải GET `bing.com/translator` để cạo `IG`, `IID`, `key`, `token` chống lạm dụng | Rất mong manh: phụ thuộc cấu trúc HTML của trang Bing |
| **Papago** (thử nghiệm) | `papago.naver.com/apis/n2mt/translate` | Thư viện `nodepapago`: ký HMAC với `deviceId` UUID + timestamp | Ghi rõ "Experimental" trong Settings |

- Dịch ngược: nếu ngôn ngữ phát hiện được trùng ngôn ngữ đích và người dùng đặt `translateReverseTarget`, ENVI tự dịch ngược sang ngôn ngữ đó.
- Panel từ điển dùng một endpoint Google khác, đơn giản hơn: `translate_a/single?client=gtx&sl=…&tl=…&dt=t&q=…`, giới hạn 1800 ký tự, tự nhận diện tiếng Nhật qua dải Unicode.
- **Không có dữ liệu từ điển nào đi kèm trong extension** — toàn bộ nghĩa/ví dụ đều lấy từ server `jpdictionary.com`. Extension chỉ mang theo danh sách động từ/tính từ bất quy tắc và code lemmatize.

### 6.4. Phát âm (TTS) — có nhưng tắt mặc định

- Tooltip: `useTTS` mặc định `"false"`. Nếu bật, mỗi lần tooltip hiện, background gọi `chrome.tts.speak(text, {lang, voiceName, volume, rate})` với giọng lấy từ `chrome.tts.getVoices()` (giọng hệ thống + giọng remote của Chrome), tốc độ và âm lượng chỉnh trong Settings. Chỉ đọc tiếng Anh và tiếng Nhật (`"en" != t && "ja" != t` → ép về `"ja"` — một chi tiết lạ, có lẽ do extension gốc là từ điển Nhật).
- Panel từ điển: dùng `window.speechSynthesis` (Web Speech API) với `rate = 0.8`, ưu tiên giọng "Microsoft Jenny Online (Natural)" rồi "Google US English", iPhone ưu tiên "Samantha". Nút loa đổi màu xanh (`#0d6efd`) trong lúc đang đọc.
- **Không có lựa chọn giọng nam/nữ** trong Settings của tooltip — chỉ chọn voice theo ngôn ngữ.

### 6.5. Lịch sử

Mỗi lần tooltip hiện bản dịch (cả hover lẫn select), background lưu `{sourceText, targetText}` vào `historyList` trong `chrome.storage.local`, tối đa **20 mục** (quá thì `pop()` mục cũ nhất). Popup hiển thị lịch sử, click để copy, có nút xóa từng mục / xóa hết / tải CSV.

---

## 7. Panel từ điển đầy đủ (hệ thống B) — để đối chiếu

Panel này là thứ người dùng thấy khi click icon sau khi bôi đen (§8). Nó đáng ghi lại vì đây mới là "panel đầy đủ" mà Quick panel cần phân biệt:

- **Kích thước:** rộng `maxWidth` mặc định 400 px (khi mở từ icon: 450 px); nếu viewport hẹp hơn 400/450 px thì co lại `viewport - 30` px, lề trái 15 px.
- **Vị trí:** `position: absolute` trong trang; `popTop = pageY + 25` (dưới điểm bôi đen 25 px), `popLeft` căn theo `pageX` nhưng kẹp vào trong viewport (`pageX + 400 > viewport - 10` → dời sang trái còn `viewport - 410`); `popTop` cũng bị kẹp: không vượt quá `scrollHeight - 360`, tối thiểu 3 px.
- **Nội dung:** nền trắng, viền `#dee2e6`, bo 4 px, bóng `0 8px 16px rgba(0,0,0,.15)`, padding `10px 5px 10px 10px`, `z-index: 99999`.
- **Cấu trúc:** nút đóng × (góc trên phải, kiểu Bootstrap `btn-close`), hàng tab (`o-nav-tabs`), nút loa, rồi nội dung theo tab: **word** (từ điển), **translate** (bản dịch Google), **analysis** ("Analyze, translate, and quickly look up" — phân tích câu). Click vào từ trong ví dụ sẽ tra từ đó ngay trong panel.
- **Đóng:** nút ×, hoặc bôi đen một vùng khác (hàm `showMobileSeachIcon`/`afterSelectText` gọi `closePopup()` trước khi xử lý lựa chọn mới).

---

## 8. Icon hiển thị: Model A hay Model B?

### 8.1. Kết luận: tooltip KHÔNG có icon; icon chỉ có ở luồng từ điển, và đó là Model A

| Luồng | Có icon không? | Model |
|---|---|---|
| **Tooltip dịch nhanh (hover/bôi đen)** | **Không.** Hover → tooltip hiện thẳng. Không có bất kỳ phần tử icon nào trong `contentScript.js` (tìm `"icon"` trong file: 0 kết quả). | Không thuộc A hay B — không có icon |
| **Panel từ điển (bôi đen → icon → click)** | **Có.** Sau khi bôi đen, một icon nhỏ (ảnh logo ENVI, base64 PNG) hiện gần điểm bôi đen. Click icon → mở panel từ điển. | **Model A** (icon xuất hiện **sau** hành động của người dùng — ở đây là bôi đen, không phải hover) |
| **Tự nhận diện block text, hiển thị sẵn icon (Model B)** | **Không có ở đâu trong extension.** | — |

### 8.2. Icon của luồng từ điển — vị trí được quyết định như thế nào

- Kích hoạt: `document.body.onmouseup` (và `ontouchend`; trên Android thêm `onpointerout`) gọi `showMobileSeachIcon()` — tên có chữ "Mobile" nhưng chạy trên cả desktop.
- Điều kiện hiện icon:
  - Có vùng chọn không rỗng; xuống dòng được gộp thành khoảng trắng.
  - **Không chứa dấu tiếng Việt** (regex loại các ký tự à á â ã è é ê … ỳ ỵ ỷ ỹ) — icon chỉ hiện cho text tiếng Anh (vì từ điển chỉ có Anh–Việt).
  - Không phải là cùng một vùng chọn như lần trước (`mobileSelected` — tránh hiện lại icon khi click lại cùng chỗ).
  - Vùng chọn không bắt đầu trong phần tử có class chứa `"not-select"`.
  - Chờ 100 ms sau `mouseup` (để selection ổn định) rồi mới hiện.
- Vị trí: `popTop = pageY + 17`, `popLeft = pageX - 12` — tức icon nằm **ngay dưới-điểm cuối của vùng chọn**, lệch xuống 17 px, sang trái 12 px. CSS: `.o-search-mobile { z-index: 2147483647; position: absolute; cursor: pointer; }` — z-index tối đa có thể của trình duyệt, luôn nổi trên mọi thứ.
- Tương tác: `mousedown` trên icon → `setDataPopup()` (lưu text đang chọn vào `wordSearchText`); `mouseup` trên icon → `showMobilePopup()` (ẩn icon, mở panel từ điển tại vị trí đã kẹp mép, xem §7). Icon tự ẩn khi: mở panel, bôi đen chỗ khác, hoặc selection rỗng.
- **Không theo dõi scroll:** icon dùng `position: absolute` theo tọa độ trang nên tự cuộn cùng trang; nhưng nếu người dùng cuộn sau khi icon đã hiện thì icon vẫn ở tọa độ trang cũ (tức trôi theo nội dung) — chấp nhận được vì icon chỉ tồn tại trong khoảnh khắc giữa bôi đen và click.

### 8.3. Một hàm chết: `afterSelectText`

Trong `popup2.js` còn một hàm `afterSelectText(e, a)` có logic gần giống `showMobilePopup` nhưng mở panel trực tiếp sau khi bôi đen (không qua icon). Hàm này **được định nghĩa nhưng không còn chỗ nào gọi tới** trong bundle 1.1.6 (chỉ 1 lần xuất hiện duy nhất là định nghĩa). Nhiều khả năng đây là tàn dư của một luồng cũ (mở panel thẳng sau khi bôi đen, không cần icon) đã bị thay bằng luồng icon. Ghi lại để tránh hiểu nhầm khi đọc code.

---

## 9. ENVI bắt chữ như thế nào (word boundary, câu, khối)

### 9.1. Hover: `caretRangeFromPoint` + `Range.expand()`

- `document.caretRangeFromPoint(x, y)` trả về một Range thu gọn tại đúng ký tự dưới con trỏ. Nếu không phải text node → trả `""` (không dịch).
- Tùy `detectType` mà Range được mở rộng:
  - `"word"` → `range.expand("word")` — lấy đúng từ dưới chuột.
  - `"sentence"` (mặc định) → `range.expand("sentence")` — lấy cả câu chứa con trỏ.
  - `"container"` → `setStartBefore` / `setEndAfter` node chứa — lấy toàn bộ khối (đoạn văn, nút bấm…). Ngoài ra, các class đặc biệt `ytp-caption-segment` (phụ đề YouTube), `LC20lb` (kết quả Google) và `ocr_text_div` (lớp phủ OCR, xem §9.3) luôn được coi như container.
- Sau đó kiểm tra `getBoundingClientRect()` của Range có chứa điểm chuột không; nếu con trỏ nằm ngoài rect (vd Range mở rộng sang dòng khác) thì trả `""`.
- ⚠️ `Range.expand()` là **API không chuẩn, chỉ có trên Chrome/Edge** (không có trên Firefox/Safari). Đây là một phụ thuộc nền tảng đáng chú ý — ENVI chỉ chạy trên Chromium.

### 9.2. Bôi đen: `selectionchange` debounce 300 ms + `mouseup`

Như §2.4: debounce 300 ms sau `selectionchange`, cộng `mouseup` để chốt. Text lấy từ `window.getSelection().toString()` (fallback `document.selection` cho IE cũ — code chết trong thực tế).

### 9.3. Chữ trong ảnh: OCR phủ lớp text trong suốt

- Khi hover qua ảnh `<img>` lớn hơn 300×300 px và `useOCR` bật, ENVI gửi ảnh qua iframe ẩn `ocr.html` (chạy OpenCV + Tesseract trong sandbox `opencvHandler.html`) để OCR.
- Kết quả OCR được vẽ thành các `<div class="ocr_text_div">` phủ đúng vị trí từng dòng chữ trong ảnh: `position: absolute; opacity: 0.7; color: transparent; border: 2px solid CornflowerBlue; font-size: 1.5rem` — chữ trong suốt nhưng vẫn là text node thật, nên hover lên ảnh sẽ dịch được như text thường (nhờ cơ chế container ở §9.1). Vị trí các div được tính lại khi `resize`.
- Ngôn ngữ OCR mặc định là `jpn_vert` (tiếng Nhật dọc!) — dấu vết cho thấy extension này fork từ một tool dịch tiếng Nhật ("Mouse Tooltip Translator" — tên gốc còn lại trong id `destructmyextension_MouseTooltipTranslator` và domain `jpdictionary.com`).

---

## 10. Ánh xạ sang TransPeek: Quick panel / instantly translate

### 10.1. Những gì chuyển thẳng được (ý tưởng, không cần viết lại logic)

| Mẫu của ENVI | Áp cho TransPeek Quick panel |
|---|---|
| Tooltip chỉ hiện **bản dịch ngắn**, không hiện từ gốc/phiên âm/ví dụ/nút bấm | Quick panel chỉ hiện bản dịch ngắn; mọi tính năng khác ở panel đầy đủ |
| Tooltip `pointer-events: none` — không chặn click | Quick panel nên "xuyên click" hoặc ít nhất không chiếm chuột khi chỉ để đọc |
| Neo theo chuột, bong bóng phía trên từ | Quick panel neo gần từ/câu đang hover; ưu tiên phía trên |
| Chiều rộng tối đa nhỏ (~200 px), chữ tự xuống dòng, nền mờ (blur) | Quick panel gọn, tối đa ~200–300 px, có thể dùng vibrancy/blur của hệ điều hành |
| Tắt thụ động: di chuột đi là mất, không cần nút đóng | Quick panel tự ẩn khi hover sang chỗ khác / blur; không cần nút × |
| Hai chế độ kích hoạt riêng (hover / select) + toggle trong Settings | Quick panel là một toggle trong Settings, độc lập với panel đầy đủ; giữ nguyên luồng select → icon → panel đầy đủ |
| Lọc đầu vào (quá dài, URL, ký hiệu) trước khi gọi API | Áp dụng cùng bộ lọc cho Quick panel để tránh spam API và tooltip rác |
| Hover có độ trễ (polling 700 ms) thay vì dịch ngay mỗi mousemove | TransPeek nên debounce hover ~300–500 ms (hiện tại Windows đã dùng hover delay 500 ms mặc định) thay vì gọi dịch liên tục |
| Icon chỉ hiện **sau khi bôi đen** (Model A), không tự hiện sẵn | Phù hợp với model hiện tại của TransPeek (select → float icon); **không** cần bắt chước Model B của Speechify cho Quick panel |

### 10.2. Những gì phải viết lại hoàn toàn theo cách native

| Vấn đề | Cách ENVI làm (web) | Cách TransPeek phải làm (native) |
|---|---|---|
| **Tìm chữ dưới chuột** | `document.caretRangeFromPoint(x, y)` + `Range.expand("word"/"sentence")` — DOM cho sẵn mọi thứ | **macOS:** `AXUIElementCopyElementAtPosition` (hit-test qua Accessibility) → lấy `AXSelectedTextRange` / `AXStringForRange` / `AXBoundsForRange` của phần tử; **Windows:** `IUIAutomation::ElementFromPoint` → `TextPattern::RangeFromPoint` → `GetText` / `GetBoundingRectangles`. Không có API nào tương đương `Range.expand("sentence")` — phải tự tách từ/câu bằng `NSStringEnumerationByWords` / `NLTokenizer` (macOS) hoặc tự quét ký tự phân tách (Windows). |
| **Định vị panel gần chữ** | CSS `position: fixed` + `transform: translate(x, y)`; Popper.js tự lật khi chạm mép | **Cửa sổ nổi không viền** (`NSPanel` kiểu `NSStatusWindowLevel` / WinUI 3 `AppWindow` + `OverlappedPresenter` + `SetWindowPos HWND_TOPMOST`); tự tính vị trí từ rect của dòng chữ, tự lật lên/xuống khi chạm mép màn hình (dùng `NSScreen.visibleFrame` / `DisplayArea`), tự xử lý đa màn hình. Không có Popper.js — phải tự viết logic lật/kẹp mép. |
| **Theo dõi cuộn trang** | Không theo dõi (tooltip `fixed` đứng yên, tự ẩn sau 1 tick) | Khó hơn: app khác cuộn thì rect chữ thay đổi mà TransPeek không nhận được event. Cách thực tế: **ẩn Quick panel ngay khi phát hiện scroll** (theo dõi `CGEvent` scroll wheel toàn cục / `GetMessage` mouse wheel) hoặc poll lại vị trí text mỗi ~500 ms; không nên cố "bám theo" chữ đang cuộn. |
| **Hiện/ẩn theo hover** | Polling 700 ms + `mousemove` | **macOS:** `CGEventTap` theo dõi mouse-moved toàn cục (cần quyền Accessibility) rồi hit-test qua AX; **Windows:** `SetWindowsHookEx(WH_MOUSE_LL)` hoặc `TrackMouseEvent` + UIA hit-test. Cả hai đều tốn hơn DOM event rất nhiều — cần throttle/debounce, và chỉ hit-test khi chuột đã dừng (~300–500 ms) chứ không phải mỗi lần di chuyển. |
| **Bắt vùng chọn (select)** | `selectionchange` event của DOM | **macOS:** `AXSelectedText` qua Accessibility (app phải expose; nhiều app không expose) hoặc fallback đọc clipboard sau khi giả lập `Cmd+C`; **Windows:** `TextPattern.GetSelection()` qua UI Automation, fallback clipboard sau `Ctrl+C`. Đây là luồng TransPeek đã có — Quick panel chỉ thay đổi *cách hiển thị*, không thay đổi *cách bắt text*. |
| **Chữ trong ảnh / app vẽ tay** | OCR bằng Tesseract trong iframe, phủ div trong suốt lên ảnh | TransPeek đã có Vision OCR (macOS) và Windows.Media.Ocr + Tesseract (Windows) — tương đương, nhưng ở native thì OCR chạy trên ảnh chụp màn hình vùng đó, không phủ div lên app khác được. Quick panel với text OCR được: chụp vùng → OCR → dịch → hiện panel; không hover-từng-dòng được như ENVI. |
| **Quyền hệ thống** | Không cần quyền gì thêm (content script chạy sẵn trong trang) | **macOS:** bắt buộc quyền Accessibility (+ Screen Recording nếu chụp màn hình OCR); người dùng phải cấp trong System Settings và **quit + mở lại app** mới có hiệu lực. **Windows:** UIA không cần quyền đặc biệt, nhưng app chạy elevated (admin) thì app thường không đọc được — cần lưu ý khi user chạy TransPeek không có quyền admin. |

### 10.3. Giới hạn cần nói thẳng với Manh

1. **Quick panel kiểu ENVI chỉ thực sự mượt trong trình duyệt.** Trong app khác, mỗi lần hover đều phải qua Accessibility/UI Automation hit-test — chậm hơn DOM hàng chục lần và không phải app nào cũng expose text (app vẽ tay bằng Canvas/DirectWrite, nhiều app Electron, game, remote desktop…). Khi app đích không expose text, Quick panel hover **không thể hoạt động** — chỉ còn luồng select → icon (luồng TransPeek đã có) và OCR.
2. **Không có Model B cho Quick panel native.** ENVI cũng không có Model B. Việc "quét liên tục để hiển thị sẵn icon trong mọi block text" ở native đồng nghĩa với việc quét cây Accessibility/UI Automation của cửa sổ đang focus liên tục — tốn CPU, giật, và vẫn bỏ sót app không expose layout. Khuyến nghị: **không làm Model B cho Quick panel**; giữ Model A (select → icon) + hover-tooltip chỉ trong trình duyệt (nếu sau này làm extension riêng).
3. **Endpoint Google không chính thức.** ENVI dùng `translate_a/t` với token `tk` tự tính và `translate_a/single?client=gtx` — cả hai đều là endpoint web không có tài liệu, Google có thể chặn bất cứ lúc nào (đúng kiểu lỗi 429 mà TransPeek-Windows đã gặp trên máy công ty của Manh). TransPeek đã dùng Google Translate theo cách riêng của mình; khi làm Quick panel chỉ cần tái dùng provider hiện có, không copy cách tính `tk` của ENVI.

---

## 11. Bản quyền / ghi công

- **Code của ENVI là proprietary, không có giấy phép mở.** Trong `.crx` không có file LICENSE nào cho code của ENVI; chỉ có các file `*.LICENSE.txt` của thư viện bên thứ ba (DOMPurify, Bootstrap, jQuery, Popper.js, Vue, axios, pdf.js… — đều là MIT/Apache). Vì vậy: **được học theo ý tưởng và mô tả lại pattern, không được copy code** (đặc biệt là thuật toán tính token `tk` cho Google Translate và hàm xáo trộn dữ liệu từ điển `f()`).
- **Dữ liệu từ điển thuộc về `jpdictionary.com`.** API `en.jpdictionary.com/api/search/e` và toàn bộ nghĩa/ví dụ là tài sản của nhà cung cấp đó; TransPeek không được gọi trộm API này hay dùng lại dữ liệu của họ. TransPeek dùng provider dịch của riêng mình (Google Translate như hiện tại) và link từ điển do Manh tự chọn.
- **Pattern được phép tham khảo tự do** (ý tưởng chung, không thuộc bản quyền): tooltip chỉ hiện bản dịch ngắn; `pointer-events: none`; neo theo chuột; icon hiện sau khi bôi đen; kẹp panel vào trong viewport; cache kết quả tra cứu; giới hạn lịch sử 20 mục.
- **Thư viện dùng lại được** (nếu cần, theo đúng giấy phép của chúng): Bootstrap Tooltip/Popper.js (ý tưởng định vị, không dùng trực tiếp trong app native), Vue/Vuetify (không liên quan tới native app).

---

## 12. Những điểm đọc source chưa kết luận được (ghi rõ để không hiểu lầm)

1. **Giao diện thực tế khi chạy:** chưa cài extension lên trình duyệt, nên màu sắc, độ mượt, cảm giác "nhẹ" thực tế của tooltip/panel là suy luận từ CSS và cấu hình — chưa kiểm chứng bằng mắt. Đặc biệt hiệu ứng `backdrop-filter: blur(15px)` có thể nặng trên máy yếu.
2. **Hành vi lật tooltip khi chạm mép trên:** suy ra từ cấu hình mặc định của Bootstrap 4 Tooltip + Popper.js (flip mặc định bật) — chưa kiểm chứng live. Nếu ENVI đã tắt flip ở đâu đó trong code thì kết luận này sai; đã tìm trong `contentScript.js` và không thấy cấu hình nào tắt flip.
3. **Vendor Papago:** code `nodepapago` có trong bundle và có trong danh sách Settings ("papago (Experimental)"), nhưng luồng dịch Papago trong background chưa đọc hết (chỉ đọc tới chỗ chọn vendor). Không rõ Papago có còn hoạt động không (API Papago đổi xác thực nhiều lần từ 2024).
4. **Nút "Bôi đen" (`boiDen` / `turnoff`) trong popup:** popup có một toggle lưu `turnoff` vào `chrome.storage.local`, nhưng chưa lần ra nó nối vào logic nào trong content script (content script không đọc key `turnoff`). Có thể là tàn dư, hoặc nối qua một đường khác chưa thấy. Không ảnh hưởng tới kết luận chính.
5. **Tab "analysis" của panel từ điển:** thấy có trong render code (tab thứ ba, tiêu đề i18n "Analyze, translate, and quickly look up") nhưng chưa đọc hết logic của tab này.
6. **OCR mặc định tiếng Nhật dọc (`jpn_vert`):** đây là giá trị mặc định trong Settings; chưa rõ người dùng Việt Nam có đổi được sang `vie`/`eng` không — danh sách ngôn ngữ OCR đầy đủ nằm trong popup Settings, chưa trích hết.
7. **`selection.js` và `setting.js` trong gói là file rỗng 0 byte** — có lẽ là file cũ còn sót lại khi đóng gói, không được manifest gọi tới. Manifest chỉ gọi `contentScript.js`, `background.js`, `popup.js`, `popup2.js` (inject động), `pdfInject.js`, `ocr.js`, `opencvHandler.js`.
8. **Mô tả trên Chrome Web Store** (nguồn web, chỉ để đối chiếu): "Tra từ điển Anh Việt, dịch mọi ngôn ngữ ngay trên trang web với Từ điển ENVI" — khớp với những gì đọc được trong `_locales/vi/messages.json`. Không lấy thêm thông tin nào khác từ web.

---

## 13. Phụ lục: bản đồ file → chức năng (để tra cứu lại)

| File | Kích thước | Vai trò |
|---|---|---|
| `manifest.json` | 4 KB | MV3; quyền `storage`, `tts`, `tabs`, `scripting`, `contextMenus`, `webNavigation`; `host_permissions: <all_urls>`; content script chạy mọi trang, mọi frame, `document_idle`; omnibox keyword `envi`; action popup = `popup.html`; options = `popup.html` |
| `contentScript.js` | 140 KB | Tooltip dịch nhanh (hàm chính `E()` dịch, `C()` ẩn, `N()` sinh CSS, `S()`/`A()` nhắn background) + DOMPurify + jQuery + Bootstrap Tooltip/Popper + OCR overlay (`P()`, `F()`, `q()`) + module settings `1542` + module URL-check `8079` + module selection `3497` |
| `background.js` | ~750 KB | Service worker: settings (module `1542` trùng với content script), 3 vendor dịch (Google `translate_a/t` + tính `tk`, Bing `ttranslatev3`, Papago), `chrome.tts.speak/stop`, lịch sử 20 mục, context menu "Tìm kiếm trên Từ điển ENVI 🚀", omnibox → `envi.jpdictionary.com/?word=…`, redirect file PDF local sang pdf.js viewer, inject `popup2.js` vào mọi trang http(s) qua `webNavigation.onDOMContentLoaded` |
| `popup.js` | ~850 KB | Vue 2 + Vuetify: popup 350×600 px kiêm trang Options — các tab cài đặt (tooltip, dịch, TTS, PDF, OCR, loại trừ ngôn ngữ, lịch sử) + nút tải lịch sử CSV |
| `popup2.js` | ~2,5 MB | Vue 2 + Vuex + axios: app từ điển nhúng vào trang — icon sau khi bôi đen, panel 400–450 px (tab word/translate/analysis), gọi `POST en.jpdictionary.com/api/search/e`, lemmatize + fallback động từ bất quy tắc, phát âm bằng `speechSynthesis` rate 0.8 |
| `popup.html` / `popup.css` | 4 KB / 1,5 MB | Khung popup + CSS Vuetify |
| `pdfInject.js`, `ocr.js`, `ocr.html`, `opencvHandler.js/.html` | nhỏ | Đọc PDF bằng pdf.js viewer (12 MB) + OCR ảnh bằng OpenCV/Tesseract trong sandbox iframe |
| `selection.js`, `setting.js` | 0 byte | File rỗng, không dùng |
| `_locales/vi/messages.json`, `_locales/en/messages.json` | 8 KB | Chuỗi giao diện song ngữ VI/EN (tên app, mô tả, nhãn Settings) |
| `icons/`, font `.woff/.woff2/.ttf/.eot` | ~2,5 MB | Icon extension + font nhúng của Vuetify |

**Các hàm/mốc quan trọng để tra lại trong source:** `E(n, r)` (dịch + hiện tooltip), `C(e)` (ẩn tooltip), `N()` (sinh CSS tooltip), `A(e)` (gửi message sang background), `S(e, t)` (gọi dịch), `setInterval(…, 700)` (polling hover), `document.caretRangeFromPoint`, `Range.expand("word"/"sentence")`, `showMobileSeachIcon` / `showMobilePopup` / `setDataPopup` / `closePopup` (luồng icon → panel từ điển), `getSearchData` (tra từ điển, API `search/e`), `translate` (Google `translate_a/single?client=gtx`).

---

## 14. Kết luận — ENVI cho TransPeek Quick panel

1. **Mẫu tooltip của ENVI là mẫu đúng cho Quick panel:** một bong bóng nhỏ, tối đa ~200 px, chỉ chứa bản dịch ngắn, neo theo chuột, phía trên từ, xuyên click, tự ẩn khi di chuột đi. TransPeek nên sao chép **quyết định sản phẩm** này, không sao chép code.
2. **ENVI không có Model B** và tooltip của nó cũng không qua icon — vì vậy báo cáo này **không ủng hộ** việc làm icon hiển thị sẵn (Model B) cho Quick panel native; giữ luồng select → icon (Model A) mà TransPeek đã có, cộng thêm tooltip hover chỉ khi kỹ thuật cho phép (xem giới hạn ở §10.3).
3. **Phần khó nhất khi đưa sang native không phải là vẽ panel, mà là tìm chữ dưới chuột** (Accessibility hit-test + tự tách từ/câu) và **định vị/lật panel khi chạm mép** (tự viết, không có Popper.js). Hai việc này cần spike riêng trước khi chốt làm Quick panel hover cho macOS/Windows.
4. **Không dùng lại được gì từ ENVI về mặt dữ liệu hay endpoint:** từ điển là của `jpdictionary.com`, endpoint Google là không chính thức. TransPeek tái dùng provider dịch hiện có của mình.
