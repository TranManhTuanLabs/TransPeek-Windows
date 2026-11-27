# Highlight Colors — màu + hiện/ẩn 6 lớp (bản nháp)

> Trạng thái: **bản nháp — SPEC, chưa làm code.** Windows chưa có mục
> "Highlight colors" trong Settings và chưa có overlay highlight trên
> trang (bước 2). Doc này định nghĩa màu + luật hiển thị cho cả mặt đọc
> (Full / Lite / Nghỉ — xem `FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md`) lẫn
> overlay trên trang khi bước 2 làm.
>
> Mirror spec mac `docs/UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md`
> (repo `TranManhTuanLabs/TransPeek`, main). Màu đã chốt 2026-11-27
> theo Speechify + pastel bản thử bên mac. Windows dịch sang ngữ cảnh
> WinUI (xem §6): Settings → **TTS Speech** (Windows không có tab
> "Audio" như mac), `ColorPicker` native WinUI, khung qua UIA
> `TextPattern.GetBoundingRectangles()` thay cho `AXBoundsForRange`.

## 1. Bố cục — 6 hàng + preset + Reset all

Mục "Highlight colors" trong Settings → TTS Speech. 6 hàng, thứ tự
đã chốt:

**Underline → Vertical line → Word → Sentence → Paragraph → Block.**

Mỗi hàng, từ trái sang phải:

- **Tên + 1 dòng mô tả** — tên lớp ("Underline", "Vertical line",
  "Word", "Sentence", "Paragraph", "Block") + 1 dòng nói lớp đó đánh
  dấu cái gì.
- **Nút info ("i")** — nút nhỏ sau chữ của hàng. Bấm mở popover giải
  thích lớp đó hoạt động ra sao + phát hiện bằng cách nào, 2–3 câu
  tiếng Anh (giữ nguyên wording như mac — xem §2). Popover của
  Vertical line và Paragraph có thêm mục "How it works"; 4 lớp còn lại
  chỉ có phần giải thích.
- **Hex** — màu hiện tại dạng `#RRGGBB` (monospace). Chỉ hiển thị, không
  phải ô nhập. Đặt sát nhóm Show/Hide + Toggle ([hex] [Show/Hide]
  [Toggle]) để 3 cái đọc như 1 control, tách khỏi colour well.
- **Toggle Show/Hide** — `Toggle` native WinUI cạnh colour well. Nhãn
  theo trạng thái: "Show" khi lớp đang bật, "Hide" khi đang tắt — luôn
  nói lớp đang hiện hay đang ẩn. Màu vẫn chỉnh được khi lớp đang ẩn,
  để user chỉnh trước rồi bật sau.
- **Colour well** — `ColorPicker` native WinUI (không chỉnh opacity).
  Bấm mở panel màu hệ thống (wheel, slider, palette, eyedropper).
- **Reset** — trả riêng lớp đó về màu mặc định.

Dưới mỗi hàng: **6 preset swatch** (hình tròn): bấm 1 cái là đổi màu
lớp đó ngay. Màu hiện tại có vòng đậm để thấy preset nào (nếu có) đang
dùng. Bộ preset gồm các màu của 6 lớp (theo mac); không nhập hex tay.

Chỉ dưới hàng Vertical line: picker **"Line follows"** (xem §4):
Sentence / Paragraph / Block.

Cuối mục: **"Reset all to default"** — trả màu + Toggle Show/Hide của
cả 6 lớp + picker "Line follows" về mặc định chính (không động tới
toggle "Hide highlights while scrolling" bên dưới — đó là tuỳ chọn
hành vi, không phải mặc định của lớp).

## 2. 6 lớp + màu mặc định (đã chốt 2026-11-27)

