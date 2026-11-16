# Báo cáo: Panel nhiều tab (chấm tròn) + App nhắc nghỉ — TransPeek

- **Ngày nghiên cứu:** 16/11/2026
- **Phạm vi:** báo cáo thứ 5 trong bộ nghiên cứu TTS/RSVP ngày 16/11/2026 — chỉ làm phần panel nhiều tab và app nhắc nghỉ, theo đúng yêu cầu của Manh (16/11 ~03:21, tách riêng ~03:29).
- **Ngôn ngữ:** tiếng Việt.
- **Trạng thái:** chỉ nghiên cứu, KHÔNG triển khai code, KHÔNG commit/push.
- **Phương pháp:** đọc trực tiếp trang chính thức của 3 app nhắc nghỉ (wnr, tadama, Pandan) + tìm kiếm web về mẫu chấm tròn (page control), cử chỉ swipe trackpad macOS và sự kiện bánh xe chuột WinUI 3. Không cài đặt hay chạy thử 3 app nhắc nghỉ; các nhận định về hành vi của chúng dựa trên mô tả của chính tác giả trên trang chính thức (ghi rõ nguồn ở cuối mỗi mục). Các nhận định về API (NSStatusItem, Shell_NotifyIcon, PointerWheelChanged…) dựa trên tài liệu chính thức và bài viết kỹ thuật đã kiểm chứng được — những chỗ chỉ là suy luận thiết kế sẽ ghi rõ "đánh giá thiết kế".

---

## 1. Bối cảnh: panel hiện tại của TransPeek và ý tưởng tab chấm tròn

Panel dịch hiện tại của TransPeek là một cửa sổ nổi nhỏ, xuất hiện khi người dùng bôi chọn văn bản: nội dung chính là ô dịch (nguồn → đích), kèm slogan và dòng hint thứ 3 ("Hover icon to translate, Click to listen") nằm dưới slogan. Ý tưởng của Manh là giữ NGUYÊN một panel duy nhất nhưng cho nó mang NHIỀU màn hình/tính năng qua các tab, chuyển tab bằng hàng chấm tròn nhỏ ở giữa đáy panel:

- **Tab dots:** 2–3 chấm nhỏ ở giữa đáy panel, mỗi chấm = 1 tab.
- **Mac:** chuyển tab bằng swipe ngang 2 ngón trên trackpad, hoặc chạm/click vào chấm.
- **Windows:** click vào chấm để chuyển, hoặc hover lên hàng chấm rồi lăn bánh xe chuột giữa (không cần click).
- **Nội dung tab đề xuất (chưa chốt, còn linh hoạt):**
  - Tab 1 = panel dịch hiện tại, có icon dấu "?" để chuyển sang Tab 2;
  - Tab 2 = trợ giúp/gợi ý — hiện dòng hint 3 (đang nằm dưới slogan) và các phím tắt xem nhanh;
  - Một tab timer — nhắc nghỉ mắt (20-20) và/hoặc Pomodoro (25 phút làm / 5 phút nghỉ);
  - Tab 3 = chưa quyết định.

Câu hỏi cần trả lời: mẫu chấm tròn có hợp với một panel nổi nhỏ không? Cử chỉ chuyển tab có an toàn, dễ phát hiện không? Và phần timer nên tích hợp tới đâu — hay cứ dùng app nhắc nghỉ có sẵn?

---

## 2. Mẫu chấm tròn (dot tab) có hợp với panel nổi nhỏ không?

### 2.1. Kết luận ngắn

**Có — nhưng chỉ với tối đa 3 tab, và chỉ khi chấm tròn được coi là "chỉ báo vị trí + nút bấm phụ", không phải là cách chuyển tab duy nhất.** Phải luôn có ít nhất một cách chuyển tab rõ ràng khác (icon "?", phím mũi tên, click trực tiếp) vì chấm tròn nhỏ vốn có khả năng phát hiện (discoverability) thấp. Chấm tròn là mẫu chuẩn cho chuỗi trang ngắn, ngang hàng — đúng bản chất "Tab 1 dịch / Tab 2 trợ giúp / Tab 3 timer" — nhưng nó không tự nói lên được "mỗi chấm là cái gì" nếu không có nhãn/tooltip và lần hướng dẫn đầu tiên.

### 2.2. Mẫu chấm tròn trong thực tế (nguồn web)

Chấm tròn là mẫu "page control" quen thuộc nhất trên iOS/macOS: một hàng chấm nhỏ ở đáy màn hình, chấm đặc = trang hiện tại, chấm mờ/nhỏ hơn = trang khác. Các nguồn thiết kế thống nhất ở mấy điểm sau (ghi rõ là thông tin lấy từ web):

- **Số lượng hợp lý là 3–7 chấm.** Baymard Institute cho rằng 3–7 là "sweet spot" — ít hơn thì không cần chỉ báo, nhiều hơn 7–8 thì chấm mất ý nghĩa vì không ai đếm chấm. Với TransPeek chỉ có 2–3 tab, con số này nằm đúng trong vùng tốt nhất của mẫu.
- **Chấm là chỉ báo trạng thái, không phải điều hướng chính.** Tài liệu thiết kế của Meta (PaginationIndicator) nhấn mạnh: dots "owns nothing but the display… never takes focus", và khuyên "Use tabs or page buttons for navigation. The dots are not interactive and must not be wrapped to appear interactive." Nói cách khác, trong các hệ thống lớn, chấm tròn được thiết kế để *cho biết đang ở đâu*, còn việc *chuyển trang* thuộc về cử chỉ vuốt hoặc nút bấm. Apple HIG cho iOS cũng ghi: người dùng chạm vào mép trái/phải của page control để sang trang kế/prev, "they can't tap a specific dot to go to a specific page" — điều hướng chủ yếu vẫn là vuốt.
- **Đặt ở giữa đáy, giữ vị trí cố định.** HIG: "Center page controls at the bottom of the screen… positioned between the bottom of the content and the bottom of the screen." Đúng với đề xuất của Manh (giữa đáy panel).
- **Chấm phải đủ tương phản trạng thái active/inactive.** Baymard nhấn mạnh: nếu chấm đang chọn và chấm chưa chọn quá giống nhau, người dùng không đọc được mình đang ở đâu. Mẫu hay dùng: chấm active to hơn / dài ra thành hình viên thuốc (pill) / đậm màu hơn.

**Áp vào TransPeek:** 2–3 tab là đúng "điểm ngọt" của mẫu chấm. Nhưng khác với onboarding trên điện thoại (người dùng *biết* mình đang vuốt qua các màn hình), panel dịch là công cụ xuất hiện chớp nhoáng để dịch một đoạn văn — người dùng không mong đợi nó có nhiều trang. Vì vậy chấm tròn ở đây có nguy cơ bị coi là "trang trí" và không bao giờ được chạm tới. Đây là rủi ro discoverability lớn nhất, cần bù bằng các biện pháp ở mục 2.5.

### 2.3. Khả năng phát hiện (discoverability) — vấn đề lớn nhất

