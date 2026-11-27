# Changelog — TransPeek-Windows

> Log thay đổi theo ngày, mới nhất lên đầu. Mỗi mục: ngày + commit + CI + thay đổi ngắn.
> Quyết định chi tiết: `DECISIONS.md`. Trạng thái hiện tại (ảnh chụp nhanh):
> `PROJECT_STATE.md`.

## 2026-11-27

- **Đồng bộ docs theo doc mới bên mac (docs-only):** (1) **Doc mới `docs/UI/UX_DESIGN/HIGHLIGHT_COLORS_UX_SPEC.md`** — mirror spec mac: 6 lớp highlight (Underline, Vertical line, Word, Sentence, Paragraph, Block), màu mặc định đã chốt 27/11 theo Speechify (từ `#5666F0`, câu `#ABB3FE`, cụm `#FFC2D1`, đoạn `#BFF2B3`, vạch `#8CBFFF`, gạch chân `#5666F0`; fill 0.30, vạch/gạch chân đặc; mặc định chỉ bật từ + gạch chân), thứ tự hàng, picker "Line follows" (mặc định Sentence; fallback đơn vị chọn → câu → cụm, không bao giờ về block), toggle "Hide highlights while scrolling" (mặc định BẬT), popover "How it works" giữ nguyên wording tiếng Anh như mac, quy tắc tách đoạn 3 tín hiệu, giới hạn kỹ thuật (fill chữ nhật cạnh cứng, không đổi màu chữ, chỉ vẽ khi app cho khung qua UIA `GetBoundingRectangles`). Dịch sang Windows: Settings → TTS Speech (không có tab Audio), `ColorPicker` WinUI, key settings đề xuất `Highlight<Layer>Hex/Shown` (chưa có trong code). (2) **`TTS_WORD_SYNC_HIGHLIGHT.md`:** màu cũ "4 lớp pastel, từ tím nhạt, câu vàng nhạt" đã lỗi thời → bảng 6 lớp Speechify; thêm luật tắt loa thì không highlight, highlight đi theo giọng (Style 1 chuyển slide, Style 2 chuyển dòng), "không bịa range, không ước lượng thời gian từ", mục §6 bỏ màu khỏi danh sách spike (đã chốt) + thêm mục bước 2 overlay trên trang (chưa làm trên Windows). (3) **`SPEECH_PLAYER.md`:** trạng thái "chờ CI xanh" → đã xong (CI 258/259/260/262 xanh); ghi chú "Windows CHƯA kiểm chứng 1 giọng" → đã kiểm chứng + sửa cả 2 chiều (`4fefa60e`); thêm ý tưởng panel nhanh trên indicator (từ mac, chưa làm) + ghi chú Model A là hướng thực tế, Model B không làm. (4) **`BLOCK_TEXT_HOVER.md`:** sửa status "on branch, not merged" → đã merge PR #3; hide-on-scroll đã chốt + làm xong (`0fc12cb7`, `ScrollDismissMonitor`); thêm mục đối chiếu Model A mac (leo ≤ 5 cấp cha, link leo qua không dừng; debounce 400 ms; không hit-test lúc kéo; stale-dismiss poll 0.3 s / khung dịch > 10 pt; giới hạn ≥ 4 và ≤ 2000 ký tự) + ghi rõ chỗ Windows khác (không leo cấp cha, không có gate lúc kéo / lúc panel hiện, cap 4000 ký tự, text nào khác rỗng cũng hiện icon). (5) Cập nhật chỉ mục `STRUCTURE_MAP.md` + `UI_DESIGN.md` cho doc mới.
- **Icon float: scroll thì ẩn ngay + bấm speak thì icon ẩn, indicator chiếm chỗ (Manh 27/11 17:56, test Build #260 ở công ty — 2 lỗi, bên mac không bị):** (1) **Scroll:** đang nghe mà hover sang khối khác rồi cuộn thì icon mới cứ hiện đủ 5s timer tự ẩn, cảm giác 2 icon cùng lúc, lâu hơn mac — vì hover service chỉ thăm dò lại khi chuột DI CHUYỂN, còn cuộn thì chữ di chuyển chứ chuột không, nên icon nằm lại ở chỗ cũ hết hạn. Sửa: `Services/ScrollDismissMonitor` mới — hook chuột mức thấp `WH_MOUSE_LL` (cùng mẫu `ClickOutsideMonitor`) bắt sự kiện lăn chuột/trackpad rồi ẩn icon ngay; mac làm tương đương bằng poll theo dõi khung khối chữ (`HoverTextDetector.onHoverStale`, ẩn khi khung dịch > 10pt). Chỉ icon float ẩn — indicator loa là player, vẫn hiện trong lúc đang phát. (2) **Bấm speak:** bấm loa trên icon float thì icon ẩn ngay, indicator hiện đúng chỗ icon vừa đứng — hai cái không chồng nhau (mac: AppDelegate đọc khung icon rồi `dismissSilently()` trước khi hiện indicator). Trước đây Windows để icon nằm dưới indicator; hết unit indicator ẩn thì icon lộ ra, mà timer tự ẩn đã dừng (chuột vẫn đặt trên icon, không có sự kiện rời chuột) nên icon kẹt lại mãi — Manh thấy "hết unit rồi mà icon không ẩn". Toggle pause/resume vẫn qua indicator như cũ; icon hiện lại khi hover/chọn mới. Đã đối chiếu code mac thật (`TriggerIconController.handleIconClick`, `AppDelegate.onSpeakRequested`, `HoverTextDetector.onHoverStale`) trước khi sửa.
- **"When a reminder fires while reading" — tuỳ chọn mới trong panel (Manh 27/11 17:47, theo mac):** picker trong thẻ Focus & Rest, ngay dưới "Run reminder in" (dạng giống nút "Run reminder in" của Windows — nhãn trên 1 dòng, picker full-width, ghi chú dưới), 2 lựa chọn "Notification only" (mặc định — reminder chờ: pending + banner + chấm cam, tự hiện khi hết đọc) / "Run now" (hiện ngay cả khi đang đọc). Chỉ áp cho lúc đang đọc; lúc không đọc reminder vẫn chạy ngay như cũ. **"Đang đọc" mở rộng sang cả nghe trên trang:** trước đây chỉ tính đọc trong panel + cửa sổ Fast Read nên lúc nghe trên trang (bấm loa) reminder chạy ngay, cướp mạch nghe, không có thông báo conflict — giờ phiên click-to-listen (đang phát hoặc tạm dừng, qua `_iconSpeech.IsSpeaking`) cũng tính là đang đọc nên reminder chờ + banner/chấm hiện lại; khi phiên nghe kết thúc tự nhiên (đọc hết unit) hoặc indicator tự ẩn lúc pause thì reminder chờ tự hiện (trừ khi user đang đọc chỗ khác). Khi reminder chạy thì giọng đang nghe dừng trước (luật 1 giọng — `OnReminderShowing` → `StopFloatSession`, kể cả khi reminder đọc tắt tiếng). Phiên loa panel không tính (control riêng, toggle = stop). Setting mới `FocusReminderWhileReading` ("NotificationOnly" / "RunNow", mặc định "NotificationOnly"); engine thêm `WaitWhileReading` (mặc định chờ) để `FireReminder` chỉ chờ khi đang đọc VÀ tuỳ chọn là chờ.
- **Alternative icon dùng chung luồng loa + indicator (Manh 27/11, theo mac `1cdaa44a`):** click loa alternative icon = click loa icon float thật — bắt đầu phiên nghe qua luồng chia sẻ `App.StartClickToListenAsync` (tách từ `OnTriggerIconSpeakRequested`, 2 trigger dùng chung: float icon `allowToggle: true`, alt icon `allowToggle: false`), indicator hiện ở đúng chỗ icon đang đứng (đọc vị trí trước khi ẩn icon). Bỏ hẳn `SystemSpeechService` riêng của `AlternativeIconController` (đường tiếng thứ 3, không phối hợp với ai, chồng tiếng được) + bỏ toggle speak/stop cũ + bỏ glyph loa khi đang nói (luôn giữ A blue, trạng thái chỉ ở indicator) + bỏ chặn hover lúc đang phát. Bấm lại alt icon = phiên mới thay phiên cũ (không toggle); Read đang phát thì tạm dừng đúng vị trí (qua `PauseAnyReading()` có sẵn), bấm Play/Space đọc tiếp. Sửa luôn doc `SPEECH_PLAYER.md` ghi sai "Windows CHƯA có alternative icon" (đã có từ 21/11, chỉ là chưa theo luật loa mới).
- **1 giọng tại 1 thời điểm — phiên mới thay phiên cũ, kể cả Read playback (Manh 27/11 00:48, spec `290ebb8e`):** chỉ 1 giọng phát tại 1 thời điểm, cả 2 chiều, không bao giờ chồng 2 luồng. (1) Read đang phát → bấm loa nghe (icon float): Read DỪNG ở chỗ đang đọc (giữ vị trí), từ được nghe; bấm Play/Space thì Read đọc tiếp từ chỗ dừng — `App.PauseAnyReading()` tạm dừng Full + panel Lite + cửa sổ Nghỉ (mỗi chỗ 1 phương thức `PauseReading()` riêng, chỉ `SetPlaying(false)`, không xoá phiên đọc) trước khi phiên float bắt đầu. (2) Click-to-listen đang phát → bấm Play / bật loa trong Read: phiên click-to-listen BỊ DỪNG trước, rồi Read mới đọc (không xếp hàng) — `RsvpReaderViewModel` thêm event tĩnh `SpeechStarting`, bắn ngay trước mỗi lần `SpeakAsync` (mọi đường đọc đều qua `SpeakCurrentPhraseAsync` nên 1 điểm chặn đủ cả 3 mặt đọc); App đăng ký 1 lần, gọi `StopFloatSession()` (dừng tiếng + xoá text phiên + ẩn indicator). **Nguyên nhân gốc (đã kiểm chứng code):** mỗi `SystemSpeechService` có 1 `MediaPlayer` riêng (App `_iconSpeech`, Full, Lite, Nghỉ mỗi cái 1 instance) — `SpeakAsync` chỉ `Stop()` instance của mình, nên 2 instance phát cùng lúc là 2 giọng chồng nhau; comment cũ "mỗi cái 1 service nên không tranh 1 MediaPlayer" chỉ đúng nửa (không tranh 1 player, nhưng vẫn chồng tiếng). **LƯU Ý:** loa panel (input/output) KHÔNG thuộc luật này — vẫn toggle = stop như cũ.
- **Luật loa mới play/pause/resume + indicator loa tách riêng khỏi icon float — CHỈ cho luồng icon float (Manh 26/11, spec đã viết sẵn trên main `e22a2245` + `a5569591` — làm code theo):** 1 nút loa = toggle Play/Pause, KHÔNG có nút Stop riêng, KHÔNG Esc để dừng. Chưa phát → bấm = play từ đầu unit; đang phát → bấm = pause (giữ vị trí); đang pause → bấm = resume đúng chỗ dừng; hết unit → stop tự động. Play mới thay play cũ (chỉ 1 phiên tại 1 thời điểm, không chồng tiếng). Pause kết thúc khi indicator tự ẩn sau 5s không tương tác (luật cũ "pause vô thời hạn" bỏ). **Indicator loa** là cửa sổ nổi RIÊNG (`SpeakerIndicatorWindow`, tái dùng máy móc cửa sổ nổi của icon float — `TransparentWindow` + vùng tròn + hiện không kích hoạt), hiện ở đúng chỗ user bấm loa, glyph ⏸ lúc đang phát / ▶ lúc đang pause, bấm để pause/resume; hiện lúc đang phát, lúc pause thì tự ẩn sau 5s (hover tạm dừng timer), ẩn thì phiên kết thúc lặng lẽ. **Icon float** về lifecycle bình thường mọi lúc (auto-hide 5s, dismiss khi block trôi, hover/select mới hiện ở chỗ mới) — không bị phiên loa ghim hay chặn, không hiện trạng thái phát nhạc (gỡ `_isSpeaking`/`SetSpeaking`/`SpeakStopped` + guard hover lúc đang phát). **Nút loa panel GIỮ NGUYÊN tạm thời (Manh 27/11 00:12)** — không đổi glyph, không đổi hành vi (vẫn toggle = stop như cũ), vì user không nghe ở đó là chính; chỉ thêm phối hợp phiên (panel bắt đầu phiên thì dừng phiên float, float bắt đầu phiên thì dừng phiên panel, mở Fast Read thì dừng cả 2 — Read mode có loa riêng). `SystemSpeechService` thêm `Pause()`/`Resume()`/`IsPaused`/`IsPlaying` (MediaPlayer giữ vị trí, không synthesize lại) — dùng cho phiên float; panel vẫn dùng `Stop()`. **Hạn chế Windows:** loa click-to-listen không có highlight từng từ (giọng hệ thống WinRT không báo vị trí từ) nên luật "giữ highlight ở từ đang dừng" không áp dụng được ở đây. AlternativeIcon (Read mode) vẫn toggle speak/stop cũ — đồng bộ sau.

## 2026-11-26

- **Đổi tên nhãn — lẻ gộp (Manh 26/11):** tray mục Reading: "Fast Read" → "Fast Read (RSVP)" (thêm key resw `Tray.Menu.FastRead` mới + sửa fallback trong `TrayIconService.cs`; trước đây resw không có key này nên tray hiện chữ fallback); "Focus rest" → "Focus & Rest" — LẦN ĐẦU TƯỞNG ĐÃ ĐÚNG SẴN (resw + code đều ghi "Focus & Rest") NHƯNG SAI: menu tray là Win32 popup menu (H.NotifyIcon chuyển MenuFlyout thành popup Win32), ký tự `&` trong menu Win32 là phím tắt (mnemonic) nên bị nuốt, không hiện ra — tray thực tế hiện "Focus rest". Sửa: ghi `&&` (resw `Focus &amp;&amp; Rest` + fallback `"Focus && Rest"`) để Win32 hiện đúng 1 dấu `&`. Settings/General: "Watch selection automatically" → "Show floating icon on text selection"; "Show icon when hovering over text" → "Auto Show icon when hovering over text (default: on)" — mặc định của `HoverBlockText` là `true` (bật) nên ghi "(default: on)". Sửa cả XAML Header lẫn resw (resw thắng lúc chạy).

## 2026-11-24

- **Enter xác nhận Done khi nút Done đang hiện (Manh 24/11, làm trên main sau khi gộp PR #6):** khi nào nút Done hiện ra (đếm ngược sau khi đọc xong ở cửa sổ Nghỉ + đếm ngược nhắc ở panel) thì user bấm Enter được thay vì phải click chuột vào Done — trước đây Enter chỉ fire khi nút Done đang focus, còn không focus thì Enter không làm gì. Sửa: `RestWindow.OnKeyDown` (Enter + `CountdownGrid` đang hiện → đóng cửa sổ, cùng hành động với nút Done) và `PanelFastReadView.HandleKey` (Enter khi `PanelReminderCountdown` đang chạy → `StopPanelReading`, cùng hành động với nút Done). Không có nút Done ở cửa sổ Full (chỉ có share) nên không đổi. Ghi vào `DECISIONS.md` + `FOCUS_REST.md` + `TASK_QUEUE.md`.
- **nhánh `fix/panel-strip-frame` — TRẢ KHUNG + SỬA CÔNG THỨC — MANH TEST #250 OK 24/11:** Manh test bản nhánh #250 (CI xanh, commit `76fbe04`): **loa HẾT CHE** — log `[PanelSize]` 24/11 09:41: outer=420x570, client=404x554, **frame=16x16**, minH=570, minParts=(fixedH=304 cardMin=218 cardMargin=8 **frameH=16** minH=570) — công thức đúng: 304+218+8+24+16=570, card thấy được = 554−304−8−24 = **218 đúng thiết kế**. Bo tròn 4 góc về lại. Kéo thu nhỏ hết cỡ dừng đúng điểm không che. **Pin VẪN MỜ — giả thuyết "gỡ khung làm mờ pin" BỊ BÁC** (khung đã trả lại, frame 16, mà pin vẫn mờ) — pin là việc riêng, điều tra sau, không chặn gộp. Gốc: minH tính số đo trong (client) nhưng đặt làm chiều cao ngoài, thiếu đúng khung; sửa: gỡ `StripWindowFrame` ở panel, card về lại 218, cộng khung đo live (`FrameHeightDips`) vào `ComputeMinPanelHeight`.
- **nhánh `fix/panel-strip-frame` — gỡ khung CHƯA ĐỦ + tăng min card 218→232 (chưa có số CI):** Manh test bản nhánh #241: frame chỉ 16x16 → 14x14 (client 406x540), loa vẫn che — `StripWindowFrame` chỉ gỡ được 2px, 14px còn lại không nằm ở style cửa sổ. Đã sync main vào nhánh (merge `9dc6f59`, PR #6 draft — PR #5 sync ngược đã đóng vì nút gỡ xung đột sẽ commit lên main). Thử tiếp theo hướng Manh chỉ: `MinOutputCardHeight` 218 → 232 (= 218 + 14 frame đo được) + XAML `MinHeight` đổi theo; log `[PanelSize]` giữ lại để xác nhận số mới.
- **nhánh `fix/panel-strip-frame` — sửa bug che loa theo số đo (chưa có số CI):** Manh test
  bản 240, log `[PanelSize]` đo được frame=16x16 (outer 420x554, client 404x538) — nguyên
  nhân thật đã rõ: chiều cao tính cho vùng hiển thị nhưng đặt theo kích thước ngoài, panel
  thiếu 16px, ô output nhận 210 thay vì tối thiểu 218 → loa/copy bị che ~8px. Sửa: tách
  `RemoveWindowFrame` thành `WindowHelpers.StripWindowFrame` dùng chung (chỉ gỡ khung,
  panel vẫn là card mờ đục) + panel gọi ở Loaded; log chẩn đoán GIỮ LẠI trên bản nhánh để
  test xác nhận frame=0. Chờ Manh test bản nhánh.

## 2026-11-23

- **Loa cửa sổ Nghỉ + loa nhớ trạng thái chung (Manh, mac parity):** cửa sổ Nghỉ có nút loa (cả 2 loại nhắc Pomodoro + nghỉ mắt đều qua `ShowRest` nên 1 chỗ nối đủ cả 2; service riêng, dispose khi đóng cửa sổ). Trạng thái loa CHUNG cho Full + Lite + Rest, lưu bền ở setting mới `RsvpSoundOn` (mặc định lần đầu TẮT; bấm loa ở mặt nào ghi ngay, mặt khác nhớ theo; read mới đọc trạng thái lần cuối; không sync live). Bấm loa khi đang tạm dừng chỉ đổi chế độ, vẫn cần bấm Play mới nói.
- **Space đọc lại khi đọc xong (Manh, mac parity):** Space khi đã đọc xong thì đọc lại từ đầu (trước đây không làm gì để nút share/Done hoạt động); cả 2 Style, Full + panel Lite. Nút share/Done vẫn bấm được bằng Enter + chuột. Ngoại lệ: cửa sổ Nghỉ — đọc xong thì hiện đếm ngược, phím đọc (Space / ←/→ / R) không làm gì để khỏi đọc lại sau lưng đếm ngược.
- **PR #4 MERGED — main `9b0369d` (CI #244 XANH, pre-release `v0.1.4.244`):** Fast Read Style 2 (Basic Reading) + speaker in Read mode — continuous phrase-by-phrase speech synced with focus, voice panel with Speed slider (0.6x–4x), safer keys (Space only when the read surface is focused), drag-to-resize font in Basic Reading, solid orange reminder banner, blue Done border, Maximize E9E3, "FastRead Full Window" naming.
- **3 quyết định mới về loa read (Manh, docs-only):** (1) thêm loa vào cửa sổ nghỉ; (2) loa nhớ trạng thái chung Full + Lite, lưu bền (thay khung Style 2 "mặc định TẮT, không lưu bền"); (3) sau này gộp speed audio vào WPM thành 1 control chung. Ghi vào `DECISIONS.md` + `FAST_READ.md` + `FOCUS_REST.md` + `TASK_QUEUE.md`. (Docs mac đã đẩy cùng ngày, commit `b4aa6042`.)
- **Đổi tên đích nhắc nhở (theo mac f08a9a3f):** "Run reminder in:" đổi "FastRead Full Window" → "FastRead Lite Window" (cửa sổ Nghỉ hiển thị view Lite, không phải cửa sổ Full thật). Chỉ đổi nhãn hiển thị; key/value/default của setting không đổi. "Run FastRead in:" và nút "Read in:" giữ nguyên tên cũ. (Dọn luôn 2 bullet trùng lặp trong `FOCUS_REST.md` từ đợt gộp PR #4.)

## 2026-11-22

- **main — "Read in:" + thứ tự reminder (theo mac b0e314f0 + 46c8e357 + 2cb39c10, chưa có
  số CI):** panel right demo thêm tiêu đề "Read in:" + 2 nút "FastRead Lite" / "FastRead
  Full Window" (đọc ngay, bỏ qua setting); radio thành "FastRead Full Window
  (recommended)"; "Run reminder in:" đổi thứ tự Lite lên trên.
- **main — bản chẩn đoán bug che loa (chưa có số CI):** log kích thước panel
  (outer/client/min/baseline) vào `windows.log` mỗi lần fit + submenu tray Diagnose
  (List windows / Open windows.log); nghi phạm khung cửa sổ thừa (`WS_DLGFRAME`).
- **v0.1.4.238 — main `4aa959b5` (CI #238 XANH):** hướng an toàn cho phím tắt lúc đọc —
  panel tự kích hoạt khi bài đọc panel-right bắt đầu (kể cả đường reminder), Space ăn
  ngay không cần click trước; đảo ngược quyết định cũ "reminder reads do NOT activate".
  (Bản `v0.1.4.235` bị keep-latest xoá khi 238 xong.)
- **v0.1.4.235 — main `27babd10` (CI #235 XANH):** icon alternative trong Fast Read
  (bôi đen text → icon A trong ô chat, rê thì dịch, bấm thì nghe bằng TTS riêng) +
  đổi mặc định cỡ icon float 26 → 24pt (đợt `bc5f20de`, CI #234 XANH).
- **Nhánh `feature/fastread-speaker-style2` — `714d933` (CI #237 XANH):** gỡ
  `GlobalSpaceTracker` (bắt Space toàn cục) + xoá file; panel tự kích hoạt khi đọc
  panel-right bắt đầu (đợt `7b912e63`).
- **main `bc5f20de` (CI #234 XANH):** "Float icon size" mặc định 26 → 24pt, picker thêm
  18/20/22, nhãn thêm "recommend 20-24pt"; "Auto-show" giữ 24pt, nhãn "recommend 20, 22pt".
- **main `b94893f1` (CI #233 XANH):** 2 picker cỡ icon float trong Settings → General
  (bôi chọn mặc định 26pt lúc đó, auto-show 24pt); chữ co theo vòng tròn.
- **main `fa68941f` (CI #231 XANH):** hover polish — icon hover nhỏ 24px, chỉ hiện khi
  chuột dừng đủ lâu (`HoverBlockDelayMs` mặc định 400ms, picker trong Settings → General).

## 2026-11-21

- **PR #3 merged (`217e04d5`):** block-text hover — rê chuột lên đoạn văn thì hiện icon
  (Model A, hit-test 1 điểm qua UIA, dwell 400ms, mặc định BẬT).
- **main `a6d8c106`:** đợt gộp 5 mục — tray có section "Reading" (Fast Read +
  Focus & Rest), OCR tải xong tự thêm ngôn ngữ, pin nghiêng -30°, dòng hướng dẫn OCR,
  số 249/123 lên README.
- **Nhánh speaker-style2:** Style 2 "Basic Reading" + nút loa ở Read mode (đọc liền mạch
  theo cụm-câu, phương án 1), fix CI #224 (CS0246), kéo chuột đổi font size, nút Maximize
  E9E3, fix test CI #228, fix CI230 (Full wheel cuộn trang Style 2) — CI #232 XANH, chờ
  Manh test rồi merge PR #4.
- **PR #2 merged (`0f8fdc7`):** Focus Timer + panel decor (Focus & Rest, banner nhắc nghỉ,
  pin sang hàng dots).

## 2026-11-18

- **Fix #164 — bug che loa/Copy (`bf74379`, CI XANH):** nguyên nhân thật — kéo NGANG cũng
  bị tính là kéo tay (khoá auto-fit) + `SettleResizedSize` chỉ biết co không biết dài,
  card tràn khỏi hàng `*` nên hàng Actions đè che đáy card. Fix: dài panel tới chiều cao
  cần (kẹp min/max), min card 238 → 218. **Chờ Manh xác nhận hết che.**

## Liên kết

- [Trạng thái dự án](PROJECT_STATE.md)
- [Quyết định](DECISIONS.md)
- [Việc đang làm / chờ](TASK_QUEUE.md)