| Lớp | Đánh dấu cái gì | Mặc định | Opacity | Bật mặc định |
|---|---|---|---|---|
| Underline | Vạch 2.5 pt đặc dưới từ + câu đang đọc | `#5666F0` (chàm, cùng màu từ) | 1.0 (đặc) | Bật |
| Vertical line | Vạch 1.75 pt đặc ở mép trái block (trong dải 8 pt bên cạnh chữ); dài theo đơn vị chọn ở §4 | `#8CBFFF` (xanh dương nhạt) | 1.0 (đặc) | Tắt |
| Word | Tô nền từ đang nói | `#5666F0` (chàm Speechify) | 0.30 | Bật |
| Sentence | Tô nền câu đang đọc | `#ABB3FE` (tím nhạt Speechify) | 0.30 | Tắt |
| Paragraph (cụm) | Tô nền đoạn quanh câu đang đọc — nhóm câu không có ranh giới đoạn giữa chúng (dòng trống, 1 lần xuống dòng, hoặc câu sau thụt đầu dòng). Block không có ranh giới thì KHÔNG có fill cụm (đoạn duy nhất đó CHÍNH LÀ cả block) | `#FFC2D1` (hồng nhạt) | 0.30 | Tắt |
| Block | Tô nền cả block đang đọc | `#BFF2B3` (xanh lá nhạt) | 0.30 | Tắt |

**Mặc định chỉ bật từ + gạch chân** — Manh muốn lần đầu hiện ít nhất,
user tự bật thêm. Màu mặc định là hex `#RRGGBB`. Block, paragraph và
line giữ bảng pastel từ bản thử (Manh 2026-11-24 00:38): hex là giá trị
làm tròn của RGB overlay dùng trước đó, nên nhìn giống hệt màu cũ.
Paragraph mặc định hồng nhạt — 1 preset đã có trong bảng, khác 4 fill
còn lại. Từ và câu theo Speechify (Manh 2026-11-27 03:41 — đọc từ source
extension Speechify: theme mặc định, trang sáng): từ là highlight50
chàm `#5666F0`, câu là highlight30 tím nhạt `#ABB3FE`. Câu của
Speechify gốc là `#e0e3ff` (highlight20) nhưng ở fill 0.30 thì nhạt tới
mức gần như không thấy, nên câu dùng bậc đậm hơn 1 nấc. Speechify tô
đặc (1.0) đồng thời tô lại màu chữ; overlay này tô đè lên trang nên
không tô lại màu chữ được — fill giữ mờ 0.30 để chữ bên dưới vẫn đọc
được. Chỉ lấy hue của Speechify, không lấy opacity. Gạch chân mặc định
là chàm của từ `#5666F0` — gạch chân đánh dấu từ + câu nên cùng màu từ.
Thứ tự vẽ (dưới lên trên): block, paragraph, sentence, word, rồi vạch,
rồi gạch chân — fill mờ nên các lớp chồng lên nhau hoà vào nhau thay vì
che nhau; vạch và gạch chân đặc nên đọc rõ trên cùng.

**Popover giải thích (giữ nguyên wording tiếng Anh như mac):**

- Underline: "A thin line under the word and sentence being read, in
  the underline color. It shows even when the fills are off, so you
  can use it instead of the fills. No rectangle, so no hard edge."
- Vertical line: "A thin vertical bar left of the block, as tall as
  the unit it follows (see 'Line follows': sentence / paragraph /
  block). It helps the eye find the unit being read at a glance."
  Thêm mục "How it works": picker "Line follows" (Sentence / Paragraph
  / Block, mặc định Sentence); fallback đơn vị chọn → câu → cụm, không
  bao giờ về block; không có frame nào thì không vẽ vạch; vạch vẫn theo
  đơn vị dù fill của đơn vị đó đang ẩn (xem §4).
- Word: "Highlights the word being spoken. It needs the app to
  provide the word's position (`TextPattern.GetBoundingRectangles` —
  mac: `AXBoundsForRange`); if the app does not provide it, the word
  highlight is skipped."
- Sentence: "Highlights the sentence containing the word being
  spoken. It needs the app to provide the sentence's position; if
  the app does not provide it, the sentence highlight is skipped."