| Vấn đề | Mức độ | Cách bù |
|---|---|---|
| Chấm quá nhỏ, ở đáy panel, dễ bị coi là trang trí | Cao | Chấm active kéo dài thành pill (rộng ~16–20px), chấm inactive 6–8px, độ tương phản rõ; thêm tooltip khi hover ("Dịch", "Trợ giúp", "Hẹn giờ") |
| Người dùng không biết panel có nhiều tab | Cao | Lần đầu mở Tab 2 (hoặc lần đầu sau khi cập nhật có tab), hiện gợi ý nhỏ một lần ("Vuốt ← → để xem Trợ giúp"); icon "?" ở Tab 1 là cầu nối phát hiện tốt nhất vì nó là một nút bấm có nghĩa, không phải chỉ báo thụ động |
| Không biết mỗi chấm là tab nào | Trung bình | Tooltip + tên tab hiện thoáng qua khi chuyển tab (vd hiện chữ "Trợ giúp" mờ 0,8 giây ở đầu panel khi vừa chuyển); không dùng nhãn chữ thường trực vì panel nhỏ, nhãn sẽ làm panel chật |
| Người dùng bàn phím/chuột không có trackpad (Mac dùng chuột) | Trung bình | Phím tắt chuyển tab (← → khi panel đang focus, hoặc phím tắt riêng trong Settings); click chấm vẫn hoạt động với mọi thiết bị |

**Lưu ý về icon "?":** đề xuất "Tab 1 có icon dấu ? để chuyển sang Tab 2" là quyết định đúng về mặt phát hiện — "?" là một affordance (dấu hiệu bấm được) rõ ràng hơn chấm tròn. Nên giữ "?" như lối vào chính tới Tab 2, còn chấm tròn là chỉ báo vị trí + lối vào phụ. Tương tự, Tab 2 nên có nút "← Quay lại Dịch" chứ không bắt người dùng phải tự tìm ra cách vuốt ngược.

### 2.4. Vuốt nhầm (accidental swipes) — Mac

Rủi ro vuốt nhầm trên Mac thấp hơn vẻ ngoài, với 3 lý do:

1. **Vuốt ngang 2 ngón trên trackpad vốn đã là cử chỉ hệ thống** ("Swipe between pages" — quay lại/trang tiếp trong trình duyệt; chuyển Space/full-screen app bằng 3–4 ngón). Nhưng trong *một cửa sổ nổi nhỏ của TransPeek*, vuốt ngang 2 ngón hiện không có ý nghĩa nào khác — panel dịch không có nội dung cuộn ngang, không có trang web để back/forward. Vì vậy gán vuốt ngang 2 ngón cho "chuyển tab" không xung đột với hành vi hiện hữu của chính panel. Xung đột duy nhất là với thói quen *toàn hệ thống* của người dùng (họ có thể vuốt theo phản xạ để back trong trình duyệt khi con trỏ đang ở trên panel) — nhưng panel nổi thường biến mất khi click ra ngoài, nên cửa sổ xảy ra nhầm lẫn là hẹp.
2. **Ngưỡng (threshold) và hướng ưu thế giải quyết hầu hết vuốt nhầm.** Chỉ chuyển tab khi: quãng đường vuốt ngang vượt ngưỡng (vd ≥ 40–60px hoặc vận tốc đủ lớn) VÀ thành phần ngang lớn hơn hẳn thành phần dọc (vd |dx| > 2×|dy|). Vuốt chéo, vuốt dọc (cuộn) sẽ không kích hoạt. Đây là kỹ thuật chuẩn; về mặt API, AppKit cung cấp `NSPanGestureRecognizer` và sự kiện swipe của trackpad (`NSEvent` loại gesture / `trackSwipeEvent`), SwiftUI có gesture vuốt — đều là API công khai, không cần framework private. (Chi tiết API: xem mục 2.7.)
3. **Người dùng chuột trên Mac không có vuốt 2 ngón.** Đây là nhóm bị bỏ quên nếu chỉ dựa vào swipe: chuột Magic Mouse có bề mặt cảm ứng và hỗ trợ vuốt 1 ngón, nhưng chuột thường thì không. Với nhóm này, click chấm + phím mũi tên + icon "?" là đường chính. Không được thiết kế kiểu "chỉ swipe mới chuyển được tab".

**Trường hợp vuốt nhầm thực sự đáng lo:** người dùng đang cuộn dọc nội dung dài trong panel (vd Tab 2 liệt kê nhiều phím tắt) bằng 2 ngón, tay hơi lệch sang ngang → panel nhảy tab giữa chừng. Cách chống: khi nội dung tab hiện tại đang cuộn dọc được (có thanh cuộn), yêu cầu ngưỡng ngang cao hơn; hoặc chỉ nhận swipe bắt đầu từ vùng trống/tiêu đề panel chứ không phải từ vùng nội dung cuộn được. Khuyến nghị: đặt ngưỡng ngang cao (≥ 50px) và chỉ kích hoạt khi vuốt bắt đầu ở vùng header/footer của panel, không kích hoạt khi bắt đầu trong vùng text có thể cuộn.

### 2.5. Windows: hover + lăn bánh xe trên hàng chấm — đánh giá công thái học

Đề xuất Windows: hover chuột lên hàng chấm rồi lăn bánh xe giữa để chuyển tab (không cần click). Đánh giá:

**Ưu điểm:**
- Không cần click nên nhanh, đúng tinh thần "panel nhẹ, thao tác ít".
- Vùng kích hoạt hẹp (chỉ hàng chấm ở đáy) nên ít xung đột với cuộn nội dung của panel — bánh xe khi con trỏ ở vùng nội dung vẫn cuộn nội dung như thường.
- Về kỹ thuật, WinUI 3 hỗ trợ đầy đủ: sự kiện `UIElement.PointerWheelChanged` (Microsoft Learn) bắn khi giá trị bánh xe thay đổi, kèm `MouseWheelDelta` (dương = lăn lên/xa người, âm = lăn xuống). Đây là routed event, gắn được lên chính hàng chấm. Không cần focus trước — sự kiện đi theo vị trí con trỏ (hit-test), đúng với yêu cầu "hover rồi lăn".

