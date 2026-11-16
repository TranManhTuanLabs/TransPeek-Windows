# Báo cáo nghiên cứu: Speechify — Text to Speech (Chrome Extension 14.15.0)

> **Phạm vi:** Extension TTS (đọc văn bản thành tiếng) — 1 trong 3 mẫu Manh cung cấp để nghiên cứu UX đọc-nghe (TTS) và panel dịch nhanh cho TransPeek.
> **Ngôn ngữ báo cáo:** Tiếng Việt.
> **Nguồn chính:** Source code đã giải nén từ file `.crx` tại `~/workspace/research_notes/tts-speechify-naturalreader-20261116/src/speechify/` (Speechify 14.15.0.0, manifest v3). Đọc trực tiếp: `manifest.json`, `content/main.js` + các chunk `content/`, `background/main.js`, `offscreen/src/main.js`, `sidepanel/`, `css/speechify.css`, `centralized-voice-list.json`, `playButtonData`.
> **Lưu ý phương pháp (quan trọng):** Toàn bộ JavaScript của extension đã được **minify + bundle (Vite, tên file hash, tên biến 1–2 ký tự) và KHÔNG kèm sourcemap** trong gói `.crx`. Vì vậy các kết luận dưới đây suy ra từ: tên file còn nghĩa, string literal còn nguyên (label UI, tên feature, endpoint, CSS), cấu trúc điều khiển còn đọc được, và đối chiếu hành vi công khai. Những chỗ chỉ suy luận được một phần sẽ được ghi rõ "chưa xác minh được từ source" ở §15. Một số khẳng định về hành vi người dùng cuối được bổ sung từ tài liệu công khai của Speechify/SlashGear và được đánh dấu **[nguồn web]**.
> **Không sao chép code:** Báo cáo chỉ mô tả *pattern* (mẫu thiết kế/hành vi) để re-implement, không trích code proprietary của Speechify.

---

## 0. Tóm tắt nhanh (đọc 2 phút)

| Câu hỏi | Trả lời ngắn |
|---|---|
| Speechify hiển thị TTS ở đâu? | 4 bề mặt: **Pill Player** (thẻ nổi dưới trang), **Side Panel** (panel bên của Chrome), **nút Play chèn sẵn trong từng bài/post/email** (site đã biết), **pill "Listen" trên PDF**. |
| Hover đoạn văn có hiện icon loa không? | **Có — nhưng là Model A (hover mới hiện)**, không phải Model B (hiện sẵn). Icon tròn trắng, đặt **bên TRÁI dòng đầu tiên của đoạn**, xuất hiện khi chuột vào vùng đoạn văn, click là đọc từ đầu đoạn đó. |
| Icon đặt ở đâu, tính thế nào? | Lấy rect của **từ đầu tiên** của đoạn bằng DOM `Range` (`getBoundingClientRect`), icon có cạnh = `max(cỡ chữ, 20px)`, căn giữa theo chiều dọc dòng đầu, dịch sang trái một khoảng bằng đúng cạnh icon (+36px vùng đệm). Có kiểm tra **che khuất** (`elementFromPoint` tại 3 điểm) và **né nút/link** (`elementsFromPoint` → dời icon xuống/phải). |
| Khi phát thì sao? | Tô sáng **câu đang đọc (nền nhạt) + từ đang đọc (nền đậm hơn)**, tự cuộn theo câu/từ (Auto-Scroll). Hover câu khác trong lúc phát → tô sáng câu đó, **click là nhảy tới đọc từ câu đó** (click-to-listen). |
| Audio từ đâu? | **Cloud là chính:** text gửi lên `audio.api.speechify.com`, trả về stream MP3/OGG theo từng chunk kèm **`speechMarks`** (mốc thời gian từng từ) để map thời gian phát → vị trí từ/câu. Giọng cloud của Speechify + Azure. **Free tier dùng giọng có sẵn trên máy** (Web Speech API `speechSynthesis`) khi không có giọng cloud miễn phí. |
| Tốc độ/giọng lúc đang phát? | Nằm **ngay trong Pill Player**: bấm nút Speed/Voice trên player → mở view chọn tốc độ (slider 0.4x–4.5x, free tối đa 1.5x) hoặc view chọn giọng (tìm kiếm, yêu thích, nghe thử). Không cần mở Settings. |
| Model A hay B? | **Trang web thường: chỉ Model A.** **Model B (nút hiện sẵn) chỉ có trên các site đã biết** (ChatGPT, Gmail, Facebook…) vì extension có config selector riêng cho từng domain, và trên PDF (pill "Listen" hiện sẵn). |
| Tích hợp native được không? | **Model A: khả thi-với-giới-hạn** (hit-test qua Accessibility/UI Automation + cửa sổ nổi). **Model B (quét liên tục, cắm sẵn icon mọi block): thực tế chỉ làm tốt trong DOM của web** — native quét cây Accessibility liên tục là quá đắt và không có thông báo thay đổi/scroll đáng tin. Xem §11–§12. |

---

## 1. Kiến trúc tổng thể của extension

**Manifest v3** (`manifest.json`):

- **Quyền:** `offscreen`, `tabs`, `tabCapture`, `contextMenus`, `storage`, `alarms`, `scripting`, `unlimitedStorage`, `system.cpu`, `system.memory`, `sidePanel`; `host_permissions: <all_urls>` + `file://`.
- **Content script** chạy trên mọi URL, `run_at: document_start` (`content-wrapper.js` → `content/main.js`, ~2,2 MB gồm ~119 file chunk). Một script nhẹ thứ hai (`content-idle-notifier.js`) chỉ báo `document_idle`.
- **Background service worker** (`background/main.js`, ~2,9 MB): giữ state toàn cục, gọi API Speechify, điều phối message giữa content/offscreen/sidepanel.
- **Offscreen document** (`offscreen/src/main.js`, ~836 KB): nơi **phát audio thật** (để audio không chết khi service worker ngủ hoặc khi chuyển tab) + Voice Typing / Meeting Capture (WebSocket transcription — không liên quan TTS phát).
- **Side Panel** (`sidepanel/sidepanel.html` + `main.js`, ~1 MB): player đầy đủ + Library.
- **Popup** (`popup/popup.html`) gần như trống — extension dùng side panel + pill player thay cho popup.
- **PDF viewer riêng** (`pdf-viewer/`, ~2,2 MB): mở PDF trong viewer của extension để đọc.
- **Phím tắt** (manifest `commands`): `Alt+A` Play/Pause, `Alt+R` Screenshot mode (chụp → OCR → nghe), `Alt+L` Save to Library, `Alt+S` Voice Typing.
- **Kết nối ngoài:** `externally_connectable` với `app.speechify.com`, `speechify.com` — web app có thể gọi extension.

**Luồng dữ liệu tổng thể:** content script parse trang → trích text blocks → đưa vào hàng đợi "playables" của **Orator** (state machine phát nhạc, nằm trong `content/chunk-F5R7TDWC.js`, ~765 KB — file lõi lớn nhất) → Orator xin audio từng playable từ cloud qua background (`tts/get-audio`) → audio phát ở offscreen document → tiến trình phát (`AUDIO_PROGRESSED` kèm `charIndex`) đẩy ngược về content để highlight từ/câu đang đọc.

**Remote config / feature flag:** nhiều tính năng (đặc biệt Hover Player: `ignoredTags`, bật/tắt theo domain) lấy từ config server (`Ye()` — global config), tức Speechify có thể bật/tắt hoặc chỉnh hành vi Hover Player mà không cần phát hành bản mới.

---

## 2. TTS được hiển thị như thế nào — 4 bề mặt player

### 2.1. Pill Player — player nổi chính trên trang (`#speechify-pill-player`)

Đây là player chính khi nghe một trang web thường. Code ở `content/init-ZVV4TUIA.js` (~55 KB).

- **Vị trí/dạng:** một thẻ (card) nổi, render bằng React vào **shadow DOM** (`#speechify-pill-player`, host `position: absolute; bottom: 0; right: 0`, `bottom: 15%` ở một biến thể) — tức **góc dưới-phải trang, nổi trên nội dung**, không chèn vào layout trang. Có nút thu gọn (collapse) và nút đóng (đóng thì hiện modal "To bring the player back, click on the extension").
- **Nội dung trong player:**
  - Nút **Play/Pause** lớn; nút **Skip Backward / Skip Forward** (nhảy theo **câu**, không phải 10–15 giây như podcast).
  - **Thanh tiến trình + seek** (kéo để tua theo % toàn bộ nội dung).
  - Nút **Speed** → mở route `/speed` (xem §9); nút **Voice / Change Voice** → mở route `/voices` (xem §9).
  - Nút **Save to Library**, **Open Library**, **Settings**, **Screenshot to Listen** ("Capture & Listen" — chụp vùng màn hình → OCR → nghe).
  - Hiển thị **tên tài liệu/tiêu đề trang** và (ở chế độ PDF) trạng thái riêng.
  - Có route `/help` (hướng dẫn), `/featureprompt/skipsentences` (giới thiệu tính năng nhảy câu).
- **Trạng thái:** `playing / paused / buffering / errored / loading`. Khi lỗi load player: "The player didn't load correctly. Please refresh it."
- **Điểm đáng chú ý cho TransPeek:** toàn bộ điều khiển (tốc độ, giọng, nhảy câu, tua) nằm **trong cùng một player đang phát**, không cần mở Settings hay cửa sổ khác. Đây chính là mẫu cho ý tưởng "điều khiển nhanh trong lúc phát" của Manh (xem §9.4).

### 2.2. Side Panel (Chrome Side Panel)

