# Speech Player (bản nháp)

> Trạng thái: **bản nháp** — mô tả player TTS (nút loa, auto-play, Fast Read RSVP).

## 1. Nút loa — play/pause/resume, hết mới stop; indicator loa TÁCH RIÊNG khỏi icon float (CHỐT 26/11/2026, thay luật cũ "bấm lần nữa là stop")

Luật chung theo mac (repo TransPeek) — theo hành vi Speechify + NaturalReader (đã đọc source thật cả 2 extension): 1 nút loa = toggle Play/Pause, KHÔNG có nút Stop riêng. Áp dụng giống nhau cho mọi unit (1 từ, 1 câu, 1 đoạn — chỉ khác độ dài). Phạm vi: loa click-to-listen từ icon float (indicator loa). Nút loa panel (input/output) GIỮ NGUYÊN tạm thời (Manh 27/11 00:12) — không đổi glyph, không đổi hành vi (vẫn toggle = stop như cũ), vì user không nghe ở đó là chính. Loa trong Read mode (bật/tắt tiếng khi đọc RSVP) là control riêng, giữ nguyên. Chi tiết Windows: Alt thay Option (mac); giọng hệ thống WinRT, KHÔNG hỗ trợ SAPI Desktop.

- **Chưa phát → bấm loa = play** từ đầu unit.
- **Đang phát → bấm loa = pause**, giữ nguyên vị trí từ/câu đang dừng.
- **Đang pause → bấm loa = resume** đúng chỗ dừng (không phát lại từ đầu).
- **Hết unit → stop tự động**: reset vị trí về đầu, gỡ highlight, ẩn indicator loa.
- **KHÔNG có cách dừng giữa chừng** (không nút Stop, không Esc — Manh chốt bỏ Esc 26/11): pause thì cứ kệ; khi indicator tự ẩn theo luật thường thì phiên kết thúc lặng lẽ (xem mục indicator bên dưới).
- **Pause thì GIỮ NGUYÊN highlight** ở từ đang dừng (không gỡ như NaturalReader) — để user thấy đang dừng ở đâu, resume nhìn rõ.
- **Pause kết thúc khi indicator tự ẩn**: không có timeout riêng kiểu "sau 1 phút tự stop" — pause kéo dài được bao lâu tùy indicator còn hiện (hover vào indicator thì timer tạm dừng, resume lúc nào cũng được trong lúc đó); indicator tự ẩn sau 5s không tương tác thì phiên kết thúc lặng lẽ. (Luật cũ "pause vô thời hạn" bỏ 26/11 — vì indicator phải có đường tự ẩn, nếu không sẽ kẹt.)
- **Play mới thay play cũ**: bấm loa unit khác trong lúc đang phát/pause thì dừng cái cũ (kèm indicator cũ), phát unit mới từ đầu (auto-detect ON: hover block khác + click; OFF: select text mới + click). Bấm lại đúng unit đang pause thì resume. Chỉ bấm loa mới mới hủy phiên cũ — select/hover text mới chỉ hiện icon float mới, phiên cũ vẫn chạy (không chồng tiếng vì chỉ có 1 phiên tại 1 thời điểm).
- Nút loa panel ở trong ô text — overlay góc dưới-phải, Margin `0,0,6,8` (đã chuyển vào trong như input, #157). Tooltip "Listen to source text" / "Listen to translation".

### Indicator loa — phần tử nổi RIÊNG, tách khỏi icon float (CHỐT 26/11 23:59)

Lý do tách: icon float chỉ có 1 cửa sổ — lúc đang phát, nó bị phiên loa chiếm (hover text khác không dịch được, mà select mới thì làm mất nút pause của tiếng cũ — tiếng cũ vẫn phát mà không còn nút nào). Cả Speechify (Pill Player) lẫn NaturalReader (minibar) đều tách player khỏi icon trigger. Tách rồi thì 2 phần tử độc lập, mỗi cái tự quản lifecycle của mình, không biết gì về nhau; điểm chung duy nhất là phiên TTS.

- **Indicator là phần tử nổi riêng** (tái dùng máy móc cửa sổ nổi của icon float), hiện ở đúng chỗ user bấm loa — vị trí tự nhiên, không bám theo text, không can thiệp vị trí thêm.
- **Biểu tượng theo trạng thái** (theo nguyên tắc "glyph cho biết lần bấm tới sẽ làm gì"): đang phát: ⏸ (bấm để pause); đang pause: ▶ (bấm để resume). Nút loa panel (input/output) GIỮ NGUYÊN tạm thời — không đổi biểu tượng theo trạng thái (xem phạm vi ở đầu §1). Icon float KHÔNG hiện trạng thái phát nhạc nữa (xem `FLOAT_ICON_UX_SPEC.md`).
- **Hiện trong lúc ĐANG PHÁT** để user luôn bấm được để pause. Trong lúc **ĐANG PAUSE**, indicator chạy theo luật ẩn bình thường (auto-hide sau 5s, hover vào thì timer tạm dừng như luật auto-hide thường); khi indicator ẩn thì phiên kết thúc lặng lẽ (stop). Resume được trong lúc indicator còn hiện.
- **Bấm loa trên icon float thì icon ẩn, indicator chiếm chỗ (Manh 2026-11-27, bug 2 — mac parity):** AppDelegate bên mac đọc khung icon rồi `dismissSilently()` trước khi hiện indicator — hai cái không bao giờ chồng nhau. Windows làm giống: `OnTriggerIconSpeakRequested` đọc vị trí icon rồi `HideIcon()` trước khi bắt đầu phiên. Nhờ vậy hết unit indicator ẩn thì không còn icon nào lộ ra nữa; hover/chọn mới thì icon hiện lại trong lúc phiên vẫn phát. (Trước đây Windows để icon nằm dưới indicator; hết unit thì icon lộ ra mà timer tự ẩn đã dừng vì chuột vẫn đặt trên icon, nên icon kẹt lại mãi.)
- **Alternative icon (icon giả trong Read) — click loa của nó = click loa icon float thật** (CHỐT 27/11 00:32, làm code Windows 27/11 theo mac `1cdaa44a`): bắt đầu phiên nghe, indicator hiện ở đúng chỗ bấm (chỗ alternative icon đang đứng), ⏸/▶, luật play/pause/resume như thường. Alternative icon KHÔNG đổi sang hình loa khi đang nói nữa — giữ glyph A blue bình thường; trạng thái phát nhạc chỉ hiện trên indicator. Bấm lại alternative icon trong lúc đang phát = phát mới thay phiên cũ (pause/resume là việc của indicator). Hover vẫn dịch như cũ (Lite: chuyển tab center; Full: mở panel tại cursor). Nút loa của cửa sổ Read (bật/tắt tiếng RSVP) là control riêng, KHÔNG đổi, không thuộc luật này.
- **Hiện tại (27/11): ĐÃ LÀM XONG** — luật mới + indicator (commit 156693b2, CI 258 xanh) + 1 giọng tại 1 thời điểm (commit 4fefa60e, CI 259 xanh) + alternative icon dùng chung luồng loa (commit f96cd224, CI 260 xanh) + sửa 2 lỗi icon (scroll ẩn ngay, bấm speak thì icon ẩn — commit 0fc12cb7, CI 262 xanh); Manh test live bản 262.
- **Ý tưởng sau này (từ mac, CHƯA LÀM):** khi indicator loa đang phát TTS, hover vào indicator thì hiện panel nhanh chỉnh tốc độ 1x/1.2x/1.5x/2x, chọn giọng, autoswitch voice nhận diện 2 ngôn ngữ — khỏi vào Settings; hoặc làm control riêng như Speechify/NaturalReader (định hướng 16/11).

### Read playback vs click-to-listen — 1 giọng tại 1 thời điểm (CHỐT 27/11 00:48)

Phiên mới thay phiên cũ, kể cả Read playback — cả 2 chiều, không bao giờ chồng 2 luồng tiếng:

- **Read đang phát → bấm loa nghe (icon float / alternative icon)**: Read DỪNG ở chỗ đang đọc (giữ nguyên vị trí), từ được nghe; bấm Play/Space thì Read đọc tiếp từ chỗ dừng.
- **Click-to-listen đang phát → bấm Play / bật loa trong Read**: phiên click-to-listen BỊ DỪNG trước, rồi Read mới đọc — Read phải dừng phiên cũ trước khi bắt đầu (không xếp hàng).
- (Đã kiểm chứng + sửa cả 2 chiều 27/11, commit `4fefa60e`: `RsvpReaderViewModel` thêm event tĩnh `SpeechStarting`, bắn ngay trước mỗi lần `SpeakAsync` — App đăng ký 1 lần, gọi `StopFloatSession()`; chiều ngược (click-to-listen bắt đầu) thì `PauseAnyReading()` tạm dừng Full + Lite + Nghỉ trước. Tương đương sửa bên mac: Read gọi `stop()` trước khi bắt đầu.)

## 2. Auto-play

- Tab TTS Speech: Auto-play source, Auto-play translation (nguồn trước, dịch sau
  qua event `SpeechEnded`). Nối vào `ShowResult` → `AutoPlayIfEnabled()`.

## 3. Fast Read (RSVP) — đã chốt luồng vào 17/11, CHƯA LÀM

- (1) Alt-click (Windows) / Option-click (Mac) → RSVP thẳng.
- (2) Chuột phải icon → menu "Fast Read".
- (3) Nút "Fast Read" trong panel dịch.
- (4) Menubar/tray. (5) Shortcut global chỉ là fallback.
- + Settings toggle đổi click → RSVP cho người dùng RSVP là chính.
- Vị trí view đọc: **giữ cả 2 phương án** (tab dãn / panel riêng), quyết ở lúc prototype.
  Nếu làm tab phải **ẩn HOÀN TOÀN chức năng panel dịch** khi đang đọc.
- Engine RSVP (tham khảo SwiftRead): WPM mặc định 275, max 2000, chunk 1–6,
  ORP pivot = ký tự giữa `floor(n/2)`, chữ cam `#f6861f`.
- **Model A là hướng thực tế cho app native; Model B KHÔNG làm** (ngay cả Speechify
  cũng chỉ Model B ở site quen viết selector tay) — xem `BLOCK_TEXT_HOVER.md`.

## Liên kết

- [Speech Engine](SPEECH_ENGINE.md)
- [Float Icon](FLOAT_ICON.md)
- [TTS Word-Sync Highlight](TTS_WORD_SYNC_HIGHLIGHT.md)
- [Panel UX (CHỐT)](../UI/UX_DESIGN/FLOAT_POPUP_PANEL_UX.md)
