# Panel TransPeek macOS — Hành vi co dãn (input / output / snap-back / khung)

**Ngày:** 16/11/2026 · **Repo mac:** `TranManhTuanLabs/TransPeek` (đọc code trực tiếp, local `~/workspace/projects/moepeek-translator/transpeek/`, HEAD `f093b75` + 2 sửa chưa commit: clamp lúc mở `6401c99`, đo chiều cao `1fe70a2`) · **Repo Windows đối chiếu:** `~/workspace/projects/transpeek-windows` (bản snapshot local, không phải git repo — đã đối chiếu được).

**Cách đọc báo cáo này:** viết cho người đang học, không phải dân IT gốc. Mỗi mục đều có ví dụ cụ thể ("dịch 1 từ" vs "dịch 1 đoạn dài") và ghi rõ số đo lấy từ đâu trong code.

---

## 1. Mô hình chung — panel co dãn như thế nào

Panel là 1 cửa sổ **không viền, không tiêu đề** (`PopupPanel: NSPanel`, style `.borderless`). Vì không có viền nên macOS **không cho kéo mép/cạnh sẵn** như cửa sổ thường — mọi hành vi co dãn đều do code của app tự làm, gồm 3 đường:

| Đường nào làm panel đổi cỡ | Khi nào chạy |
|---|---|
| **Auto-fit** (tự động): `PopupPanelController.resizeForResultContent()` | Khi nội dung kết quả đổi (dịch xong, đổi ngôn ngữ, provider xong thêm) |
| **Tay kéo** (người dùng kéo): `ResizeGripView` (góc) + `EdgeResizeHandle` (cạnh phải/dưới) | Khi anh kéo chuột |
| **Chia ô nhập** (divider): `DraggableDividerView` | Khi anh kéo vạch ngang giữa ô nhập và kết quả |

Panel **không bao giờ tự đổi chiều rộng** — chỉ đổi chiều cao theo nội dung. Rộng chỉ đổi khi anh kéo tay. Đây là thiết kế có chủ ý, giống cả 2 bên mac/Windows.

---

## 2. Ô nhập (input) — dãn/co theo nội dung

### 2.1. Ô nhập tự dãn khi gõ/dán text dài

- Ô nhập là 1 ô text AppKit (`SourceTextEditor`, bọc `NSTextView`) trong `SourceInputView.swift`.
- Mỗi lần nội dung đổi, nó tự đo chiều cao thật của text (`measuredContentHeight()` — đo bằng `layoutManager.usedRect`) rồi báo lên trên qua `onContentHeightChange`.
- `PopupView.expandInputHeightIfNeeded` nhận số đó rồi tính chiều cao mục tiêu:
  - **Dãn lên** tới đúng chiều cao nội dung, nhưng không vượt **trần**.
  - **Co lại** khi anh xoá text: co về **mốc baseline** (xem dưới), tức là xoá text dài đã dán thì ô nhập thu nhỏ lại, không để khoảng trống thừa.

### 2.2. Các con số của ô nhập

| Giá trị | Số | Lấy từ đâu |
|---|---|---|
| Cao tối thiểu (min) | **36pt** | `inputMinHeight = 36` trong `PopupView` |
| Cao baseline (mốc mặc định) | **48pt** | Setting `popupInputHeight`, mặc định 48 (`Constants.swift`) |
| Cao tối đa (max) | **6 dòng text** (tính theo cỡ chữ hiện tại), đồng thời không vượt quá **chiều cao panel − 120pt** | `maxInputHeight` trong `PopupView` (giữ lại 120pt cho thanh ngôn ngữ + kết quả) |
| Ví dụ ở cỡ chữ 14 | 6 dòng ≈ 125pt | 6 × chiều cao 1 dòng (~17pt) + dòng gợi ý phím tắt + 8pt đệm |

Nói đơn giản: **gõ 1 từ → ô nhập cao ~48pt (1–2 dòng). Dán 1 đoạn 5 dòng → ô nhập dãn lên ~110pt. Dán 1 đoạn 20 dòng → ô nhập dừng ở trần 6 dòng (~125pt), phần còn lại cuộn trong ô.** Xoá hết về 1 từ → ô nhập co về 48pt.

### 2.3. Vạch chia kéo tay (divider)

- Giữa ô nhập và vùng kết quả có 1 vạch ngang (`DraggableDividerView`, cao 8pt, con trỏ đổi thành mũi tên dọc khi di vào).
- Kéo vạch lên/xuống = đổi chiều cao ô nhập, chặn trong [36pt, trần 6 dòng] như trên.
- **Chỉ kéo vạch mới lưu** chiều cao ô nhập vào setting (`popupInputHeight`, lưu lúc thả chuột). Còn chiều cao ô nhập tự dãn theo nội dung (mục 2.1) thì **không lưu** — đóng panel mở lại, ô nhập về đúng mốc baseline 48pt (hoặc mốc anh kéo vạch lần trước).
- Không có snap-back cho ô nhập: kéo vạch tới đâu thì ở đó.

