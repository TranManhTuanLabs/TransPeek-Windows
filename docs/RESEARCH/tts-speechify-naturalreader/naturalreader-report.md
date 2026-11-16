# Báo cáo nghiên cứu: NaturalReader — AI Text to Speech (Chrome Extension 8.0.3.0)

> **Phạm vi:** đọc source code của extension NaturalReader 8.0.3.0 (file `.crx` đã giải nén sẵn), đối chiếu với tài liệu Help Center công khai của NaturalReader (đánh dấu rõ là nguồn web).
> **Mục đích:** đánh giá UX đọc to (TTS) + icon loa để tích hợp vào TransPeek (macOS + Windows), theo hướng app đã chốt ngày 2026-11-13: **một float icon chung — hover = dịch, click = nghe (TTS)**; về sau có thêm Settings "Quick panel / instantly translate".
> **Ngôn ngữ báo cáo:** tiếng Việt. Thuật ngữ kỹ thuật giữ nguyên tiếng Anh.
> **Lưu ý bản quyền:** NaturalReader là phần mềm proprietary. Báo cáo này chỉ **mô tả pattern** để re-implement, **không copy code**.

---

## 0. Tổng quan nhanh (đọc 2 phút là đủ)

| Hạng mục | Kết luận |
|---|---|
| Icon loa khi **hover block text** | **KHÔNG có.** Trên trang web thường, NaturalReader không hiện icon khi hover. Icon chỉ hiện **sau khi user bôi chọn text** (mouseup), đặt tại vị trí chuột, tự ẩn sau 3 giây |
| Icon **hiển thị sẵn (Model B)** | **Chỉ có trong 3 app riêng biệt** (ChatGPT, Gmail, Outlook) qua DOM hook đặc thù — không phải phát hiện block chung cho mọi trang web |
| Hover có tác dụng gì? | Ở chế độ **Click to Read**: hover một câu → câu đó highlight xanh nhạt + con trỏ pointer; **click câu đó → đọc từ câu đó**. Không có icon |
| Highlight khi đọc | Có, 2 lớp: **câu** (nền màu) + **từ** (nền đậm, chữ sáng), 4 bảng màu (light/dark/ice/warm); tự cuộn trang theo câu đang đọc (auto-scroll), có nút "Relocate" khi câu đang đọc ra ngoài viewport |
| Giọng đọc | 2 tầng: **Free = giọng hệ thống của trình duyệt/OS** (Web Speech API `speechSynthesis`); **trả phí = cloud TTS của NaturalReader** (AWS API Gateway, audio MP3 stream + speech marks để highlight từng từ). Không dùng giọng của Google/Microsoft trực tiếp làm backend chính |
| Điều khiển khi đang đọc | Nằm **ngay trong player đang chạy** (minibar trong trang + Chrome Side Panel): nút Speed hiện hệ số (vd "1.0x") mở slider WPM; nút Voices mở danh sách giọng; không có popup điều khiển rời |
| Chia text | Câu → dùng thư viện NLP **compromise** tách câu; câu dài >500 ký tự chẻ nhỏ theo dấu câu; gom lại thành chunk 50–250 ký tự; preload trước 4 câu sau, 2 câu trước |
| Kết luận Model B cho app native | **Feasible-with-limitations cho Model A** (hover → hit-test → hiện icon); **Model B (quét sẵn toàn bộ block + đặt sẵn icon) không thực tế với app native** — xem §9, §10 |

---

## 1. Nguồn đã đọc

### 1.1. File mẫu

- File `.crx` gốc: `~/workspace/user/files/NaturalReader_-_AI_Text_to_Speech_-_Cu__a_ha_ng_Chrome_tru__c_tuye__n_8.0.3.0.crx` (3,0 MB)
- Source đã giải nén: `~/workspace/research_notes/tts-speechify-naturalreader-20261116/src/naturalreader/`
- Manifest: **Manifest V3**, `minimum_chrome_version: 96`, quyền: `scripting, activeTab, contextMenus, storage, tts, ttsEngine, webNavigation, tabs, sidePanel`, `host_permissions: <all_urls>`.

### 1.2. Cách đọc source

Hầu hết file JS trong `.crx` là **minified một dòng** (không có xuống dòng). Tôi đã beautify bằng `js-beautify` ra `/tmp/nr/` để đọc. Các file đã đọc kỹ:

| File | Vai trò |
|---|---|
| `manifest.json`, `main.js` | Khai báo extension; service worker import 19 module background |
| `background/extension.js` | Điều phối chung: click icon toolbar, mở/đóng Side Panel, hotkey, context menu, theo dõi tab |
| `background/reading-bar.js` | Settings mặc định + inject reading bar / selection icon / email & GPT icon |
| `background/script-injector.js` | Inject content script theo loại trang (html, Google Docs, Kindle, Gmail, Outlook, ChatGPT…) |
| `background/reader.js` | Điều phối play/pause/stop/readSelection ở tầng background |
| `background/online-tts.js` | Gọi API TTS cloud (fetch + Cognito), xử lý speech marks, streaming WebSocket |
| `background/auth.js`, `background/ocr.js`, `background/pdf.js`, `background/convert.js`, `background/capture.js` | Đăng nhập/license; OCR ảnh/PDF qua API; mở PDF trên web app; convert |
| `injected/nr-ext-dom/nr-ext-dom-detector.js` | **Selection icon**: hiện icon sau khi bôi chọn |
| `injected/nr-ext-dom/nr-ext-dom-controller.js` | Highlight câu/từ, auto-scroll, click-to-read, relocate |
| `injected/nr-ext-dom/nr-ext-gmail-icon.js`, `nr-ext-gpt-icon.js`, `nr-ext-outlook-icon.js` | Icon hiển thị sẵn trong Gmail / ChatGPT / Outlook |
| `injected/nr-ext-reader/reader.js` | State machine đọc: init/loading/reading/pause; điều khiển câu hiện tại, next/prev |
| `injected/nr-ext-reader/tts.js` | **3 TTS engine**: `WindowSpeechEngine` (free), `ChromeTtsEngine`, `OnlineTtsEngine` (cloud) |
| `injected/nr-ext-reader/tts-stream.js` | Streaming audio qua WebSocket + AudioContext, gapless scheduling, jitter buffer |
| `injected/nr-ext-reader/tts-text.js` | Tách câu (compromise NLP), chẻ/gom chunk, chuẩn hoá text, preview text đa ngôn ngữ |
| `injected/nr-ext-reader/voices.js` | Quản lý 4 tier giọng (free/lite/plus/pro), preset, auto-select voice theo ngôn ngữ |
| `injected/nr-ext-text-processor/html-doc.js` | Phát hiện block text trên trang HTML, bọc từng câu vào thẻ `<nr-sentence>` |
| `injected/nr-ext-text-processor/{google-doc,kindle,gmail-doc,outlook-mail,gpt,google-drive-doc}.js` | Text processor riêng cho từng loại trang |
| `injected/nr-ext-reading-bar/nr-ext-reading-bar.js` + `.html` | **Minibar**: thanh player nổi trong trang (Shadow DOM, kéo thả được) |
| `injected/nr-ext-reading-bar/sidepanel.js` + `sidepanel.html` | **Chrome Side Panel**: player + 6 view (reading/voices/add/mobile/settings/account) |
| `injected/nr-ext-quick-start/*` | Overlay onboarding lần đầu |
| `libs/compromise.min.js` | Thư viện NLP tách câu (bundle kèm) |

### 1.3. Nguồn web đối chiếu (đánh dấu rõ)

- Help Center NaturalReader: "Click to Read: After the extension is activated, click any readable text on the webpage to begin listening from there"; "Highlight to Read: highlight words on the webpage, then click on an icon (or right click > Read Selection)"; "Auto-scroll: Follows the current text being read aloud by scrolling the page down automatically. On by default"; "Clickable text will be highlighted when the pointer hovers over it."
  - Nguồn: https://help.naturalreaders.com/en/articles/6106379-chrome-extension-reader-settings và https://help.naturalreaders.com/en/articles/9788570-settings-chrome-extension
- Khớp hoàn toàn với những gì đọc được trong source → các mô tả hành vi dưới đây có căn cứ kép (source + docs công khai).

### 1.4. Những chỗ đọc source chưa kết luận được (nói thẳng)

- `sidepanel.js` (~121 KB minified) chỉ đọc qua grep có mục tiêu — danh sách đầy đủ các preset tốc độ trong side panel chưa liệt kê hết được (biết chắc có slider 0–100 map sang WPM, xem §7).
- Endpoint lấy **danh mục giọng lite/plus/pro** từ server chưa xác định được trong các file đã mẫu — `voices.js` gộp các map `freeVoices/liteVoices/plusVoices/proVoices` được nạp lúc runtime; file `.crx` không chứa file JSON danh mục giọng tĩnh (chỉ có preview text + URL ảnh đại diện giọng trên S3). Xem §6.5.
- `ChromeTtsEngine` (dùng `chrome.tts`) có vẻ là **dead code**: engine free thực tế được khởi tạo là `WindowSpeechEngine` (`const freeTts = new WindowSpeechEngine`); không thấy chỗ nào khởi tạo `ChromeTtsEngine` trong các file đã đọc.
- Chi tiết tạo span từng từ (`setWordForMainTextAndCC` trong `nr-ext-text-processor.js`) và hành vi đổi giọng giữa lúc đang đọc chưa trace end-to-end — chỉ suy ra từ các code path liên quan (ghi rõ "suy luận" ở những chỗ đó).
- Không cài/chạy thử extension live (đọc source đã đủ rõ theo yêu cầu task).

---

## 2. TTS được hiển thị như thế nào (player UI, toolbar, floating panel)

NaturalReader có **hai mặt phẳng player song song**, cộng thêm icon toolbar:

### 2.1. Minibar — thanh player nổi chèn trong trang web

