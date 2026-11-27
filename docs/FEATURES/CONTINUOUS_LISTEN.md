# Đọc nối tiếp (Continuous Listen) — bản nháp

> Trạng thái: **bản nháp** — code + test + spec đã làm xong trên nhánh `feature/continuous-listen`, **CHƯA ĐẨY**, chờ Manh duyệt + test bản nhánh. Chốt thiết kế 27/11/2026 (word-start).

## 1. Hành vi (CHỐT 27/11/2026)

- **Bôi đen text → bấm Speak → đọc đúng từ bôi đen.** Hết chunk đầu thì tự tìm đoạn tiếp theo trong trang và đọc tiếp, tới hết trang thì dừng. Không bắt đầu từ đầu câu — đọc đúng từ đang bôi đen (word-start, xem §6 vì sao khác mac).
- **Hover text → bấm Speak → đọc đoạn đang hover từ đầu đoạn**, rồi nối tiếp các đoạn sau như trên (cùng 1 cơ chế, xem §2).
- **KHÔNG tự chạy khi bôi đen** — bôi đen chỉ để copy/dịch bình thường; chỉ bấm Speak mới đọc.
- **Dừng khi:** hết trang (không tìm được đoạn tiếp theo), hoặc user dừng — pause rồi để indicator tự ẩn (phiên kết thúc lặng lẽ, như luật loa), hoặc bấm loa text khác (phiên mới thay phiên cũ), hoặc mở Fast Read / panel bắt đầu phiên loa / reminder hiện (mọi đường dừng đều qua `StopFloatSession`).
- **Indicator loa giữ ở chỗ bấm Speak ban đầu** cho cả chuỗi (v1 — chưa di chuyển theo đoạn đang đọc).
- **Ngôn ngữ detect 1 lần** từ chunk đầu (setting `SourceLanguage` hoặc `DetectLanguage`) và dùng cho cả chuỗi — trang viết bằng 1 thứ tiếng.
- **Bấm lại icon float đúng text đang đọc** thì vẫn toggle pause/resume như cũ (luật loa click-to-listen không đổi); bấm text khác thì phiên mới thay chuỗi cũ.

## 2. Cơ chế dùng chung cho hover + bôi đen (như mac)

Cả 2 đường đều vào chung `App.StartClickToListenAsync` và dùng chung 1 module `ContinuousListen/` — không làm 2 đường riêng:

- `StartClickToListenAsync` nói chunk đầu (text bôi đen, hoặc đoạn hover từ đầu), rồi — khi đoạn chứa nó khớp (xem §4) — gọi `ContinuousListenController.StartChain` ghi lại đoạn + ngôn ngữ + vị trí indicator.
- Khi chunk hết, handler `SpeechEnded` (`App.OnIconSpeechEnded`) KHÔNG dọn dẹp ngay mà gọi `ContinuousListenController.AdvanceAsync()`: probe tìm đoạn tiếp theo, nói nó; hết đoạn thì controller báo `chainEnded` và App mới dọn dẹp như cũ (`EndClickToListenSession`: ẩn indicator, ẩn icon cùng text, `ClickToListenEnded` cho reminder chờ).
- Đây là cách mac làm: cả hover-Speak lẫn select-Speak đều đặt `isChaining = true` và cùng đi qua `onEnded` → `nextBlock(below:)` (`ContinuousSpeakController`, repo mac, nhánh `feature/continuous-reading`).

## 3. Tìm đoạn tiếp theo — probe phía dưới

- **Điểm probe:** giữa ngang của khung đoạn vừa đọc (`rect.Left + rect.Width / 2` — giữa đoạn mới là chữ, mép có thể là lề/cột khác), ở các khoảng cách tăng dần dưới đáy đoạn: **[4, 24, 48, 80]pt** như mac (`nextBlockProbeOffsets`), đổi ra pixel theo DPI — **[5, 32, 64, 107]px ở 100%** (1pt = 4/3px ở 96 DPI, nhân thêm tỉ lệ DPI). 4 mức (không phải 1): khoảng cách tới đoạn sau thay đổi (dòng khít / dòng trống / tiêu đề), nên probe leo dần tới khi gặp đoạn hoặc hết thang (hết trang).
- **Cách lấy đoạn tại 1 điểm** (giống `BlockTextHoverService.TryGetParagraph`): `automation.FromPoint` → `TextPattern.RangeFromPoint` → `ExpandToEnclosingUnit(Paragraph)` → `GetText(-1)` + `GetBoundingRectangles()` (hợp các khung dòng = khung đoạn). **KHÔNG** dùng fallback `DocumentRange` / `ValuePattern` / `Name` — đó là đường lấy text của control đơn lẻ, không phải đoạn trong tài liệu; probe đoạn thất bại nghĩa là "không có đoạn ở điểm này", chuỗi thử mức tiếp theo (hoặc kết thúc).
- **Chỉ nhận đoạn KHÁC** (`ContinuousListenChain.IsNewParagraph`): đoạn ứng viên phải có text thật và khác text đoạn vừa đọc (so sau khi trim — text UIA hay kèm xuống dòng thừa). Cùng text = gặp lại đúng đoạn cũ (hoặc tiêu đề lặp) — không đọc lại, thử mức tiếp theo.
- **Hết thang mà không gặp đoạn khác = hết trang** — kết thúc chuỗi, dọn dẹp như phiên nghe thường.
- **Không bao giờ probe vào cửa sổ của chính TransPeek** (panel, icon, Reader): probe nào chạm process của mình (kể cả pid 0 = không rõ) đều bỏ — đọc nhầm text của chính app thì tệ hơn là kết thúc chuỗi sớm. (Khác monitor icon ở chỗ này: monitor fail-open cho pid 0 để khỏi mất icon; chuỗi fail-closed vì đọc nhầm thì tệ hơn dừng sớm.)