`sidepanel/sidepanel.html` + `main.js` (~335 KB): panel bên chuẩn của Chrome (`manifest.side_panel`), chứa player đầy đủ + Library + cùng các control Speed/Voice (component `SpeedControls` dùng chung với pill). Mở bằng cách bấm icon extension → toggle `side-player`. Khi user bấm icon extension mà pill đang ẩn, extension mở side panel và tự phát nếu đang có text được chọn.

### 2.3. Nút Play chèn sẵn trong từng bài/post/email (per-domain Play Buttons)

Code ở `content/playButtonData-IZ7PLJUL.js` + `content/chunk-WTBO35FW.js` và các file `init-*` riêng cho từng site (Google, Yahoo Mail, Gmail…).

- **Cơ chế:** một bảng config **theo domain** (`playButtonsData`): mỗi domain có `configFeature` (`individual-posts-player`, `paragraph-player`, `gmail-support`, `google-gen-ai-result`) và danh sách feature (`listenToAnswer`, `listenToPost`, `listenInInbox`, `listenBeforeSending`, `listenToComment`, `listenToTweet`, `paragraphPlayer`…), kèm selector CSS của khối nội dung trên site đó.
- **Các site có config sẵn:** ChatGPT, Claude, DeepSeek, Gemini, Grok, Perplexity, OpenEvidence, Google AI Studio, NotebookLM ("Listen to answer"); Gmail, Outlook ("Listen in Inbox" / "Listen before sending"); Facebook, LinkedIn, Reddit, X, YouTube ("Listen to post/tweet/comment"); Google Search (AI Overview, FAQ, panels, sites).
- **Cách chèn:** `MutationObserver` trên container của trang (debounce 32–100 ms), mỗi khi DOM đổi thì quét selector, **chèn một nút nhỏ "▶ Listen / ⏸ Pause" (+ thời lượng đọc) vào đầu/cuối mỗi post/email/câu trả lời** — render React vào một `<div>` tạo mới, `prepend`/`append` vào đúng vị trí trong post. Có chống chèn trùng (kiểm tra đã có container class `speechify-*-play-button-container` chưa) và tự gỡ khi post biến mất. **Đây là Model B** (xem §10) — nhưng chỉ làm được vì Speechify **viết tay selector cho từng site**.
- Nút này click → phát đúng nội dung của post đó (`queueAndPlayContent` với `getContent` trích text từ chính element post, bỏ qua quote/banner/snippets).

### 2.4. Pill "Listen" trên PDF

Trong `content/main.js`: khi phát hiện trang là PDF (URL `.pdf`, `contentType === application/pdf`, hoặc PDF viewer), extension chèn một pill **"▶ Listen"** cố định **giữa-dưới màn hình** (`position: fixed; bottom: 24px; left: 50%; transform: translateX(-50%); z-index: 2147483647`, nền `#484848`, bo tròn 9999px, có nút × ẩn khi hover). Click → tải PDF về cache (5 phút) rồi mở `pdf-viewer/pdf-viewer.html?file=...&autoplay=1`. Đây cũng là Model B nhưng ở mức **toàn tài liệu** (1 nút cho cả trang), không phải từng block.

### 2.5. Selection Player (đọc text đang chọn)

Khi user bôi chọn text, `content/main.js` chạy hàm probe `ee()`: kiểm tra document có focus không, selection có rỗng không, trang có phải PDF/EMBED không, có đang phát/error không, rồi trả về trạng thái `{readable, canPlayPause, selectionControls}`. Nếu selection đọc được → cho phép phát (Alt+A hoặc bấm icon extension). Riêng Google Docs có module riêng (`selection-player-JGPWTCMP.js`) vì Docs không dùng selection DOM thường. Đây là flow gần nhất với flow hiện tại của TransPeek (bôi chọn → icon → phát).

---

## 3. Hover Player — icon loa khi hover đoạn văn (Model A) ⭐

Đây là phần quan trọng nhất của báo cáo. Code chính: **`content/init-BKLAAS4F.js`** (~17 KB, đọc được gần trọn vẹn).

### 3.1. Kết luận trước: đây là Model A, KHÔNG phải Model B

- Icon **chỉ xuất hiện sau khi chuột hover vào một đoạn văn**. Không có icon nào được cắm sẵn trên trang khi chưa hover.
- Ấn tượng "hiện sẵn" đến từ 2 yếu tố: (a) vùng hover là **cả đoạn văn** (chuột vào bất cứ chỗ nào trong đoạn là icon hiện ngay, không cần hover đúng dòng đầu); (b) trên các site đã biết, nút Play **được chèn sẵn trong từng post** (§2.3) — đó mới là Model B thật, nhưng chỉ áp dụng cho site có config tay.
- Icon cũng **chỉ tồn tại 1 cái tại 1 thời điểm** (đoạn đang hover) + tối đa vài icon cũ đang fade-out (200 ms).

### 3.2. Khi nào Hover Player được bật

- Hover Player và Paragraph Player (Google Docs) **loại trừ lẫn nhau**: biến đếm `J()` — chỉ 1 trong 2 được active; nếu Paragraph Player đang chạy thì Hover Player không khởi động (`gn()`).
- Hover Player **không chạy** trên một số loại trang (hàm kiểm tra `un()` — các predicate đã minify, suy từ ngữ cảnh gồm Google Docs, Kindle, và các trang đặc biệt khác).
- Nội dung phải load xong: Hover Player lấy danh sách block từ **hàng đợi phát của Orator** (xem §7). Nếu content chưa sẵn, nó tự thử lại với **backoff hàm mũ 1s → 2s → … tối đa 30s**.
- Remote config có thể tắt/bật và chỉnh `ignoredTags`.

### 3.3. Đoạn văn nào được tính là "hover được" (điều kiện lọc)

Hàm lọc `w(t)` — một element chỉ thành "block hover được" khi thỏa **tất cả**:

1. Là element node (không phải text node lẻ).
2. Tag **không nằm trong danh sách top-level bị loại**: `span a li ol ul dd dl dt h1 h2 h3 h4 h5 h6 select textarea button label audio video dialog embed menu nav noframes noscript object script style svg aside footer #footer pre summary form input iframe img map meta link title track area base basefont col colgroup head html param source table tbody td tfoot th thead tr` — tức **chỉ các khối chứa văn bản thuần** (thực tế là `p`, `div`, `article`, `section`…); heading, list-item, link, nút, form, media đều bị loại ở tầng này.
3. **Không chứa** bất kỳ descendant nào khớp danh sách tag bị loại ở trên (đoạn có nút/link/form bên trong → loại).
4. **Text dài hơn 10 từ** (`textContent.trim().split(/\s+/).length > 10`) — đoạn ngắn không hiện icon.
5. **Không nằm trong** `a, button, form` (kiểm tra `closest("a, button, form")`).
6. **Nhìn thấy được** (xem §3.5): nằm trong viewport và không bị element khác che quá 20%.

Nhận xét: bộ lọc này thiên về **an toàn** (thà không hiện còn hơn hiện sai chỗ) — đoạn ngắn, đoạn có link/nút, heading đều không có icon.

### 3.4. Vị trí icon — cách tính chính xác (smart positioning)

Hàm `Oe()` (tạo "hovered paragraph" object) + `getPlayerIconRect()`:

1. **Tìm rect dòng đầu tiên:** dùng một generator duyệt text node (`Ct` — TreeWalker + tách từ) để dựng **DOM `Range` bao đúng từ đầu tiên** của đoạn (`Ae(r, 1)`), rồi `range.getBoundingClientRect()`. Tương tự, `Ae(r, 3)` dựng Range bao **3 từ đầu tiên** → `getFirstThreeWordsRect()`. (Dùng Range thay vì rect của cả element để icon bám đúng **dòng đầu tiên** dù đoạn dài nhiều dòng, và bám đúng từ đầu tiên kể cả khi đoạn bắt đầu bằng inline element.)
2. **Kích thước icon:** `H = max(fontSize của đoạn, 20)` px (đoạn chữ 16px → icon 20px; đoạn chữ lớn → icon to theo).
3. **Vị trí icon:** `new DOMRect(B.x - H, R.top + G, H, H)` với `R` = rect từ đầu tiên, `G = (R.height - H) / 2`, `B` = rect 3-từ-đầu đã mở rộng sang trái 36px (hoặc 16px ở một chế độ). Nói cách khác: **icon là hình tròn đặt ngay BÊN TRÁI dòng đầu tiên của đoạn, căn giữa theo chiều dọc dòng đầu, cách mép trái của chữ một khoảng bằng đúng cạnh icon**. Đây là vị trí "gutter" (lề) quen thuộc của các app đọc.
4. **Vùng hover (hoverAreaRect):** hợp (union bounding rect) của 3 rect — rect dòng đầu, rect 3-từ-đầu mở rộng, và rect icon — tất cả padding thêm 4px. **Chuột vào bất cứ điểm nào trong vùng hợp này là icon hiện** (thực tế gần như cả đoạn văn). Có một hàm `Q()` mở rộng rect thêm padding ngang/dọc trước khi hợp.
5. **Trường hợp đặc biệt:** nếu đoạn nằm trong ngữ cảnh heading (`h` flag), điểm neo đổi thành `firstElementChild` của đoạn thay vì chính đoạn.

### 3.5. Chống che khuất & né element tương tác (2 lớp kiểm tra)

**Lớp 1 — đoạn có đang nhìn thấy không (`de()` + `te()`):**
- Lấy rect của 3 từ đầu tiên; nếu rect nằm ngoài viewport → không hiện.
- Lấy **3 điểm mẫu** ở 25% / 50% / 75% chiều ngang, giữa chiều cao của rect 3-từ-đầu; gọi `document.elementFromPoint` tại mỗi điểm. Một điểm được tính là "bị che" nếu element trả về **không phải** đoạn văn (hoặc con của nó) **và** không phải element trong suốt (`pointer-events: none`, `visibility: hidden`, `opacity < 0.1`). Nếu **≥ 20% số điểm bị che** → coi như đoạn không nhìn thấy → không hiện icon.