- File: `injected/nr-ext-reading-bar/nr-ext-reading-bar.html/.js`; chèn vào trang qua `chrome.scripting.executeScript`, dùng **Shadow DOM** (`e.shadow.getElementById(...)`) để CSS không đụng trang gốc.
- **Kéo thả được** khắp trang (thư viện `displace.js`); có 2 chế độ `min`/`max` (thu gọn / mở rộng).
- Bố cục (chế độ mở rộng):
  - **Nút Play/Pause hình tròn**, bên trong có **vòng progress** (`ext-progress-overlay`) chạy theo tiến độ đọc; hai nhãn thời gian **còn lại / tổng** (`00:00`).
  - Nút **Rewind / Forward** (lùi/tới một câu; số câu nhảy cấu hình được: `backwardSkipNumber: [1, 2]`, `forwardSkipNumber: [2, 4]`).
  - Hàng submenu 5 nút: **Voices** (kèm ảnh đại diện giọng đang chọn) · **Speed** (hiện hệ số, vd "1.0x") · **CC** (closed captions: hiện câu đang đọc dạng text) · **Immersive** (chế độ đọc tập trung) · **Settings**.
  - Bấm mỗi nút mở một **menu trượt ra ngay trong minibar** (danh sách giọng, slider tốc độ dọc, cài đặt…), không mở cửa sổ mới.
  - Nút đóng (X) + tay kéo (drag handle).
- **Immersive Reader** (chế độ đọc tập trung): một lớp phủ riêng trong trang (`nr-immersive-reader`) chứa toàn bộ text đã parse, phân trang (prev/next page, nhập số trang), chỉnh cỡ chữ (slider 12–40, mặc định 18), giãn dòng, làm mờ text chưa đọc (`isImmersiveBlurText`). Câu đang đọc trong immersive view được highlight bằng class riêng `nr-immersive-s{N}` / `nr-immersive-s{N}w{M}`.

### 2.2. Chrome Side Panel — panel bên phải của trình duyệt

- File: `injected/nr-ext-reading-bar/sidepanel.html/.js`; khai báo `side_panel.default_path` trong manifest.
- Thanh nav trên cùng có **6 view**: Reading · Voices (hiện avatar/ảnh giọng đang chọn) · Add (thêm text/file) · Mobile · Settings · Account.
- View Reading: nút chọn giọng (ảnh đại diện), skip back/forward, nút **Play/Pause lớn**, nút **Speed hiện "1.0x"** (bấm mở popup chỉnh tốc độ), menu user, nút Text Settings (chữ "T"), **thanh slider tiến độ** (kéo để nhảy tới % bài đọc), **slider âm lượng**.
- Panel và minibar **đồng bộ trạng thái** qua background (message `readerOnPlay/readerOnPause/setProgressTime…`); chỉ một tab được đọc tại một thời điểm (`beingReadTabId`); chuyển tab khác thì dừng tab cũ.

### 2.3. Toolbar icon + trạng thái

- Click icon extension trên toolbar: nếu panel đang mở → đóng; nếu đang đọc ở tab khác → dừng tab cũ; nếu minibar đang hiện → ẩn; ngược lại → mở Side Panel + inject reading bar (`background/extension.js`, hàm `actionOnClicked`).
- Icon đổi theo trạng thái: đang đọc → icon khác; đang load → **icon xoay** vẽ bằng `OffscreenCanvas` (16 vạch xoay, 30fps); lỗi → icon khác.
- Khi đang đọc mà user chuyển tab: content script nhận `tabOnActivated` để đồng bộ highlight; nếu tab đang đọc bị navigate → tự `stop()` + đóng panel.

### 2.4. Các điểm vào khác (entry points)

- **Context menu** (chuột phải): "Read Selection".
- **Hotkey** (khai báo trong manifest): `Ctrl+Shift+Y` / `Cmd+Shift+Y` mở reading bar; `Alt+P` play/pause; `Alt+S` đổi nhanh giữa 2 preset giọng; forward/rewind/stop; toggle hiện selection icon; "Go to Tab Being Read"; "Highlight to Read" (chưa gán phím mặc định, user tự gán).
- **Quick Start overlay**: lần đầu cài, hiện overlay hướng dẫn theo loại trang (webpage/PDF/Google Docs/email), có nút tắt.
- **Nút "Go to Tab Being Read"** để quay lại tab đang đọc dở.

---

## 3. Flow khi user hover qua một block text — có hiện icon loa không?

**Câu trả lời ngắn: không.** Trên trang web thường, NaturalReader **không hiện bất kỳ icon nào khi hover**. Có 3 cơ chế riêng biệt, cần phân biệt rõ:

### 3.1. Selection icon — icon chỉ hiện SAU KHI bôi chọn (không phải hover)

File: `injected/nr-ext-dom/nr-ext-dom-detector.js` (`NRDomDetector`).

- Trigger: sự kiện **`mouseup`** trên `window`. Sau khi thả chuột, code kiểm tra `window.getSelection()`:
  - Phải có text được chọn (không phải click rỗng).
  - Độ dài selection phải **vượt ngưỡng tối thiểu** (mặc định **20 ký tự**, setting `LimitedReadSelectionCharAmount`, bật/tắt bằng `LimitedReadSelectionOn`). Selection ngắn hơn → không hiện icon. (Lưu ý nhỏ: `defaultSettings` ghi `LimitedReadSelectionOn: false` nhưng `initSettings` khi chưa có setting lưu thì mặc định `true` — hiệu lực thực tế theo `initSettings` là **bật**.)
  - Có thể tắt hẳn bằng setting `readIcon` (mặc định `true`) hoặc hotkey `toggleShowReadIcon`.
- **Vị trí:** đặt tại **vị trí chuột lúc thả** (`pageX + 5`, `pageY + 5`) — tức là neo theo chuột, **không neo theo mép block text**, không căn theo dòng/câu.
- **Hình thức:** một `<iframe>` 50×50 trong suốt, `z-index` cực lớn, bên trong là nút play 30×30 (ảnh `readicon.svg`). Dùng iframe để cô lập CSS/không bị trang gốc ảnh hưởng.
- **Ẩn khi nào:** tự ẩn sau **3 giây** (`setTimeout` 3000ms); ẩn ngay khi user bấm chuột xuống chỗ khác (`mousedown` → kiểm tra lại selection); ẩn khi click vào chính icon (đồng thời gửi message `readSelection`).
- **Click icon →** background inject reading bar + đọc **đúng đoạn đã chọn** (`readOption: "selection"`), sau đó (nếu bật Click to Read) có thể đọc tiếp tới cuối trang.
- Với Google Docs: selection được đọc qua overlay DOM đặc thù của Docs (`.kix-selection-overlay`, `.kix-canvas-tile-selection`) — xem §4.3.

### 3.2. Click to Read — hover chỉ highlight, không icon

File: `injected/nr-ext-dom/nr-ext-dom-controller.js` (`bindClickToReadEvents`).

- Sau khi extension đã được kích hoạt (đã đọc lần đầu, `isFirstRead === false`) và setting `isClickToRead` bật (mặc định `true`):
  - **Hover** một câu → câu đó được gắn class `.nr-onhover` (CSS: `background-color: #D5E5FE`, con trỏ `pointer`). Đây là **highlight xem trước**, không phải icon.
  - **Click** câu đó → đọc từ câu đó (`reader.readIndex(n)`), có debounce.
  - `mouseout` → gỡ class.
- Ngoại lệ: với Google Docs, khi reader đang `pause`/`init` thì hover/click câu bị bỏ qua (tránh xung đột với canvas của Docs).
- Đây là pattern gần nhất với "hover → hành động" của NaturalReader, nhưng affordance là **chính câu text đổi màu**, không phải icon nổi.

### 3.3. Icon hiển thị sẵn trong app riêng (ChatGPT / Gmail / Outlook) — xem §8.3

---

## 4. Phát hiện / bắt dòng hiện tại để hiện icon và highlight câu đang đọc

### 4.1. Phát hiện block text trên trang HTML (parse lúc bấm đọc)

File: `injected/nr-ext-text-processor/html-doc.js`. Đây là phần "block detection" của NaturalReader — nhưng lưu ý: nó chạy **một lần khi bắt đầu đọc** (hoặc khi text thay đổi), **không phải quét liên tục**.

1. **Thu thập block ứng viên** (`function r`): duyệt DOM từ `document.body`, giữ lại các element khớp:
   - Tiêu đề `h1–h6`, đoạn `p`, link `a[href]`, danh sách `ol/ul`, `dl`, `tbody` (bảng lớn), và các element có text node con dài ≥ 3 ký tự (`getInnerText ≥ 100` ký tự cho block chính, ≥ 3 cho block nhỏ).
   - Chỉ lấy element **đang hiển thị** (`:visible`) và `offset().left >= 0` (loại element ẩn/tràn màn hình).
   - Bỏ qua các tag trong `ignoreTags` (nav/script/style/sup/nút… — danh sách cụ thể nằm trong file text-processor, xem §4.4) và element `float: right` / `position: fixed`.
   - Đánh dấu block chứa nhiều đoạn con bằng data `read-aloud-multi-block` để đệ quy xử lý từng đoạn con.
2. **Cắt header/footer**: nếu tổng độ dài text thu được < 1000 ký tự thì lấy block dài nhất làm "nội dung chính"; ngược lại dùng thống kê **mean ± 2·stdev** độ dài các block để cắt các block đầu/cuối dài bất thường (thường là header/footer/nav) — hàm `a(e,t,n)` tính mean/stdev.
3. **Trích text**: `TreeWalker` (`NodeFilter.SHOW_TEXT`) đi qua từng text node của block, nối `innerText`; đồng thời **ghi nhớ danh sách text node gốc** để lát nữa map ngược câu → vị trí DOM.
4. **Tách câu + bọc câu** (xem §5): mỗi câu được bọc trong một element tự chế `<nr-sentence class="nr-s{N}" id="nr-s{N}" page="0">` bằng cách `splitText` tại đúng ranh giới câu (hàm `g` so khớp từng ký tự giữa text node gốc và câu đã xử lý). Nhờ đó mỗi câu có **địa chỉ DOM ổn định** (`nr-s0`, `nr-s1`, …) để highlight/cuộn/click.
5. **Chống parse trùng**: lưu 1000 ký tự đầu của `document.body.innerText` (`docInnerText`); nếu không đổi thì reject `ERR_ALREADY_PARSED`. Nếu text bị sửa trong lúc đọc → lỗi `ERR_TEXT_EDITED` → parse lại từ đầu.
6. **Đọc theo lựa chọn (selection)**: lấy `Range` của selection, `splitText` tại `startOffset`/`endOffset`, chỉ bọc các câu nằm trong selection (đánh dấu class `nr-selected`), sau đó parse thêm phần trước/sau selection để có thể "đọc tiếp tới cuối trang" (hàm `d`/`h` xử lý phần trước/sau theo `compareDocumentPosition`).