---

## 3. Vùng kết quả (output) — dãn/co theo nội dung

Đây là phần phức tạp nhất, gồm 3 tầng: **đo chiều cao → báo lên controller → auto-fit đổi khung panel**.

### 3.1. Tầng 1 — Đo chiều cao từng thẻ kết quả

- Mỗi provider (Google Translate...) có 1 thẻ `ProviderResultCard`, bấm chevron để thu/mở nội dung (mặc định mở).
- Nội dung dịch hiển thị bằng ô text AppKit (`SelectableResultTextView`, bọc `NSTextView`) để chọn được chữ bên trong.
- Ô text này **không tự báo chiều cao** — phải đo bằng code: `reportContentHeight()` dùng `layoutManager.ensureLayout` + `usedRect` để tính chiều cao thật của text, rồi báo lên qua `onContentHeightChange`.
- Thẻ kết quả nhận số đó vào `resultTextHeight` (khởi đầu là **48pt — chỉ là số ước lượng**) rồi đặt khung cố định `.frame(height: resultTextHeight)`.
- **Điểm yếu ở đây:** khung là cố định, nên nếu số đo lần đầu sai (nhỏ hơn thực tế) thì text bị cắt ngắn, panel tưởng nội dung ngắn nên không dãn. Chi tiết xem mục 6.2.

### 3.2. Tầng 2 — Đo chiều cao toàn panel (3 số đo)

`PopupView` dùng `GeometryReader` đo 3 số, gộp lại thành `PopupResultLayout` (qua `PopupResultLayoutPreferenceKey` — gom 1 lần rồi mới báo, để không trộn số đo cũ/mới):

| Số đo | Nghĩa | Đo ở đâu |
|---|---|---|
| `contentHeight` | Chiều cao **toàn bộ** nội dung kết quả (kể cả phần đang bị cuộn khuất) | `GeometryReader` nền của cột kết quả trong `ScrollView` |
| `viewportHeight` | Chiều cao **phần đang nhìn thấy** của vùng kết quả | `GeometryReader` nền của chính `ScrollView` |
| `containerHeight` | Chiều cao **toàn bộ panel** hiện tại | `GeometryReader` nền của cả panel |

Từ 3 số này tính ra **chiều cao phần khung cố định** (gọi là `chromeHeight`): `chromeHeight = containerHeight − viewportHeight` = chiều cao của top bar + ô nhập + thanh ngôn ngữ + nút bấm + link từ điển + footer (mọi thứ trừ vùng kết quả).

Mỗi lần 1 trong 3 số đổi, `PopupView` báo lên controller (`onResultLayoutChange`), controller hẹn chạy auto-fit.

### 3.3. Tầng 3 — Auto-fit: đổi khung panel theo nội dung

`PopupPanelController.resizeForResultContent()` chạy (qua 1 Task gộp, để không chạy nhiều lần cùng lúc), tính khung mới bằng `PopupPanelSizing.frameToFit`:

```
chiều cao cần = max(chiều cao baseline, chromeHeight + contentHeight)
chiều cao mới = min(chiều cao cần, 800, chiều cao màn hình − 16)
giữ nguyên mép trên, dãn xuống dưới, kẹp trong màn hình
```

- **`chiều cao baseline`** = setting `popupDefaultHeight` (mặc định **350pt**) — tức là chiều cao đã lưu. Đây là **sàn**: panel không bao giờ auto-fit xuống dưới con số này (xem lỗ hổng mục 7.1).
- **Chiều cao tối đa** = 800pt (`maxSize`), đồng thời không vượt màn hình.
- **Chỉ đổi khi khác hơn 0.5pt** so với khung hiện tại — tránh rung/nhấp nháy do đo sai số nhỏ.
- **Chiều rộng giữ nguyên** — auto-fit không bao giờ đổi rộng.

### 3.4. Các "cửa chặn" (guard) — khi nào auto-fit KHÔNG chạy

Auto-fit bỏ qua (không làm gì) khi gặp 1 trong các điều này:

| Cửa chặn | Nghĩa đơn giản |
|---|---|
| Panel đang ẩn | Không có panel thì khỏi dãn |
| `isUserResizing == true` | **Anh đang kéo tay** — auto-fit đứng yên để không giật lại chiều cao lúc anh kéo |
| `manualResizeGeneration == translationGeneration` | **Anh vừa kéo tay trong lần dịch này** — tôn trọng cỡ anh vừa chọn, không tự sửa lại cho tới lần dịch mới |
| Thiếu số đo (`contentHeight` / `viewportHeight` / `containerHeight` chưa có) hoặc `viewportHeight == 0` | Chưa đo xong thì chưa biết dãn bao nhiêu, đợi lần sau |