## 4. Đoạn phải được capture lúc bôi đen / hover (bài học từ mac)

- **Vì sao:** tới lúc bấm Speak thì đoạn chứa chỗ bôi đen/hover thường đã không còn lấy được nữa — chuột bấm xuống để bôi đen thì hover ngừng bám theo, và hover bị chặn khi icon đang hiện (bài học mac follow-up `fa187c83`/`5259d106`: `hoveredBlock` gần như luôn nil lúc click Speak). Nên đoạn phải được lấy **ngay lúc monitor phát hiện** và giữ lại tới lúc Speak.
- **Đường bôi đen:** `SelectionMonitor.TryGetSelection` lấy thêm đoạn chứa selection (`RangeFromPoint` tại điểm neo của selection → mở rộng tới `Paragraph` → text + khung), trả qua callback; `App.OnSelectionChanged` lưu vào `_lastParagraphText` / `_lastParagraphRect`. Mở rộng từ điểm neo (không mở rộng chính range selection — `ExpandToEnclosingUnit` sửa range tại chỗ, làm lệch điểm neo icon về góc đoạn).
- **Đường hover:** `BlockTextHoverService.TryGetBlockText` trả thêm khung đoạn (chỉ khi đường paragraph thật sự thành công; các fallback `DocumentRange` / `Value` / `Name` trả khung element — không phải khung đoạn, nên báo null); `App.OnBlockTextHovered` lưu text (chính là text đoạn) + khung. Đường re-show từ cache (`ShowCached`) dùng khung đoạn của lần probe gần nhất (chuột vẫn trong cùng đoạn nên vẫn đúng).
- **Kiểm tra khớp trước khi chain** (`ContinuousListenChain.CanChain`): đoạn đã lưu phải có text + khung thật, và phải chứa text sắp nói (text bôi đen là chuỗi con của đoạn; text hover thì bằng text đoạn). Không khớp = context cũ (bôi đen/hover khác, hoặc text re-grab qua clipboard đã đổi) → **không chain**, đọc đúng text rồi dừng như cũ. Không bao giờ chain trên phỏng đoán.
- **Icon alternative trong Reader KHÔNG chain:** selection trong Reader không capture đoạn bao giờ, nên `OnAlternativeIconSpeakRequested` xóa context đoạn cũ trước khi gọi `StartClickToListenAsync` — nếu không, chuỗi sẽ probe nhầm sang trang của app khác đã lưu trước đó.

## 5. Fallback — bước nào hỏng cũng về hành vi cũ

- Không lấy được đoạn lúc bôi đen/hover (provider không có `Paragraph`, probe UIA lỗi) → không có context → đọc đúng text bôi đen/hover rồi dừng, như trước khi có tính năng này.
- Probe đoạn tiếp theo thất bại ở mọi mức / không có đoạn khác → hết trang → kết thúc chuỗi, dọn dẹp như phiên nghe thường.
- Nói đoạn tiếp theo thất bại (không có giọng, tổng hợp lỗi) → kết thúc chuỗi.
- Mọi lỗi UIA đều được nuốt (try/catch quanh từng bước probe): cây accessibility hỏng không bao giờ làm crash app hay kẹt chuỗi — tệ nhất là chuỗi dừng sớm.
- Không có sentence splitter: word-start không cần tìm câu (xem §6).

## 6. Khác mac — word-start thay vì sentence-start (CHỐT 27/11/2026 22:32)