### 4.2. Theo dõi câu/từ đang đọc (highlight + cuộn)

File: `injected/nr-ext-dom/nr-ext-dom-controller.js`.

- **Câu hiện tại** = `reader.currReadIndex` (chỉ số trong mảng `textsForTts`). Khi chuyển câu:
  - `highlightSentence(index)`: gửi message `highlightSentence` cho background (để đồng bộ side panel) + gắn class vào element `.nr-s{N}` trong trang: `nr-sentence-highlight-<màu>` + `nr-highlighted-sentence`; đồng thời gỡ class ở câu cũ (`removeHighlight`).
  - `highlightWord(sentenceIndex, wordIndex, word)`: gắn class `nr-word-highlight-<màu>` + `nr-highlighted-word` vào span `.nr-s{N}w{M}`.
  - 4 bảng màu (file `injected/nr-ext-dom/nr-ext-dom.css`): **light** (câu nền `#B5C687` xanh lá nhạt, chữ `#0d3b55`; từ nền `#0d3b55`, chữ `#E7ECEE`) · **dark** (câu `#0d3b55`, từ `#EFD369`) · **ice** (câu `#F1F3F4`, từ `#0F7895`) · **warm** (câu `#ebdcb2`, từ `#563e20`).
  - Setting `beHighlighted` (mặc định `["sentence", "word"]`) cho phép chỉ highlight câu, chỉ highlight từ, hoặc tắt hẳn; `highlightColour` mặc định `"light"`.
- **Auto-scroll**: sau khi highlight, kiểm tra câu có trong viewport không (`getBoundingClientRect` trong khung hình). Nếu có → `scrollIntoView({ behavior: "smooth", block: "center" })`. Nếu không → hiện **nút "Relocate"** trong minibar để user bấm nhảy tới câu đang đọc. Google Docs dùng công thức cuộn riêng theo viewport của Docs (`.kix-appview-editor`).
- **Đồng bộ immersive view**: mọi thao tác highlight/cuộn được mirror sang bản sao text trong immersive reader với class `nr-immersive-s{N}`.
- **Dừng/tạm dừng** → gỡ toàn bộ highlight (`readerOnPause`/`readerOnStop` → `removeHighlight()`).

### 4.3. Các loại trang đặc biệt (mỗi loại một text processor riêng)

| Loại trang (`docType`) | Cách lấy text |
|---|---|
| `html` (mặc định) | Như §4.1 |
| `googleDoc` | Đọc overlay DOM của Google Docs (`.kix-canvas-tile-selection`, `.kix-selection-overlay`, `kix-lineview`), dùng `google-docs-annotated.js` inject ở `document_start` (world `MAIN`) |
| `googleDriveDoc` / `googleDrivePreview` | `.drive-viewer-paginated-scrollable` |
| `kindle` | Đọc Cloud Reader của Amazon, có `highlightLocation` + `getTextsOfNextPage` (tự lấy trang sau khi đọc hết trang) |
| `gmail` | Parse thread email; có icon tai nghe riêng (§8.3) |
| `outlookMail` | Parse mail Outlook web |
| `gpt` | Parse message ChatGPT; có nút play riêng (§8.3) |

Việc inject file nào do `background/script-injector.js` quyết định theo hostname/URL (`getDocType` trong `injected/utils.js`).

### 4.4. Ghi chú về "caret tracking" / DOM Range mapping

- NaturalReader **không theo dõi caret** (vị trí con trỏ soạn thảo) và **không dùng `document.caretRangeFromPoint`** để bắt dòng dưới chuột. Cơ chế của nó là: **parse trước toàn bộ trang → bọc câu → mọi thao tác sau đó chỉ là tra cứu theo index** (`nr-s{N}`).
- Với selection, nó dùng `window.getSelection()` + `Range.startOffset/endOffset` + `splitText` để cắt đúng ranh giới selection — đây là phần duy nhất dùng DOM Range mapping, và chỉ chạy khi user đã chọn text.
- Hệ quả: hover một dòng bất kỳ **không** cho extension biết "đang ở câu nào" nếu chưa parse; nhưng sau khi parse xong, hover/click câu nào cũng biết ngay index qua `id="nr-s{N}"`.

---

## 5. Chuyện gì xảy ra khi audio bắt đầu phát + cách chia text thành chunk

### 5.1. Luồng phát (state machine)

File: `injected/nr-ext-reader/reader.js` (`Reader`).

- Trạng thái: `init` → `parsing` → `loading` → `reading` ⇄ `pause` → `init` (stop). `playId` (tăng mỗi lần play) để huỷ các request cũ khi user bấm next/prev nhanh (`ERR_INVALID_PLAY_ID`).
- `play()`:
  1. Đồng bộ settings từ background (`getReadingBarSettings`, race với timeout 3s).
  2. Chọn engine theo `voiceType`: `"free"` → `freeTts` (Web Speech); còn lại → `onlineTts` (cloud). Đổi engine thì `pause` engine cũ trước.
  3. Nếu là lần đọc đầu (`isFirstRead`) hoặc text chưa parse → `getTextsFromPage(readOption)` (§4.1) → mảng `textsForTts: [{original, processed}]`.
  4. Tính tổng thời gian đọc ước lượng: `tổng ký tự / (speed/180 × 5)` giây → gửi `setProgressTime` cho minibar/side panel.
  5. Gọi `tts.play(processedText, currReadIndex, onEvent)`; callback `onEvent` nhận các event `start` / `word` / `end` / `pause` / `loading` / `error`.
  6. Event `start` → state `reading`, `setBeingRead` (background ghi nhận tab đang đọc), `readerOnPlay()` → highlight câu (§4.2) + đổi icon toolbar + minibar chuyển nút play→pause.
  7. Event `end` → tự `playNext()`: tăng `currReadIndex`, đọc câu tiếp; hết câu mà `readOption === "all"` → sang trang tiếp theo (`scrollToAdjacentPage`: Kindle/Google Docs/PDF có khái niệm trang; trang HTML thường thì kết thúc → `stop()`).
  8. Event `word` → `highlightWord` (§4.2) + cập nhật CC.
- `forward()`/`backward()`: tăng/giảm `currReadIndex` (nhảy 1 câu; giữ phím thì nhảy theo `forwardSkipNumber`); ở biên thì chuyển trang hoặc quay về đầu/cuối.
- `readIndex(n)` (click-to-read): dừng câu hiện tại, đặt `currReadIndex = n`, đọc từ đó; đồng thời `scrollTo(n)`.
- **Đọc lại từ đầu sau khi chọn đoạn**: nếu `readOption === "selection"` và đọc hết đoạn chọn mà Click-to-Read đang bật → tự chuyển sang đọc tiếp phần còn lại của trang (`readOption = "all"`, `currReadIndex = 0`).

### 5.2. Chia text thành chunk đọc được

File: `injected/nr-ext-reader/tts-text.js` (`TtsText`).

1. **Tách câu — `getNlpSentences(text)`**: dùng thư viện **compromise** (bundle `libs/compromise.min.js`): `nlp(text).sentences().data().map(s => s.text.trim())`. Fallback: nếu lỗi thì coi cả đoạn là 1 câu.
2. **Chẻ câu dài — `e(sentence, 500)`**: câu dài hơn 500 ký tự được chẻ tại điểm ngắt tốt nhất trong 500 ký tự đầu, ưu tiên: dấu kết thúc câu (`? ! ¿ ¡ 。 ～ …… ！ ？`) → nếu không có thì dấu phẩy/chấm phẩy/hai chấm (`, ; : ， ； ：`) → cuối cùng là khoảng trắng. Hàm `n(text, maxLen)` tìm vị trí ngắt.
3. **Gom chunk — `i(pieces)`** (`processSentencesByLength`): gom các mảnh nhỏ thành chunk **50–250 ký tự** (cộng dồn cho tới khi thêm mảnh nữa sẽ vượt 250; chunk dưới 50 ký tự được giữ lại chờ gom tiếp). Kết quả: mỗi phần tử của `textsForTts` là một "câu đọc" dài vừa phải — đủ ngắn để gọi API nhanh, đủ dài để giọng đọc tự nhiên.
4. **Chuẩn hoá text — `processText(text)`**:
   - Thay thế phát âm (pronunciation editor): danh sách `pe.peList`/`pe.peMap` (user tự định nghĩa từ → cách đọc), áp bằng RegExp có/không word-boundary.
   - Chuẩn hoá dấu câu: `<br>` → `". "`; bỏ ký tự zero-width/diều khiển; dấu nháy cong → thẳng (`'`, `"`); dấu full-width (`！？，。：；`) → half-width; `…` → `.`; `—` → `-`; `etc`/`&c.` → `etc.`
   - Chuẩn hoá bằng compromise: `nlp(text).normalize({case:false, punctuation:false, contractions:false, unicode:false, numbers:false})`.
5. **Chunk 60 ký tự — `getTextChunks(text)`**: cắt text thành đoạn 60 ký tự kèm `charIndex` — dùng cho cơ chế ước lượng thời gian từng từ khi không có speech marks (§6.4).
6. **Preview text**: file chứa sẵn câu mẫu cho nhiều ngôn ngữ (en/vi/zh/ja/ko/…) để user nghe thử giọng trong danh sách giọng.

### 5.3. Preload (đọc trước)