Hai cửa chặn giữa là cặp song sinh: `translationGeneration` tăng lên 1 mỗi lần dịch mới (`TranslationCoordinator`, dòng 610), còn `manualResizeGeneration` được set bằng số generation hiện tại mỗi khi anh thả tay kéo. Nhờ vậy, **kéo tay chỉ chặn auto-fit trong đúng lần dịch đó**; dịch từ mới là auto-fit chạy lại bình thường. Test `streamingResultsRespectManualResize` khoá đúng hành vi này.

---

## 4. Co lại (snap-back) — chỉ có dọc, không có ngang

### 4.1. Snap-back dọc (đã có, đã test ok)

Khi anh **thả chuột** sau khi kéo (góc hoặc cạnh dưới), hàm `finishPanelResize` chạy:

1. **Luôn lưu chiều rộng** vừa kéo vào setting (`popupDefaultWidth`).
2. Tính **chiều cao vừa nội dung** (`popupFittedFrame` — chính là công thức auto-fit ở mục 3.3, tính từ số đo mới nhất).
3. **Nếu anh kéo cao hơn mức vừa nội dung (> 0.5pt)** → panel tự co về đúng mức vừa nội dung (có animation), và **không lưu** chiều cao thừa đó, **không chặn** auto-fit sau này. Đây là snap-back.
4. **Nếu không** (kéo vừa hoặc thấp hơn mức vừa nội dung) và chiều cao đổi hơn **8pt** so với lúc bắt đầu kéo → **lưu** chiều cao đó (`popupDefaultHeight`) và **chặn** auto-fit trong lần dịch này (`manualResizeGeneration = generation`).

Ví dụ:
- Dịch 1 từ (nội dung cần cao 260pt). Anh kéo panel lên 500pt rồi thả → panel tự co về 260pt, không lưu 500pt.
- Dịch 1 đoạn dài (nội dung cần 600pt). Anh kéo lên 600pt rồi thả → giữ nguyên 600pt, lưu lại, auto-fit không sửa nữa trong lần dịch này.

### 4.2. Snap-back ngang (chưa có — Manh bảo chưa cần)

- Kéo cạnh phải (chỉ đổi rộng): lúc thả, code **chỉ lưu rộng rồi dừng** (`resizeHeight: false`) — không có bước "co rộng về vừa nội dung".
- Lý do chưa làm: rộng vừa nội dung khó định nghĩa hơn cao (text wrap lại theo rộng — panel rộng hơn thì text cần ít dòng hơn, nên "rộng vừa" không có 1 con số cố định như cao). Manh chốt 16/11: **chưa cần làm ngang**.
- Hệ quả: kéo rộng thừa thì panel giữ nguyên rộng đó, và rộng đó được lưu lại cho lần mở sau.

### 4.3. Tay kéo dọc còn 1 chi tiết: không giật khi kéo xuống

Lúc kéo, cờ `isUserResizing` phải giữ `true` suốt lúc kéo để auto-fit không nhảy vào. Trước đây, SwiftUI tạo lại tay kéo giữa chừng làm `onDisappear` tắt cờ sớm → auto-fit nhảy vào kéo panel về → **giật**. Đã sửa (commit `8577738`): `onDisappear` chỉ tắt cờ khi **không còn kéo** (`dragStartMouse == nil`). Manh xác nhận #192: kéo dọc hết giật.

---

## 5. Text dài vs text ngắn — panel xử lý khác nhau ra sao

| | Text ngắn (1 từ, 1 câu) | Text dài (1 đoạn dài) |
|---|---|---|
| Ô nhập | Cao ~48pt (baseline) | Dãn tới trần 6 dòng (~125pt), còn lại cuộn trong ô |
| Vùng kết quả | Thẻ kết quả thấp (~48pt) | Thẻ kết quả cao theo text |
| Panel (auto-fit) | Cao **~260pt** (mức vừa nội dung — test khoá con số này) | Dãn lên tới `min(cần, 800, màn hình−16)` |
| Khi vượt trần | — | Panel dừng ở 800pt hoặc mép màn hình, text còn lại **cuộn** trong `ScrollView` |
| Kéo tay | Kéo cao thừa → thả ra **co về 260pt** | Kéo cao → giữ, ô kết quả dãn theo; kéo tới 800 thì dừng |

Con số **260pt** (cao vừa nội dung khi text ngắn) được test khoá trong `PopupPanelLayoutTests` (kèm dải cho phép 350–380 cho trường hợp mở lại). Test còn khoá 2 tính chất quan trọng:
- **Layout phải ổn định** (`longTextLayoutSettles`): sau khi dịch xong, panel không được resize liên tục mãi — phải dừng lại trong 10 giây.
- **Đóng/mở lại không giữ cỡ cũ** (`dismissAndReopenDiscardsPendingResize`): đóng panel lúc đang dịch text dài rồi mở lại dịch text ngắn → panel về cỡ text ngắn (350–380pt), không giữ cỡ dài cũ.