**Lớp 2 — icon có đè lên nút/link không (`fe()`):**
- Lấy 3 điểm trên icon (giữa, 25%, 75% chiều ngang); gọi `document.elementsFromPoint`. Nếu có element tương tác (`input, button, select, textarea, a, [role=checkbox/radio/button/switch]`) đè lên vị trí icon **và** element đó không nằm trong/không chứa đoạn văn → **dời icon**:
  - Thử dời **xuống dưới** một khoảng `overlap.bottom - icon.top + 4`;
  - Nếu vẫn đụng, thử dời **sang phải** `overlap.left - icon.right - 4`;
  - Nếu cả hai đều đụng → **ẩn icon luôn** (không hiện icon đè lên nút của trang).
- Kiểm tra này chạy lại mỗi khi `hoveredParagraph` đổi (subscribe store) — tức icon tự né mỗi lần hover đoạn mới.

### 3.6. Theo dõi chuột & vòng đời hiển thị

- **`mousemove` throttle 16 ms** (~60fps): mỗi event, tìm block đầu tiên mà `hoverAreaRect` chứa điểm chuột (`bt()` — point-in-rect). Tìm thấy → set `hoveredParagraph`; không thấy → clear.
- **Render:** React render vào `<div id="speechify-hover-player-container">` nằm trong **shadow root** chung của extension (`tt.acquireShadow`). Wrapper icon: `position: absolute; left = iconRect.left + window.scrollX - 4; top = iconRect.top + window.scrollY; width/height = iconRect + 8px; z-index: 2147483640`. Icon bên trong: hình tròn nền trắng, bóng `0px 3px 12px rgba(0,0,0,0.12)`, `border-radius: 9999px`, chứa SVG tam giác play (viewBox 32×32) hoặc 2 vạch pause khi đoạn đó đang phát, hoặc spinner xoay khi đang buffering. Fade-in 100 ms, fade-out 200 ms.
- **Icon cũ:** khi chuyển sang đoạn khác, icon cũ không biến mất ngay mà đưa vào danh sách `h`, fade-out rồi hủy sau **200 ms** (tránh nhấp nháy khi rê chuột qua nhiều đoạn).
- **Bám theo scroll/resize:** `scroll` listener (debounce 100 ms) + `ResizeObserver` + `IntersectionObserver` trên element đoạn đang hover + vòng lặp `requestAnimationFrame` — mỗi khi layout đổi, tính lại `getFirstThreeWordsRect()` và render lại vị trí icon. Nhờ vậy icon **đi theo đoạn văn khi cuộn trang** (miễn đoạn còn trong viewport; ra khỏi viewport thì ẩn).
- **Ẩn icon khi:** chuột rời đoạn (mouseleave), chuyển sang đoạn khác, đoạn bị che/không nhìn thấy, route Settings đang mở, content chưa load, hoặc Hover Player bị tắt từ xa.

### 3.7. Click icon thì xảy ra gì

- Click icon của đoạn đang hover → `orator.seekToPosition(paragraphId, [0, 0])` (nhảy tới đầu đoạn) rồi `orator.play("Hover Player")`; gửi analytics `extension_hover_player_button_clicked` (kèm `isClonedVoice`).
- Nếu đoạn đó **đang phát** → click = **pause** (icon đổi thành pause). Nếu **đang buffering** → icon spinner.
- Sau khi bắt đầu phát, tooltip gợi ý ("click-to-listen hint") được ẩn.
- **Không có hover-to-play:** chỉ hover thì icon hiện, **phải click mới phát**. Không có chế độ "hover là tự đọc".

### 3.8. Paragraph Player (Google Docs) — biến thể Model A riêng cho Docs

- Config: `googledocs → configFeature: "paragraph-player"`, mô tả chính thức trong code: **"Hover any paragraph to start listening from it"** / "Paragraph Play — Hover a paragraph, click to listen from it."
- Vì Google Docs render văn bản bằng cấu trúc DOM đặc biệt (không phải `<p>` thường; có `div[speechify-play-window-id]`), extension có module riêng (`init-UGHVGDP3.js`) để map paragraph Docs → playable. Hành vi với user giống Hover Player: hover đoạn → icon → click → đọc từ đoạn đó.
- Bài học cho TransPeek: **mỗi loại app/trang có cấu trúc riêng thì cần adapter riêng** — không có một thuật toán hover chung cho mọi app.

---

## 4. Click-to-listen trong lúc phát (sentence player) — "bấm vào câu để nhảy tới"

Code: `content/chunk-UJT4KNAL.js` (~10 KB, tên feature `"speechifyClickToPlay"`, `"sentence-player"`).

- **Chỉ active khi đang phát/buffering:** đăng ký hook `PLAYBACK_STATE_CHANGED`; listener `mousemove` chỉ gắn khi `playing`/`buffering`, gỡ khi pause/stop.
- **Tìm câu dưới chuột** (throttle 32 ms), theo 3 nhánh tùy loại trang:
  - **Trang thường:** `document.elementsFromPoint(x, y).slice(0, 5)` → tìm element chứa text của block đang phát → trong element đó, tìm câu mà **rect của DOM Range câu đó** (lấy qua `getClientRects()`, cộng dung sai `w` px) chứa điểm chuột.
  - **Google Docs:** `document.caretRangeFromPoint(x, y)` → dùng TreeWalker đếm offset ký tự từ đầu đoạn → suy ra index câu (qua ranh giới câu đã tính trước).
  - **Kindle:** tìm element con (`p, li, h1–h6, blockquote`) chứa điểm chuột bằng `getBoundingClientRect`, rồi match câu bằng nội dung text.
- **Hiển thị:** câu đang hover được **tô sáng** (chỉ tô câu, `sentenceOnly: true`, qua cùng hệ highlight §6) và **con trỏ chuột đổi thành `pointer`**; một tooltip gợi ý nhỏ ("click-to-listen hint") hiện gần rect dòng của câu.
- **Click:** `seekToPosition(playableId, [sentenceIndex, 0], elementCharOffset)` → Orator nhảy tới đọc từ câu đó; nếu click đúng câu đang đọc thì chỉ bỏ highlight. `pointerdown` được chặn (`preventDefault` + `stopImmediatePropagation`) để click này **không bôi chọn text** của trang.
- **Khi cuộn trang trong lúc phát:** listener `scroll` (capture) tính lại vị trí câu đang hover; nếu câu ra khỏi vùng nhìn thấy thì bỏ highlight.
- Ý nghĩa với TransPeek: đây là tính năng **"đọc tới đâu, bấm vào câu là nhảy tới đó"** — khác với Hover Player ở chỗ nó chỉ chạy **trong lúc đang phát** và nhắm tới **câu**, còn Hover Player chạy **khi chưa phát** và nhắm tới **đoạn**. Hai tính năng dùng chung hạ tầng (block list, sentence index, Range rect) nhưng vòng đời ngược nhau.

---

## 5. Khi bắt đầu phát: highlight + auto-scroll + theo dõi vị trí

### 5.1. Highlight 2 lớp (câu + từ)

Hệ highlight tên `speechifyPlaybackHighlighter` (`content/chunk-F5R7TDWC.js`, factory `AE()`):

- **Không sửa DOM của trang:** highlight **không bọc từ/câu trong `<span>`**. Thay vào đó, nó tính **rect của Range** cho câu đang đọc và từ đang đọc (qua `getClientRects()`), rồi **vẽ các hình chữ nhật phủ lên trên** (overlay layers — 3 lớp: câu, từ, và một lớp ẩn/hiện; hàm `draw(V, null, rawSentenceVecs, wordBackground, sentenceBackground)`), đồng thời gắn attribute `[data-speechifyplaybackhighlighter-highlight]` lên node đang highlight. Nhờ vậy highlight không phá vỡ layout/selection của trang và gỡ sạch được (`removeHighlights` ẩn cả 3 lớp).
- **2 màu riêng:** nền câu (`highlightColor.sentence.background`, nhạt) và nền từ (`highlightColor.word.background`, đậm hơn) — màu chỉnh được trong Settings (`highlightColorKey`).
- **Bật/tắt:** Settings có mục "Text Highlighting — Real-time text highlights while listening." (kèm video minh họa `videos/text_highlighting.mp4`). Tắt thì chỉ nghe, không tô.
- **Trường hợp đặc biệt:** có 2 chế độ tính rect — `highlightVec` (tính vector rect cho cả câu/từ, dùng khi map được chính xác element ↔ text) và `highlightElement` (tính `lineRect`/`wordRect` từ sentenceBounds/wordBounds). Có xử lý "continuation" (câu kéo dài sang element kế tiếp) và "keypoint" (điểm nhấn).

### 5.2. Auto-Scroll (tự cuộn theo)

- Settings: "Auto-Scroll" (video `videos/autoscroll.mp4`). Khi bật, mỗi khi từ/câu mới bắt đầu, extension **cuộn trang để đưa từ đang đọc vào vùng nhìn thấy** (hàm cuộn mượt bằng `requestAnimationFrame`, chỉ cuộn khi mục tiêu lệch > 15px so với vị trí hiện tại, tránh giật). Có hỗ trợ scroll bên trong element cuộn riêng (`scrollElement`) chứ không chỉ `window`.
- **Font hỗ trợ đọc:** có thêm tính năng đổi font toàn trang sang **Open-Dyslexic** và phóng to cỡ chữ (`init-DFBGYNXC.js` — ghi lại font-size/font-family gốc vào attribute rồi đổi, khôi phục khi tắt; chỉ áp dụng cho element trong viewport, theo dõi bằng `MutationObserver` + `scroll` debounce 600 ms).