- `OnlineTtsEngine` giữ cache `preloads`: khi đang đọc câu `i`, nó **tải trước audio của 4 câu sau và 2 câu trước** (`numPreloads = {prev: 2, next: 4}`), xoá các câu ngoài cửa sổ này khỏi cache. Nhờ đó chuyển câu gần như không có độ trễ, kể cả khi dùng cloud TTS.
- Mỗi preload là một Promise "có trạng thái" (`isPending()/isFulfilled()`) để biết audio đã sẵn sàng chưa; nếu chưa sẵn thì hiện trạng thái `loading` (icon xoay).
- Lỗi ở một câu (hết hạn mức, lỗi license) → thử lại tối đa 3 lần, có fallback đổi voice source `vtx ↔ gmn` (xem §6.3).

---

## 6. Full audio pipeline

### 6.1. Sơ đồ tổng thể

```
Trang web (content script, world ISOLATED)
  └─ Reader (state machine) ──> chọn engine theo voiceType
        ├─ free ──> WindowSpeechEngine ──> window.speechSynthesis (giọng OS/trình duyệt)
        │              (ChromeTtsEngine dùng chrome.tts — có trong code, dường như không được dùng)
        └─ online ──> OnlineTtsEngine ──> background (service worker)
                        ├─ fetch POST tới AWS API Gateway (mp3) ──> <audio> / blob URL
                        ├─ WebSocket streaming (vtx/gmn) ──> AudioContext, gapless
                        └─ speech marks (header x-smark / API riêng) ──> word timer 50ms
```

### 6.2. Giọng đọc — 4 tier, nhiều nguồn

File: `injected/nr-ext-reader/voices.js` (`Voices`).

| Tier (`voiceType`) | Nguồn giọng | Ghi chú |
|---|---|---|
| `free` | **Giọng hệ thống**: `window.speechSynthesis.getVoices()` (giọng OS: Apple/Google/Microsoft/acapela… tùy máy) | Không cần tài khoản, không giới hạn; chất lượng tùy máy |
| `lite` | Cloud NaturalReader, gói Lite | Giọng "premium lite"; hết hạn mức ngày → tự hạ xuống `free` + hiện thanh thông báo |
| `plus` | Cloud, gói Plus | Mặc định `voiceType: "plus"` trong defaultSettings |
| `pro` | Cloud, gói Pro | Cao nhất |

- **Nguồn giọng trong cloud** (trường `source` của mỗi voice): `aca` (Acapela), `gmn` (Google Gemini — model `25flash-default`, có bản `25lite` cho streaming), `vtx` (Vertex AI), `xai` (xAI), `openai` (OpenAI), `ms-cloned` / `cloned` (giọng clone của user — `voice-cloning.js`), `st` (đặc biệt: **không có speech marks** — xem §6.4).
- Mỗi voice có: `id`, `name`, `language` (vd `en-US`), `languageName` (vd "English (US)"), `gender` (`male`/`female` — có filter theo giới tính trong danh sách giọng), `type`, `source`. Key giọng hiển thị dạng `"Tên Ngôn ngữ Tier"` (vd `"Jane English (US) lite"`).
- **Chọn giọng mặc định theo ngôn ngữ**: `defaultFreeVoice` / `defaultLiteVoice` / `defaultPlusVoice` / `defaultProVoice` + bảng `preferredVoicesByLang` (ghi nhớ giọng user thích cho từng ngôn ngữ + tier).
- **Auto-select voice** (setting `isAutoSelectVoice`, mặc định tắt): khi đọc đoạn chọn/context-menu, tự detect ngôn ngữ bằng `chrome.i18n.detectLanguage` (lấy 1000 ký tự đầu) rồi chọn giọng phù hợp (`voices.autoSelectVoice(lang)`).
- **Preset giọng**: 2 preset (`preset1Voice`, `preset2Voice`); hotkey `Alt+S` đổi nhanh giữa preset 1 và giọng đang chọn.
- **Nghe thử giọng**: `reader.preview(voice)` — với giọng Plus thì phát file mp3 mẫu trên S3 (`formPlusVoiceUrl` → `wwwnaturalreaderscom.s3.amazonaws.com/pw-previews-…`); với giọng khác thì đọc câu mẫu (`getPreviewText`) bằng chính engine đó.
- **Voice cloning**: user có thể tạo giọng clone của mình (community/cloned voices), có mục riêng trong danh sách giọng, kèm filter (giới tính, tag, locale, sắp xếp).

### 6.3. API tạo audio (cloud) — backend của chính NaturalReader

File: `injected/nr-ext-reader/tts.js` (`OnlineTtsEngine`) + `background/online-tts.js`.

- **Endpoint theo tier** (AWS API Gateway, region `us-east-1`):
  - Premium: `https://moxu0s1jnk.execute-api.us-east-1.amazonaws.com/prod-wpm/tts`
  - Plus: `https://2poo4vxwjc.execute-api.us-east-1.amazonaws.com/prod-wps/tts`
  - Pro: `https://lsni5ansg0.execute-api.us-east-1.amazonaws.com/prod-wpro/tts`
- **Tham số query** (hàm `I()` build URL): `&v=<source>` (aca/gmn/vtx/xai/openai…), `&r=<voice id>`, `&s=<speed WPM>` (chỉ khi streaming/backend), `&lo=<locale>` (vd `zh-CN` → `cmn-CN` cho vtx), `&gdr=f|m` (gender), `&model=25flash-default` (Gemini), `&ins=Accent:<lang>` (OpenAI), `&e=<email>&l=<license>` (xác thực/license), `&vn=<extension version>`, `&app=ext`, `&sm=true|false` (có lấy speech marks không), `&ca=true|false` (có phải câu preview không).
- **Xác thực**: background dùng AWS Cognito Identity (`cognito-identity.us-east-1.amazonaws.com`) để lấy credential rồi ký request (hàm `m()` trong `background/online-tts.js`); mỗi request tăng bộ đếm sử dụng (`incr-tts` endpoint).
- **Gọi API**: content script gửi message `sendTtsRequest` sang service worker; service worker `fetch POST` (body là text + tham số), nhận về **audio/mpeg blob** → chuyển thành `data:` URL → content script tạo `blob:` URL → gán vào `<audio>` element (`new Audio()` / `document.createElement("AUDIO")`).
- **Retry/fallback**: tối đa 3 lần; nếu source là `vtx` lỗi thì thử `gmn` và ngược lại; lỗi `1005` (hết hạn mức ngày) / `1006` / `ERR_LICENSE_INVALID` → không retry mà hạ tier giọng + báo user; lỗi `429 Too Many Requests` → cờ `hasTooManyRequestsError`, delay 500ms rồi gọi lại.
- **Streaming** (chỉ voice `vtx`/`gmn`, file `tts-stream.js`): mở WebSocket tới `wss://xujs4waht8.execute-api.us-east-1.amazonaws.com/prod-wpro/` (lấy URL qua `getStreamingUrl`); server trả từng frame audio (WAV/PCM) + speech marks cuối luồng; client decode bằng `AudioContext.decodeAudioData` (WAV) hoặc tự decode PCM16 → `AudioBuffer`, rồi **schedule gapless** bằng chuỗi `AudioBufferSourceNode` nối tiếp nhau qua `GainNode` (âm lượng). Có **bộ điều khiển jitter buffer tự thích nghi** (target 300–2000ms, tăng penalty khi underrun, tạm `suspend()` AudioContext khi buffer cạn dưới 40ms, `resume()` khi đủ 80% target). Streaming lỗi → fallback về fetch thường.
- **Vượt qua chính sách autoplay của Chrome**: nếu trang chặn phát audio (`MEDIA_ELEMENT_ERROR`), extension mở một **tab ẩn được ghim** (`assets/player.html`) làm "audio player" và điều khiển từ xa qua message (`playerSetSrc/playerPlay/playerSetVolume/playerLoad…`); tab này tự đóng khi hết lỗi.

### 6.4. Theo dõi vị trí phát để highlight từ/câu

Ba cơ chế, theo thứ tự ưu tiên:

1. **Speech marks từ server (chính xác nhất)**: server trả kèm header **`x-smark`** (base64 của JSON `{text, language, segments: [{words: [{text, start}]}]}` hoặc định dạng legacy `word|start|end|time` nối nhau) — mỗi từ có `time` (ms) tuyệt đối trong audio. Ngoài ra có API riêng `https://r0lk19wn54.execute-api.us-east-1.amazonaws.com/prod/speech-marks` để lấy marks cho câu đã có audio. Hàm `processSpeechMarks` parse + kiểm tra độ khớp text (Levenshtein similarity < 0.5 → bỏ marks, tránh highlight lệch). Timer `setInterval(…, 50ms)` so `performance.now() - startTime` với `mark.time / rate` → bắn event `word` → highlight từ (§4.2). Lưu ý: voice source `st` **không có marks** (`l(source)` trả `false` cho `"st"`) → không highlight từ được.
2. **Web Speech boundary events (giọng free)**: `SpeechSynthesisUtterance.onboundary` (`name === "word"`, `charIndex`/`charLength`) → event `word` trực tiếp, không cần timer. Workaround bug Chrome: với giọng Google, cứ 5 giây lại `pause()`/`resume()` một lần để tránh Chrome tự dừng đọc đoạn dài.
3. **Ước lượng khi không có marks** (fallback): tính tổng thời lượng audio (`audio.duration`) chia đều theo độ dài text; hoặc ước lượng bằng **số âm tiết** — đếm cụm nguyên âm trong từ (`/[aeiouy]{1,2}/g`, trừ hậu tố `-es`/`-ed`/`-e`), thời lượng = `số âm tiết / (0.95 × WPM) × 60 × 1000` ms. Độ chính xác chỉ tương đối — highlight từ có thể lệch nhẹ so với tiếng thực tế.

### 6.5. Điều khiển giọng — tốc độ, giọng, nam/nữ