- Paragraph: "Highlights the paragraph containing the current
  sentence. A paragraph is detected by a blank line, a single line
  break, or an indented line. If the block has no paragraph break,
  the cluster is not shown — the block has its own Show toggle."
  Thêm mục "How it works": cụm là đoạn chứa câu đang đọc (dòng trống,
  1 lần xuống dòng, hoặc dòng thụt đầu dòng — thụt đầu dòng nghĩa là
  tab hoặc 2+ khoảng trắng ở đầu câu sau; 1 khoảng trắng không tính);
  block không có ranh giới đoạn thì không có fill cụm — dùng Toggle
  Show của block để tô cả block (xem §3).
- Block: "Highlights the whole text block being read. It is always
  available when a block is detected."

## 3. Hành vi

- **Đổi là có hiệu lực ngay — không restart.** Overlay đọc màu từ
  settings lúc vẽ, và timer follow vẽ lại mỗi 0.3 s trong lúc hiện, nên
  màu chọn trong Settings hiện ở lần vẽ sau (ngay nếu indicator đang
  hiện, không thì ở lần nghe sau).
- **Lưu dạng chuỗi hex** trong `settings.json` — key ĐỀ XUẤT (chưa có
  trong code; `AppSettings.cs` hiện chỉ có `RsvpHighlightCurrentLine`
  cho highlight dòng ở Style 2): `HighlightUnderlineHex`,
  `HighlightLineHex`, `HighlightWordHex`, `HighlightSentenceHex`,
  `HighlightParagraphHex`, `HighlightBlockHex` (màu) +
  `HighlightUnderlineShown`, `HighlightLineShown`, `HighlightWordShown`,
  `HighlightSentenceShown`, `HighlightParagraphShown`,
  `HighlightBlockShown` (bật/tắt) + `HighlightLineFollows`
  ("Sentence"/"Paragraph"/"Block") + `HighlightHideWhileScrolling`
  (bool). Giá trị lưu sai (chỉ có thể do sửa tay file settings) thì rơi
  về màu mặc định của lớp đó.
- **Opacity cố định, không chỉnh được.** Fill luôn 0.30, vạch luôn đặc
  (1.0) — picker không có chỉnh opacity, không lưu opacity. Fill đặc
  hoàn toàn sẽ che chữ, vạch phải đọc như dấu mốc chứ không phải vết mờ.
- **Hex chỉ hiển thị.** Chữ hex cạnh well cho biết giá trị hiện tại;
  đổi màu bằng well (panel màu hệ thống) hoặc swatch preset, không bao
  giờ gõ hex tay.
- **Cụm (paragraph) là nhóm câu quanh câu đang đọc, tách theo ranh giới
  đoạn.** Controller tách text của block thành đoạn tại: dòng trống (2+
  lần xuống dòng giữa các câu), 1 lần xuống dòng (1), hoặc câu sau thụt
  đầu dòng (tab hoặc 2+ khoảng trắng ở đầu câu; 1 khoảng trắng không
  tính). CRLF tính là 1 lần xuống dòng. Quy tắc này đã kiểm chứng bằng
  mô phỏng 9 trường hợp bên mac (xem spec mac §4).
- **Cụm không fallback về cả block.** Block không có ranh giới đoạn thì
  không vẽ fill cụm — đoạn duy nhất đó chính là cả block, dùng Toggle
  Show của block để tô.
- **Pause giữ highlight ở từ dừng; hết unit thì gỡ highlight; tắt loa
  (speaker OFF) thì không highlight** — xem
  `../../FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md` §2.

## 4. "Line follows" — vạch theo đơn vị nào

Picker dưới hàng Vertical line: **Sentence / Paragraph / Block**,
mặc định **Sentence**. Vạch dài bằng đơn vị đang theo.

- **Fallback:** đơn vị chọn → câu → cụm, **không bao giờ về block**.
  Vd: chọn Paragraph mà cụm không có frame thì về câu; câu cũng không
  có frame thì không vẽ vạch.