**Nhược điểm và cạm bẫy — cần xử lý cẩn thận:**
1. **Vùng hover quá nhỏ.** Hàng chấm cao khoảng 20–28px, rộng khoảng 60–80px. Người dùng phải ngắm chính xác vào dải hẹp đó rồi mới lăn — thao tác "ngắm + lăn" thực ra không nhanh hơn "click vào chấm" bao nhiêu, mà còn khó phát hiện hơn (không có dấu hiệu nào cho biết "lăn ở đây sẽ chuyển tab"). Nếu không có gợi ý trực quan (vd hàng chấm sáng lên / nở ra khi hover, tooltip "Lăn chuột để chuyển tab"), tính năng này gần như vô hình.
2. **Lăn nhầm khi chỉ định cuộn.** Người dùng lăn bánh xe để cuộn nội dung panel, con trỏ vô tình đang ở đúng hàng chấm → tab nhảy bất ngờ. Cách chống: yêu cầu tích lũy delta đủ lớn (vd cộng dồn `MouseWheelDelta` tới ngưỡng 120 — một "nấc" bánh xe chuẩn — mới chuyển 1 tab), và debounce (sau mỗi lần chuyển tab, bỏ qua các delta trong ~300ms tiếp theo). Một nấc bánh xe = 1 tab, không chuyển 2 tab vì một lần lăn mạnh.
3. **Trackpad Windows cũng bắn PointerWheelChanged.** Trên laptop Windows, vuốt 2 ngón trên trackpad được hệ thống dịch thành sự kiện bánh xe — nghĩa là vuốt 2 ngón dọc trên hàng chấm cũng chuyển tab. Đây là hành vi nhất quán với chuột, không phải lỗi, nhưng cần nhớ khi test: đừng ngạc nhiên khi trackpad cũng kích hoạt.
4. **Nội dung cuộn được + ScrollViewer "ăn" sự kiện.** Tài liệu Microsoft Learn lưu ý: một số control (vd `ScrollViewer`, `ListView`) có xử lý class-level cho `PointerWheelChanged` và có thể đánh dấu sự kiện là đã xử lý, khiến handler ngoài không nhận được. Hàng chấm nên là một vùng riêng (vd `StackPanel`/`Grid` chứa các chấm), KHÔNG đặt bên trong `ScrollViewer` của nội dung — nếu không, sự kiện lăn có thể bị nuốt hoặc bị hiểu là cuộn nội dung.
5. **Người dùng không có bánh xe (chuột không dây mini, chỉ dùng touchpad 1 ngón…).** Click chấm vẫn là đường chính, không được bỏ.

**Kết luận cho Windows:** hover + lăn là một "đường tắt thú vị" nhưng KHÔNG nên là cách chuyển tab chính. Cách chính là click chấm (và phím mũi tên). Hover+lăn chỉ nên là bổ sung, với ngưỡng + debounce + hiệu ứng hover rõ ràng, và phải có tooltip hướng dẫn. Nếu thử nghiệm cho thấy người dùng hay lăn nhầm, tắt mặc định tính năng lăn-chuyển-tab này và chỉ giữ click.

### 2.6. Chấm tròn khi panel resize — hành vi

Panel TransPeek có thể resize (người dùng kéo to/nhỏ; nội dung dịch dài/ngắn làm panel cao/thấp khác nhau). Hàng chấm cần giữ các tính chất sau:

- **Dải footer có chiều cao cố định.** Dành riêng một dải đáy cao cố định (vd 24–28px, tính cả padding) chỉ cho hàng chấm, không để nội dung các tab "đè" lên hay đẩy chấm đi chỗ khác. Nội dung tab cuộn/thay đổi chiều cao bên trong vùng của nó; dải chấm luôn ở đáy, luôn ở giữa theo chiều ngang. Đây cũng là cách iOS đặt page control: cố định ở đáy, tách khỏi nội dung.
- **Chấm neo giữa, không neo trái/phải.** Khi panel rộng ra, hàng chấm vẫn ở giữa (center), không dạt sang một bên. Khi panel hẹp lại, hàng chấm không bị cắt — với 2–3 chấm thì tổng chiều rộng chỉ ~40–70px, panel dịch dù hẹp nhất cũng rộng hơn nhiều, nên không có nguy cơ tràn. (Nguy cơ tràn chỉ xuất hiện nếu sau này tăng lên 5+ tab — lý do nữa để giữ tối đa 3 tab.)
- **Chuyển tab không đổi kích thước panel một cách giật cục.** Các tab có nội dung cao khác nhau (Tab 1 dịch có thể dài, Tab 2 trợ giúp ngắn). Nếu mỗi tab đặt chiều cao riêng, panel sẽ nhảy chiều cao mỗi lần chuyển tab — khó chịu và làm mất vị trí hàng chấm trên màn hình. Nên: panel giữ chiều cao hiện tại khi chuyển tab (chỉ đổi nội dung bên trong, vùng nội dung cuộn nếu dài hơn), hoặc animate chiều cao mượt mà. Khuyến nghị: giữ nguyên kích thước panel khi chuyển tab; nội dung tab dài thì cuộn bên trong.
- **Chấm active dạng pill giúp định vị khi resize.** Khi chuyển tab, chấm active dài ra thành pill và trượt sang vị trí mới (animation ~150–200ms) — chuyển động này giúp mắt người dùng theo kịp "mình vừa sang tab nào", đặc biệt khi panel vừa resize xong và bố cục còn lạ.

### 2.7. Khả thi kỹ thuật (không code, chỉ đánh giá)