- **Tốc độ**: setting `speed` tính bằng **WPM** (words per minute), mặc định **180**, slider **50–900 WPM** (bước 5), hiển thị hệ số = `WPM / 180` (1.0x = 180 WPM, tối đa 5.0x). Áp dụng:
  - Giọng free: `utterance.rate = speed/180` (riêng giọng Google: nén đường cong trên 1x: `1 + (rate-1)/4` vì giọng Google đọc nhanh sẵn).
  - Cloud audio: `audio.playbackRate = speed/180` (đổi tốc độ phát của file mp3) và chia thời gian marks cho cùng hệ số.
  - Tốc độ dưới 40 WPM bị ép về 180 (tránh giá trị vô lý).
- **Chọn giọng**: danh sách giọng trong minibar/side panel có ô tìm kiếm, lọc theo ngôn ngữ, **giới tính (male/female)**, tag, locale; giọng đã ghim (pinned) lên đầu; giọng clone/community có mục riêng; đổi giọng áp dụng cho câu đang đọc trở đi (suy luận từ code path `updateVoice` → `setReadingBarSetting("selectedVoice")` + preload lại; chưa trace end-to-end — xem §1.4).
- **Âm lượng**: setting `volume` mặc định `0.75` (slider 0.2–1.0 trong minibar); riêng giọng Acapela (`aca`) bị nhân 0.5 (giọng này to sẵn).
- **Nam/nữ**: là thuộc tính của voice (`gender`), chọn qua filter trong danh sách giọng; không có nút toggle nam/nữ nhanh. Tham số `&gdr=f|m` được gửi kèm request TTS.
- **Tự động đổi giọng theo ngôn ngữ**: `isAutoSelectVoice` (mặc định tắt) — xem §6.2. Không có "autoswitch giữa 2 ngôn ngữ trong cùng một bài đọc" (chỉ detect một lần cho đoạn chọn lúc bắt đầu).

### 6.6. OCR (liên quan, ngắn gọn)

- `background/ocr.js`: OCR qua API riêng `https://ytdvktzcdf.execute-api.us-east-1.amazonaws.com/prod/ocr` — dùng cho ảnh/PDF user upload lên web app NaturalReader (không phải OCR màn hình thời gian thực). Có kiểm tra license (`canOCR`: chỉ một số loại license mới được OCR ảnh).
- PDF: extension không đọc trực tiếp PDF viewer của Chrome; `background/pdf.js` mở PDF trên trang web NaturalReader để đọc/upload.

---

## 7. Điều khiển nhanh trong lúc đang phát (speed/voice)

**Kết luận: mọi điều khiển nhanh đều nằm ngay trong player đang chạy — không có cửa sổ/popup điều khiển rời.**

| Điều khiển | Vị trí | Hành vi khi đang phát |
|---|---|---|
| **Speed** | Nút "Speed" trong minibar (hiện hệ số, vd `1.0x`) → mở menu slider **dọc** 50–900 WPM + nút +/− + nhãn hệ số và WPM | Đổi tốc độ **áp dụng ngay lập tức**: `audio.playbackRate` đổi + timer marks chia lại theo hệ số mới; không cần dừng/đọc lại |
| **Speed (side panel)** | Nút "1.0x" trên thanh player → popup slider 0–100 (map sang WPM) | Như trên |
| **Voices** | Nút "Voices" trong minibar (ảnh đại diện giọng) → menu danh sách giọng (tìm kiếm, lọc ngôn ngữ/giới tính/tag) | Đổi giọng áp dụng từ câu hiện tại trở đi; preload cache được nạp lại cho giọng mới (suy luận — xem §1.4) |
| **Voices (side panel)** | Nút ảnh giọng trên thanh player + tab Voices riêng | Như trên |
| **Preset giọng nhanh** | Hotkey `Alt+S` | Đổi nhanh giữa preset 1 và giọng đang chọn, không cần mở menu |
| **Play/Pause, Rewind/Forward** | Nút tròn trong minibar + thanh player side panel | Dừng/tiếp tục; lùi/tới 1 câu (hoặc 2/4 câu khi giữ) |
| **Tiến độ** | Vòng progress quanh nút play (minibar) + slider tiến độ (side panel) | Kéo slider để nhảy tới % bài đọc |
| **Volume** | Slider trong menu Settings của minibar + slider trong side panel | Áp dụng ngay (`audio.volume` / `GainNode`) |
| **CC** | Nút "CC" trong minibar | Hiện/ẩn dòng text câu đang đọc (closed captions) |
| **Immersive** | Nút "Immersive" trong minibar | Chuyển sang view đọc tập trung, giữ nguyên vị trí câu đang đọc |

**Đánh giá cho TransPeek (ý tưởng "hover icon loa → mở tab 2/3 chỉnh nhanh"):** pattern của NaturalReader chứng minh **điều khiển ngay trong player đang chạy là đủ và nhanh nhất** — user không phải rời khỏi flow nghe. Điểm khác biệt: player của NR là một thanh riêng (minibar/side panel), còn TransPeek chỉ có một panel popup nhỏ → việc nhét speed/voice vào **tab 2/3 của chính panel** (hiện khi hover icon loa lúc đang đọc) là tương đương về UX, và gọn hơn là mở thêm một cửa sổ điều khiển rời kiểu NR (NR không làm vậy). Chi tiết so sánh xem báo cáo tổng hợp `recommendation.md` (do task tổng viết).

---

## 8. Icon model — NaturalReader dùng Model A, Model B, hay cả hai?

### 8.1. Kết luận

| Model | NaturalReader có dùng không? | Ở đâu |
|---|---|---|
| **Model A** (hover block → icon hiện) | **Không, với nghĩa chặt.** Không có flow nào mà user hover một block text rồi icon loa hiện ra | — |
| **Model A-biến thể** (hover → highlight, click → đọc) | **Có** — Click to Read (§3.2) | Mọi trang web sau khi đã parse |
| **Selection-triggered** (bôi chọn → icon hiện tại chỗ chuột) | **Có — đây là cơ chế icon chính** (§3.1) | Mọi trang web |
| **Model B** (icon hiển thị sẵn trong block, không cần hover/bấm gì) | **Có, nhưng CHỈ trong 3 app riêng** (§8.3) | ChatGPT, Gmail, Outlook |

### 8.2. Vì sao không có Model A/B chung cho mọi trang web?

- Extension **không quét DOM để tìm block text khi trang vừa load**. Nó chỉ parse khi user **chủ động bấm đọc** (icon toolbar / hotkey / selection icon / context menu). Lý do thực tế: quét + bọc câu cho toàn bộ trang tốn CPU, làm chậm trang, và dễ vỡ layout (phải `splitText` + chèn element lạ vào DOM gốc).
- Icon duy nhất "nổi trên trang" là selection icon — và nó neo theo **chuột**, không neo theo block, vì nó không biết (và không cần biết) block nào đang được chọn: nó chỉ cần biết "có selection hay không".
- Click-to-Read dùng hover-highlight thay cho icon vì sau khi parse, mỗi câu đã là một element có thể hover/click trực tiếp — thêm icon nổi là thừa và gây nhiễu thị giác.

### 8.3. Model B trong 3 app riêng — cách nó quyết định vị trí icon

Đây là phần gần nhất với "tự nhận diện blocktext và hiển thị sẵn icon" mà Manh mô tả — nhưng **mỗi app một đoạn code riêng**, không có cơ chế chung:

**ChatGPT** (`nr-ext-dom/nr-ext-gpt-icon.js`):
- Dùng **`MutationObserver`** theo dõi `document.body` (`childList + subtree`). Mỗi khi có message mới của assistant xuất hiện, tìm nút "copy" trong message đó rồi **chèn thêm một nút play tròn** (`nr-ext-gpt-read-btn`, class `nr-ext-play`, `border-radius: 50%`, `margin-top: 5px`) ngay sau nút copy trong cùng một group.
- Click nút → gửi `injectReadingBarAndRead` → đọc message đó.
- Vị trí = **bên trong chính message block**, cạnh các nút hành động có sẵn của ChatGPT. Không cần hover, không cần chọn text.

**Gmail** (`nr-ext-dom/nr-ext-gmail-icon.js`):
- Sau khi trang load, tìm các hàng email (class `.gH.acX.bAm` — hàng tiêu đề email trong list) rồi **chèn icon tai nghe SVG** (40×20, `margin-left: 20px`) vào cuối mỗi hàng.
- Click icon → đọc thread email đó (`injectReadingBarAndRead`, `op: "emailThread"`).
- Icon hiện sẵn trong list email, không cần hover vào email trước.

**Outlook** (`nr-ext-dom/nr-ext-outlook-icon.js`): tương tự Gmail — chèn icon vào giao diện mail Outlook web.

**Điểm chung của 3 trường hợp:** đều dựa vào **selector/classname riêng của từng app** (biết trước cấu trúc DOM của app đó) + `MutationObserver` để bắt nội dung load động. Không có thuật toán "phát hiện block text chung" nào cả. Nếu app đổi class name → icon vỡ (code có đoạn tự dọn icon cũ khi không còn khớp).

---

## 9. CÂU HỎI ĐÁNH GIÁ CHÍNH: Auto-detect block + pre-display icon có tích hợp được vào app NATIVE (TransPeek) không?

### 9.1. NaturalReader làm được nhờ cái gì (mà native không có sẵn)

| Khả năng | Trong trình duyệt (extension) | Trong app native |
|---|---|---|
| Đọc cấu trúc text | **DOM**: `querySelector`, `TreeWalker`, `Range`, `getBoundingClientRect()` — đồng bộ, rẻ, chính xác từng ký tự | **Accessibility tree** (macOS) / **UI Automation tree** (Windows) — IPC qua lại với app khác, chậm hơn nhiều, độ chi tiết tùy app |
| Chèn highlight vào text | Chèn thẳng element `<nr-sentence>` / class CSS vào DOM của trang | **Không thể** chèn vào text của app khác; chỉ có thể vẽ **hình chữ nhật overlay** đè lên trên (hoặc mirror text sang panel của mình) |
| Biết khi nào text đổi/cuộn | `MutationObserver`, `scroll` event, `resize` — miễn phí, chính xác | AX/UIA notification (thường thiếu/sai) hoặc **polling** (tốn CPU) |
| Vẽ icon lên trên app khác | DOM element/`iframe` trong cùng trang — z-index, tự theo scroll | Cửa sổ nổi riêng (floating window), phải tự tính vị trí + tự bám theo scroll/cuộn/resize của app khác |
| Phát audio | `<audio>` / Web Audio trong cùng process | Native audio (AVFoundation / MediaPlayer) — thực ra dễ hơn extension |