### 5.3. Theo dõi vị trí phát (playback position tracking)

- Orator giữ `currentPlaybackLocation = {playableId, position: [sentenceIndex, wordIndex], ref, markCharIndex}`.
- Mỗi khi audio tiến triển, player phát event `progress` → Orator gửi `AUDIO_PROGRESSED {playableId, charIndex, markCharIndex}` → state machine cập nhật `currentPlaybackLocation` → hàm `Wj()` map vị trí này thành **câu/từ đang đọc** → vẽ highlight + cuộn nếu cần. Khi hết một playable → `AUDIO_PLAYBACK_ENDED` → chuyển sang playable kế (tăng `playbackCursorIndex`).
- Nhờ `markCharIndex` (chỉ số ký tự theo mốc thời gian của cloud) mà highlight bám đúng từ ngay cả khi tốc độ phát đổi (xem §8.4).

---

## 6. Chia nhỏ văn bản: block → playable → câu

1. **Trích block (content extraction):** hàm `F({startingNode, ignoreCache, shouldFindHeadings, minimumTotalCharThresholdForGaussianHeuristic: 4, getAllTextBlocksInitialThreshold: 1})` — một parser kiểu readability: duyệt DOM từ một node gốc, gom các text node thành **text block**, lọc bằng ngưỡng ký tự (heuristic Gaussian, ngưỡng tối thiểu 4 ký tự) và nhận diện heading. Kết quả là danh sách block, mỗi block giữ **tham chiếu tới element DOM gốc** (`ref`) để sau này highlight/seek được.
2. **Playable (đơn vị phát):** mỗi block thành một **playable** `{id, text, sentences[], ref, voice, duration}`. `sentences` được tách bằng **thư viện tách câu** bundle sẵn trong `chunk-F5R7TDWC.js` (tokenizer, xử lý abbreviation, ranh giới newline/HTML, tag cho phép `p/div/ul/ol`, giữ whitespace tùy chọn).
3. **Hàng đợi (queue):** Orator giữ `queue[]` + `playbackCursorIndex`. Mỗi playable được **tìm nạp audio trước** (prefetch — xem §8.3) để phát liên tục không ngắt.
4. **Tiến triển:** trong một playable, vị trí là `[sentenceIndex, wordIndex]`; hết câu → câu kế; hết playable → playable kế. Seek có 3 dạng: `seekToPosition(playableId, [s, w])` (nhảy tới câu/từ), `seekToProgress(0..1)` (tua theo %), `SEEK_TO_PLAYABLE`.
5. **Lưu ý:** chunking ở đây là **theo block/câu của trang**, không phải chunk theo số từ cố định. Độ dài một playable = một đoạn văn (paragraph) trên trang.

---

## 7. Audio pipeline đầy đủ

### 7.1. Giọng (voices) — cloud là chính, máy là dự phòng

- **Danh sách giọng** nằm trong file bundle `centralized-voice-list.json` (~260 KB): tổ chức theo tab ("Recommended" + tab theo ngôn ngữ), mỗi giọng có `displayName`, `name`, `language`, `engine`, `gender`, `labels` (use-case, accent, độ tuổi, âm sắc/timbre, phong cách), `avatarImage`, `previewAudio` (file mp3 mẫu host trên `vms.cdn.speechify.com`), `slug`.
- **2 engine:**
  - **`speechify`** — giọng riêng của Speechify (premium), gồm cả giọng người nổi tiếng/đối tác (vd: **Gwyneth Paltrow**, **John Rhys-Davies**; giọng mặc định **Geffen**, nữ, en-US). Đếm trong file có ~121 mục engine `speechify`.
  - **`azure`** — Microsoft Azure TTS. **Tiếng Việt có 2 giọng Azure: Linh (nữ) và Minh (nam), `vi-VN`.** Các ngôn ngữ khác cũng có giọng Azure tương tự.
  - Tổng cộng file liệt kê ~146 giọng nữ / ~146 giọng nam (đếm cả trùng lặp qua các tab). Theo tài liệu công khai **[nguồn web]**, Premium có 1000+ giọng, 60+ ngôn ngữ; free có 10 giọng chuẩn.
- **Giọng trên máy (client voices):** khi user free không có giọng cloud miễn phí, extension dùng **giọng có sẵn của trình duyệt/OS qua Web Speech API** (`window.speechSynthesis.getVoices()`). Code có trạng thái `hasClientVoices` và thông báo "No Free Voices — We couldn't find any free voices on your device. To begin listening:…" — tức free tier **fallback về giọng hệ thống**. Nghe thử giọng: giọng cloud thì phát file mp3 mẫu; giọng non-Speechify thì dùng `speechSynthesis` + SSML (`chunk-YU6JL2V4.js`).
- **Chọn giọng:** voice picker có tìm kiếm ("Search voices"), giọng yêu thích (`/user-settings/set-favorite-voices`), lịch sử chọn giọng (`get-voice-selection-history`), giọng nổi bật theo danh mục. Đổi giọng lúc đang phát → `setVoice` gửi `EDIT_ALL_PLAYABLES` (đổi voice cho cả queue).
- **Nam/nữ:** chọn trực tiếp theo từng giọng cụ thể (mỗi giọng có `gender`), không có toggle nam/nữ chung. **Không tìm thấy** tính năng "autoswitch voice tự phát hiện 2 ngôn ngữ" trong source (giọng gắn theo playable/queue, đổi tay) — xem §15.

### 7.2. API nào tạo audio

- **Endpoint cloud:** background service worker mở kênh message `"tts"` với 3 method: **`get-audio`**, **`get-playback-auth`**, **`fetch-preview-url`**. Audio server: **`https://audio.api.speechify.com`**; API host: **`https://4n41y71c5.api.speechify.com/v1`**; voices API: `https://voices.api.speechify.com/v0`; auth: `https://auth.api.speechify.com/v1`. (Các host khác: entitlement, library, books, catalog, llm, payment, user-profile, diagnostics.)
- **Không dùng Web Speech API để tạo giọng chính.** Web Speech API chỉ làm: (a) fallback giọng máy cho free tier, (b) nghe thử giọng non-Speechify, (c) hủy phát khi stop (`speechSynthesis.cancel()`).
- **Không có engine TTS bundle trong extension** (không có model ONNX/WASM cho TTS; file `ort.*.wasm` trong `offscreen/` phục vụ OCR/Voice Typing, không phải TTS). Toàn bộ giọng "hay" đều là **cloud**.

### 7.3. Stream / phát audio như thế nào

- **Fetch streaming:** `fetchStreamingAudioFn(text, voice, ...)` gọi cloud TTS ở chế độ **`streamBinaryChunks`** — response là stream các **chunk nhị phân MP3/OGG** (`"chunk_index"`, `"StreamChunkResponse"`, `"Response.Chunk"`), không phải một file mp3 tải xong mới phát. Nhờ vậy bấm play là có tiếng gần như ngay (buffering ngắn) dù đoạn dài.
- **Kèm `speechMarks`:** cùng với audio, server trả **`speechMarks`** — mốc thời gian từng từ/ký tự (có `endTime`). Orator cache audio theo `(playableId, voice)`; entry cache nào có `speechMarks` thì dùng để map thời gian → vị trí chữ (hàm `H5(duration, text, speechMarks)` → `[sentenceIndex, wordIndex]`; `H2(...)` → khoảng `[start, end]` của từ).
- **Player:** `streamingAudioPlayerFn(text, playbackRate)` — có 2 bản implement: **`f7(...)` chạy ở "offscreen"** (phát trong offscreen document — audio sống sót khi service worker ngủ/chuyển tab) và **`s7(...)` chạy ở "page"** (localAudioPlayerFn — giọng máy qua `speechSynthesis`); thêm `c7(...)` là `hdAudioPlayerFn` (offscreen). Trong code lõi có `new Audio` (2 chỗ) và dùng `currentTime`/`playbackRate` của HTML audio element (không thấy dùng `MediaSource` — 0 kết quả — và không dùng `AudioContext.decodeAudioData` cho luồng TTS chính).
- **Tiến trình:** player phát event `progress` → Orator nhận `{charIndex, markCharIndex}` → cập nhật `currentPlaybackLocation` (§5.3). Hết audio → event `ended` → `AUDIO_PLAYBACK_ENDED`.
- **Prefetch + cache:** chính sách `getPrefetchPolicyFn({playbackRate, voice})`; `maxLookahead` mặc định **4 playable** / `bufferTargetSeconds` **30 giây** — tức extension tải trước audio của ~4 đoạn kế tiếp (hoặc 30 giây) để phát liền mạch. Audio đã tải được cache lại (key theo playable + voice) nên tua lại/seek không cần tải lại.
- **Ước lượng thời lượng:** `predictDurationFn` ước lượng độ dài audio từ text + speechMarks để vẽ thanh tiến trình trước khi audio về đủ.

### 7.4. Điều khiển giọng/tốc độ (voice controls)

- **Tốc độ:** xem §9 (slider 0.4x–4.5x, free tối đa 1.5x). Tốc độ áp dụng bằng `playbackRate` của audio element (và `getPrefetchPolicyFn` cũng nhận `playbackRate` để tính prefetch).
- **Giọng:** đổi trong lúc phát được (EDIT_ALL_PLAYABLES); giọng gắn theo từng playable nên đổi giọng là đổi cho cả queue còn lại.
- **Tự tăng tốc ("Speed up as you go"):** setting `auto-speed-up` — "Speed up as you go every 500 words" (tăng tốc dần theo số từ đã nghe, gradient `speedRampGradient`).
- **Giới hạn free:** `DEFAULT_FREE_USER_SPEED_LIMIT` + thông báo "You reached your speed limit" khi kéo quá 1.5x ở bản free **[mức 1.5x khớp tài liệu công khai, nguồn web]**.