| Nền tảng | API/cơ chế | Đánh giá |
|---|---|---|
| macOS (Swift/SwiftUI + AppKit) | Swipe ngang 2 ngón: `NSPanGestureRecognizer` (AppKit, API công khai) hoặc xử lý `NSEvent` gesture / `trackSwipeEvent` trên NSView; SwiftUI có `.gesture(DragGesture…)` và `.onSwipe` kiểu tương tự. Click chấm: `Button` SwiftUI bình thường. | Khả thi, API công khai, không cần quyền gì thêm. Lưu ý: cần kiểm tra `isTouchSwipeNavigationEnabled` nếu dùng swipe điều hướng toàn trang; và đừng dùng framework private MultitouchSupport (một số app cử chỉ dùng nó để đọc vị trí ngón tay thô — không cần thiết ở đây, lại mất cửa App Store). |
| Windows (WinUI 3 + C#) | Click chấm: `Button`/`Tapped`. Hover+lăn: `UIElement.PointerWheelChanged` + `PointerPointProperties.MouseWheelDelta`, cộng dồn tới ngưỡng 120 rồi chuyển 1 tab, debounce ~300ms. | Khả thi, API công khai. Lưu ý ScrollViewer có thể nuốt sự kiện — đặt hàng chấm ngoài ScrollViewer; dùng `AddHandler(..., handledEventsToo: true)` chỉ khi thật sự cần (và phải cân nhắc vì nó có thể phá hành vi cuộn chuẩn). |
| Cả hai | Giữ kích thước panel khi chuyển tab; dải footer cố định; tooltip cho từng chấm. | Việc của layout, không có rủi ro API. |

### 2.8. Đề xuất bố cục tab và cử chỉ theo từng nền tảng

**Bố cục chung (cả 2 nền tảng):**
- Tối đa **3 tab**, không thêm tab thứ 4 nếu chưa có tính năng thật sự cần chỗ. **Không tạo chấm cho Tab 3 "chưa quyết định"** — chấm chỉ xuất hiện khi tab đã có nội dung. Panel 2 chấm hôm nay tốt hơn panel 3 chấm với 1 chấm dẫn tới trang trống.
- Dải footer đáy cao **24–28px**, chỉ chứa hàng chấm, căn giữa. Chấm inactive 6–8px, mờ 40–50%; chấm active dạng pill dài ~16–20px, đậm màu. Hit-target (vùng bấm được) mỗi chấm tối thiểu **24×24px** (vùng trong suốt quanh chấm) — chấm nhìn nhỏ nhưng bấm dễ.
- Tooltip khi hover mỗi chấm: tên tab ("Dịch", "Trợ giúp", "Hẹn giờ"). Khi vừa chuyển tab, hiện tên tab mờ ở đầu panel ~0,8 giây rồi biến mất.
- Luôn có nút bấm chữ/icon để chuyển tab ngoài chấm tròn: Tab 1 có icon "?" → Tab 2; Tab 2 có "← Dịch" để về; tab timer có nút đóng/dừng rõ ràng.
- Phím tắt khi panel đang focus: **← / →** chuyển tab; áp dụng cả 2 nền tảng (Windows và Mac dùng chuột đều cần đường này).

**Mac (trackpad là chính, chuột là phụ):**
- Chính: **swipe ngang 2 ngón** (ngưỡng ≥ ~50px, ưu thế ngang |dx| > 2×|dy|), **click chấm**, icon "?", phím ← →.
- Chỉ nhận swipe bắt đầu ở vùng header/footer (không nhận khi bắt đầu trong vùng text cuộn được) để tránh nhảy tab khi đang cuộn dọc.
- Lần đầu tiên sau khi có tab mới: hiện gợi ý nhỏ một lần ("Vuốt ← → để chuyển tab"), có nút "Đã hiểu" / tự biến mất sau vài giây, không hiện lại.
- Người dùng chuột: click chấm + phím mũi tên là đủ; không cần mô phỏng swipe bằng chuột.

**Windows (chuột là chính, trackpad laptop là phụ):**
- Chính: **click chấm**, icon "?", phím ← →.
- Phụ (mặc định BẬT nhưng có công tắc tắt trong Settings): **hover lên hàng chấm + lăn 1 nấc bánh xe = chuyển 1 tab**, cộng dồn delta tới ngưỡng 120, debounce 300ms sau mỗi lần chuyển. Khi hover hàng chấm, hàng chấm sáng/nở nhẹ + tooltip "Lăn chuột để chuyển tab" để người dùng phát hiện ra tính năng.
- Trackpad laptop Windows: vuốt 2 ngón dọc trên hàng chấm cũng chuyển tab (do hệ thống dịch thành sự kiện bánh xe) — chấp nhận, ghi chú trong trợ giúp.
- Nếu test thực tế thấy lăn nhầm nhiều: tắt mặc định, chỉ giữ click.

**Nội dung từng tab (đánh giá, không chốt cứng):**
- **Tab 1 — Dịch (hiện tại):** giữ nguyên. Icon "?" ở góc chuyển sang Tab 2. Dòng hint 3 hiện tại ("Hover icon to translate, Click to listen") nên *chuyển sang Tab 2* khi Tab 2 ra mắt — vì khi đã có tab trợ giúp, dòng hint thường trực ở Tab 1 trở thành nhiễu thị giác; Tab 1 chỉ giữ slogan gọn.
- **Tab 2 — Trợ giúp / gợi ý:** hiện dòng hint 3 + danh sách phím tắt xem nhanh (dịch, nghe TTS, mở Settings, chuyển tab) + link tới tài liệu đầy đủ. Nội dung tĩnh, ngắn, không cuộn quá 1 màn hình panel. Đây là tab "đọc một lần rồi thôi" nên không cần cập nhật thường xuyên.
- **Tab timer (hẹn giờ / nhắc nghỉ):** xem phần 3–4 — khuyến nghị chỉ là tab *nhẹ* (hiển thị đếm ngược + nút bật/tắt nhắc nhẹ), không phải bản sao của app nhắc nghỉ. Nếu chưa làm được bản nhẹ này, **đừng tạo tab timer trống** — để 2 tab trước đã.
- **Tab 3 (chưa quyết định):** để trống cho tương lai (vd tab điều khiển TTS khi đang phát, hoặc tab RSVP — xem các báo cáo khác trong bộ này). Không gán trước khi tính năng chưa rõ.

---

## 3. Ba app nhắc nghỉ: wnr, tadama, Pandan

Manh đã chọn 3 app này làm mẫu và chốt hướng: chúng đã rất tốt, dùng ngay được, nên **TransPeek KHÔNG làm lại chúng — chạy riêng**. Phần này kiểm chứng lại từng app xem chúng làm gì và *cưỡng chế* nghỉ tới mức nào, để xác nhận hướng "không tích hợp" là đúng.

### 3.1. Bảng so sánh nhanh

| | wnr | tadama | Pandan |
|---|---|---|---|
| Tác giả / nguồn | Roderick Qiu — https://github.com/RoderickQiu/wnr, https://getwnr.com | Pavel Alexeev — https://00b.in/tadama, https://tadamapp.com | Sindre Sorhus — https://sindresorhus.com/pandan (Mac App Store) |
| Nền tảng | Windows, macOS, Linux (Electron) | macOS (menu bar) | macOS 26+ (menu bar) |
| Giá / giấy phép | Miễn phí, mã nguồn mở (MPL-2.0); ~1.100 sao GitHub | (trang chính không ghi giá; app menu-bar macOS) | Miễn phí, không thu thập dữ liệu cá nhân; 6 MB; v1.18.1 (09/2026) |
| Triết lý | **Nghiêm khắc (strict)** — bắt nghỉ thật | **Khó chịu vừa đủ** — cửa sổ nghỉ lớn, khó lờ đi | **Nhẹ nhàng / nhận thức thời gian** — chỉ nhắc, không ép |
| Kiểu nhắc | Pomodoro / làm-nghỉ tùy chỉnh; chế độ toàn màn hình | Pomodoro 25 làm / 5 nghỉ, 4 chu kỳ thì nghỉ dài | Đếm thời gian dùng máy; nhắc nhẹ theo khoảng tùy chọn |
| Mức cưỡng chế | Cao nhất trong 3 app | Trung bình–cao | Thấp nhất (không khóa máy) |

### 3.2. wnr — Work aNd Rest (nghiêm khắc nhất)

- **Là gì:** app hẹn giờ làm việc/nghỉ ngơi đa nền tảng (Windows, macOS, Linux), viết bằng Electron, mã nguồn mở MPL-2.0, repo tạo từ 2019, ~1.100 sao GitHub, 85 release. Slogan của tác giả: "Stricter. Prettier. More features." (nghiêm khắc hơn, đẹp hơn, nhiều tính năng hơn).
- **Cách nhắc / cưỡng chế:** điểm khác biệt lớn nhất là **Fullscreen focus mode** — khi tới giờ nghỉ, app chiếm toàn màn hình và **chặn cả virtual desktop lẫn mọi màn hình** cho tới khi hết phiên nghỉ; người dùng không thể lách qua màn hình khác hay desktop khác để làm tiếp. Có thể **tắt hẳn nút cancel/skip/stop**, hoặc bật **parent control** (chế độ phụ huynh — cần mật khẩu/quyền để thay đổi) khi muốn ranh giới cứng hơn nữa. Ngoài ra app nhắc mỗi 10 phút khi người dùng "drift away" (lạc khỏi nhịp làm/nghỉ).
- **Tính năng khác:** thống kê (statistics), mini mode, predefined tasks (nhiệm vụ đặt trước), nhiều tùy chỉnh, đồng bộ WebDAV, đa ngôn ngữ.
- **Điểm yếu cần biết:** bản macOS/Windows không được ký bằng certificate trả phí (tác giả nói rõ trên trang chủ: không đủ tiền mua certificate), nên lần đầu mở trên Mac cần `xattr -cr` hoặc chuột phải → Open; trên Windows SmartScreen sẽ hỏi "run anyway". Đây là ma sát cài đặt, không phải lỗi tính năng.
- **Vị trí trong bộ 3:** đây là lựa chọn khi Manh muốn *bị ép nghỉ thật* — người hay bấm "Skip" theo phản xạ sẽ không lách được. Đổi lại, nó là app "nặng" nhất về mặt can thiệp: chiếm toàn màn hình, chặn desktop.

### 3.3. tadama — Pomodoro kiểu "khó chịu vừa đủ"

- **Là gì:** app Pomodoro cho macOS, sống ở menu bar ("lives in the menu bar, so it's always just a click away"). Trang chính thức ngắn gọn, nhấn mạnh: ít nút, ít tính năng, ít tùy chọn — "Only what you need for productivity and nothing more."
- **Cách nhắc / cưỡng chế:** chu kỳ Pomodoro cổ điển — **làm 25 phút, nghỉ 5 phút, lặp lại, mỗi 4 chu kỳ nghỉ dài**. Khi gần hết giờ làm có nhắc nhẹ "hurry up" (tranh thủ gói việc lại; bấm vào thông báo để xin thêm giờ). Khi tới giờ nghỉ, **một cửa sổ lớn bật lên che hầu hết màn hình**, nhắc đi nghỉ; tác giả nói thẳng: "It's *designed* to be annoying enough that you'll take the break" (cố tình làm đủ khó chịu để người dùng chịu nghỉ). Nếu di chuyển cửa sổ nghỉ đi, **sau 30 giây nó tự nhảy về chỗ cũ**. Có thể thu nhỏ cửa sổ nghỉ nếu muốn nghỉ ngay tại máy. Có hotkey gọi app từ bất cứ đâu, điều khiển hoàn toàn bằng bàn phím, thời lượng làm/nghỉ tùy chỉnh.
- **Điểm tinh tế:** **tự tạm dừng khi người dùng rời máy**; nếu rời máy hơn 5 phút, khi quay lại sẽ bắt đầu một phiên làm việc (workflow session) mới thay vì tính tiếp phiên cũ — nghĩa là thời gian rời máy thật sự được tính là nghỉ, không bị tính gian. Trang 00b.in còn ghi có **hỗ trợ cử chỉ trackpad**.
- **Vị trí trong bộ 3:** lựa chọn cân bằng — ép hơn Pandan (cửa sổ lớn, tự nhảy về), nhưng mềm hơn wnr (không chặn toàn bộ desktop/màn hình, vẫn thu nhỏ được, vẫn xin thêm giờ được). Phù hợp với người muốn Pomodoro đúng nghĩa mà không muốn bị khóa máy.

### 3.4. Pandan — nhận thức thời gian, nhẹ nhất

- **Là gì:** app menu-bar macOS của Sindre Sorhus (tác giả nhiều app macOS nổi tiếng), phát hành 05/2021, miễn phí, **không thu thập dữ liệu cá nhân**, 6 MB, yêu cầu macOS 26+ (bản 1.18.1, 09/2026; có bản cũ cho macOS 11–15 trên GitHub/Dropbox). Triết lý ghi rõ trên trang chủ: đây là công cụ **time awareness (nhận thức thời gian), không phải time tracker hay Pomodoro timer truyền thống** — "gently reminding you how long you've been on the computer so you can be aware of that."
- **Cách nhắc / cưỡng chế:** **không khóa máy, không che màn hình.** App hiện ở menu bar **thời gian của phiên dùng máy hiện tại** (hoặc tổng thời gian hôm nay), kèm hoặc không kèm icon. Tự nhận biết khi người dùng rời máy: mặc định **kết thúc phiên sau 10 phút không dùng bàn phím/chuột**, quay lại thì tính phiên mới. Có thể đặt **nhắc nhẹ theo khoảng tùy chọn** (vd mỗi 25 phút) dưới các hình thức: popover, notification, HUD giữa màn hình, hoặc **chỉ đổi icon trên menu bar** — kèm âm thanh, haptic, hoặc chạy một Shortcut tùy chọn. Cửa sổ History xem lại các phiên trong 120 ngày qua, xuất CSV/JSON.
- **Dùng làm nhắc nghỉ mắt:** chính tác giả hướng dẫn — đặt nhắc nghỉ, rồi tạo một Shortcut dùng action "Wait" đúng số giây muốn nghỉ mắt, sau đó thêm "Play Sound" hoặc "Show Alert" để báo hết giờ nghỉ; chọn Shortcut đó trong Settings của Pandan. Nghĩa là Pandan *có thể* làm nhắc nghỉ mắt 20-20-20, nhưng phải tự ghép qua Shortcuts — không có sẵn nút "20-20-20" bấm là chạy.
- **Tích hợp Shortcuts:** app đi kèm các action cho app Shortcuts có sẵn của macOS (New Session, Set Enabled State, chạy Shortcut làm thông báo…), nên có thể tự động hóa khá sâu nếu muốn.
- **Vị trí trong bộ 3:** lựa chọn nhẹ nhất, ít can thiệp nhất — dành cho người chỉ cần *biết mình đã ngồi máy bao lâu* và tự quyết định khi nào nghỉ. Đánh đổi: người hay lờ nhắc nhở sẽ lờ luôn Pandan, vì nó không ép được.

### 3.5. Phổ cưỡng chế — và vì sao "không làm lại" là đúng

Ba app xếp thành một phổ rõ ràng, từ nhẹ tới nặng:

**Pandan (chỉ nhắc, không ép) → tadama (cửa sổ lớn, khó lờ) → wnr (toàn màn hình, chặn desktop, cấm skip).**

Mỗi nấc trên phổ này đều đã có người làm tốt, làm lâu năm, và được người dùng thật đánh giá cao (wnr 1.100+ sao GitHub; Pandan được Lifehacker và nhiều review App Store khen là "không xâm lấn"; tadama tồn tại ổn định như một app Pomodoro gọn). Để TransPeek làm lại một app nhắc nghỉ ở bất kỳ nấc nào cũng đồng nghĩa với:

- **Làm lại một sản phẩm hoàn chỉnh** (đếm giờ, phát hiện idle, thống kê, tùy chỉnh, xử lý fullscreen/meeting…) trong khi đó không phải việc chính của TransPeek (dịch + nghe TTS);
- **Cạnh tranh với chính các app mà người dùng đã có thể cài ngay** — và người dùng TransPeek hoàn toàn có thể cài song song: TransPeek để dịch/nghe, Pandan/wnr/tadama để nhắc nghỉ. Hai việc không xung đột nhau;
- **Gánh thêm trách nhiệm khó chịu nhất của thể loại này:** quyết định khi nào được phép làm phiền người dùng, khi nào được khóa màn hình họ. Một app dịch mà tự nhiên khóa màn hình người dùng để bắt nghỉ mắt sẽ bị coi là "lấn sân" và dễ bị gỡ hơn là được khen.

**Xác nhận hướng của Manh: KHÔNG tích hợp toàn bộ app nhắc nghỉ. Dùng riêng 3 app trên.** Gợi ý chọn nhanh cho Manh: muốn nhẹ và chỉ cần biết giờ → **Pandan** (Mac); muốn Pomodoro đúng nghĩa, hơi ép → **tadama** (Mac); muốn ép nghỉ thật, đa nền tảng (cả Windows) → **wnr**. Trên Windows, trong 3 app này chỉ wnr có bản Windows — Pandan và tadama là Mac-only, nên nếu Manh cần nhắc nghỉ trên máy Windows công ty thì wnr là lựa chọn duy nhất trong bộ 3 (hoặc dùng app khác ngoài bộ này).

---

## 4. Câu hỏi hiển thị timer: 2 icon menubar/tray hay 1 icon thông minh?

### 4.1. Câu hỏi của Manh

Nếu timer *có* được tích hợp (dù chỉ bản nhẹ), đếm ngược hiển thị ở đâu?

- **(A) Một app có HAI icon menubar (Mac) / tray icon (Windows) chạy song song** — một icon TransPeek, một icon timer?
- **(B) MỘT icon được thiết kế thông minh để chuyển trạng thái hoặc gộp trạng thái** — vd icon đổi hình để hiện đếm ngược, hoặc hiện cả trạng thái TransPeek lẫn trạng thái timer trong cùng một icon?

Ràng buộc thêm của Manh (16/11 ~03:26): **danh sách menubar chính đã khá dài**, tích hợp timer mà thêm icon nữa sẽ làm nó rối và dài hơn nhiều — điều này nghiêng về hoặc dùng app riêng, hoặc tích hợp mà **không thêm icon menubar nào**.

Khung quyết định của Manh: **HOẶC dùng app riêng và không tích hợp, HOẶC tích hợp mà KHÔNG có thêm icon menubar/tray — timer chỉ xuất hiện trong tab 2/3 của panel.**

### 4.2. Phương án A — hai icon song song: kỹ thuật làm được, trải nghiệm không nên làm

**macOS — kỹ thuật: LÀM ĐƯỢC.** Một app có thể tạo nhiều `NSStatusItem` qua `NSStatusBar.system.statusItem(withLength:)` — API không giới hạn số lượng item trên mỗi app. Bằng chứng thực tế (nguồn web): app Multimeter đặt **một status item cho mỗi đồng hồ đo** người dùng thêm, và tác giả của nó viết bài chi tiết về cách quản lý nhiều item (tạo ngược thứ tự để được đúng thứ tự hiển thị, vì macOS đặt item mới tạo về phía trái các item đã có; dùng `autosaveName` riêng cho từng item để vị trí được nhớ giữa các lần mở). Nghĩa là "một app, hai icon menubar" là hành vi đã có app thật dùng, không phải thủ thuật chưa kiểm chứng.

**Windows — kỹ thuật: LÀM ĐƯỢC.** Mỗi tray icon là một lần gọi `Shell_NotifyIcon` với `NIM_ADD`, phân biệt bằng cặp (window handle + uID) hoặc GUID (Windows 7+). Một process có thể gọi nhiều lần với uID khác nhau để có nhiều icon; thư viện `H.NotifyIcon` cho WinUI/WPF cũng ghi rõ hỗ trợ tạo nhiều TrayIcon ("Added ability to create multiple TrayIcon"). Không có giới hạn API nào cấm một app có 2 icon tray.

**Nhưng UX của phương án A là tệ, nhất là với ràng buộc của Manh:**

| Vấn đề UX | Mac | Windows |
|---|---|---|
| Thêm 1 icon vào danh sách đã dài | Menubar dài thêm 1 ô (~20–28px + khoảng cách); trên MacBook có notch, icon mới có thể bị notch che hoặc đẩy icon khác vào vùng ẩn | Tray icon thứ 2 rất dễ rơi vào vùng overflow (mũi tên ^) — người dùng không thấy đếm ngược nếu không mở khay |
| Thứ tự không ổn định | macOS đặt item mới về phía trái item cũ; vị trí còn bị ghi đè bởi vị trí đã lưu theo `autosaveName`; 2 icon của cùng 1 app có thể bị tách ra, không đứng cạnh nhau sau khi khởi động lại | Thứ tự tray icon do Explorer quyết định, người dùng kéo thả được nhưng app không kiểm soát được |
| Người dùng không biết icon nào là gì | 2 icon cùng 1 app, cùng phong cách — bấm nhầm icon timer lại mở menu TransPeek và ngược lại. Tooltip giúp được phần nào nhưng tooltip chỉ hiện khi hover, không giải quyết được việc *nhìn lướt* | Tương tự; tooltip tray icon giới hạn 127 ký tự, vẫn phải hover mới thấy |
| Tắt/mở độc lập gây rối trạng thái | Tắt TransPeek thì icon timer còn không? Thoát app thì cả 2 cùng mất — nhưng người dùng có thể tưởng "tắt timer" là tắt icon timer, trong khi app vẫn chạy ngầm | Tương tự |
| Chi phí bảo trì | Phải quản lý vòng đời 2 item (tạo/xóa/đổi menu/đổi icon), xử lý khi hệ thống ẩn item, xử lý multi-monitor (macOS clone status item sang từng màn hình) | Phải quản lý 2 `NOTIFYICONDATA`, xử lý khi Explorer crash và tạo lại icon (taskbar recreated), xử lý khi icon bị ẩn vào overflow |

**Kết luận phương án A:** kỹ thuật cho phép, nhưng là phương án tệ nhất về trải nghiệm trong bối cảnh menubar đã dài. **Không chọn A.**

### 4.3. Phương án B — một icon chuyển/gộp trạng thái: kỹ thuật làm được, nhưng có giới hạn đọc được

**macOS — kỹ thuật: LÀM ĐƯỢC.** Một `NSStatusItem` có `button` (NSStatusBarButton) với `image`/`title` có thể đổi bất cứ lúc nào. Hai cách thực hiện:
- **Chuyển trạng thái (switch):** icon đổi hình theo trạng thái — vd bình thường là icon TransPeek, khi timer đang chạy thì icon đổi thành đồng hồ + số phút còn lại, khi tới giờ nghỉ thì đổi thành icon "mắt"/màu khác. Pandan thực tế đã làm đúng kiểu này trong MỘT item: menu bar hiện thời gian phiên + icon, và có thể chỉ đổi icon làm hình thức nhắc.
- **Gộp trạng thái (combine):** vẽ một icon tổng hợp — vd icon TransPeek nhỏ + một vòng tròn tiến độ (progress ring) quanh nó, hoặc icon + chuỗi text "25:00" bên cạnh. `NSStatusItem` hỗ trợ `length = NSStatusItem.variableLength` để item rộng theo nội dung text.

**Windows — kỹ thuật: LÀM ĐƯỢC.** Gọi `Shell_NotifyIcon` với `NIM_MODIFY` để thay `hIcon` và tooltip của icon tray hiện có. Icon mới được **vẽ động** (vd dùng GDI/Direct2D vẽ bitmap 16×16/32×32: nền icon TransPeek + vòng tiến độ hoặc số phút). Tooltip đổi theo trạng thái ("TransPeek — nghỉ mắt sau 12:34"). Đây là kỹ thuật chuẩn, nhiều app Pomodoro/timer đã dùng.

**Giới hạn thực tế của phương án B — và đây mới là điểm quyết định:**

1. **Không gian đọc quá nhỏ.** Icon menubar/tray chỉ ~18–22px. Chữ đếm ngược "25:00" vẽ trong 22px gần như không đọc được; vòng tiến độ nhỏ thì chỉ cho biết "đang chạy", không cho biết "còn bao lâu" nếu không hover xem tooltip. Gộp 2 trạng thái vào 1 icon 22px đồng nghĩa với cả 2 trạng thái đều hiển thị kém.
2. **Mơ hồ trạng thái.** Người dùng nhìn lướt không phân biệt được icon đang ở trạng thái nào: đây là "TransPeek đang bật", "timer đang chạy", hay "tới giờ nghỉ"? Phải học một bảng mã màu/hình mới — gánh nặng ghi nhớ không đáng có cho một tính năng phụ.
3. **Vẫn chiếm thêm chiều rộng menubar** nếu gộp bằng cách thêm text "25:00" bên cạnh icon — item variable-length sẽ dài ra, đúng thứ Manh muốn tránh (menubar đã dài).
4. **Trạng thái timer không phải lúc nào cũng cần thấy.** Đếm ngược chỉ có ý nghĩa khi người dùng đang *chờ* nó; lúc còn lại nó là nhiễu thị giác thường trực trên menubar. Một icon "thông minh" vẫn là một icon thường trực.

**Kết luận phương án B:** kỹ thuật làm được và tốt hơn A, nhưng vẫn không giải quyết được gốc rễ: timer không xứng đáng có mặt thường trực trên menubar/tray. B chỉ nên dùng ở dạng tối giản nhất — xem mục 4.4.

### 4.4. Khuyến nghị: theo khung của Manh — dùng app riêng là chính; nếu tích hợp thì KHÔNG thêm icon

**Khuyến nghị chính: DÙNG APP RIÊNG (không tích hợp timer).**

- Mac: **Pandan** nếu Manh muốn nhắc nhẹ + biết thời gian dùng máy; **tadama** nếu muốn Pomodoro hơi ép; **wnr** nếu muốn ép nghỉ thật và cần cả Windows.
- Windows (máy công ty): **wnr** là lựa chọn duy nhất trong bộ 3 có bản Windows.
- Lý do: xem mục 3.5 — 3 app đã tốt, làm lại là lãng phí; timer không phải việc chính của TransPeek; và menubar đã dài.

**Khuyến nghị phụ — NẾU sau này vẫn muốn có timer trong TransPeek thì chỉ làm bản NHẸ, và tuân thủ nghiêm khung "không thêm icon":**

1. **Timer chỉ sống trong tab 2/3 của panel** — đếm ngược chỉ hiện khi người dùng mở panel và chuyển sang tab timer. Đóng panel là hết thấy timer. Không có icon menubar/tray thứ 2 (không chọn A). Không đổi icon TransPeek thành đồng hồ (không chọn B ở dạng thường trực).
2. **Nhắc nhẹ ONLY — không khóa máy.** Khi tới giờ nghỉ, TransPeek chỉ gửi **một notification hệ thống** (macOS `UNUserNotificationCenter` / Windows toast notification): tiêu đề + 1 dòng ("Nhìn xa 20 giây nhé" / "Nghỉ 5 phút thôi"), có nút "Nhắc lại sau 5 phút" và "Bỏ qua", tự biến mất, **không** chiếm màn hình, **không** chặn desktop, **không** bắt người dùng phải nghỉ. Đây là ranh giới đỏ: TransPeek không bao giờ được làm phiên bản "fullscreen focus mode" của wnr hay cửa sổ "tự nhảy về" của tadama — đó là việc của app chuyên trách.
3. **Hai chế độ nhắc nhẹ, tắt mặc định:** (a) 20-20-20 — mỗi 20 phút nhắc nhìn xa 20 giây; (b) Pomodoro — làm 25 phút / nghỉ 5 phút, 4 chu kỳ nghỉ dài. **Mặc định TẮT cả hai** (opt-in trong Settings) để người dùng hiện tại không bỗng nhiên bị nhắc. Chỉ đếm thời gian khi máy đang hoạt động (idle thì dừng đếm — học Pandan: kết thúc phiên sau ~10 phút không dùng phím/chuột; học tadama: rời máy > 5 phút thì tính là đã nghỉ).
4. **Ngoại lệ duy nhất cho phương án B (nếu thật sự cần):** khi timer đang chạy, **tooltip** của icon TransPeek hiện có được thêm dòng "Nghỉ mắt sau 12:34" — chỉ tooltip, không đổi hình icon, không thêm text lên menubar. Đây là cách duy nhất "hiện đếm ngược" mà không tốn thêm 1px menubar nào. Và chỉ bật khi người dùng đã bật timer trong Settings.
5. **Không thêm mục nào vào menu menubar chính** ngoài một dòng "Nhắc nghỉ: Bật/Tắt" (nếu tích hợp) — tốt nhất là để hẳn trong Settings, menu chính giữ nguyên để khỏi dài thêm, đúng lo ngại của Manh.

**Tóm tắt quyết định timer:**

| Lựa chọn | Kết luận |
|---|---|
| A — 2 icon song song | Kỹ thuật được, UX không nên. **Không làm.** |
| B — 1 icon chuyển/gộp trạng thái | Kỹ thuật được, tốt hơn A, nhưng chữ đếm ngược không đọc được ở 22px và trạng thái dễ gây nhầm. **Không làm ở dạng thường trực.** Chỉ chấp nhận biến thể tối giản: tooltip thêm dòng đếm ngược. |
| Khung của Manh | **Chọn theo khung này: dùng app riêng là chính; nếu tích hợp thì timer chỉ ở trong tab panel, không thêm icon.** |
| Mức tích hợp tối đa (nếu có) | Nhắc nhẹ bằng notification hệ thống, tự tắt, có snooze; không khóa máy, không che màn hình; mặc định tắt; chỉ đếm khi máy hoạt động. |

---

## 5. Khuyến nghị tổng hợp

### 5.1. Tab chấm tròn — chốt

1. **Làm — với tối đa 3 tab, chấm tròn là chỉ báo + nút phụ, không phải cách chuyển duy nhất.** Luôn giữ icon "?" (Tab 1 → Tab 2), nút quay lại, click chấm, phím ← →.
2. **Không tạo chấm cho tab chưa có nội dung.** Hôm nay làm 2 tab (Dịch + Trợ giúp) trước; tab timer chỉ thêm khi bản nhắc nhẹ đã sẵn sàng; Tab 3 để trống cho tương lai (vd tab điều khiển TTS/RSVP).
3. **Bố cục:** dải footer đáy cố định 24–28px, chấm căn giữa; chấm active dạng pill 16–20px; hit-target mỗi chấm ≥ 24×24px; tooltip tên tab; tên tab hiện thoáng qua 0,8 giây khi chuyển.
4. **Mac:** swipe ngang 2 ngón là chính (ngưỡng ≥ ~50px, ưu thế ngang, chỉ nhận khi bắt đầu ở header/footer), cộng click chấm + phím mũi tên. Gợi ý một lần khi có tab mới.
5. **Windows:** click chấm là chính; hover + lăn bánh xe là phụ (ngưỡng 1 nấc = 1 tab, debounce 300ms, hiệu ứng hover + tooltip, có công tắc tắt trong Settings). Nếu test thấy lăn nhầm nhiều thì tắt mặc định.
6. **Chuyển tab giữ nguyên kích thước panel** (nội dung dài thì cuộn bên trong), tránh panel nhảy chiều cao. Chuyển dòng hint 3 sang Tab 2 khi Tab 2 ra mắt để Tab 1 gọn lại.

### 5.2. App nhắc nghỉ — chốt

7. **KHÔNG tích hợp toàn bộ app nhắc nghỉ. Dùng riêng:** Mac → Pandan (nhẹ) / tadama (Pomodoro hơi ép) / wnr (ép thật); Windows → wnr (lựa chọn duy nhất trong bộ 3 có bản Windows).
8. **Nếu tích hợp thì chỉ bản NHẸ:** nhắc bằng notification hệ thống, tự tắt, có snooze, không khóa máy, không che màn hình; 2 chế độ 20-20-20 và Pomodoro 25/5; mặc định TẮT; chỉ đếm khi máy hoạt động.

### 5.3. Hiển thị timer — chốt

9. **Không chọn A (2 icon song song)** — kỹ thuật được nhưng làm menubar/tray dài và rối, đúng điều Manh lo ngại.
10. **Không chọn B ở dạng thường trực** — icon 22px không đọc được đếm ngược, trạng thái dễ gây nhầm. Chỉ chấp nhận biến thể tối giản: tooltip của icon TransPeek hiện có thêm dòng đếm ngược.
11. **Theo khung của Manh: hoặc dùng app riêng, hoặc tích hợp mà KHÔNG thêm icon — timer chỉ ở trong tab 2/3 của panel.** Khuyến nghị mặc định là vế đầu (dùng app riêng).

### 5.4. Thứ tự làm (đề xuất)

1. **Trước:** 2 tab (Dịch + Trợ giúp) với chấm tròn + swipe/click — giá trị rõ ràng, rủi ro thấp.
2. **Sau (nếu Manh vẫn muốn):** tab timer bản nhẹ (notification only, không icon) — làm sau khi tab 1–2 đã ổn định và được Manh test.
3. **Không làm:** app nhắc nghỉ đầy đủ trong TransPeek; icon menubar/tray thứ 2; icon gộp trạng thái thường trực.

---

## 6. Nguồn tham khảo

- wnr — repo: https://github.com/RoderickQiu/wnr (MPL-2.0, ~1.101 sao, 537 commit, 85 release; truy cập 16/11/2026) và trang chủ: https://getwnr.com (mô tả Fullscreen focus mode, chặn virtual desktop/màn hình, tắt cancel/skip/stop, parent control).
- tadama — trang tác giả: https://00b.in/tadama và trang chính thức: https://tadamapp.com (Pomodoro 25/5, cửa sổ nghỉ lớn tự nhảy về sau 30 giây, tự tạm dừng khi rời máy, hotkey; truy cập 16/11/2026).
- Pandan — trang chính thức: https://sindresorhus.com/pandan (time awareness, nhắc nhẹ popover/notification/HUD/đổi icon, session 10 phút idle, history 120 ngày, Shortcuts; v1.18.1, macOS 26+; truy cập 16/11/2026).
- Page control / dot indicator — Baymard Institute, "Page Control UI" (https://baymard.com/blog/page-control-ui); Apple iOS HIG, "Page Controls"; Meta, "PaginationIndicator" (https://wearables.developer.meta.com/docs/develop/webapps/design/components/mrbd/pagination-indicator/).
- Nhiều NSStatusItem — Deallocated Objects, "Ordering Menu Bar Status Items When AppKit Won't Let You" (https://deallocatedobjects.com/posts/ordering-menu-bar-status-items) — bằng chứng một app có thể có nhiều status item, kèm hành vi thứ tự vị trí.
- Nhiều tray icon Windows — Microsoft Learn, `Shell_NotifyIcon` / `UIElement.PointerWheelChanged` (Windows App SDK); NuGet `H.NotifyIcon.WinUI` (ghi rõ hỗ trợ nhiều TrayIcon).
- Cử chỉ macOS — Apple Developer Docs, "Adopting gesture recognizers in your app" (`NSPanGestureRecognizer`, `NSClickGestureRecognizer`…); Eon Codes, "Two Finger Swipe" (xử lý `NSEvent` gesture cho swipe 2 ngón).

## 7. Giới hạn của báo cáo

- Ba app nhắc nghỉ được đánh giá qua trang chính thức và repo công khai, **không cài đặt/chạy thử** trong đợt này — các mô tả về hành vi (vd wnr chặn desktop, tadama tự nhảy cửa sổ về) là lời của chính tác giả, chưa được kiểm chứng bằng chạy thật.
- Phần chấm tròn/cử chỉ là **đánh giá thiết kế dựa trên quy ước nền tảng và tài liệu API**, chưa có prototype hay test người dùng thật — các ngưỡng cụ thể (50px, 120 delta, debounce 300ms, 24–28px footer) là điểm khởi đầu hợp lý để test, không phải số đo đã kiểm chứng.
- wnr là app Electron đa nền tảng; tadama và Pandan là Mac-only — nhận định "Windows chỉ có wnr trong bộ 3" dựa trên thông tin nền tảng công khai của 3 app.
- Không sao chép code hay tài nguyên của 3 app; mọi mô tả ở đây là mô tả mẫu hành vi để tự triển khai lại (nếu cần), không phải hướng dẫn copy.