### 9.2. API nào làm block detection trên native?

**macOS (Swift/SwiftUI + AppKit + Accessibility):**
- `AXUIElementCreateSystemWide()` + `AXUIElementCopyElementAtPosition` (hit-test tại điểm chuột) hoặc duyệt cây: `AXUIElementCopyAttributeValues(kAXChildrenAttribute)` → các role `AXStaticText`, `AXTextArea`, `AXTextField`, `AXWebArea`…
- Lấy text: `kAXValueAttribute`; lấy khung: `kAXFrameAttribute` (tọa độ màn hình); lấy selection: `kAXSelectedTextRangeAttribute`; lấy rect của một range: `AXUIElementCopyParameterizedAttributeValue(kAXBoundsForRangeParameterizedAttribute)` (chỉ một số app hỗ trợ).
- Theo dõi thay đổi: `AXObserver` + notification `kAXValueChangedNotification`, `kAXSelectedTextChangedNotification`, `kAXFocusedUIElementChangedNotification` (nhiều app không phát đủ notification).
- Yêu cầu quyền **Accessibility** (user bật trong System Settings → Privacy & Security → Accessibility; app phải được trust `AXIsProcessTrusted()`).

**Windows (WinUI 3 + C# + UI Automation):**
- `IUIAutomation` (COM, namespace `System.Windows.Automation` / `UIAutomationClient`): `ElementFromPoint` (hit-test), duyệt cây bằng `TreeWalker`/`FindAll` với `ControlType.Text/Edit/Document`, pattern **`TextPattern`/`TextPattern2`**: `GetSelection()`, `GetVisibleRanges()`, `GetBoundingRectangles()` cho từng range.
- Theo dõi thay đổi: `StructureChangedEvent`, `FocusChangedEvent`, `TextChangedEvent` (hỗ trợ không đều giữa các app).
- Không cần quyền đặc biệt cho app cùng mức integrity; app chạy elevated (admin) thì UIA client thường không với tới được trừ khi có `uiAccess=true` + ký cert.

### 9.3. Icon có overlay lên trên cửa sổ app khác được không?

**Được, nhưng phải tự làm mọi thứ mà DOM cho miễn phí:**
- **macOS**: `NSPanel` borderless, `level = .floating` (hoặc `.statusBar` nếu muốn trên cả fullscreen), `isNonactivatingPanel = true`, `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]`; vị trí tính từ `AXFrame` (tọa độ màn hình, gốc dưới-trái — nhớ lật trục Y so với AppKit). Icon là một `NSButton` trong panel.
- **Windows**: cửa sổ WinUI 3 borderless, `SetWindowPos(HWND_TOPMOST)`, style `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (không cướp focus, không hiện taskbar); vị trí từ `BoundingRectangle` của UIA element.
- **Bám theo scroll**: khi app gốc cuộn, AXFrame/UIA rect đổi nhưng **không có event nào báo "đang cuộn" một cách đáng tin** cho mọi app. Thực tế phải **poll** (vd mỗi 150–250ms kiểm tra lại rect của element đang neo) hoặc đăng ký scroll notification (chỉ một số app phát). Polling toàn bộ cây thì đắt; polling chỉ 1 element đang neo thì rẻ. Icon phải tự ẩn khi rect ra khỏi màn hình / app đổi focus / window resize.
- **Z-order**: panel nổi nằm trên app khác, nhưng sẽ nằm dưới menu hệ thống, dưới màn hình khóa, và có thể bị app fullscreen "nuốt" nếu level không đủ cao; trên Windows, cửa sổ topmost vẫn thua cửa sổ topmost khác mở sau.

### 9.4. Cái gì vỡ khi app gốc không expose text layout?

| Trường hợp | Hậu quả |
|---|---|
| **Text vẽ custom** (canvas, OpenGL, game, đồ họa) | AX/UIA tree không có node text → **không detect được gì**. Vd: Google Docs vẽ text bằng canvas — chính NaturalReader cũng phải dùng overlay DOM riêng của Docs, không đọc được canvas |
| **Ảnh chứa text** (scan, screenshot, meme) | Không có text node → cần OCR (TransPeek đã có Vision OCR / Windows.Media.Ocr — nhưng OCR toàn màn hình liên tục là không khả thi về hiệu năng; chỉ OCR khi user chọn vùng) |
| **Electron app** (VS Code, Slack, Discord…) | Chromium có AX tree nhưng **chỉ khi accessibility được bật** (thường là khi có screen reader chạy); mặc định nhiều Electron app expose rất ít, hoặc chỉ expose khi user bật cờ `--force-renderer-accessibility`. VS Code khá tốt, Slack/Discord kém hơn |
| **Virtualized list** (danh sách dài chỉ render vài dòng) | Chỉ các dòng đang hiển thị mới có node → icon chỉ hiện cho dòng đang thấy; cuộn là mất node cũ, phải detect lại |
| **App không hỗ trợ `BoundsForRange` / TextPattern range rect** | Biết có text nhưng **không biết rect từng dòng/từ** → chỉ đặt được icon ở mép block, không đặt chính xác theo dòng; highlight từng từ gần như không làm được |
| **Secure text field / DRM / trình phát video** | Bị chặn hoàn toàn (AX trả rỗng / UIA từ chối) |
| **App chạy quyền cao hơn** (Windows admin) | UIA client thường không đọc được trừ khi có uiAccess |
| **Trình duyệt** (Chrome/Edge/Firefox) | Có AX/UIA tree qua renderer accessibility — nhưng **chậm và hay thiếu** so với DOM thật; đây chính là lý do extension trong trình duyệt làm tốt hơn hẳn app native ở mảng này |

### 9.5. Chi phí hiệu năng của việc quét liên tục

- **Quét toàn bộ cây AX/UIA liên tục là không khả thi**: mỗi lần đọc attribute là một IPC round-trip (macOS AX ~0.1–1ms/attribute, cây vài nghìn node → hàng trăm ms); UIA tương tự. Quét mỗi lần chuột di chuyển = lag + tốn pin + làm chậm cả app đích (app đích phải phục vụ AX request trên main thread của nó).
- **Cách duy nhất thực tế là event-driven + throttle** (giống hệt cách NaturalReader làm — parse khi cần, không quét liên tục):
  1. Theo dõi chuột toàn cục (macOS `CGEventTap` — cần quyền Accessibility + Input Monitoring; Windows `WH_MOUSE_LL` low-level hook).
  2. Chỉ hit-test khi chuột **đứng yên ~150–300ms** trên cùng một vị trí (debounce), không hit-test mỗi lần di chuyển.
  3. Cache cây AX/UIA theo cửa sổ, invalidate khi nhận focus/scroll notification hoặc khi rect cũ không còn khớp.
  4. Chỉ quét **cửa sổ đang active**, không quét toàn hệ thống.
- Ngay cả vậy, hit-test + lấy text/rect mỗi lần hover vẫn tốn vài chục ms — chấp nhận được cho một icon xuất hiện sau độ trễ nhỏ, nhưng **không đủ nhanh để "đặt sẵn icon cho mọi block"** như Model B.

### 9.6. Verdict

**Feasible-with-limitations.**

- **Model A (hover → hit-test → hiện icon): khả thi** trên cả macOS và Windows, với giới hạn: chỉ hoạt động tốt với app expose text qua Accessibility/UI Automation; cần quyền Accessibility (macOS) / hook chuột toàn cục; icon bám scroll bằng polling nhẹ; hiệu năng chấp nhận được nếu debounce + cache.
- **Model B (quét sẵn toàn bộ block + đặt sẵn icon, không cần hover): không thực tế với app native.** Nó chỉ khả thi trong DOM vì: (1) query toàn bộ block rẻ (một lần `querySelectorAll`), (2) `MutationObserver` báo thay đổi miễn phí, (3) icon là DOM node nên tự theo scroll/resize. Trên native, cả 3 điều này đều đắt hoặc không có. NaturalReader cũng **không làm Model B** cho trang web chung — chỉ làm cho 3 app riêng mà nó biết trước cấu trúc DOM (ChatGPT/Gmail/Outlook), tức là "Model B có điều kiện, theo app".
- **Khuyến nghị cho TransPeek:** làm **Model A** (hover → icon) như một **tùy chọn** bên cạnh flow bôi chọn hiện tại; không làm Model B quét sẵn. Riêng trong **trình duyệt** (nếu sau này có extension riêng cho TransPeek) thì Model B mới là sân nhà của nó — xem `recommendation.md`.

---

## 10. CÂU HỎI KỸ THUẬT: TransPeek có tự detect chuột hover qua block text ở app khác và hiện icon loa được không?

### 10.1. Trả lời ngắn

**Được, với Model A (hover → icon), nhưng là một tính năng có điều kiện, không phải "chạy mọi nơi như con trỏ I-beam".** Con trỏ I-beam là do **chính app đích tự đổi** khi chuột vào vùng text của nó; TransPeek là app thứ ba nên không thể đổi cursor của app khác — chỉ có thể hiện **icon nổi của mình** gần dòng text đó.

### 10.2. API phát hiện (theo stack hiện tại)

**macOS — Swift/SwiftUI + AppKit + Accessibility:**
1. `CGEventTapCreate` (event tap `kCGHIDEventTap`, mask `kCGEventMouseMoved`) để nghe chuột toàn cục — cần quyền **Accessibility** (và từ macOS 10.15+, event tap chuột cần cả **Input Monitoring** nếu muốn đọc ngoài app mình; thực tế `CGEventTap` cho mouse-moved thường chỉ cần Accessibility, nhưng nên xin cả hai và xử lý khi bị từ chối).
2. Khi chuột đứng yên đủ lâu (debounce ~200ms), gọi `AXUIElementCopyElementAtPosition(systemWide, x, y)` → element dưới chuột.
3. Kiểm tra role: `AXStaticText` / `AXTextArea` / `AXTextField` / `AXWebArea` / `AXCell`…; lấy `kAXValueAttribute` (text) và `kAXFrameAttribute` (rect màn hình). Nếu element là container, duyệt con để tìm text node gần điểm chuột nhất.
4. Muốn chính xác tới **dòng**: thử `kAXBoundsForRangeParameterizedAttribute` (không phải app nào cũng hỗ trợ); nếu không có thì dùng rect của cả block text và đặt icon ở **mép phải/trên của block**.
5. Hiện icon: `NSPanel` non-activating, level `.floating`, đặt tại góc block (vd mép phải của dòng, hoặc dưới chuột + offset như NaturalReader làm với selection icon).

**Windows — WinUI 3 + C# + UI Automation:**
1. Hook chuột toàn cục: `SetWindowsHookEx(WH_MOUSE_LL)` (low-level mouse hook, chạy trong process mình, không cần inject DLL) hoặc `Raw Input`.
2. Debounce tương tự, rồi `IUIAutomation.ElementFromPoint(x, y)` → element dưới chuột.
3. Kiểm tra `ControlType` (`Text`, `Edit`, `Document`, `DataItem`…); lấy `TextPattern` → `GetVisibleRanges()` / `RangeFromPoint` → `GetBoundingRectangles()` để có rect từng dòng/từ.
4. Hiện icon: cửa sổ WinUI 3 borderless, `SetWindowPos(HWND_TOPMOST)`, `WS_EX_NOACTIVATE`, đặt tại rect của dòng/block.

### 10.3. Cách đặt icon (positioning)

- **Neo theo dòng/block, không neo theo chuột** (khác với selection icon của NR): lấy rect của dòng text đang hover → đặt icon ở **mép phải của dòng** (kiểu Speechify) hoặc **góc trên-trái của block**. Cần padding để icon không che chữ.
- **Theo scroll**: poll rect của element đang neo mỗi ~150–250ms; rect đổi → dời icon theo; rect biến mất/ra ngoài màn hình → ẩn icon. Chỉ poll 1 element nên chi phí thấp.
- **Ẩn icon khi**: chuột rời khỏi block (hit-test ra element khác), user bấm chuột, app đổi focus, cửa sổ di chuyển/resize, text bị sửa.
- **Click icon →** lấy text của block/dòng đó (qua AX/UIA value hoặc range) → đưa vào flow TTS hiện tại của TransPeek (đọc từ dòng đó; có thể đọc tiếp các dòng sau trong cùng block — cần tự duyệt các text node sau đó trong cây AX/UIA).

### 10.4. Giới hạn (tóm tắt từ §9.4, áp cho tính năng này)

- App không expose text (canvas, ảnh, Electron accessibility tắt, game) → **không detect được**, icon không hiện — phải fallback về flow bôi chọn hiện tại (đây là lý do flow bôi chọn phải luôn giữ làm mặc định).
- Không lấy được rect từng dòng (thiếu `BoundsForRange`/TextPattern) → icon chỉ đặt được ở mép block, kém chính xác.
- **Hiệu năng**: hit-test AX/UIA mỗi lần hover có độ trễ (vài chục ms); phải debounce + cache, nếu không chuột sẽ lag.
- **Quyền**: macOS cần Accessibility (+ Input Monitoring cho event tap); nếu user từ chối → tính năng tắt hẳn, app phải báo rõ. Windows cần hook chuột (không cần admin cho app thường) nhưng app elevated thì UIA bị hạn chế.
- **Highlight từng từ/câu trong app khác là không làm được** (không chèn được vào text của app khác); chỉ có thể vẽ khung overlay mờ lên trên — và khung overlay cũng chỉ chính xác khi có rect từng dòng/từ.
- **Độ ổn định**: AX/UIA notification không đều; một số app (đặc biệt là app dùng framework tự vẽ) đổi cây accessibility bất chợt → icon nháy/đặt sai chỗ. Cần cơ chế tự ẩn khi không chắc chắn.

### 10.5. Model B (icon hiển thị sẵn, không cần hover) cho app native?

**Không thực tế.** Để đặt sẵn icon cho mọi block text trên màn hình, app phải: quét toàn bộ cây AX/UIA của cửa sổ active **liên tục** (vì user có thể cuộn/mở tab mới/gõ thêm text bất cứ lúc nào), tính rect cho từng block, tạo một cửa sổ nổi cho mỗi icon (hoặc một cửa sổ chứa nhiều icon), và cập nhật vị trí tất cả icon mỗi khi trang cuộn. Chi phí: quét cây đã đắt (§9.5), nhân với số block, nhân với tần suất cập nhật → **tốn CPU/pin, lag, và vẫn sai** khi app không phát notification. Trong DOM, việc này rẻ vì `querySelectorAll` + `MutationObserver` + layout tự theo scroll. **Kết luận: Model B chỉ nên làm trong extension trình duyệt; app native chỉ làm Model A.**

### 10.6. Nên làm trong TransPeek hay để cho browser extension?

- **Model A (hover → icon)**: nên làm trong TransPeek native như một **tùy chọn** (mặc định tắt hoặc bật sau khi user đã quen flow bôi chọn), vì nó mở rộng được flow hiện tại sang các app không phải trình duyệt (Word, PDF reader, ghi chú…) — nơi extension không với tới. Nhưng phải đặt kỳ vọng đúng: nó chỉ tốt ở app expose text đầy đủ (trình duyệt, Word, TextEdit, Notes…), và cần xin quyền Accessibility rõ ràng.
- **Model B**: để cho **browser extension riêng** (nếu TransPeek làm extension Edge/Chrome sau này) — nơi DOM làm cho nó gần như miễn phí. Không cố làm Model B trong app native.
- Xem thêm verdict tổng trong `recommendation.md`.

---

## 11. Khả năng tích hợp vào TransPeek

### 11.1. Bối cảnh hướng app (nhắc lại)

TransPeek: **một float icon chung** — **hover = dịch** (dịch/tra từ), **click = nghe** (TTS). Tương lai có Settings "Quick panel / instantly translate" (panel nhỏ gọn chỉ hiện text đích). NaturalReader dùng **nhiều điểm vào khác nhau** (selection icon, click-to-read, toolbar, hotkey, icon trong Gmail/ChatGPT) — TransPeek đã chọn **một icon duy nhất** nên không copy y nguyên được; chỉ lấy pattern rồi map vào model một icon.

### 11.2. macOS — repo `TranManhTuanLabs/TransPeek` (Swift/SwiftUI, AppKit, Vision OCR, popup panel)

| Pattern của NaturalReader | Map vào TransPeek macOS | Mức độ |
|---|---|---|
| Selection icon (bôi chọn → icon 30×30 tại vị trí chuột, tự ẩn 3s) | **Đã có sẵn khái niệm tương đương** (float icon hiện khi bôi chọn). NR cho thêm: ngưỡng ký tự tối thiểu (20), tự ẩn sau 3s, toggle bật/tắt icon — **chuyển trực tiếp được** | Concept transfer, code viết lại |
| Click-to-Read (hover câu → highlight xanh nhạt, click → đọc từ đó) | Cần AX hit-test + overlay rect (§10). Chỉ khả thi sau khi có Model A. Highlight bằng **khung overlay** chứ không chèn vào text app khác | Cần native rewrite, có giới hạn |
| Chia câu/chunk (compromise NLP → câu → chunk 50–250 ký tự) | Dùng **NaturalLanguage framework** của Apple (`NLTagger` tách câu/từ, `NLLanguageRecognizer` detect ngôn ngữ) thay cho compromise — cùng ý tưởng, API native | Concept transfer |
| Tốc độ WPM (50–900, mặc định 180, hệ số = WPM/180) | **Chuyển trực tiếp**: TransPeek mac đã có tab TTS Speech với rate 0.5x–2x; có thể thêm hiển thị WPM + slider như NR | Concept transfer |
| Speech marks → highlight từng từ | **macOS có sẵn, tốt hơn NR**: `AVSpeechSynthesizerDelegate` — `speechSynthesizer(_:willSpeakRangeOfSpeechString:utterance:)` cho callback từng từ **chính xác**, không cần server marks hay ước lượng | Native tốt hơn, không cần backend |
| Preload 4 câu sau / 2 câu trước | **Chuyển trực tiếp** nếu sau này dùng cloud TTS; với giọng hệ thống thì không cần preload (tổng hợp tại chỗ, nhanh) | Concept transfer |
| Highlight 2 lớp câu + từ, 4 bảng màu, auto-scroll, nút Relocate | Chỉ áp dụng được **trong panel/immersive view của chính TransPeek** (mirror text sang panel mình rồi highlight); **không** highlight được trong app khác. Nút Relocate → tương đương "nhảy tới câu đang đọc" trong panel của mình | Một phần transfer |
| Minibar nổi kéo thả + Side Panel 6 view | Tương đương **popup panel** của TransPeek (NSPanel nổi). NR chứng minh player nên có: play/progress, speed, voices, CC, settings — TransPeek có thể lấy bố cục này cho panel TTS (tab 2/3 khi đang đọc — xem §7) | Concept transfer |
| Cloud TTS (AWS endpoints riêng của NR) | **Không dùng được** — đó là backend private của NaturalReader (cần license/email, tính phí theo hạn mức). TransPeek dùng **giọng hệ thống** (`AVSpeechSynthesizer`, đã có trong tab TTS Speech) trước; cloud TTS (nếu cần) phải tự xây hoặc dùng API bên thứ ba | Không transfer — cần backend riêng |
| Icon trong Gmail/ChatGPT/Outlook (Model B theo app) | Không áp dụng cho app native. Chỉ có ý nghĩa nếu TransPeek làm **extension trình duyệt riêng** | Để cho extension |
| Hotkey (Alt+P play/pause, Alt+S đổi preset giọng) | **Chuyển trực tiếp**: TransPeek đã có hotkey; thêm preset giọng 1/2 + phím đổi nhanh là pattern tốt | Concept transfer |

### 11.3. Windows — repo `TranManhTuanLabs/TransPeek-Windows` (WinUI 3 + C#)

| Pattern của NaturalReader | Map vào TransPeek Windows | Mức độ |
|---|---|---|
| Selection icon / float icon | **Đã có** (click icon float để nghe — theo memory 2026-11-16). Thêm: ngưỡng ký tự tối thiểu, tự ẩn sau 3s, toggle | Concept transfer |
| Tốc độ WPM + slider | **Đã có** Default Speech Rate 0.5x–2x trong tab TTS Speech. Có thể thêm hiển thị WPM (rate × 180) cho dễ hiểu | Concept transfer |
| Giọng hệ thống + chọn giọng EN/VI + accent | **Đã có** (`SystemSpeechService`: `SpeechSynthesizer` + `MediaPlayer`, chọn giọng theo Id). NR cho thêm: preset 2 giọng + phím đổi nhanh (Alt+S), auto-select giọng theo ngôn ngữ detect | Concept transfer |
| Word highlight khi đọc | **Hạn chế lớn trên Windows**: `Windows.Media.SpeechSynthesis.SpeechSynthesizer` **không có callback từng từ** như `AVSpeechSynthesizer` của macOS. Muốn highlight từ phải dùng cách của NR: **ước lượng thời gian** (số âm tiết / WPM) hoặc dùng **SSML bookmark** (`<bookmark mark="..."/>` + sự kiện `BookmarkReached` của `MediaPlayer`? — thực tế `SpeechSynthesizer` không expose bookmark event ra C# một cách trực tiếp; cần kiểm chứng). Đây là điểm **macOS làm tốt hơn Windows** về mặt native | Cần spike kiểm chứng |
| Chia câu/chunk | Dùng thư viện NLP cho C# hoặc tự tách câu bằng regex + danh sách dấu câu (không có compromise cho C#; có thể port logic chẻ 500 ký tự + gom 50–250 ký tự của NR — logic này đơn giản, tự viết được) | Viết lại, logic đơn giản |
| Preload | Chỉ cần nếu dùng cloud TTS; giọng hệ thống thì `SpeechSynthesizer.SynthesizeTextToStreamAsync` theo câu là đủ nhanh | Concept transfer |
| Hover → icon (Model A) | UIA `ElementFromPoint` + `TextPattern` (§10.2). WinUI 3: cửa sổ topmost `WS_EX_NOACTIVATE` | Cần native rewrite |
| Cloud TTS | Như macOS: không dùng backend của NR; nếu cần giọng cloud thì dùng Azure/OpenAI TTS riêng | Không transfer |

### 11.4. Những gì KHÔNG nên copy từ NaturalReader

- **Không copy code**: toàn bộ source trong `.crx` là proprietary của NaturalReader (không có license mở). Chỉ mô tả pattern và tự implement lại.
- **Không dùng endpoint AWS của họ** (prem/plus/pro TTS, speech-marks, OCR, Cognito): đó là backend thương mại, cần license key + tài khoản trả phí của họ; gọi lậu là vi phạm điều khoản và sẽ bị chặn.
- **Không copy asset**: icon `readicon.svg`, ảnh đại diện giọng (S3), font, CSS — tự vẽ/thiết kế lại theo phong cách TransPeek.
- **Không copy y nguyên số lượng surface**: NR có cả minibar + side panel + toolbar + context menu + hotkey + icon trong 3 app — quá nhiều điểm vào cho một app tối giản như TransPeek. Lấy **một** pattern chính (float icon + panel) và tối đa 1–2 điểm vào phụ (hotkey, menubar/tray).

### 11.5. Gợi ý thứ tự làm (nếu Manh chọn hướng tích hợp)

1. **Trước mắt, không cần NR**: hoàn thiện flow hiện tại (bôi chọn → hover dịch / click nghe) + tab TTS Speech đã có.
2. **Rẻ nhất, giá trị cao**: WPM slider + hiển thị hệ số (đã có rate, thêm WPM), preset 2 giọng + phím đổi nhanh, ngưỡng ký tự tối thiểu cho icon, tự ẩn icon sau vài giây.
3. **Trung bình**: highlight từ khi đọc — macOS dùng `willSpeakRangeOfSpeechString` (dễ); Windows cần spike SSML bookmark/ước lượng (khó hơn).
4. **Đắt, làm sau cùng (nếu làm)**: Model A hover → icon qua AX/UIA, mặc định tắt, chỉ bật khi user xin.
5. **Không làm trong app native**: Model B đặt sẵn icon; cloud TTS của NR; icon trong Gmail/ChatGPT (để cho extension riêng).

---

## 12. Tóm tắt verdict cho TransPeek

1. **Icon loa của NaturalReader là selection-triggered, không phải hover-triggered.** Hover chỉ có tác dụng trong Click-to-Read (highlight câu + click để đọc từ đó). Không có icon nào hiện khi hover block text trên trang web chung.
2. **Model B (icon hiển thị sẵn) chỉ tồn tại trong 3 app riêng** (ChatGPT/Gmail/Outlook) nhờ biết trước cấu trúc DOM của từng app + `MutationObserver`. Không có cơ chế phát hiện block chung nào trong extension này.
3. **Pipeline TTS của NR là hybrid**: giọng free = Web Speech API của trình duyệt (word highlight qua `onboundary`); giọng trả phí = cloud TTS riêng (MP3 + speech marks qua header `x-smark`, streaming WebSocket + AudioContext cho voice mới). TransPeek **không dùng được backend này**; hướng đúng là giọng hệ thống (macOS `AVSpeechSynthesizer` cho word callback chính xác; Windows `SpeechSynthesizer` cần workaround cho word highlight).
4. **Điều khiển khi đang đọc nằm ngay trong player** (speed slider WPM 50–900, danh sách giọng có filter nam/nữ, preset giọng, volume, CC) — ủng hộ ý tưởng "chỉnh nhanh trong tab 2/3 của panel" thay vì mở cửa sổ điều khiển rời.
5. **Native feasibility: feasible-with-limitations.** Model A (hover → hit-test AX/UIA → icon nổi) làm được trên cả macOS và Windows nhưng chỉ tốt ở app expose text, cần quyền Accessibility/hook chuột, icon phải tự bám scroll bằng polling, và không highlight được từng từ trong app khác. Model B (quét sẵn + đặt sẵn icon) **không thực tế** với app native — để dành cho browser extension.
6. **Chia text**: tách câu bằng NLP → chẻ câu dài >500 ký tự theo dấu câu → gom chunk 50–250 ký tự → preload 4 trước/2 sau — pattern này chuyển thẳng sang TransPeek được (macOS dùng `NLTagger`, Windows tự tách câu).

---

## Phụ lục A. Bảng thuật ngữ / từ viết tắt dùng trong báo cáo

| Thuật ngữ | Nghĩa |
|---|---|
| AX / Accessibility tree | Cây Accessibility của macOS — cấu trúc phân cấp các UI element mà hệ thống expose cho app khác đọc (text, vị trí, role…) |
| UIA / UI Automation | Framework tương đương của Windows để đọc cây UI của app khác |
| DOM | Document Object Model — cây cấu trúc của trang web |
| TTS | Text-to-Speech (chuyển text thành giọng nói) |
| WPM | Words per minute — số từ đọc trong một phút (tốc độ đọc) |
| Speech marks | Dữ liệu thời gian từng từ (từ nào, bắt đầu lúc mấy ms) do server TTS trả kèm audio, dùng để highlight từ đang đọc |
| `x-smark` | Response header của NaturalReader chứa speech marks (base64) |
| Minibar | Thanh player nổi nhỏ của NaturalReader chèn trong trang web |
| Side Panel | Panel bên của Chrome (Chrome Side Panel API) |
| Click to Read | Tính năng: click vào câu bất kỳ trên trang để đọc từ câu đó |
| Highlight to Read | Tính năng: bôi chọn text rồi bấm icon/context-menu để đọc đoạn đó |
| Immersive Reader | Chế độ đọc tập trung: hiện text đã parse trong một view riêng, không bị xao nhãng bởi trang gốc |
| `nr-sentence` / `nr-s{N}` | Element/thẻ tự chế của NaturalReader bọc mỗi câu trong trang (`N` = chỉ số câu) |
| CC | Closed captions — hiện text câu đang đọc dạng chữ trong player |
| Cognito | AWS Cognito — dịch vụ xác thực của Amazon, NaturalReader dùng để ký request TTS |
| PCM / AudioContext | PCM: audio thô chưa nén; AudioContext: Web Audio API để giải mã và phát audio theo từng chunk |
| Model A / Model B | Xem §8: A = hover block rồi icon mới hiện; B = icon hiển thị sẵn trong block không cần hover |

## Phụ lục B. Link tham khảo

- Chrome Web Store — NaturalReader: https://chromewebstore.google.com/detail/naturalreader-ai-text-to/kohfgcgbkjodfcfkcackpagifgbcmimk
- Help Center — Reader Settings (Click to Read / Highlight to Read / Auto-scroll): https://help.naturalreaders.com/en/articles/6106379-chrome-extension-reader-settings
- Help Center — Settings (Chrome Extension): https://help.naturalreaders.com/en/articles/9788570-settings-chrome-extension
- Help Center — Reading Webpages (Chrome Extension): https://help.naturalreaders.com/en/articles/6093937-reading-webpages-chrome-extension
- NaturalReader Online: https://www.naturalreaders.com/online/

---

*Báo cáo viết ngày 2026-11-16 từ source code NaturalReader 8.0.3.0 (.crx) + tài liệu Help Center công khai. Không cài/chạy thử extension live. Chỉ mô tả pattern để re-implement, không copy code/asset/backend của NaturalReader.*