---

## 6. Các lỗi đã gặp và đã sửa (đều trong ngày 16/11)

### 6.1. Cờ `isUserResizing` bị kẹt → text dài không tự dãn (sửa ở #193)

- **Triệu chứng:** text dài không tự dãn; anh giả kéo 1 cái rồi thả thì nó dãn ra.
- **Nguyên nhân:** kéo bị ngắt giữa chừng (SwiftUI tạo lại tay kéo) mà `onEnded` không chạy, `onDisappear` giữ cờ vì `dragStartMouse` còn khác nil → cờ `isUserResizing` kẹt ở `true` mãi → auto-fit bị chặn mãi.
- **Cách sửa** (commit `f093b75`, trong `resizeForResultContent`): trước khi kiểm tra cờ thì kiểm tra thêm — **kéo thật lúc nào cũng phải giữ nút chuột**. Nếu cờ báo "đang kéo" mà không có nút chuột nào đang giữ (`NSEvent.pressedMouseButtons == 0`) thì chắc chắn cờ đã cũ → tắt cờ đi rồi cho auto-fit chạy.
- **Còn sót không:** cách này chỉ chữa lúc auto-fit chạy. Nếu cờ kẹt mà không có lần đo layout nào mới (không có `onResultLayoutChange`), auto-fit cũng không được gọi để mà chữa. Trong thực tế mỗi lần dịch mới đều có đo mới nên ổn, nhưng không có cơ chế thử lại riêng — xem khuyến nghị mục 8.

### 6.2. Đo chiều cao khi ô text chưa có rộng → text dài không tự dãn (sửa ở #195)