- **Không có frame nào thì không vẽ vạch** — không đoán, không vẽ vạch
  dài bằng cả block.
- **Vạch vẫn theo đơn vị dù fill của đơn vị đó đang ẩn** — tắt fill câu
  không làm vạch ngừng theo câu.

## 5. Ẩn lúc cuộn — "Hide highlights while scrolling"

Toggle riêng dưới mục màu, **mặc định BẬT**. Khi khung block đổi
(trang đang cuộn) thì ẩn fill + vạch; khi khung đứng yên 2 lần poll
(~0.6 s) thì hiện lại. Hành vi này đã làm bên mac (27/11); trên Windows
là spec cho overlay bước 2 + mặt đọc — chưa làm.

## 6. Giới hạn kỹ thuật

- **Fill hình chữ nhật có cạnh cứng.** Overlay tô hình chữ nhật trên
  trang, không tô theo hình từng glyph như Speechify (Speechify tô
  chính chữ nên không có cạnh cứng). Cạnh cứng rõ hơn ở chỗ các lớp
  chồng nhau.
- **Không đổi được màu chữ.** Speechify tô lại màu chữ của chính nó;
  overlay WinUI tô đè lên trang của app khác nên không đổi màu chữ
  được — vì vậy fill giữ mờ 0.30.
- **Chỉ vẽ khi app cho khung.** Khung từ/câu/cụm lấy qua UIA
  `TextPattern.GetBoundingRectangles()` (mac: `AXBoundsForRange`). App
  nào không cho khung từ thì bỏ qua highlight từ; không cho khung câu
  thì bỏ qua highlight câu — không bịa khung, không ước lượng vị trí.
- **Vạch 1.75 pt trong dải 8 pt** bên trái block; vạch không đè lên chữ.
- **Màu highlight cố định, không đổi theo dark/light.** Icon float theo
  dark/light system cũng chưa làm (xem `FEATURES/FLOAT_ICON.md`) — 2
  việc khác nhau, không gộp.
- **WinRT không có word-boundary callback** nên highlight theo từ trên
  mặt đọc / trên trang cần spike cloud TTS speech marks trước khi chốt
  (xem `../../FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md` §3, §6) — doc này
  chỉ định nghĩa màu + luật hiển thị, không hứa cơ chế từ.

## 7. Chưa làm / làm sau

- **Overlay highlight trên trang (bước 2: đọc nối tiếp + chaining)** —
  chưa làm trên Windows. Khi làm: chaining qua `SpeechEnded` (hết 1
  block → hit-test UIA 1 điểm ngay dưới khung block vừa đọc để tìm
  block tiếp theo, như Model A bên mac); khung từ/câu/cụm lấy qua
  `GetBoundingRectangles()`; app không cho khung thì rơi về chỉ tô cả
  đoạn. Kế hoạch thử bên mac là research dùng chung trong repo mac
  (`docs/RESEARCH/tts-speechify-naturalreader/step2-trial-plan.md`),
  không copy sang Windows.
- **Mục "Highlight colors" trong Settings** — chưa có UI; làm cùng đợt
  với overlay / word-sync, sau nhánh `feature/shared-read-focus`
  (xem `../../FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md` §6).

## Liên kết

- [TTS word-sync highlight](../../FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md)
  (spec highlight từ/câu theo giọng — 1 component chung Full + Lite +
  Nghỉ; màu + luật hiển thị ở doc này)
- [Speech Player](../../FEATURES/SPEECH_PLAYER.md) (luật loa + indicator
  + 1 giọng tại 1 thời điểm)
- [Fast Read](../../FEATURES/FAST_READ.md) (RSVP — mặt đọc Full / Lite /
  Nghỉ; `RsvpHighlightCurrentLine` là highlight dòng Style 2, khác 6
  lớp ở đây)
- [Float Icon](../../FEATURES/FLOAT_ICON.md) (icon float — vòng đời,
  scroll ẩn ngay)
- Spec mac: repo `TranManhTuanLabs/TransPeek`,
  `docs/UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md`
