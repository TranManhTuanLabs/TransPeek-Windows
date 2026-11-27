# TTS Word-Sync Highlight — highlight từ/câu theo giọng đọc

**Trạng thái (27/11): SPEC — chưa làm code.** Mirror spec mac
`docs/FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md` (commit `ec7c452c`,
https://github.com/TranManhTuanLabs/TransPeek/commit/ec7c452cd43d9fb74a0c180f86ce12f9a7dbac80).
Nguồn research dùng chung ở repo mac:
`docs/RESEARCH/tts-speechify-naturalreader/recommendation.md` (§3, §8–§9)
và `rsvp-report.md` §6.5. Spec này viết cho Windows (WinRT, không SAPI
Desktop, `MediaPlayer` riêng mỗi surface) — chỗ nào khác mac thì ghi rõ.

## 1. Phạm vi

**Làm:** 1 component chung cho mọi mặt đọc — highlight từ/câu theo giọng
đọc, dùng chung cho **cửa sổ Full + panel Lite + cửa sổ Nghỉ** (cả 3 đều
dùng chung `RsvpReaderViewModel`, nên 1 component là đủ — không làm 2–3
bản riêng).

**Ngoài phạm vi:**
- Trình đọc file local (PDF/doc/TXT/MD) — đó là bước 3 (chỉ đọc file
  local), làm sau, riêng.
- Highlight trên panel dịch — panel dịch là chỗ xem bản dịch, không phải
  mặt đọc; không highlight ở đó.
- Đọc PDF là Pro (chốt 23/11); Word/Md free hay Pro chưa quyết — không
  thuộc spec này.
- **Overlay highlight trên trang (đọc nối tiếp / chaining) — bước 2,
  chưa làm trên Windows.** Bên mac overlay này đã có sẵn
  (`ReadingIndicatorOverlay` — highlight câu/từ theo giọng ngay trên
  trang đang đọc, qua `ContinuousSpeakController` + `onWordRange`).
  Trên Windows chưa có overlay nào trên trang: click-to-listen hiện chỉ
  đọc 1 unit rồi dừng, chưa tự tìm block tiếp theo. Bước 2 sẽ làm sau,
  riêng (kế hoạch thử bên mac: `docs/RESEARCH/tts-speechify-naturalreader/step2-trial-plan.md`
  trong repo mac — research dùng chung, không copy sang Windows).
  Spec này chỉ định nghĩa highlight cho **mặt đọc** (Full/Lite/Nghỉ);
  khi bước 2 làm thì overlay trên trang dùng chung bảng màu + luật
  highlight ở §2 (xem `../UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md`).

## 2. Hành vi (đã chốt theo research + luật loa)

- **Từ hiện tại + câu hiện tại highlight theo giọng.** Khi giọng đang đọc
  từ nào thì từ đó sáng; câu chứa từ đó sáng ở lớp nhạt hơn. Highlight
  đi theo giọng, không đi ngược lại (giọng dẫn, mắt theo).
- **Pause GIỮ highlight ở từ đang dừng** — không gỡ như NaturalReader.
  User nhìn thấy đang dừng ở đâu, resume nhìn rõ ngay (cùng luật loa
  26/11: pause giữ nguyên highlight ở từ dừng).
- **Hết unit → gỡ highlight**, reset vị trí về đầu (cùng luật loa: hết
  unit thì stop tự động).
- **Resume tiếp tục từ chỗ dừng** — không đọc lại từ đầu unit, không nhảy
  về đầu (cùng luật loa 26/11: đang pause bấm resume thì đọc tiếp đúng
  chỗ dừng).
- **Màu — 6 lớp theo mac, đã chốt (Manh 2026-11-27), lấy từ Settings
  "Highlight colors", không hardcode màu mới trong spec này.** Mặc định
  (fill mờ 0.30 để chữ đọc được; vạch + gạch chân đặc 1.0):
  từ `#5666F0` (chàm Speechify), câu `#ABB3FE` (tím nhạt Speechify),
  cụm `#FFC2D1` (hồng nhạt), đoạn `#BFF2B3` (xanh lá nhạt),
  vạch dọc `#8CBFFF` (xanh dương nhạt, 1.75 pt), gạch chân `#5666F0`
  (chàm, như màu từ, 2.5 pt). **Mặc định chỉ bật từ + gạch chân** —
  câu, cụm, đoạn, vạch đều TẮT (Manh muốn lần đầu hiện ít nhất, user tự
  bật thêm). Chi tiết đầy đủ (bảng màu, thứ tự hàng, "Line follows",
  ẩn lúc cuộn, popover "How it works"): xem
  `../UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md`.
  (Sửa 27/11: bảng màu cũ trong spec này — "4 lớp pastel, từ tím nhạt,
  câu vàng nhạt" theo bản thử overlay nhánh `feature/continuous-reading`
  — đã lỗi thời; từ + câu nay theo Speechify, xem bảng trên.)
- **Tắt loa (speaker OFF) thì không highlight** — Read chạy bằng đồng hồ
  WPM im lặng thì chỉ có focus RSVP (chữ cam ORP pivot) như hiện tại,
  không thêm highlight từ/câu.
- **Highlight đi theo giọng (follow):** khi giọng sang từ ở slide/dòng
  khác thì focus + highlight chuyển theo tới slide/dòng chứa từ đó
  (auto-scroll/follow, không để highlight chạy ra ngoài màn hình).
  Style 1: chuyển slide chứa từ; Style 2: chuyển dòng chứa từ.
- **Tốc độ:** highlight chạy theo tốc độ giọng (0.5x–4x, chung control
  WPM/speed sau này). Ở tốc độ cao, khoảng lặng ở dấu phẩy có thể không
  co lại theo tốc độ (xem §4) — highlight vẫn chờ giọng, không tự chạy
  trước.

## 3. Cơ chế Windows — highlight theo CÂU là baseline, highlight theo TỪ chưa chốt

**Đây là điểm Windows khác mac nhiều nhất — đọc kỹ trước khi hứa.**

- **WinRT `SpeechSynthesizer` KHÔNG có word-boundary callback.**
  `SystemSpeechService` chỉ có `SpeechEnded` (từ `MediaPlayer.MediaEnded`)
  — biết khi nào hết 1 lượt đọc, không biết giọng đang ở từ nào. Mac có
  `willSpeakRangeOfSpeechString` miễn phí (word-range), Windows không có
  tương đương. Vì vậy:
- **Baseline an toàn: highlight theo CÂU (cụm-câu).** Code hiện tại đã đọc
  theo cụm-câu: `RsvpReaderViewModel` tách text bằng
  `RsvpEngine.BuildPhrases(text)` (cap ~20 từ/cụm, xem §4) rồi đọc từng
  cụm qua `SpeakCurrentPhraseAsync()` — cụm nào đang đọc thì biết rõ, nên
  highlight được cụm/câu hiện tại mà không cần callback từng từ.
- **Highlight theo TỪ: CHƯA CHỐT, cần spike trước.** Chỉ làm khi spike
  chứng minh được 1 trong các cách sau (theo thứ tự ưu tiên):
  1. **Cloud TTS có speech marks** (kiểu NaturalReader: audio + marks
     thời gian từng từ, poll mỗi 50 ms; hoặc Azure AI Speech timepoints
     — mac đã phân tích SwiftRead dùng cách này). Cần spike: giọng cloud
     nào, giá, độ trễ, có ổn định không.
  2. **Cách khác** (nếu có) — spike phải chứng minh được bằng code chạy
     thật, không đoán.
  - **ĐỪNG hứa highlight theo từ trên Windows trước khi spike xong.**
    Research (§9) đã cảnh báo: đừng chốt UI highlight-từ trên Windows
    trước khi spike xong.
- **Không bịa range, không ước lượng thời gian từ.** Không có word-range
  thì không highlight từ — chỉ highlight câu/cụm hiện tại theo nhịp
  utterance (xem fallback ở §3 mac: provider không có word-range thì
  chỉ highlight câu/slide). Không tự tính thời gian từng từ để "đoán"
  từ đang đọc.
- **Component bám vào `RsvpReaderViewModel`, không bám từng
  `MediaPlayer`.** Mỗi mặt đọc có 1 `SystemSpeechService` + 1
  `MediaPlayer` RIÊNG (đã sửa "1 giọng" ở commit `4fefa60e`: App phối hợp
  qua `PauseAnyReading()` + event tĩnh `SpeechStarting` để chỉ 1 giọng
  phát tại 1 thời điểm). Highlight phải đi theo ViewModel chung (nơi biết
  cụm hiện tại + vị trí đọc), không đi theo từng MediaPlayer riêng — nếu
  không sẽ có 3 highlight lệch nhau.
- **Đọc theo cụm đã có sẵn — dùng lại, không viết lại.**
  `UsePhraseSpeech` (loa bật + có speaker + `ProGate.WordSyncEnabled`)
  đã đọc theo cụm; component highlight chỉ cần biết "cụm hiện tại là cụm
  nào + từ nào trong cụm (nếu có word tracking)".

## 4. Dấu phẩy — tách theo cụm-câu, cap ~20 từ, không tách ở mọi phẩy

(Nguồn: `rsvp-report.md` §6.5 — bổ sung 22/11/2026 từ báo cáo của Manh:
audio read-along chưa tốt vì dấu phẩy ngắt nhịp.)

- **Không tách utterance ở mọi dấu phẩy.** Đoạn phẩy dày (cách nhau chỉ
  4–6 từ lại có phẩy) mà tách ở mọi phẩy thì mỗi utterance chỉ 4–6 từ,
  càng nhiều khoảng lặng nối utterance, nghe càng rời rạc ("nói 4–6 từ,
  ngắt, nói 4–6 từ, ngắt").
- **Tách theo cụm-câu, cap ~20 từ** — đúng như `RsvpEngine.BuildPhrases`
  đang làm: cụm kết thúc ở cuối câu/mệnh đề/cuối đoạn. Phẩy chỉ là pause
  NHẸ bên trong cụm, không phải điểm tách.
- **Không mẫu nào chỉnh được pause ở dấu phẩy trong audio TTS** — tất cả
  (SwiftRead, Speechify, NaturalReader) đều để giọng tự ngắt theo prosody.
  Cái kiểm soát được là timing hiển thị và việc tách highlight khỏi nhịp
  audio (timepoints + poll 50 ms) để khoảng lặng không làm kẹt highlight.
- **Cần test nghe thật trước khi chốt số:** nghe cùng đoạn ở 1x vs 2x/4x,
  xem pause ở phẩy có dài bất thường không; thử 2 giọng khác nhau (pause
  prosody khác nhau theo giọng).

## 5. Pro gate — hiện tại MỞ, đổi sang Pro cần Manh chốt riêng

- **Trạng thái hiện tại (27/11): `ProGate.WordSyncEnabled = true`**
  (`RsvpReaderViewModel.cs`) — word-sync **KHÔNG bị khóa** trên Windows
  lúc này. Loa đọc theo cụm (`UsePhraseSpeech`) đang chạy với gate mở.
- **Theo plan Pro nội bộ, TTS word-sync nằm trong nhóm Pro** (cùng với
  API dịch AI, API TTS, Fast Read đọc file, Speech cùng Fast Read,
  Window Read chọn theme/font). Nhưng đó là plan — **đổi gate sang Pro
  cần Manh chốt riêng**, không tự khóa lặng lẽ. Khi Manh chốt, đổi
  `WordSyncEnabled` thành false cho bản free và mở khi có Pro; free giữ
  nguyên core hiện tại.
- **Không trình bày word-sync như đã có sẵn** khi viết README/marketing
  (cùng quy tắc 26/11: 2 tính năng đang làm dở — giọng AI + word-sync
  highlight block text — không được ghi như đã ship).

## 6. Chưa chốt / cần spike (liệt kê rõ — không đoán)

1. **Cơ chế highlight theo từ trên Windows** — cloud TTS nào (Azure?
   Google? khác?), speech marks/timepoints có ổn định không, giá, độ trễ.
   Spike phải chạy thật trước khi chốt.
2. ~~**Màu highlight chính xác**~~ — **ĐÃ CHỐT 27/11** (bỏ khỏi danh sách
   spike): 6 lớp theo Speechify + pastel, xem §2 và
   `../UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md`. Windows không tự đặt
   màu khác.
3. **Highlight theo câu có đủ cho v1 không, hay bắt buộc phải có highlight
   theo từ mới ship?** — Manh quyết sau khi xem spike.
4. **WinRT có tôn trọng SSML `<break>` không** — nếu có thì chỉnh pause ở
   phẩy ngay trong 1 utterance mà không cần tách (cần test nhỏ; mac cũng
   chưa kiểm chứng `AVSpeechUtterance(init?(SSMLRepresentation:))`).
5. **Hành vi highlight ở tốc độ 2–4x** — pause ở phẩy có co lại theo tốc
   độ không, hay vẫn dài làm highlight "đứng hình" (xem §4, cần test nghe).
6. **Highlight ở cửa sổ Nghỉ** — cùng component chung (vì dùng chung
   ViewModel) hay có khác biệt gì khi đang đếm ngược.
7. **Component chung phím/focus (`feature/shared-read-focus`)** — làm trước
   word-sync; event `SpeechStarting` (tĩnh, thêm 27/11 cho luật 1 giọng)
   và event `PlayingStarted` (dự kiến cho shared-read-focus) có dùng chung
   hay tách riêng cho rõ — quyết lúc làm shared-read-focus, không quyết ở
   spec này.
8. **Bước 2 — đọc nối tiếp + overlay trên trang** — chưa làm trên Windows
   (xem §1). Khi làm: chaining qua `SpeechEnded` (hết 1 block → hit-test
   UIA 1 điểm ngay dưới khung block vừa đọc để tìm block tiếp theo, như
   Model A bên mac); khung từ/câu/cụm lấy qua
   `TextPattern.GetBoundingRectangles()` (UIA) thay cho `AXBoundsForRange`
   bên mac — app nào không cho khung thì rơi về chỉ tô cả đoạn (như mac).
   Kế hoạch thử bên mac là research dùng chung, không copy sang Windows.

## Liên kết

- Spec mac: repo `TranManhTuanLabs/TransPeek`,
  `docs/FEATURES/TTS_WORD_SYNC_HIGHLIGHT.md` (commit `ec7c452c`,
  https://github.com/TranManhTuanLabs/TransPeek/commit/ec7c452cd43d9fb74a0c180f86ce12f9a7dbac80)
  — mirror spec này.
- Research dùng chung (repo mac, không copy sang Windows):
  `docs/RESEARCH/tts-speechify-naturalreader/recommendation.md` (§3 TTS,
  §8 thứ tự triển khai, §9 rủi ro) và `rsvp-report.md` §6.5 (dấu phẩy),
  §6.1–§6.4 (khả thi tích hợp native).
- Windows: [Speech Player](SPEECH_PLAYER.md) (luật loa + indicator + 1
  giọng) · [Speech Engine](SPEECH_ENGINE.md) (WinRT, không SAPI Desktop)
  · [Fast Read](FAST_READ.md) (RSVP, `RsvpReaderViewModel`,
  `BuildPhrases`) · [Float Icon](FLOAT_ICON.md) ·
  [Highlight Colors UX](../UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md)
  (6 lớp màu + hiện/ẩn + "Line follows" + ẩn lúc cuộn).