---

## 8. Điều khiển nhanh trong lúc phát (speed/voice)

### 8.1. Cách Speechify làm: control nằm TRONG player đang phát

- Trên **Pill Player** (và Side Panel) có sẵn nút **Speed** và **Voice**. Bấm → player **chuyển route nội bộ** sang view `/speed` hoặc `/voices` (không mở popup/Settings/cửa sổ mới):
  - **View Speed:** slider tùy chỉnh với **4 đoạn phi tuyến** — 0.4–1, 1–1.5, 1.5–3, 3–4.5 — mỗi đoạn 8 bước; có `aria-label="Playback Speed"`, `aria-valuenow`; kèm toggle "Speed up as you go every 500 words". Free bị chặn ở 1.5x ("You reached your speed limit").
  - **View Voice:** tìm kiếm giọng ("Search voices"), danh sách theo tab ngôn ngữ/danh mục, giọng yêu thích, bấm giọng là nghe thử ngay (mp3 mẫu hoặc `speechSynthesis`), chọn là đổi luôn cho queue đang phát.
- **Trong lúc phát, player luôn hiện trên trang** nên mọi control chỉ cách **1 cú click** — không cần rời khỏi trang, không cần mở Settings.
- **Nút Skip câu** (Backward/Forward) nằm ngay cạnh Play/Pause trên player — cũng là "điều khiển nhanh trong lúc phát" nhưng ở mức câu.
- Ngoài ra còn có **tooltip/hint** và **toast** (vd: "Save to Library") — không phải control phát.

### 8.2. So với ý tưởng của Manh (hover icon loa → mở tab 2/3 của panel để chỉnh nhanh)

- **Điểm giống:** cả hai đều muốn "chỉnh nhanh mà không mở Settings".
- **Điểm khác:** Speechify **không dùng hover icon để mở control** — control nằm sẵn trên player đang phát (player đã hiện thì không cần thêm bước hover icon). Hover icon của Speechify chỉ có 1 nhiệm vụ: **bắt đầu phát từ đoạn đó** (play/pause), không mở panel chỉnh.
- **Đánh giá sơ bộ cho TransPeek** (quyết định cuối ở `recommendation.md`): vì panel dịch của TransPeek **chỉ mở khi hover** (không hiện thường trực như pill của Speechify), ý tưởng "hover icon loa → mở tab 2/3 chỉnh tốc độ/giọng" là **hợp lý** — nó bù cho việc TransPeek không có player thường trực. Nhưng cần tránh xung đột với flow hiện tại (hover = dịch, click = nghe): nên gắn việc mở tab chỉnh vào **hover icon loa** (không phải click), hoặc vào **một nút nhỏ riêng trên panel khi đang phát**, chứ đừng bắt user click icon loa rồi mới chỉnh được (click đã mang nghĩa "nghe"). Một phương án khác đúng kiểu Speechify hơn: **khi đang phát TTS, hiện một mini-player nhỏ (dạng pill) có sẵn nút Speed/Voice** — user chỉnh trong 1 click, không cần qua tab 2/3. Chi tiết so sánh sẽ nằm ở `recommendation.md`.

### 8.3. Tốc độ/giọng có "preset" không?

- Không có preset 1x/1.2x/1.5x/2x dạng nút bấm sẵn trong source đã đọc — tốc độ là **slider liên tục** (4 đoạn, 8 bước/đoạn). Manh đề xuất preset 1x/1.2x/1.5x/2x cho TransPeek thì **đơn giản hơn slider của Speechify** và đủ dùng (đặc biệt khi free cap 1.5x thì preset 1/1.2/1.5 là vừa; 2x chỉ có ý nghĩa ở bản trả phí/cloud).

---

## 9. Mô hình icon: Model A vs Model B — kết luận

| Bề mặt | Model | Điều kiện |
|---|---|---|
| **Hover Player** (trang web thường) | **A** — hover đoạn văn trước, icon mới hiện | Chuột vào `hoverAreaRect` của đoạn đủ dài (>10 từ), nhìn thấy được, không bị nút/link che. Icon ở lề trái dòng đầu. |
| **Paragraph Player** (Google Docs) | **A** — hover đoạn văn trước, icon mới hiện | Như trên, nhưng adapter riêng cho DOM của Docs. |
| **Play Buttons** (ChatGPT, Gmail, Facebook, LinkedIn, Reddit, X, YouTube, Google Search…) | **B** — nút **hiện sẵn** trong từng post/email/câu trả lời | Chỉ trên domain có config tay (selector viết sẵn). Chèn bằng MutationObserver khi post xuất hiện. |
| **Pill "Listen" trên PDF** | **B** — hiện sẵn | Toàn tài liệu, 1 nút cố định giữa-dưới trang. |
| **Selection Player** | Không phải A/B — bôi chọn rồi phát (Alt+A / icon extension) | Có selection hợp lệ. |
| **Click-to-listen (sentence)** | Không phải A/B — chỉ chạy **trong lúc phát** | Hover câu → tô sáng → click để nhảy. |

**Kết luận model:** Speechify **không hề** pre-display icon loa cho mọi block text trên trang web thường. Cái "tự nhận diện block text và hiển thị sẵn icon" mà Manh mô tả (Model B) **chỉ tồn tại ở dạng**: (1) nút Play chèn sẵn trong từng post **trên site đã biết** (nhờ selector viết tay, không phải tự nhận diện generic), và (2) pill "Listen" trên PDF (1 nút cho cả trang). Trên trang web bất kỳ, Speechify dùng **Model A thuần túy** — và Model A của họ rất tốt vì: vùng hover rộng (cả đoạn), icon bám đúng dòng đầu (Range của từ đầu tiên), né nút/link, kiểm tra che khuất, bám theo scroll.

---

## 10. CÂU HỎI ĐÁNH GIÁ THEN CHỐT: Model B có tích hợp được vào app native (TransPeek macOS/Windows) không?

### 10.1. Vì sao extension làm được Model B (và Model A) trên web

- Extension chạy **trong cùng tiến trình render** với trang: nó có **toàn bộ DOM** trong tay, đọc/ghi rẻ (`getBoundingClientRect`, `Range`, `MutationObserver` báo thay đổi DOM gần như miễn phí).
- Parse toàn trang **một lần** khi content đổi (Orator `getContent`), rồi dùng lại danh sách block cho cả Hover Player lẫn highlight — không quét lại theo từng lần di chuột.
- Icon là **element DOM/shadow-DOM** trong chính trang → tự đi theo scroll, tự hưởng z-index/layering của trang, không cần cửa sổ riêng.
- Với site đã biết, selector viết tay cho biết **chính xác** post nằm ở đâu → chèn nút sẵn là rẻ và ổn định.

### 10.2. Tương đương native là gì

Để làm Model B native, TransPeek phải tự làm những việc mà DOM cho miễn phí:

| Việc trên web (miễn phí) | Tương đương native | Khả thi? |
|---|---|---|
| Đọc toàn bộ DOM | **Duyệt cây Accessibility (macOS) / UI Automation (Windows)** của app đang mở: `AXUIElementCopyAttributeValue(kAXChildren...)` / `IUIAutomation::GetRootElement + FindAll(TreeScope_Descendants)` | Được, nhưng **chậm** (cây AX của Chrome có thể hàng chục nghìn node; duyệt hết mất hàng trăm ms–giây) và **không đầy đủ** (xem §10.3). |
| `MutationObserver` báo DOM đổi | **Không có tương đương.** Native không có API "báo khi text trên màn hình đổi". Chỉ có một số notification rời rạc (vd: `AXSelectedTextChanged`, `AXValueChanged`, `AXWindowMoved`) mà không phải app nào cũng phát, và **không có notification cho scroll** ở đa số app. | **Không có** → phải **quét lại định kỳ (polling)**. |
| `getBoundingClientRect` / Range rect theo dòng | macOS: `AXUIElementCopyParameterizedAttributeValue(kAXBoundsForRangeParameterizedAttribute)` với `AXTextMarkerRange` — **chỉ một số app hỗ trợ** (TextEdit, Safari, Pages…). Windows: `TextPattern.GetBoundingRectangles()` — chỉ khi app expose `TextPattern` (Word, Edge, Notepad mới…). | Tùy app, **không phổ quát**. |
| Icon là element trong trang (tự theo scroll, tự đúng z-order) | **Cửa sổ nổi riêng** (`NSPanel` borderless, level `.floating`/`.screenSaver` / WinUI window `Topmost` + transparent) đặt chồng lên app khác. Phải **tự tính lại vị trí khi user cuộn** (vì không biết app cuộn lúc nào) và tự xử z-order (cửa sổ nổi có thể bị app khác che hoặc che nhầm popup của app). | Được, nhưng **tốn công + dễ lệch** (icon trôi khỏi dòng chữ khi cuộn, hoặc ở lại khi app đã chuyển trang). |
| Biết chính xác post nào là post nào (selector tay) | Không có "selector" cho app native. Chỉ có thể dựa vào role AX/UIA (`AXStaticText`, `AXTextArea`, `Text` control type) — **rất thô**, không phân biệt được "post Facebook" vs "label nút". | **Không tương đương.** |

### 10.3. Cái gì làm gãy Model B native