- **Triệu chứng (test trên #194):** ngắn/trung bình dãn đúng, **chỉ dài bị**; giả kéo thì hiện ra "đã dãn sẵn" (không phải vừa dãn xong).
- **Nguyên nhân:** `SelectableResultTextView.reportContentHeight()` đo chiều cao ngay lúc `updateNSView` chạy, nhưng lần đầu ô text chưa có chiều rộng (`bounds.width == 0`, panel vừa mở chưa xếp layout xong) nên đo sai (nhỏ). `resultTextHeight` kẹt ở số nhỏ → khung text bị cắt ngắn → `contentHeight` đo được cũng nhỏ → panel không dãn. Chỉ khi giả kéo (đổi khung → đo lại với rộng đúng) mới ra số đúng.
- **Cách sửa** (commit `1fe70a2`, đang ở local, CI #195): `reportContentHeight()` **bỏ qua khi rộng = 0** (không báo số sai, không lưu lại); `updateNSView` khi rộng = 0 thì **hẹn đo lại sau khi layout xong** (`DispatchQueue.main.async`) để báo chiều cao đủ.
- **Còn sót không:** vẫn phụ thuộc vào `updateNSView` được gọi lại sau layout. Nếu SwiftUI không gọi lại (nội dung không đổi), số đo đúng chỉ tới từ lần hẹn async đó. Đủ cho trường hợp đã gặp, nhưng là chỗ mong manh — xem khuyến nghị mục 8.

### 6.3. Chặn tối thiểu bị bỏ qua lặng lẽ (sửa ở #192)

- **Triệu chứng (test trên #191):** set `minSize = 400×260` rồi mà vẫn thu nhỏ hơn được.
- **Nguyên nhân:** tay kéo đọc thuộc tính `panel.minSize` lúc kéo, nhưng `NSHostingView` có thể reset thuộc tính này khi xếp layout → tay kéo đọc được số nhỏ hơn.
- **Cách sửa** (commit `f9690f1`): đặt hằng `PopupPanel.minimumSize = 400×260`, tay kéo chặn **thẳng vào hằng này** (không đọc thuộc tính cửa sổ nữa), đồng thời set cả `contentMinSize`.

---

## 7. Giá trị khung: mặc định / tối thiểu / tối đa / nhớ kích thước

### 7.1. Các con số lấy từ đâu

| Giá trị | Số | Nguồn |
|---|---|---|
| Rộng mặc định | **450pt** | Setting `popupDefaultWidth` (mặc định 450) |
| Cao mặc định | **350pt** | Setting `popupDefaultHeight` (mặc định 350) |
| Rộng tối thiểu | **400pt** | `PopupPanel.minimumSize.width` |
| Cao tối thiểu | **260pt** | `PopupPanel.minimumSize.height` (= cao vừa nội dung khi text ngắn) |
| Tối đa | **800×800pt** | `PopupPanel.maxSize` trong `init` |
| Cao ô nhập mặc định | **48pt** | Setting `popupInputHeight` (mặc định 48) |

### 7.2. Tối thiểu/tối đa được enforce ở những chỗ nào

| Chỗ enforce | Rộng | Cao |
|---|---|---|
| `PopupPanel.init` (thuộc tính cửa sổ) | `minSize` / `contentMinSize` = 400×260, `maxSize` = 800×800 | như cột rộng |
| Tay kéo góc + cạnh (lúc kéo) | Chặn vào `PopupPanel.minimumSize` và `panel.maxSize` | như cột rộng |
| `setupPanel` lúc mở panel | Chặn cỡ khôi phục: `max(đã lưu, 400)` | Chặn cỡ khôi phục: `max(đã lưu, 260)` (thêm ở commit `6401c99`) |
| Auto-fit `frameToFit` | Không đổi rộng | Chặn tối đa 800 + màn hình; **không chặn tối thiểu** (dựa vào baseline + nội dung ≥ 260 trong thực tế) |
| Settings (slider tay chỉnh) | Slider 280...800 | Slider 200...800 |

**Chỗ chưa enforce:**
- **Slider Settings lỗi thời:** dải slider rộng 280...800 và cao 200...800 vẫn là số của mức tối thiểu cũ (280×200). Người dùng kéo slider xuống 280/200 thì panel vẫn mở ở 400/260 (nhờ chặn ở `setupPanel`), nhưng slider hiển thị 1 đằng, panel mở 1 nẻo — dễ gây nhầm.
- **`frameToFit` không chặn tối thiểu tường minh:** nếu `popupDefaultHeight` bị set thấp (ví dụ slider 200) và nội dung cực ngắn, chiều cao tính ra vẫn ≥ 260 nhờ `chromeHeight + contentHeight` — an toàn trong thực tế nhưng không có bảo đảm bằng code.

### 7.3. Nhớ kích thước — lưu/khôi phục hoạt động ra sao

**Lưu (lúc thả tay kéo — `finishPanelResize`):**
- **Rộng: lưu mọi lần**, dù chỉ đổi 1pt (`Defaults[.popupDefaultWidth] = Int(panel.frame.width)`).
- **Cao: chỉ lưu khi đổi hơn 8pt** so với lúc bắt đầu kéo, và **không lưu khi bị snap-back** (kéo cao thừa rồi co về — chiều cao thừa đó không được lưu).
- Vị trí panel cũng được nhớ riêng (nếu bật `popupRememberPosition`): góc trên-trái lưu mỗi lần panel di chuyển; lúc mở lại thì tính lại vị trí từ góc đó theo cỡ panel hiện tại, kẹp trong màn hình.

**Khôi phục (lúc mở panel — `setupPanel`):**
- Mở ở đúng cỡ đã lưu (rộng + cao), nhưng chặn dưới ở mức tối thiểu 400×260 (mục 7.2).
- Sau đó auto-fit chạy 1 lần (`scheduleResultResize` lúc `showAtCursor`/`showAtScreenCenter`) để chỉnh cao theo nội dung.
- **Lưu ý:** panel được **tạo mới hoàn toàn mỗi lần mở** (`dismiss()` huỷ panel cũ, `resultLayout` reset về rỗng) — tránh state cũ kẹt lại từ lần dịch trước.

**Trường hợp lưu/khôi phục sai đã biết:** cỡ lưu từ bản cũ (lúc tối thiểu còn 280×200) có thể nhỏ hơn 400×260 → đã chặn ở `setupPanel` (commit `6401c99`).

### 7.4. Lỗ hổng lớn nhất còn tồn tại: "sàn chiều cao = cỡ đã lưu"

Đây là điểm cần chú ý nhất. Auto-fit tính:

```
chiều cao cần = max(popupDefaultHeight [cỡ đã lưu], chromeHeight + contentHeight)
```

Nghĩa là **chiều cao anh kéo lần trước trở thành sàn cho mọi lần sau**:
- Ví dụ: anh dịch 1 đoạn dài, kéo panel lên 600pt → 600pt được lưu. Hôm sau anh dịch 1 từ → panel mở ở 600pt (vì `setupPanel` mở ở cỡ đã lưu) và auto-fit cũng không hạ xuống dưới 600pt (vì baseline = 600pt). Panel cao thừa cho 1 từ, phải tự kéo nhỏ lại.
- Đây **đúng là lỗi Windows từng bị** (CI #143/#145: "panel vẫn nhớ vị trí dọc siêu dài dù chỉ 1 từ") và **Windows đã sửa** bằng cách bỏ sàn này (xem bảng mục 8). **Mac vẫn còn lỗi này.**
- Snap-back không cứu được trường hợp này: snap-back chỉ chạy lúc thả tay kéo, còn panel mở ra đã cao sẵn thì không có ai co nó lại.

---

## 8. So sánh mac vs Windows

Windows đọc từ `TranslatePanelWindow.xaml` + `TranslatePanelWindow.xaml.cs` (bản snapshot local). Lưu ý: Windows vừa bỏ nhánh `longText` nâng cap output (commit `66bd21d`, sau #147 "output siêu expand dài") — bảng dưới phản ánh code mới nhất trên local.

| Hành vi | macOS | Windows | Nhận xét |
|---|---|---|---|
| Rộng tối thiểu | 400pt (`PopupPanel.minimumSize`) | 400pt (`MinPanelWidth`) | Giống nhau (mac làm mẫu) |
| Cao tối thiểu | 260pt cố định (`minimumSize.height`) | **Động** (`ComputeMinPanelHeight`): đo các hàng cố định + thẻ output tối thiểu 150pt, sàn 450pt | Windows cao hơn nhiều (450pt) vì nó cộng cả thẻ output tối thiểu; mac lấy đúng cao vừa nội dung ngắn |
| Tối đa | 800×800 | 800×800 (`MaxPanelSize`), kẹp thêm theo vùng làm việc màn hình | Giống nhau |
| Rộng mặc định / mở | 450pt (`popupDefaultWidth`) | 440pt (`PanelWidth`) | Gần giống |
| Cao lúc mở | **Mở ở cao đã lưu** (`popupDefaultHeight`, mặc định 350) | **Luôn mở gọn ở 450pt** (`MinPanelHeight`), không dùng cao đã lưu | **Khác nhau — quan trọng** (xem 7.4) |
| Auto-fit | `frameToFit`: cần = max(**cao đã lưu**, chrome + nội dung); tối đa 800/màn hình; giữ rộng | `ResizeToFitContent`: đo `DesiredSize` của toàn panel ở rộng hiện tại; cần = đúng cao nội dung (**không có sàn cao đã lưu**); tối đa = vùng làm việc − 16 | Windows đã bỏ sàn cao đã lưu (sửa #143/#145); mac vẫn còn |
| Text dài (auto-fit) | Dãn panel tới đủ nội dung (tới 800/màn hình), còn lại cuộn | Ô output **giữ cap 260pt** khi auto-fit, text dài cuộn trong ô; panel chỉ cao tới nội dung đã cap | mac cho thấy nhiều chữ hơn không cần cuộn; Windows panel gọn hơn, cuộn sớm hơn |
| Text dài (kéo tay) | Kéo cao → ô kết quả dãn theo (ScrollView `*`) | Kéo cao → `PersistResizedSize` nâng cap ô output lên bằng cao toàn bộ text, ô dãn theo | Giống nhau về cảm giác |
| Snap-back dọc | Có (`finishPanelResize` lúc thả): kéo cao hơn vừa nội dung > 0.5pt → co về vừa nội dung, không lưu cao thừa | Có (`PersistResizedSize`, chạy sau khi kéo xong 300ms): kéo cao hơn cần > 8pt → co về mức cần; không thì giữ + nâng cap | Giống nhau; khác ngưỡng (mac 0.5pt, Windows 8pt) và thời điểm (mac lúc thả, Windows debounce 300ms) |
| Snap-back ngang | Không có (Manh bảo chưa cần) | Không có | Giống nhau |
| Chặn auto-fit khi kéo tay | `manualResizeGeneration == translationGeneration` (chặn đúng lần dịch đang kéo); lần dịch mới tự mở lại | `_manualResizeGeneration == _epoch` (chặn trong lần hiện panel này); **xóa chặn ngay khi có kết quả mới** (`ShowResult` set null) | Tương đương; Windows xóa tường minh khi kết quả mới về, mac dựa vào số generation tăng mỗi lần dịch |
| Ô nhập tự dãn | Tới **6 dòng**, đồng thời chừa 120pt cho phần còn lại; co về baseline 48pt khi xoá | `TextBox` tự dãn tới **`MaxHeight = 120`** cố định; cửa sổ dãn theo (`SizeChanged` → auto-fit) | Tương đương; mac linh hoạt theo cỡ panel, Windows cố định 120 |
| Ô nhập kéo tay | Vạch chia ngang, lưu vào `popupInputHeight` lúc thả | Không có vạch chia | mac hơn 1 tính năng |
| Cơ chế kéo | **Tự chế** (cửa sổ không viền → hệ thống không cho kéo): `DragGesture` + `setFrame`, 3 tay kéo (góc 3 vạch, cạnh phải/dưới vô hình 6pt) | **Native**: dải resize của hệ điều hành (8px vật lý) + `WM_GETMINMAXINFO` chặn tối thiểu ngay lúc kéo; grip 3 vạch chỉ để trang trí | Windows được hệ điều hành chặn min/max hộ; mac phải tự chặn bằng code (đã từng sót — xem 6.3) |
| Nhớ kích thước | Rộng lưu mọi lần thả; cao lưu khi đổi > 8pt và không bị snap-back; mở lại dùng cả 2 | Rộng + cao lưu sau khi kéo xong 300ms (debounce), ngưỡng đổi > 0.5pt cho cả 2; mở lại chỉ dùng rộng, cao luôn mở gọn | Windows đồng nhất ngưỡng 0.5pt; mac lệch nhau (rộng mọi lần, cao > 8pt) |
| Vị trí mở | Gần con trỏ (+20pt, kẹp trong màn hình) hoặc vị trí đã lưu (góc trên-trái) | Gần con trỏ (+8pt, kẹp trong vùng làm việc) hoặc giữa màn hình (input mode); không thấy có nhớ vị trí | Tương đương |
| Con trỏ đổi dạng | `onHover` trên tay nắm (góc: chéo; cạnh: ngang/dọc); monitor theo dõi chuột toàn cục đang chờ xoá | Hệ điều hành tự đổi con trỏ trên dải native | mac phải tự làm, Windows được miễn phí |

**Cái mac nên học theo Windows:**
1. **Bỏ sàn chiều cao đã lưu trong auto-fit** (quan trọng nhất — mục 7.4): mở panel gọn rồi để auto-fit dãn theo nội dung, thay vì mở ở cao đã lưu. Windows đã chứng minh sửa này hết lỗi "mở cao cho text ngắn" (#143/#145).
2. **Xoá chặn auto-fit ngay khi kết quả mới về** (như `ShowResult` của Windows): hiện mac dựa vào generation tăng — tương đương trong hầu hết trường hợp, nhưng xóa tường minh thì dễ đọc và ít bị kẹt hơn.
3. **Ngưỡng lưu đồng nhất** (Windows: 0.5pt cho cả rộng lẫn cao; mac hiện tại: rộng mọi lần, cao > 8pt).
4. **Chặn tối thiểu bằng hệ điều hành** thì không áp dụng được cho mac (cửa sổ không viền), nhưng bài học là: **đừng tin thuộc tính `minSize` của cửa sổ** — mac đã học bài này ở #191/#192 (chặn thẳng vào hằng số).

**Cái Windows nên học theo mac:**
1. **Ô nhập có vạch chia kéo tay** và baseline lưu riêng (`popupInputHeight`) — Windows không có.
2. **Đo chiều cao kết quả rồi dãn panel đủ nội dung** (mac dãn tới 800/màn hình; Windows cap output 260 khi auto-fit nên text dài phải cuộn sớm hơn).

---

## 9. Các lỗ hổng / điểm chưa nhất quán còn tồn tại (mac)

| # | Vấn đề | Mức độ | Bằng chứng trong code |
|---|---|---|---|
| 1 | **Auto-fit sàn = cao đã lưu** (mục 7.4): kéo cao 1 lần rồi dịch từ ngắn vẫn mở cao | **Cao** — lỗi hành vi thấy được, Windows đã từng bị và đã sửa | `frameToFit(baselineHeight: Defaults[.popupDefaultHeight])` + `setupPanel` mở ở cỡ đã lưu |
| 2 | **Slider Settings lỗi thời**: rộng 280...800, cao 200...800 — đầu dưới thấp hơn minimum mới 400/260 | Trung bình — hiển thị sai so với thực tế | `GeneralSettingsView.swift` (slider), `Constants.swift` (minimum 400×260) |
| 3 | **Không có cơ chế thử lại auto-fit**: nếu 1 lần auto-fit return sớm (thiếu số đo, cờ kẹt...), không có lần hẹn nào khác ngoài lần đo layout tiếp theo | Trung bình — 2 lỗi 6.1/6.2 đều là dạng "mất 1 lần đo" | `scheduleResultResize` (coalesce, không retry) |
| 4 | **Ngưỡng lưu không đồng nhất**: rộng lưu mọi thay đổi, cao chỉ lưu khi > 8pt | Thấp — panel có thể rộng dần theo các lần kéo vô tình | `finishPanelResize` |
| 5 | **`frameToFit` không chặn tối thiểu tường minh**: chỉ chặn tối đa + màn hình; tối thiểu dựa vào nội dung ≥ 260 trong thực tế | Thấp — an toàn hiện tại nhưng không có bảo đảm bằng code | `PopupPanelSizing.frameToFit` |
| 6 | **Đo chiều cao result vẫn qua đường vòng** (`resultTextHeight` khởi đầu 48 rồi mới đo thật; phụ thuộc `updateNSView` được gọi lại) | Thấp — đã giảm rủi ro ở #195, cần theo dõi test thực tế | `SelectableResultTextView.reportContentHeight`, `ProviderResultCard` |
| 7 | **Không có snap-back ngang** (cố ý, Manh bảo chưa cần) | Thấp — ghi nhận để sau này quyết | `finishPanelResize(resizeHeight: false)` |
| 8 | **Monitor con trỏ đang chờ xoá**: `PopupResizeCursorMonitor` vẫn còn trong code, việc xoá (chỉ dùng `onHover`) đã làm xong local, chưa đẩy | Việc dở dang, không phải lỗi | `PopupPanelController.startResizeCursorMonitor`, staging `batch-remove-cursor-monitor-push/` |

---

## 10. Khuyến nghị — nên làm gì tiếp (theo thứ tự ưu tiên, KHÔNG triển khai trong báo cáo này)

### P0 — Làm ngay (lỗi hành vi thấy được)
1. **Bỏ sàn chiều cao đã lưu trong auto-fit** (lỗ hổng #1): đổi `baselineHeight` trong `frameToFit`/`popupFittedFrame` từ `popupDefaultHeight` sang chiều cao vừa nội dung (`chromeHeight + contentHeight`), giống Windows. Đồng thời cân nhắc cho panel **mở gọn** (ở cao tối thiểu 260pt) rồi để auto-fit dãn ra, thay vì mở ở cao đã lưu — đúng bài Windows đã làm ở #143/#145. Kiểm tra bằng test: kéo cao 600pt cho text dài → đóng → dịch 1 từ → panel phải về ~260pt, không ở 600pt.

### P1 — Sửa trong đợt tới (dễ làm, hết nhầm lẫn)
2. **Đồng bộ slider Settings với minimum mới**: Default Width thành 400...800, Default Height thành 260...800 (`GeneralSettingsView.swift`).
3. **Thêm chặn tối thiểu tường minh trong `frameToFit`**: `height = max(height, minimumSize.height)` để không dựa vào "nội dung lúc nào cũng ≥ 260".

### P2 — Tăng độ tin cậy của auto-fit
4. **Thử lại 1 lần khi auto-fit return sớm do thiếu số đo**: nếu `resizeForResultContent` return vì `contentHeight`/`viewportHeight`/`containerHeight` còn nil, hẹn chạy lại sau ~100ms (tối đa 1 lần để tránh vòng lặp). Điều này bao luôn cả 2 lỗi 6.1/6.2 nếu chúng tái diễn dưới dạng khác.
5. **Đồng nhất ngưỡng lưu**: rộng cũng chỉ lưu khi đổi > 8pt như cao (hoặc cả 2 về 0.5pt như Windows — chọn 1 ngưỡng rồi dùng chung).

### P3 — Cân nhắc sau (cần Manh quyết)
6. **Snap-back ngang**: chỉ làm khi có định nghĩa rõ "rộng vừa nội dung" (text wrap đổi theo rộng nên khó hơn dọc). Manh đã bảo chưa cần — để sau.
7. **Hoàn tất việc xoá `PopupResizeCursorMonitor`** (đã làm xong local, đang chờ đẩy sau khi #195 được xác nhận).
8. **Theo dõi sau #195**: test lại đúng bộ 3 ngắn/trung bình/dài trên bản #195 để xác nhận sửa đo chiều cao (6.2) đã hết hẳn, không chỉ hết với 1 độ dài.

---

## Phụ lục — File code đã đọc (để tra lại)

**mac** (`~/workspace/projects/moepeek-translator/transpeek/`):
- `Sources/UI/PopupPanel/PopupView.swift` — layout panel, tay kéo, divider, snap-back, input height
- `Sources/UI/PopupPanel/PopupPanelController.swift` — vòng đời panel, auto-fit, nhớ vị trí/kích thước
- `Sources/UI/PopupPanel/PopupPanelSizing.swift` — `frameToFit`, `PopupResultLayout`
- `Sources/UI/PopupPanel/PopupPanel.swift` — cửa sổ, `minimumSize`, chặn kéo top bar
- `Sources/UI/PopupPanel/SourceInputView.swift` — ô nhập, đo chiều cao input
- `Sources/UI/PopupPanel/ProviderResultCard.swift` — thẻ kết quả, `resultTextHeight`
- `Sources/UI/PopupPanel/SelectableResultTextView.swift` — đo chiều cao output (đã sửa #195)
- `Sources/Utilities/Constants.swift` — các giá trị mặc định (450/350/48)
- `Sources/Utilities/PopupPositioning.swift` — vị trí panel gần con trỏ
- `Tests/PopupPanelLayoutTests.swift`, `Tests/PopupPanelSizingTests.swift` — test khoá hành vi

**Windows** (`~/workspace/projects/transpeek-windows/`, bản snapshot local):
- `src/TransPeek/Views/TranslatePanelWindow.xaml` — layout, cap output 260, grip 3 vạch
- `src/TransPeek/Views/TranslatePanelWindow.xaml.cs` — `ResizeToFitContent`, `PersistResizedSize`, `ComputeMinPanelHeight`, `WM_GETMINMAXINFO`, `ShowResult`/`ShowPanelNear`/`ShowInputMode`
- `src/TransPeek.Core/Settings/AppSettings.cs` — `PanelWidth` 440, `PanelHeight` 480 (mặc định)