- Mac đọc từ **đầu câu** chứa từ bôi đen (`playOffset = sentence.location`, "never mid-sentence"). Đó là workaround cho giới hạn của mac: overlay highlight của mac cần biết câu đang đọc để vẽ khung + map vị trí từ (`handleWordRange` cộng `playOffset`), nên nó phải bắt đầu ở ranh giới câu.
- Windows v1 **không có overlay** — không có gì để map — nên không có lý do gì để lùi về đầu câu. Đọc đúng từ bôi đen là đúng và đơn giản hơn (không cần tách câu). **Không copy sentence-start của mac sang.**
- Nếu sau này Windows làm overlay/highlight trên trang thì xem lại quyết định này (lúc đó mới cần ranh giới câu).

## 7. Code map

| File | Việc |
|---|---|
| `src/TransPeek.Core/ContinuousListen/TextParagraph.cs` | Record đoạn (text + khung) — value type dùng chung Core/app. |
| `src/TransPeek.Core/ContinuousListen/ContinuousListenChain.cs` | Logic thuần: trạng thái chuỗi (`Start` / `AcceptCandidate` / `Cancel`), điểm probe (`NextProbePoints`), nhận biết đoạn mới (`IsNewParagraph`), kiểm tra khớp (`CanChain`). Có unit test. |
| `src/TransPeek/ContinuousListen/ContinuousListenController.cs` | Controller app: giữ chain (Core), probe UIA đoạn tiếp theo, nói qua callback của App, báo `chainEnded` khi chuỗi tự hết. Không chạm WinUI window. |
| `src/TransPeek/Services/SelectionMonitor.cs` | Capture đoạn chứa selection lúc phát hiện (trả qua callback). |
| `src/TransPeek/BlockText/BlockTextHoverService.cs` | Trả khung đoạn qua callback (chỉ khi đường paragraph thật sự thành công); giữ khung cho đường re-show cache. |
| `src/TransPeek/App.xaml.cs` | Lưu context đoạn (`_lastParagraphText` / `_lastParagraphRect`); tạo controller; `StartClickToListenAsync` bắt đầu chuỗi; `OnIconSpeechEnded` nối tiếp thay vì dọn dẹp; `EndClickToListenSession` (dọn dẹp chung); `StopFloatSession` hủy chuỗi mọi đường dừng; icon alternative xóa context đoạn. |
| `tests/TransPeek.Tests/ContinuousListenChainTests.cs` | Unit test Core: điểm probe (100% + 150% DPI), đoạn mới/cũ, `CanChain`, chuyển trạng thái. |

Quy tắc code áp dụng: C# 12 (project `LangVersion` 12 — không dùng tính năng C# 13/14; gán qua `?.` cần C# 14 nên chỉ dùng `?.Invoke()`); mọi block code có comment ghi MỤC ĐÍCH.

## 8. Trạng thái + test

- **Nhánh `feature/continuous-listen`, CHƯA ĐẨY** — code + test + spec này, chờ Manh duyệt rồi mới đẩy 1 commit (CI nhánh xanh → gửi link artifact cho anh test).
- **Unit test Core:** `ContinuousListenChainTests` — 18 test (điểm probe, đoạn mới/cũ, `CanChain`, chuyển trạng thái). Chạy trên CI Linux cùng các test Core khác (không cần WinUI/UIA).
- **Chưa test live:** UIA probe + chuỗi thật (bôi đen 1 từ → đọc đúng từ → nối đoạn tới hết trang) cần Manh test trên máy công ty với bản nhánh — các app khác nhau (Word/Edge/Chrome/Notepad) expose `TextPattern` khác nhau, nên probe có thể khác nhau theo app.
- **Test checklist cho Manh (bản nhánh):** (1) bôi đen 1 từ trong Word/Edge → bấm Speak → có đọc đúng từ đó (không lùi về đầu câu) rồi nối đoạn tiếp theo tới hết trang không; (2) hover 1 đoạn → bấm Speak → có đọc từ đầu đoạn rồi nối tiếp không; (3) bấm dừng (pause rồi để indicator tự ẩn) giữa chuỗi → có dừng hẳn không; (4) bôi đen text trong app không expose UIA (hoặc Reader) → có đọc đúng text rồi dừng (không chain, không crash) không.

## Liên kết

- [Speech Player (luật loa click-to-listen)](SPEECH_PLAYER.md)
- [Block Text Hover (đường hover + cách lấy đoạn)](BLOCK_TEXT_HOVER.md)
- [Float Icon](FLOAT_ICON.md)
- [Structure Map](../STRUCTURE_MAP.md)
- Mac (tham khảo, không copy): `ContinuousSpeakController` (`start(from:fromSentenceContaining:)`, `onEnded` → `nextBlock(below:)`, `nextBlockProbeOffsets [4, 24, 48, 80]`) + `docs/RESEARCH/tts-speechify-naturalreader/step2-trial-plan.md` §367–380 — repo `TranManhTuanLabs/TransPeek`, nhánh `feature/continuous-reading`.