1. **App không expose text layout:** text vẽ tay (Canvas/WebGL, game, remote desktop, ảnh chụp màn hình, PDF render bằng hình), app Electron expose cây AX rất thô (thường chỉ có 1 node `AXWebArea` chứa toàn bộ trang, không có từng đoạn), app dùng framework tự vẽ (Flutter, Qt custom, Java Swing cũ). Với các app này, **không lấy được block/dòng nào cả** — chỉ còn cách OCR toàn màn hình (đúng như TransPeek đang làm bằng Vision/Windows.Media.Ocr) rồi tự suy block từ OCR boxes — chậm và kém chính xác hơn DOM nhiều.
2. **Không có thông báo thay đổi:** muốn icon "hiện sẵn và luôn đúng chỗ", phải quét lại cây AX/UIA **liên tục** (mỗi lần quét vài trăm ms–giây, tùy app) + quét lại khi cuộn (mà không biết khi nào user cuộn) → **tốn CPU/pin, giật máy**, và icon vẫn lệch trong khoảng giữa 2 lần quét.
3. **Quá nhiều icon:** một trang báo có hàng chục–trăm đoạn → hàng chục cửa sổ nổi cùng lúc (mỗi icon là 1 cửa sổ hoặc 1 view trong 1 cửa sổ lớn phủ màn hình). Quản lý z-order, click-through, hiệu năng vẽ — phức tạp và dễ lỗi.
4. **Quyền:** macOS cần **Accessibility (TCC)** để đọc cây AX của app khác + **Event Tap** để theo dõi chuột toàn cục; Windows cần **UI Automation** (đọc được, không cần quyền đặc biệt cho app thường, nhưng **không đọc được app chạy quyền admin** — UIPI — khi TransPeek chạy quyền thường).
5. **Trình duyệt là trường hợp khó nhất:** Chrome/Edge expose cây AX khổng lồ và chậm; Firefox/Safari khác nhau. Trong khi đó, chính trình duyệt lại là nơi user đọc nhiều nhất — và cũng là nơi **extension làm tốt nhất** (vì có DOM thật).

### 10.4. Verdict: **FEASIBLE-WITH-LIMITATIONS cho Model A; WEBSITE-ONLY trong thực tế cho Model B**

- **Model A (hover mới hiện icon): KHẢ THI-VỚI-GIỚI-HẠN trên native.** Vì chỉ cần hit-test **tại 1 điểm chuột** (khi chuột dừng/hover) thay vì quét toàn bộ màn hình — xem chi tiết ở §11.
- **Model B (quét sẵn, cắm sẵn icon mọi block): CHỈ THỰC TẾ TRONG NGỮ CẢNH WEB/DOM.** Trên native, chi phí quét liên tục + thiếu notification thay đổi/scroll + app không expose text khiến nó **không đạt chất lượng chấp nhận được** nếu làm generic cho mọi app. Model B native chỉ khả thi ở dạng **thu hẹp**: (a) chỉ cho **một số app đã biết** (adapter riêng, giống cách Speechify viết selector tay cho từng site — vd: chỉ làm cho Safari/Chrome/Word/PDF reader cụ thể); hoặc (b) chỉ quét **khi user dừng chuột / bấm phím tắt** (biến tướng thành Model A có "quét trước 1 vùng"); hoặc (c) làm bằng **browser extension riêng** cho phần web (Path 2 — xem `recommendation.md`).
- **Khuyến nghị thực tế cho TransPeek:** làm **Model A** cho native (hover → icon → click nghe; hover → dịch theo flow hiện có), và để **Model B cho extension trình duyệt riêng** nếu sau này làm (nơi DOM cho phép). Đừng cố làm Model B generic trên desktop.

---

## 11. CÂU HỎI KỸ THUẬT: TransPeek có tự phát hiện chuột hover lên dòng text ở app khác và hiện icon loa được không?

### 11.1. Model A native — được, với giới hạn (tương tự §10.4)

**macOS (Swift/SwiftUI + AppKit + Accessibility):**
- **Theo dõi chuột toàn cục:** `NSEvent.addGlobalMonitorForEvents(matching: .mouseMoved)` (cần quyền Accessibility) hoặc `CGEventTap` (cần Accessibility + tin cậy cao hơn). Throttle ~30–60 ms (đừng hit-test mỗi event chuột — quá đắt).
- **Hit-test:** `AXUIElementCopyElementAtPosition(app, x, y)` (cần `AXUIElementCreateSystemWide()` + quyền Accessibility) → element dưới chuột → kiểm tra role có phải text không (`AXStaticText`, `AXTextArea`, `AXTextField`, `AXWebArea`…), lấy text (`kAXValueAttribute`) và frame (`kAXFrameAttribute`).
- **Rect theo dòng:** nếu app hỗ trợ, dùng `AXTextMarker` (`kAXBoundsForRangeParameterizedAttribute`, `kAXTextMarkerRangeForUIElementParameterizedAttribute`) để lấy rect của dòng/từ — tương đương `Range.getBoundingClientRect()` trên web. **Không phải app nào cũng hỗ trợ** (Safari/TextEdit/Pages tốt; Chrome/Electron kém; app vẽ tay không có).
- **Đặt icon:** `NSPanel` borderless, `level = .floating` (hoặc `.screenSaver` nếu muốn trên cả fullscreen), `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]`, `isMovableByWindowBackground = false`, đặt tại `frame.origin.x - iconSize` (lề trái dòng) như Speechify. Icon là `NSView` vẽ hình tròn + SVG play. Cần **tự theo dõi scroll**: vì không có notification scroll đáng tin, cách thực tế là **poll lại vị trí mỗi ~100–200 ms trong khi icon đang hiện** (chỉ khi đang hover, không poll liên tục cả ngày), hoặc ẩn icon ngay khi phát hiện chuột/cửa sổ đổi (dùng `AXWindowMoved`/`AXWindowResized` notification nếu app có phát).
- **Quyền:** bắt buộc **Accessibility (TCC)** — user phải bật trong System Settings > Privacy & Security > Accessibility; không có quyền thì không hit-test được app khác. Đây là rào cản UX lớn nhất (TransPeek đã cần quyền này cho float icon hiện tại — cần nói rõ với user là bật 1 lần dùng cho cả 2 tính năng).

**Windows (WinUI 3 + C# + UI Automation):**
- **Theo dõi chuột:** low-level mouse hook `WH_MOUSE_LL` (`SetWindowsHookEx`) — không cần quyền đặc biệt; hoặc `TrackMouseEvent`/poll `GetCursorPos` bằng timer.
- **Hit-test:** `IUIAutomation.ElementFromPoint(x, y)` → `IUIAutomationElement`; kiểm tra `ControlType` (`Text`, `Edit`, `Document`) và `IsTextPatternAvailable`. Lấy text qua `TextPattern.GetSelection()` / `DocumentRange.GetText()`, lấy **rect từng dòng** qua `TextPattern.GetVisibleRanges()` + `ITextRangeProvider.GetBoundingRectangles()` (tương đương Range rect trên web — Word/Edge/Notepad hỗ trợ; Chrome/Electron hỗ trợ một phần; app vẽ tay không có).
- **Đặt icon:** cửa sổ WinUI borderless, `Topmost`, nền transparent, `WS_EX_NOACTIVATE` (để không cướp focus), đặt tại lề trái dòng. Theo dõi scroll bằng cách **poll lại `GetBoundingRectangles` mỗi ~100–200 ms** khi icon đang hiện (UIA không có event scroll chung).
- **Quyền:** đọc UIA của app thường không cần quyền admin; **app chạy quyền admin (UIPI) thì TransPeek chạy quyền thường không đọc được** — cần chạy TransPeek quyền admin mới đọc được (không nên ép user).
- **Hiệu năng:** `ElementFromPoint` trên cây UIA lớn (Chrome) có thể mất **chục–trăm ms**; phải throttle + chỉ hit-test khi chuột **dừng lại ~100–150 ms** (hover dwell) thay vì mỗi lần di chuột, và cache element/rect vừa hit-test.

**Giới hạn chung (cả 2 nền tảng):**
- App không expose text qua AX/UIA → không hit-test được → **rơi về flow hiện tại của TransPeek: bôi chọn text → icon hiện** (flow này luôn hoạt động vì dựa vào clipboard/selection, không cần AX tree). Đây là lý do flow bôi-chọn phải **giữ làm mặc định**, hover-icon chỉ là **tùy chọn bổ sung**.
- Độ trễ: hit-test AX/UIA chậm hơn DOM vài chục lần — icon sẽ hiện **chậm hơn** trên web (cảm giác ~100–300 ms sau khi hover, thay vì tức thì). Cần debounce/dwell để tránh icon nhấp nháy khi rê chuột nhanh.
- Icon trôi khi cuộn: không có notification scroll → phải poll hoặc ẩn icon khi phát hiện scroll (bằng cách so sánh rect cũ/mới).
- **Kết luận Model A native: KHẢ THI, nên làm dạng opt-in** (toggle trong Settings, mặc định tắt hoặc chỉ bật cho app đã biết), với dwell ~150 ms + throttle + fallback về bôi-chọn.

### 11.2. Model B native (icon hiện sẵn không cần hover) — không thực tế

- Như §10.4: cần **quét liên tục toàn bộ cây AX/UIA** của app trước mặt (hàng trăm ms–giây mỗi lần quét), **không có notification** khi nội dung/scroll đổi, phải quản lý **hàng chục cửa sổ nổi** cùng lúc, và **gãy hoàn toàn** với app không expose text (Electron thô, Canvas, ảnh).
- Trên web, Model B rẻ vì: parse 1 lần + MutationObserver miễn phí + icon là element trong trang. Native **không có bất kỳ điều nào trong 3 điều đó**.
- **Kết luận: Model B chỉ thực tế trong DOM của trang web.** Trên native, thứ gần nhất làm được là: **khi user dừng chuột ~300–500 ms trong một vùng text, quét nhanh vùng đó (chỉ vùng nhìn thấy) rồi hiện icon cho block đó** — nhưng đó thực chất đã là Model A có "quét trước", không phải Model B.

### 11.3. So sánh với "con trỏ I-beam tự đổi"

- I-beam **không phải** do OS hit-test: chính **app đang chạy** tự báo với OS rằng "vùng này là text" (qua `NSTextView`/`UIElement` của nó) và OS đổi cursor theo. App tự biết layout của mình nên việc này rẻ và chính xác.
- TransPeek ở **bên ngoài** app: phải hỏi ngược lại app qua AX/UIA (chậm, thiếu, tùy app). Vì vậy **không thể kỳ vọng độ mượt như I-beam** — icon của TransPeek sẽ luôn chậm hơn và kém tin cậy hơn cursor của hệ thống. Đây là giới hạn kiến trúc, không phải do code chưa tối ưu.

---

## 12. Khả thi tích hợp vào TransPeek

### 12.1. Bảng mapping: cái gì chuyển thẳng, cái gì phải viết lại

| Tính năng Speechify | Chuyển sang TransPeek được không? | Ghi chú |
|---|---|---|
| Hover → icon loa ở lề trái dòng đầu (Model A) | **Được — viết lại native** (§11.1) | Dùng AX hit-test (mac) / UIA ElementFromPoint (Win); icon = cửa sổ nổi; dwell ~150 ms; chỉ khi app expose text; fallback bôi-chọn. |
| Click icon → đọc từ đầu đoạn | **Được — concept chuyển thẳng** | TransPeek đã có flow click icon → TTS; chỉ cần thêm "đọc từ đoạn đang hover" (lấy text qua AX/UIA hoặc clipboard). |
| Tô sáng câu + từ đang đọc | **Được — viết lại** | Cần **word timing**: macOS `AVSpeechSynthesizerDelegate.speechSynthesizer(_:willSpeakRangeOfSpeechString:utterance:)` cho ra range từng từ (dùng được với giọng hệ thống); Windows WinRT `SpeechSynthesizer` **không có** word-boundary event → muốn highlight từng từ trên Windows thì cần **cloud TTS trả speechMarks** (như Speechify) hoặc SSML bookmark, hoặc chỉ highlight theo **câu** (dễ hơn nhiều: tự chia câu, đổi câu khi hết câu). |
| Auto-scroll theo câu/từ | **Được — viết lại, chỉ trong panel của mình** | TransPeek không cuộn được app khác; chỉ cuộn được **text hiển thị trong panel của TransPeek** (khi panel đang show text gốc/bản dịch). |
| Click câu đang phát để nhảy tới | **Được — trong panel của mình** | Chỉ áp dụng cho text trong panel TransPeek (click câu → seek TTS). Không áp dụng cho text ở app khác (vì không sửa được DOM của app khác). |
| Pill Player nổi + Speed/Voice trong player | **Được — concept chuyển thẳng** | TransPeek có thể thêm **mini-player khi đang phát TTS** (nút pause, nhảy câu, speed, voice) — xem §8.2. Đây là phần **nên học nhất** từ Speechify. |
| Speed slider 0.4–4.5x + auto-speed-up | **Được** | Với giọng hệ thống: macOS `AVSpeechUtterance.rate`, Windows `SpeechSynthesizer.Options.SpeakingRate` (0.5x–2x ở WinRT — hẹp hơn 0.4–4.5x của cloud). Muốn 4.5x thì cần cloud TTS. |
| Nút Play chèn sẵn trong từng post (Model B) | **Không — chỉ làm được trong extension trình duyệt** | Cần DOM + selector tay từng site. Nếu TransPeek làm extension Edge/Chrome riêng (Path 2) thì đây là tính năng của extension, không phải của app native. |
| Cloud TTS + speechMarks + prefetch/cache | **Không tái dùng được — phải tự xây** | Backend Speechify là proprietary. TransPeek muốn tương đương thì cần **tự tích hợp một cloud TTS có word timing** (vd: Azure TTS trả `viseme`/`word boundary` qua SSML, hoặc ElevenLabs…) — tốn chi phí/vận hành, hoặc chấp nhận chỉ dùng giọng hệ thống (không có word timing trên Windows). |
| Voice picker (tìm kiếm, yêu thích, nghe thử) | **Được — concept chuyển thẳng** | TransPeek đã có tab TTS Speech (Windows) / tab audio (mac) liệt kê giọng hệ thống — học thêm: nghe thử giọng, yêu thích, lịch sử chọn. |
| PDF "Listen" pill | **Được — concept chuyển thẳng (mức tài liệu)** | TransPeek mở PDF thì có thể hiện 1 nút "Nghe" cho cả tài liệu (đọc text đã OCR/trích từ PDF). |
| Dyslexic font / tăng cỡ chữ toàn trang | **Không áp dụng** | Chỉ có ý nghĩa trong trình duyệt (sửa DOM trang). TransPeek chỉ chỉnh được font trong panel của mình. |

### 12.2. macOS — `TranManhTuanLabs/TransPeek` (Swift/SwiftUI, AppKit, Vision OCR, popup panel)

- **Chuyển thẳng:** concept hover-icon (Model A), pill/mini-player khi phát, skip câu, speed/voice trong player, highlight câu/từ (nhờ `AVSpeechSynthesizer` có word-range callback — lợi thế của mac), click câu trong panel để seek.
- **Viết lại native:** hit-test qua `AXUIElementCopyElementAtPosition` + `AXTextMarker` (thay cho DOM Range); icon overlay bằng `NSPanel` borderless (thay cho shadow-DOM div); highlight bằng overlay `NSView` vẽ rect (không sửa được text của app khác — chỉ highlight được text trong panel của TransPeek, hoặc vẽ rect phủ lên app khác bằng cửa sổ trong suốt — **khuyên không vẽ phủ lên app khác**: dễ lệch khi cuộn, gây khó chịu).
- **Tận dụng sẵn có:** Vision OCR của TransPeek là **fallback quý giá** cho app không expose text (Speechify không có fallback này trên web) — khi AX không lấy được text, dùng OCR vùng dưới chuột (chụp màn hình vùng nhỏ quanh chuột → Vision) rồi mới hiện icon. Chậm hơn nhưng phủ được app vẽ tay/ảnh.
- **Thứ tự làm gợi ý:** (1) mini-player khi phát TTS (dễ, giá trị cao); (2) highlight câu (dễ) rồi từ (nhờ AVSpeech word-range); (3) hover-icon Model A dạng opt-in, chỉ bật cho app đã test (Safari, TextEdit…) rồi mở rộng dần.

### 12.3. Windows — `TranManhTuanLabs/TransPeek-Windows` (WinUI 3 + C#)

- **Chuyển thẳng:** như macOS (hover-icon concept, mini-player, skip câu, speed/voice picker, voice picker có nghe thử).
- **Viết lại native:** hit-test qua `IUIAutomation.ElementFromPoint` + `TextPattern.GetBoundingRectangles()`; icon overlay bằng WinUI window `Topmost` + transparent + `WS_EX_NOACTIVATE`; theo dõi chuột bằng `WH_MOUSE_LL` hook.
- **Khó hơn mac ở 2 điểm:**
  1. **Word timing:** WinRT `SpeechSynthesizer` không có word-boundary event (macOS có). Muốn highlight từng từ trên Windows thì hoặc (a) chỉ highlight theo **câu** (khả thi ngay: tự chia câu, chuyển câu khi `SpeechEnded` của từng câu — đọc từng câu một thay vì cả đoạn), hoặc (b) dùng **cloud TTS có speechMarks** (tốn chi phí), hoặc (c) dùng `System.Speech` (.NET Framework, có `SpeakProgress` — nhưng không dùng được sạch trong WinUI 3 / packaged app, cần cân nhắc).
  2. **UIA chậm và thiếu trên Chrome/Electron:** `ElementFromPoint` trên Chrome có thể mất hàng trăm ms — cần dwell + cache + giới hạn chỉ hit-test khi chuột dừng.
- **Tận dụng sẵn có:** `SystemSpeechService` (đã có: `SpeechSynthesizer` + `MediaPlayer`, chọn giọng theo ngôn ngữ) + tab TTS Speech (đã có: accent, rate 0.5x–2x, chọn giọng EN/VI, test) — chỉ cần thêm **mini-player khi phát** (speed preset, voice, skip câu) và **highlight theo câu** là đã gần Speechify ở mức cơ bản.

### 12.4. Lưu ý cho flow "1 icon chung" của TransPeek (hover = dịch, click = nghe)

- Hover-icon Model A của Speechify là **1 icon = 1 việc (nghe)**. TransPeek có **1 icon = 2 việc** (hover → dịch, click → nghe). Khi thêm hover-icon cho app khác, cần giữ nguyên ngữ nghĩa này: **icon hiện khi hover đoạn văn; hover icon → panel dịch (như hiện tại); click icon → nghe đoạn đó**. Đừng để hover icon tự mở panel dịch che mất icon (panel dịch nên mở lệch sang bên, icon vẫn bấm được) — hoặc icon loa chỉ hiện khi panel dịch chưa mở.
- Rủi ro: user hover đoạn văn để đọc/dịch, icon loa hiện lên che chữ (đặc biệt khi icon ở lề trái mà đoạn văn sát mép trái màn hình). Học Speechify: icon nhỏ (20px), có kiểm tra che khuất + né nút, và **chỉ hiện khi chuột dừng** (dwell) chứ không hiện ngay khi rê qua — giảm phiền nhiễu.

---

## 13. Bản quyền / licensing

- **Speechify là sản phẩm proprietary** (Speechify Inc.), code trong `.crx` đã minify nhưng vẫn thuộc bản quyền của họ. **Không sao chép code, không trích nguyên văn file, không dùng lại asset** (icon, âm thanh `audio/en-US-Henry-Audio-Visual-CTA.mp3`, font, video minh họa) của extension.
- **Được phép học:** các **pattern UX chung** đã mô tả trong báo cáo này (icon play ở lề trái dòng đầu khi hover, pill player có speed/voice bên trong, nhảy câu, tô sáng câu/từ theo thời gian phát, prefetch audio) — đây là các mẫu giao diện/hành vi phổ biến, không phải sáng chế riêng; TransPeek **tự implement lại từ mô tả**, bằng code của mình, trên stack của mình.
- **Giọng đọc:** giọng `speechify` (Geffen, Gwyneth Paltrow, John Rhys-Davies…) và giọng Azure (Linh, Minh…) là **tài sản thương mại** — TransPeek **không được** dùng lại. Muốn giọng hay tương đương thì tự mua/tích hợp: giọng hệ thống (miễn phí, đã có) hoặc cloud TTS riêng (Azure/ElevenLabs… — tự trả phí, tự quản lý key).
- **Backend/API:** các endpoint `*.api.speechify.com` là dịch vụ riêng, cần tài khoản + trả phí — không dùng được cho TransPeek. Mọi tích hợp cloud TTS cho TransPeek phải qua provider khác do Manh chọn.
- **Tên/thương hiệu:** không dùng tên "Speechify", logo, hay claim "powered by Speechify" trong TransPeek.

---

## 14. Những điểm chưa xác minh được từ source (ghi rõ để khỏi hiểu lầm)

1. **Code đã minify, không có sourcemap** — mọi suy luận về tên hàm/biến nội bộ (vd: `Oe`, `Ae`, `F`, `Z`) là suy từ hành vi + string literal, không phải tên gốc. Một số predicate (vd: danh sách trang loại trừ Hover Player trong `un()`) đã minify thành các hàm 1 ký tự nên **không đọc được điều kiện chính xác**.
2. **Hover Player có bật mặc định cho mọi user không?** — Code cho thấy có remote config + lifecycle loại trừ lẫn nhau với Paragraph Player, nhưng **không xác định được** giá trị mặc định (bật/tắt) và điều kiện rollout theo tài khoản (free/premium) chỉ từ source. **[Nguồn web]** SlashGear (2022) mô tả hover → nút play hiện cạnh đoạn văn như hành vi mặc định — nhưng bài đó đã cũ (863 ngày) và có thể khác bản 14.15.0.
3. **Vị trí chính xác của Pill Player** (góc dưới-phải vs giữa-dưới): code cho host `position: absolute; bottom: 0; right: 0` và một chỗ set `bottom: 15%`, nhưng do minify nên **không khẳng định được** tọa độ cuối cùng trên mọi kích thước màn hình. Riêng pill PDF thì chắc chắn **giữa-dưới** (CSS ghi rõ trong source).
4. **"Speed Reader"** — chuỗi này xuất hiện gần SpeedControls trong `chunk-EG263LGW.js`, nhưng **không rõ** đó là tên một tính năng riêng (RSVP?) hay chỉ là label của mục tốc độ. Không kết luận.
5. **Autoswitch voice theo ngôn ngữ** — **không tìm thấy** trong source (giọng gắn theo playable/queue, đổi tay). Có thể tính năng này không tồn tại trong extension, hoặc nằm ở web app/mobile (không có trong `.crx`). Đừng giả định là có.
6. **Chi tiết giao thức `get-audio`** (method HTTP, tham số SSML, cách server đóng gói speechMarks vào stream) — chỉ thấy tên method + host + chế độ `streamBinaryChunks`, **không thấy** request/response mẫu (nằm trong code minify sâu của background). Muốn biết chính xác phải bắt network khi chạy thật.
7. **Hiệu năng thực tế** (hit-test AX/UIA mất bao lâu, Hover Player có giật trên trang nặng không) — không đo được từ source tĩnh; cần test live (được phép cài extension để quan sát — nhưng báo cáo này chỉ dựa trên source + tài liệu công khai).
8. **Giá/limit free chính xác tại thời điểm đọc** (10 giọng, cap 1.5x, 5 file library) lấy từ **tài liệu công khai của Speechify [nguồn web]**, không phải từ source trong `.crx` (source chỉ có `DEFAULT_FREE_USER_SPEED_LIMIT` và chuỗi "You reached your speed limit" mà không ghi rõ số).

---

## 15. Phụ lục — file nguồn đã đọc & endpoint

**File đã đọc kỹ (đủ để kết luận):**
- `manifest.json` — quyền, content script, commands, side panel.
- `content/main.js` — probe selection, PDF pill, lifecycle các feature, play/pause Alt+A.
- `content/init-BKLAAS4F.js` — **Hover Player** (toàn bộ logic vị trí, lọc, che khuất, né nút, render, click).
- `content/chunk-UJT4KNAL.js` — **Click-to-listen / sentence player** trong lúc phát.
- `content/chunk-WTBO35FW.js` + `content/playButtonData-IZ7PLJUL.js` — config play button theo domain.
- `content/init-2T3JS3R2.js` (Google Search), `content/init-2F54DTKL.js` (Yahoo Mail), `content/chunk-732QWX3W.js` (nút Listen/Pause dùng chung) — play button chèn sẵn.
- `content/init-ZVV4TUIA.js` — **Pill Player** (control, route /speed, /voices, skip câu).
- `content/init-RGA2DWWB.js`, `content/init-DH5HHXIJ.js` — side player, voice/speed picker, granular settings.
- `content/chunk-EG263LGW.js` — settings UI (highlight, auto-scroll, speed slider 0.4–4.5x, auto-speed-up, voice picker).
- `content/chunk-F5R7TDWC.js` (~765 KB) — **Orator** (state machine phát, queue playables, prefetch, speechMarks → highlight, auto-scroll) + sentence splitter + highlighter.
- `content/init-UGHVGDP3.js` — Paragraph Player (Google Docs).
- `content/init-DFBGYNXC.js` — dyslexic font / font-size toàn trang.
- `background/main.js` (~2,9 MB) — API host, kênh `tts` (`get-audio`, `get-playback-auth`, `fetch-preview-url`).
- `offscreen/src/main.js` — phát audio ở offscreen, WebSocket (voice typing/meeting — không phải TTS).
- `sidepanel/sidepanel.html` + `sidepanel/main.js` — side panel player.
- `css/speechify.css` — container, z-index, hover popup, print-hide.
- `centralized-voice-list.json` (~260 KB) — danh sách giọng (engine `speechify` + `azure`; vi-VN: Linh, Minh).
- `features.json` — chỉ chứa `{"hermes": true}` (không có thông tin thêm).

**Endpoint/host đã thấy trong source:**
- `https://audio.api.speechify.com` (audio server), `https://4n41y71c5.api.speechify.com/v1` (API host), `https://auth.api.speechify.com/v1`, `https://voices.api.speechify.com/v0`, `https://entitlement.api.speechify.com`, `https://library.api.speechify.com`, `https://books.api.speechify.com`, `https://catalog.api.speechify.com`, `https://llm.api.speechify.com`, `https://payment.api.speechify.com`, `https://user-profile.api.speechify.com`, `https://diagnostics.api.speechify.com`, `https://vms.cdn.speechify.com` (avatar + mp3 nghe thử giọng).

**Tài liệu web đã đối chiếu (đánh dấu [nguồn web] trong bài):**
- SlashGear, "Can Google Chrome Read To You?" — mô tả hover đoạn văn → nút play hiện cạnh đoạn.
- Speechify Help Center, "Listen to a webpage" — flow bấm icon extension → play từ đầu bài; tap vào từ để đọc từ chỗ đó.
- Speechify blog, "How to have Google Chrome read aloud" / "What free text to speech for dyslexia can you use in 2026?" — free: 10 giọng chuẩn, tối đa 1.5x; Premium: 1000+ giọng, 60+ ngôn ngữ, tối đa 4.5x.

---

## 16. Kết luận ngắn cho Manh

1. **Speechify trên web thường dùng Model A** (hover đoạn → icon loa ở lề trái dòng đầu → click để nghe). Model B (nút hiện sẵn) chỉ có trên site đã biết (nhờ selector viết tay) và trên PDF. **Đừng hiểu lầm rằng Speechify "tự nhận diện mọi block và hiện sẵn icon" trên mọi trang** — không phải vậy.
2. **Cái đáng học nhất:** (a) icon bám đúng dòng đầu bằng Range của từ đầu tiên + né nút/link + kiểm tra che khuất; (b) **mọi control tốc độ/giọng/nảy câu nằm ngay trong player đang phát** (1 click, không mở Settings); (c) highlight câu+từ bằng speechMarks của cloud + click câu để nhảy tới trong lúc phát.
3. **Cho TransPeek native:** làm **Model A** (hover → icon, opt-in, có dwell, fallback bôi-chọn) + **mini-player khi phát TTS** + **highlight theo câu** (từ thì mac làm được ngay nhờ AVSpeech word-range, Windows cần cloud TTS hoặc chỉ highlight câu). **Model B generic trên desktop là không thực tế** — phần web-style (nút hiện sẵn theo block) nên để cho **extension trình duyệt riêng** (Path 2) nếu sau này làm.
4. Khuyến nghị tổng thể (Path 1 vs Path 2, RSVP, multi-tab panel, break app, quick-controls) nằm ở **`recommendation.md`** cùng thư mục.
