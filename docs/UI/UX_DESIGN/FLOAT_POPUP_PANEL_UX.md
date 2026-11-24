# Float / Popup Panel — UX Spec (CHỐT)

> **Trạng thái: CHỐT — 20/11/2026, theo nhánh `feature/focus-timer-decor`
> (CI #207 xanh, head `d364f879`; đợt 18:09 `53daae66` đang chờ CI —
> xem §11.6).**
> Các con số trong doc này là **spec**. Đổi bất kỳ số đã chốt nào cũng cần Manh duyệt trước.
> Spec đã bao gồm: hint 1 dòng (#161) + loa/Copy overlay trong ô output
> (card 3 hàng + overlay, xem §6 — "card 4 hàng" cũ đã bỏ) + nút "?" +
> card shortcuts nhúng (18/11, "?" trước Pin) + layout đa tab hub 3 tab
> (§10–§11, 17/11) + thẻ Focus & Rest (§11.3, 20/11) + lite reader
> panel right (§11.1) + banner nhắc nghỉ + icon translate-first
> (`FLOAT_ICON.md` §3, 20/11) + viền nút Capture/Fast Read (§11.2).
> Doc này chỉ cho **Windows**. Bên mac có yêu cầu tương tự nhưng **chưa viết panel UX vì chưa chốt size** — khi nào mac chốt thì viết riêng, đối chiếu với doc này.

## 1. Kích thước chốt (locked sizes)

| Hạng mục | Số chốt | Nguồn trong code |
|---|---|---|
| Mở mặc định | **420 × 450** | `AppSettings.PanelWidth = 420`, `PanelHeight = 450` (migration SettingsVersion 2) |
| Tối thiểu | **400 × 450** | `MinPanelWidth = 400`, `MinPanelHeight = 450`; OS kẹp qua `WM_GETMINMAXINFO` |
| Tối đa | **rộng 600 / cao 800** | `MaxPanelWidth = 600`, `MaxPanelSize = 800` |
| Cap ô output | **260** | `TranslationTextMaxHeight = 260` — text dài cuộn trong ô, không dãn panel |
| Min output card | **218** | `MinOutputCardHeight = 218` (cả const C# và `MinHeight="218"` trong XAML) |
| Cap ô nguồn | **120** | `SourceTextBox MaxHeight="120"` |
| Min ô dịch | **60** | `TranslationTextBox MinHeight="60"` |

**Giải slider Default Width / Height trong Settings — Windows KHÔNG CÓ (khác mac):**
mục "Popup Panel" trong Settings → General chỉ có 2 slider cỡ chữ (12–24), không có
slider rộng/cao. Mặc định 420×450 cố định trong code (`AppSettings.PanelWidth = 420`,
`PanelHeight = 450`, migration SettingsVersion 2). Giải kích thước cho phép của panel là
**rộng 400–600, cao 450–800** (= min/max ở bảng trên, OS kẹp qua `WM_GETMINMAXINFO`) —
nếu sau này thêm slider thì giải chính là khoảng này.

Baseline tham chiếu: **#153** (mở mặc định 420×450, min 400×450, max rộng 600 / cao 800, cap output 260, auto-fit + snap-back ngang/dọc, thu provider gọn card). Nếu một bản sau phá layout thì quay về số của #153 rồi sửa lại từ đó.

## 2. Khung layout (RootGrid — 10 hàng, Padding 12)

`RootGrid` có 10 hàng: Row 0–4 + Row 6–9 là `Auto`, Row 5 (`*`, output
card) là hàng duy nhất dãn theo chiều cao; các hàng `Auto` chỉ cao bằng
nội dung. (Trước 20/11 doc ghi 8 hàng — sai: panel đã có 10 hàng từ khi
thêm hàng chấm Row 9 ở đợt đa tab; bảng dưới ghi đủ 10 hàng.)

| Hàng | Kiểu | Nội dung | Số liệu chốt |
|---|---|---|---|
| Row 0 | Auto | **TitleBar** — tên app + slogan + hint, căn giữa | Margin `0,0,0,16` (đáy = hintSize, mặc định 13). Tên app Bold, màu `#0EA5E9`. Slogan xám. Hint: "Hover icon to translate, Click to listen". **Cả hàng là vùng kéo di chuyển** (native caption region) — panel không có title bar riêng |
| Row 1 | Auto | **SourceTextBox** — ô nhập, sửa được | `MaxHeight 120`, Margin `0,0,0,4`. Enter = dịch lại, Shift+Enter = xuống dòng. Loa + Copy **nằm trong ô** ở góc dưới-phải (overlay, Margin `0,0,6,8`) |
| Row 2 | Auto | **Hint** — 11pt, **PHẢI ở 1 dòng ở mọi chiều rộng** (xem §6) | "Select a word, then click Dictionary to look it up" (rút ngắn 17/11 #161 — câu cũ dài ~103 ký tự, xuống 2 dòng khi kéo hẹp, gây che loa) |
| Row 3 | Auto | **Hàng ngôn ngữ** — Source ⇄ Target + Capture OCR | AutoSuggestBox Source/Target **rộng 112**, nút Swap ⇄ Padding `4,2`, Spacing `4`. Nút "Capture OCR" ở bên phải, **viền xanh blue `#0078D4` 40% (`#660078D4`), 1px, chỉ viền** (Manh 2026-11-20 17:05) |
| Row 4 | Auto | **Card shortcuts nhúng ("?")** (`EmbeddedShortcutsCard`, Collapsed mặc định) | Card cùng style provider (CornerRadius 6, Border 1), header "Keyboard shortcuts" + ×, ScrollViewer MaxHeight 240 với 10 dòng (cùng `BuildShortcutRows` như panel left). Mở thì panel cao thêm ~280, đóng thì về cũ — xem §3 |
| Row 5 | `*` | **Output card** (`ProviderCardBorder`) | Margin `0,0,0,8`, CornerRadius 6, BorderThickness 1, `MinHeight 218`. Bên trong **3 hàng**: header (tên provider 13pt SemiBold + trạng thái) / divider / TextBox (`*`, FontSize 14, MinHeight 60, MaxHeight 260) với **loa/Copy overlay trong ô TextBox** (góc dưới-phải, Margin `0,0,6,8` như ô nhập) — xem §6 |
| Row 6 | Auto | **Actions** — Fast Read trái, "?" + Settings phải | Fast Read (icon E736 + text "Fast Read", **viền nghệ `#C7800D` 40% (`#66C7800D`), 1px, chỉ viền** — Manh 2026-11-20 17:05) ở đầu trái; "?" (chữ "?", SemiBold 12pt, tooltip "Keyboard shortcuts") + Settings (E713) nhóm ở phải, Spacing 4, Margin `0,0,0,8`. **Pin KHÔNG còn ở đây từ 2026-11-20** — nó ở mép trái hàng chấm (Row 9). "?" bật/tắt card shortcuts nhúng — xem §3 |
| Row 7 | Auto | **DictionaryBarControl** — link tra từ điển | |
| Row 8 | Auto | **Footer** — link GitHub căn giữa + build text căn phải | |
| Row 9 | Auto | **Hàng chấm** (`TabDotsRow`) — chuyển tab | Pin (E718, Margin `10,0,0,0`) ở mép trái, dùng chung cả 3 tab; 3 chấm căn giữa (#0EA5E9 đã chọn / #C8C8C8 chưa); chevron ‹ › (E76B/E76C) 2 bên; lăn chuột trên hàng chấm + topbar + bottom bar để chuyển tab (throttle 0.4s). **KHÔNG có phím tắt ←/→ chuyển tab** (Manh 2026-11-20 11:30) — xem §11.4 |

**Loa/Copy ở trong ô output — GIỮ NGUYÊN (Manh chốt 17/11, cả 2 repo):** mỗi provider có nút loa/Copy riêng ngay dưới text của nó. Đưa ra ngoài thành hàng cố định thì phải thêm logic định tuyến nút về đúng provider (nút thuộc provider nào, provider đang load thì sao) — phức tạp hơn mà không đáng. Nhược điểm đã biết: text dài phải kéo xuống đáy ô mới thấy nút Copy — chấp nhận.

### Cỡ chữ theo Settings (chạy lúc mở panel và khi lưu Settings)

- **"App name & Slogan Font Size"** (`PopupTopBarFontSize`, mặc định **17**, slider 12–24): tên app = N, slogan = N−1 (sàn 8), hint = N−4 (sàn 8). Mặc định: 17 / 16 / 13. Đáy TitleBar = cỡ hint.
- **"Source & Translation Text Size"** (`PopupFontSize`, mặc định **14**, slider 12–24): chữ trong cả 2 ô Source + Translation. Kéo slider là panel giãn live theo (`RequestResizeToFit`).
- Slogan giữ nguyên "Translate anywhere, Peek instantly!" — chỉ hoa T và P (Manh cố ý), không đổi thành Title Case.

## 3. Hành vi (behaviour)

- **Auto-fit mỗi lần panel hiện:** đo nội dung rồi vừa khung; text dài cuộn trong ô output (cap 260), panel không tự dãn.
- **Snap-back NGANG (ngưỡng +8):** kéo rộng hơn mặc định 420 quá 8pt mà không giảm được
  chiều cao nội dung quá 8pt → thả ra co về 420; text dài (rộng thêm giúp bớt dòng, chiều
  cao giảm hơn 8pt) thì giữ nguyên rộng đã kéo. Nguồn: `SettleResizedSize`
  (`clampedW > defaultW + 8` và `hAtDefault <= hAtDragged + 8`).
- **Snap-back DỌC (ngưỡng +8):** kéo cao hơn chiều cao cần quá 8pt → thả ra co về chiều
  cao nội dung (`clampedH > neededH + 8` trong `SettleResizedSize`). Ngưỡng +8 cả 2 chiều
  — giống mac.
- **Kéo tay chỉ to ra thêm trong phiên đó.** Settings chỉ đặt giá trị mặc định; **mỗi lần mở panel về đúng mặc định (420×450)**. Cỡ kéo tay KHÔNG lưu vào Settings.
- **Thu/mở provider card:** thu thì card về MinHeight 0, panel thụt theo (auto-fit lại ngay cả sau khi đã kéo tay).
- **Kéo di chuyển:** bằng vùng caption là TitleBar (Row 0). **Kéo resize:** viền native (BottomBorder 7, RightBorder 8) + grip trang trí 12×12 ở góc dưới-phải (chỉ trang trí, không phải tay nắm thật).
- **Ẩn panel:** click chuột ra chỗ khác (ClickOutsideMonitor) hoặc Esc. **KHÔNG có nút đóng tay, và cũng KHÔNG CẦN** — workflow của Manh là tra xong click ra chỗ khác, rồi tra cái mới.
- **Mở panel:** mặc định ở kích thước Settings (420×450), không mở compact, không nhớ cỡ kéo cũ.
- **Nút "?" — card shortcuts nhúng (Manh 2026-11-18, như mac, "?" trước Pin):** bấm "?" bật/tắt card shortcuts nhúng ngay trên đầu card provider Google (provider vẫn ở dưới, như mac — card là section thường trong panel, KHÔNG phải provider card, KHÔNG phải popover; Flyout cũ đã bỏ). Nội dung: tiêu đề "Keyboard shortcuts" + 10 dòng theo đúng thứ tự Settings → Keyboard Shortcuts (Open translate panel đầu tiên). Mỗi dòng: tên hành động + phím tắt hiện tại (lấy từ Settings lúc mở, "Control" rút gọn thành "Ctrl") + nút ▶ (glyph Play E768) chạy trực tiếp — gọi đúng code path như bấm phím tắt/tray; chỉ nút ▶ mới chạy, bấm vào dòng thì không. Bấm **toàn thanh top** của card (hoặc nút × ở góc phải, hoặc "?" lần nữa) là ẩn card; lúc card mở, dấu "?" đổi màu accent. Card có cuộn riêng (MaxHeight 240) nên panel chỉ cao thêm ~280 lúc mở. Bấm ▶ thì **ẩn panel trước rồi chạy** (Manh 2026-11-18, như mac). Loa/Copy KHÔNG đụng — giữ trong ô output.

## 4. Text dài / text ngắn

- **Text ngắn (1 word – 5 line):** card ở min 218, panel ở chiều cao mặc định/tối thiểu. Nút loa/Copy phải nằm gọn trong card, cách đáy card 8px.
- **Text dài (từ ~6 line trở lên):** output cuộn trong ô (cap 260); panel không tự dãn thêm. Chỉ kéo tay mới dãn panel, lúc đó cap 260 được nâng theo để ô dãn cùng.
- **Nguồn dài:** SourceTextBox cap 120, cuộn trong ô.
- **Thu provider:** card chỉ còn header, panel thụt theo; mở lại thì auto-fit về đúng nội dung.

## 5. Chiều cao tối thiểu động (`ComputeMinPanelHeight`)

```
minH = max(450, fixedH + cardMin + cardMargin + 24)
```

- `fixedH` = tổng ActualHeight các hàng `Auto` (bỏ hàng `*`, đo sau `UpdateLayout`).
- `cardMin` = 218 khi provider mở, 0 khi thu.
- `cardMargin` = margin trên + dưới của card (8) khi mở, 0 khi thu.
- Trần trên = work area của màn hình. Min height được OS kẹp qua `WM_GETMINMAXINFO`
  (`MinPanelWidth` 400 × min height động này), nên kéo nhỏ hơn là không được.
- Auto-fit (`ResizeToFitContent`): đo `DesiredSize` của root, kẹp giữa min height động
  và max 800, rộng kẹp 400–600.

## 6. Bug che loa/Copy — TRẢ KHUNG + SỬA CÔNG THỨC (Manh chốt 23/11)

> **Trạng thái hiện tại (24/11/2026 — Manh test #250 OK, loa HẾT CHE):** gốc là **công
> thức** — minH tính bằng số đo trong (client) nhưng đặt làm chiều cao ngoài, thiếu đúng
> khung (log `[PanelSize]` bản 250, 24/11: outer=420x570, client=404x554, frame=16x16,
> minH=570, frameH=16, card thấy được đúng 218). **Pin VẪN MỜ** — giả thuyết "gỡ khung
> làm mờ pin" BỊ BÁC (khung đã trả lại mà pin vẫn mờ); pin là việc riêng, điều tra sau.
> Nhánh `fix/panel-strip-frame` đang gộp vào main (PR #6). Toàn bộ §6 dưới đây giữ nguyên
> làm lịch sử điều tra. Loa/Copy GIỮ NGUYÊN trong ô output (Manh chốt 17/11).

**Bug che loa/Copy (từ #149):** nút loa/Copy ở góc dưới-phải ô output bị cắt ở mép
dưới card khi tra 1–5 line; từ 6 line trở lên không bị. **Nguyên nhân thật đã tìm ra
(Manh, 17/11 05:52) và fix #161 đã đẩy — chỉ còn chờ Manh test xác nhận hết che.**

- Đã đo từ video build 158 (1 word "lea", panel 424×550): đáy card ở y≈417,
  nút bị cắt đúng ở y=417 — chỉ hiện ~20px trên ~28px của nút, thiếu ~8px.
  Nút nằm sát đáy card thay vì cách 8px như Margin bottom 8.
- **Giả thuyết "thiếu cố định ~8px" ĐÃ BỊ BÁC BỎ** bởi dữ kiện 17/11 04:31:
  giờ 3-line cũng bị, chỉ từ 6-line trở lên mới không bị. Nếu chỉ thiếu 8px cố định
  thì 3-line (trước đây không bị) không thể tự nhiên bị, và 6-line không thể đặc biệt hơn.
- **Nguyên nhân (kết luận 17/11, #160):** loa/Copy từ #157 nằm **overlay trong cùng ô `*`
  của TextBox** — hàng `*` lấy theo phần thừa của panel, nên khi panel/card ra thiếu
  ~8px thì nút (neo ở đáy ô `*`) bị cắt theo mép card. Nâng `MinOutputCardHeight`
  không sửa được vì chiều cao card bị kẹp bởi hàng `*` của panel, không phải bởi MinHeight.
  (Nút loa ở ô nhập không bị vì ô nhập nằm ở hàng Auto — không bao giờ bị bóp.)
- **Fix #160 (cấu trúc, không đổi số):** loa/Copy chuyển sang **hàng Auto riêng**
  ở cuối card (hàng thứ 4 trong card: header / divider / TextBox `*` / loa-Copy Auto).
  Grid cấp phát hàng Auto đủ chiều cao TRƯỚC khi chia phần thừa cho hàng `*`, nên
  nút không bao giờ bị bóp hay cắt theo mép card — hàng TextBox `*` mới là chỗ hấp thụ
  phần thiếu. Card MinHeight vẫn 218; TextBox `*` ở mức tối thiểu còn ≈146
  (218 − 2 viền − 29 header − 1 divider − 40 hàng nút), vẫn đủ MinHeight 60.
  **Chưa xác nhận hết che cho tới khi Manh test bản #160** (1 word, 3 line, 6 line).
- **NGUYÊN NHÂN THẬT — Manh tìm ra 17/11 05:52, xác nhận bằng code (fix #161):**
  hint dưới ô input (Row 2, `TextWrapping="Wrap"`, ~103 ký tự) dãn 1 dòng khi panel
  rộng, xuống 2 dòng khi kéo hẹp → hàng hint cao thêm ~15px. `ComputeMinPanelHeight()`
  đọc `row.ActualHeight` (giá trị layout LẦN TRƯỚC — comment trong code còn ghi sai là
  "during a drag the input height does not change, so the stale value is correct":
  đúng với ô input, SAI với hint vì hint đổi chiều cao theo chiều rộng). Sau kéo tay
  thì auto-fit bị skip (không được chống lại thao tác kéo của user) nên panel không cao
  thêm → 15px thừa trừ vào hàng `*` OutputCardRow → card bị kẹp dưới MinHeight, cắt
  từ đáy → loa/Copy ở đáy card bị che. Đây là lý do #160 không sửa được: #160 sửa
  BÊN TRONG card, còn chỗ bị bóp là Ở PANEL (hàng `*` của panel), nằm trên card.
  Mẫu 1–5 line bị / ≥6 line không bị cũng khớp: text dài thì panel cao hơn mức tối
  thiểu, 15px thừa được hàng TextBox `*` bên trong card hấp thụ; text ngắn thì panel
  đang ở mức tối thiểu nên cắt thẳng vào loa.
- **Fix #161 (rút ngắn hint, không đổi số):** bỏ "Enter: translate again" và
  "Esc: close" (Esc là chuẩn, panel đã tự ẩn khi click ra ngoài; Enter dịch lại ít
  dùng, panel đã tự dịch khi bôi chọn), chỉ giữ tính năng khó đoán nhất là tra từ điển.
  Hint mới: "Select a word, then click Dictionary to look it up" (51 ký tự, vừa 1 dòng
  ở chiều rộng tối thiểu 400). **Quy tắc: hint PHẢI vừa 1 dòng ở 400px** — ghi chú
  cảnh báo đã có trong XAML ngay trên dòng hint; lengthen lại là che loa trở lại.
  **Chưa xác nhận hết che cho tới khi Manh test bản #161** (kéo hẹp + 1 word / 3 line / 6 line).
- **Fix #162 (đưa loa/Copy VÀO TRONG ô output — Manh 17/11 06:14):** #160 đã đưa loa/Copy
  ra hàng Auto riêng dưới ô TextBox để tránh bị che, nhưng đó chỉ là tránh triệu chứng —
  nguyên nhân thật (hint xuống 2 dòng) đã sửa ở #161 nên overlay không còn bị kẹp nữa.
  #162 đưa loa/Copy về lại **overlay trong ô TextBox** (cùng ô `*`, sau TextBox trong
  XAML để vẽ lên trên, Margin `0,0,6,8` như ô nhập — giống #157). Card về lại 3 hàng;
  TextBox `*` ở mức tối thiểu còn ≈186 (218 − 2 viền − 29 header − 1 divider), rộng
  hơn lúc có hàng loa riêng (≈146). **Sau #162 thì saved pre-release bản #162.**
  **Chưa xác nhận cho tới khi Manh test bản #162** (loa/Copy trong ô output, hết che ở
  400/420/600 + 1 word / 3 line / 6 line).
- **#161 VẪN BỊ CHE (Manh test 17/11 06:17) — giả thuyết hint BỊ BÁC BỎ:** hint đã 1 dòng
  mà vẫn che, nên hint không phải nguyên nhân. Đã kiểm tra code: không hàng nào khác wrap
  theo chiều rộng (TitleBar xếp dọc, DictionaryBar xếp ngang, footer, hàng ngôn ngữ đều
  không wrap); DictionaryBar set link đồng bộ (chỉ favicon tải async, không đổi chiều cao);
  RootGrid Padding đúng 12 nên +24 trong công thức là đúng.
- **Thí nghiệm #163 (Manh 17/11 06:23–06:24: "tăng height lên xem hết không", "min height
  mở lần đầu"):** kéo đáy xuống qua điểm che là hết che; kéo đáy lên thì OS min-track
  (WM_GETMINMAXINFO = ComputeMinPanelHeight) chặn đúng TẠI điểm che — tức chính min
  tính ra đã thiếu ~8px so với nhu cầu thật. #163 tăng MinOutputCardHeight 218 → 238
  (+20, cả const C# và MinHeight trong XAML) để min tính ra tăng đúng 20 và panel mở cao
  thêm 20. Nếu #163 hết che → chiều cao đúng là nguyên nhân (thiếu ~8–20px trong chuỗi
  min); bước sau là tìm đúng số px thiếu trong công thức, KHÔNG giữ +20. Nếu #163 vẫn
  che → chiều cao bị loại trừ, nguyên nhân ở chỗ khác (vị trí/cắt xén, không phải cao).
  Số chốt §1 (min card 218, min panel 450) CHƯA đổi — chỉ đổi sau khi thí nghiệm xác nhận.
- **#163 VẪN BỊ CHE (Manh test 17/11 06:44) — CHIỀU CAO BỊ LOẠI TRỪ:** min card +20 không
  sửa được, nên shortfall KHÔNG nằm ở cardMin. Đã check chiều cao output theo lệnh Manh:
  chuỗi chiều cao tự nhất quán — minH = fixedH + cardMin + cardMargin + 24 nên khi panel
  đạt minH thì card LUÔN = cardMin + cardMargin (= 246 ở #163), TextBox ~214, nút cách
  đáy card 8px; DictionaryBar là StackPanel ngang 1 hàng (không wrap) nên fixedH ổn định;
  TextBox Min 60 / Max 260 không phải chỗ kẹt. Suy ra panel không đạt minH trên máy Manh,
  hoặc nguyên nhân không phải chiều cao (vị trí/cắt xén/z-order). Cần ảnh chụp #163 lúc
  bị che (+ xác nhận footer hiện Build #163) để phân biệt — KHÔNG đẩy thêm bản mò khi
  chưa có bằng chứng. Case study AGENTS.md §3 (viết theo giả thuyết hint) vẫn chờ sửa
  lại khi tìm ra nguyên nhân thật.
- **NGUYÊN NHÂN THẬT ĐÃ TÌM RA (Manh 17/11 06:50, test #163): kéo rộng ra thì hết che,
  kéo hẹp vào hint 2 dòng + output nhiều dòng hơn thì bị.** Cơ chế (đã xác nhận bằng code):
  (1) kéo NGANG (chỉ đổi rộng) cũng bị tính là kéo tay → `OnAppWindowChanged` đặt
  `_manualResizeGeneration = _epoch`, auto-fit bị khóa hết lần hiện đó, panel không tự
  cao thêm; (2) kéo hẹp làm hint xuống 2 dòng (+~15) và text output xuống nhiều dòng
  (text dài +100–200); (3) `SettleResizedSize` (300ms sau khi thả kéo) chỉ biết CO panel
  khi thừa (snap-back), KHÔNG biết DÀI panel khi thiếu — nhánh text dài hơn hộp chỉ tăng
  `TranslationTextBox.MaxHeight` mà không tăng chiều cao panel, nên ô text không có chỗ
  để cao thêm; (4) card tràn khỏi hàng `*`, hàng Pin/Settings vẽ đè lên đáy card → che
  loa/Copy; (5) kéo rộng ra text bớt dòng, card vừa → hết che. Giải thích vì sao hint
  (+15) và #163 (+20) không ăn: shortfall do text wrap có thể tới +100–200. **Fix đề
  xuất (chờ Manh duyệt): trong `SettleResizedSize`, khi `neededH` > chiều cao hiện tại
  thì dài panel ra tới `neededH` (đã kẹp tối đa 800), giữ nguyên snap-back co lại khi
  thừa.** **FIX #164 ĐÃ LÀM (Manh duyệt 17/11 06:59): `SettleResizedSize` thêm nhánh
  `else if (neededH > clampedH)` → `clampedH = neededH` (dài panel tới chiều cao cần,
  đã kẹp tối đa 800) + nâng `MaxHeight` ô text như trước; nhánh co snap-back giữ nguyên.
  Đồng thời trả min card 238 → 218 (const + XAML) — bỏ số thí nghiệm #163. Đang chờ
  Manh test #164: kéo hẹp vào (hint 2 dòng, output nhiều dòng) xem còn che không + kéo
  rộng ra vẫn snap-back bình thường.** Số chốt §1: min card 218 (đã trả về), min panel
  450 — đúng số chốt.

Lịch sử mò số (để tránh lặp lại): min output card 150 → 160 (#151) → 170 (#152) →
180 (#154) → 210 (#155, đo từ ảnh: card 3 line ~206px, card 1 word ~193px, chênh 13px)
→ 218 (#159). Không lần nào hết che.

> **Cập nhật 22/11/2026 — fix #164 KHÔNG hết che:** Manh xác nhận trên bản v0.1.4.238
> vẫn che. Repro: mở lần đầu đã che; kéo nhỏ hết cỡ dừng ĐÚNG điểm che; dịch từ mới
> không kéo vẫn che; text rất dài không che, 3–6 dòng vẫn che. Nghi phạm mới: khung cửa
> sổ thừa (`WS_DLGFRAME` sót lại sau `SetBorderAndTitleBar`, chỉ icon mới được strip
> frame) ăn mất chiều cao vùng hiển thị — phần thiếu là hằng số, không đổi khi tăng
> cardMin (giải thích #163 +20 vô ích). **Bản chẩn đoán:** log kích thước panel
> (outer/client/min/baseline) vào `windows.log` mỗi lần fit + submenu tray Diagnose
> (List windows / Open windows.log) để đo đúng số px thiếu, rồi sửa theo số đo.
> Chi tiết: `docs/TASK_QUEUE.md` + `docs/DECISIONS.md`.

> **Cập nhật 23/11/2026 — nguyên nhân thật ĐÃ XÁC NHẬN bằng số đo, đang sửa:**
> Manh test bản 240 (máy công ty Dell Pro 24, 1920x1080, scale 100%), log
> `[PanelSize]` ở đầu Google Doc test: outer=420x554, client=404x538, **frame=16x16**,
> minH=554 (fixedH=304 + cardMin=218 + cardMargin=8 + 24), baseline=554, height=554.
> Cơ chế: chiều cao tính cho vùng hiển thị nhưng đặt theo kích thước ngoài → vùng hiển
> thị thực chỉ 538, thiếu 16px → ô output nhận 210 thay vì tối thiểu 218 → loa/copy bị
> che ~8px. Khớp ghi chú của Manh: "1 chữ bị che loa/copy ô output", "3 line không bị"
> (chỉ khi card ở mức tối thiểu mới bị kẹp; text dài hơn thì 16px thiếu được hấp thụ bởi
> text cuộn trong ô). Chiều rộng cũng lệch tương tự (đo ở 420 nhưng hiển thị ở 404).
> **Fix (hướng 1, Manh duyệt 23/11):** tách `TransparentWindow.RemoveWindowFrame`
> thành `WindowHelpers.StripWindowFrame` dùng chung (chỉ gỡ khung, panel vẫn là card mờ
> đục) + panel gọi ở Loaded sau `MakeBorderlessTopmost` + `MakeToolWindow`. Log chẩn đoán
> GIỮ LẠI trên bản nhánh để xác nhận số mới; gỡ khi gộp main. Rủi ro: gỡ khung
> cũng gỡ bóng đổ DWM — panel có thể trông phẳng hơn, Manh test sẽ thấy.
> Chi tiết: `docs/DECISIONS.md` (mục Strip frame panel) + `docs/TASK_QUEUE.md`.

> **Cập nhật 23/11/2026 (chiều) — gỡ khung chỉ được 2px, đang thử tăng min card:**
> Manh test bản nhánh #241 (12:34, cùng máy): frame 16x16 → **14x14** (client 406x540),
> loa vẫn che. `StripWindowFrame` chỉ gỡ được viền 1px mỗi phía; 14px còn lại không nằm
> ở style cửa sổ — giả thuyết gỡ khung triệt để BỊ BÁC. Thử tiếp (Manh chỉ): tăng min
> output card 218 → 232 (= 218 + 14 frame đo được; card thấy được = cardMin − frame).
> Log `[PanelSize]` giữ lại để xác nhận số mới. Chi tiết: `docs/DECISIONS.md` (mục Strip
> frame panel) + `docs/TASK_QUEUE.md`.

> **Cập nhật 23/11/2026 (tối) — tăng min card KHÔNG hết, TRẢ KHUNG + SỬA CÔNG THỨC:**
> Manh test bản nhánh #248 (14:19, cùng máy): frame vẫn **14x14** (outer=420x568,
> client=406x554, minH=568, cardMin=232), loa vẫn che; pin mờ hơn + mất bo tròn 4 góc
> (ảnh so sánh bản 211 vs 248). **Gốc là công thức:** minH = fixedH(304) + cardMin(232) +
> cardMargin(8) + 24 = 568 tính cho vùng hiển thị, nhưng `MoveAndResize` +
> `WM_GETMINMAXINFO` đặt theo kích thước ngoài → card thực chỉ 218 thay vì 232, loa/copy
> ở đáy card bị cắt đúng ~14px. Tăng cardMin chỉ dời số thiếu, không sửa được gốc.
> **Fix (hướng 2, Manh chốt 23/11):** (1) gỡ `StripWindowFrame` ở panel — trả bo tròn +
> bóng đổ + pin sắc như bản 211 (pin mờ là tác dụng phụ của gỡ khung, code pin không
> đổi); (2) card về lại **218** (const + XAML — 232 chỉ là cách vá tạm); (3) cộng khung
> đo live (`FrameHeightDips()`: outer − client qua `AppWindow.Size` + `GetClientRect`,
> chia scale) vào `ComputeMinPanelHeight` — panel mở đúng 568 ngoài, card đủ 218 trong.
> `StripWindowFrame` KHÔNG bị xoá (icon nổi trong suốt vẫn dùng). Log `[PanelSize]` giữ
> lại (thêm `frameH`) để xác nhận số mới. Chi tiết: `docs/DECISIONS.md` (mục Trả khung +
> sửa công thức minH) + `docs/TASK_QUEUE.md`.

> **Cập nhật 24/11/2026 — Manh test #250 OK, loa HẾT CHE (xác minh bằng số thật):**
> Log `[PanelSize]` 24/11 09:41 (cùng máy): outer=420x570, client=404x554,
> **frame=16x16**, minH=570, minParts=(fixedH=304 cardMin=218 cardMargin=8 frameH=16
> minH=570) — công thức đúng: 304+218+8+24+16=570, card thấy được = 554−304−8−24 =
> **218 đúng thiết kế**. Loa/copy không còn che; bo tròn 4 góc về lại; kéo dọc thu nhỏ
> hết cỡ dừng đúng điểm không che. **Pin VẪN MỜ — giả thuyết "gỡ khung làm mờ pin" BỊ
> BÁC** (khung đã trả lại, frame 16, mà pin vẫn mờ); pin là việc riêng, điều tra sau,
> không chặn gộp PR #6. Chi tiết: `docs/DECISIONS.md` (mục Trả khung + sửa công thức
> minH) + `docs/TASK_QUEUE.md`.

## 7. Quy tắc sửa (để không phá layout)

1. **Trước khi sửa UI: lên khung layout trước, Manh duyệt khung, rồi mới sửa dần theo khung.**
   Không đẩy bản mò từng lỗi vặt bắt Manh test từng bản (bài học chuỗi #150→#158).
2. Không đổi số đã chốt (§1) nếu Manh chưa duyệt.
3. Mọi thay đổi layout phải cập nhật doc này **trong cùng đợt** — doc và code đi cùng nhau.
4. Native trước, tự chế sau: tính năng nào control native không có thì **báo Manh 1 câu trước**
   rồi mới làm custom (bài học thanh kéo resize Settings).
5. Fallback: nếu bản mới phá layout, quay về số baseline #153 (§1) rồi sửa lại từ đó.

## 8. Tham chiếu code

- `src/TransPeek/Views/TranslatePanelWindow.xaml` — layout (RootGrid 8 hàng, card 3 hàng trong, loa/Copy overlay trong ô output)
- `src/TransPeek/Views/TranslatePanelWindow.xaml.cs` — hằng số (~dòng 50–121),
  `ComputeMinPanelHeight` (~940), `ResizeToFitContent` (~705), `SettleResizedSize` (~1070),
  `RefreshTopBarFontSize` / `RefreshPopupFontSize` (~355–385)
- `src/TransPeek.Core/Settings/AppSettings.cs` — `PanelWidth = 420`, `PanelHeight = 450`,
  migration `SettingsVersion` (v1 reset 440×450, v2 rộng 440→420)
- `src/TransPeek/Views/TranslatePanelWindow.xaml.cs` — `SetProviderCardExpanded`
  (thu/mở card, auto-fit lại)

## 9. Kiểm chứng (checklist test panel — chạy mỗi khi sửa layout)

- **Hết che loa:** tra 1 word, 3 line, 6 line — ở cả 3 chiều rộng (hẹp 400, mặc định 420,
  rộng 600). Loa/Copy phải hiện đủ, cách đáy card 8px, không bị cắt.
- **Hint 1 dòng:** kéo panel hẹp về 400 rồi rộng ra 600 — hint luôn 1 dòng, không xuống
  2 dòng ở bất kỳ chiều rộng nào.
- **Snap-back ngang:** kéo rộng thừa cho text ngắn → thả ra, panel co về 420.
- **Snap-back dọc:** kéo cao thừa → thả ra, panel co về chiều cao nội dung.
- **Text dài:** tra đoạn dài — output cuộn trong ô (cap 260), panel không tự dãn;
  kéo tay dãn panel thì ô dãn theo.
- **Thu/mở provider:** thu card → panel thụt theo; mở lại → auto-fit về đúng nội dung.
- **Slider font:** kéo "Source & Translation Text Size" và "App name & Slogan Font Size"
  trong Settings — panel giãn live theo, không che, không tràn.
- **Mở lại:** đóng panel rồi mở lại — về đúng mặc định 420×450, không nhớ cỡ kéo cũ.
- **Card shortcuts nhúng ("?"):** bấm "?" → card hiện trên đầu card provider, đủ 10 dòng theo thứ tự Settings, phím tắt đúng giá trị hiện tại; bấm nút ▶ → **ẩn panel trước rồi chạy hành động** (Manh 2026-11-18, như mac — vd OCR mở khung chọn vùng, Settings mở cửa sổ Settings); bấm toàn thanh top của card / nút × / bấm lại "?" → card ẩn, panel ở lại; Esc ẩn luôn panel (không còn kiểu Esc chỉ đóng bảng như Flyout cũ).

## 10. Layout đa tab (hub) — quyết định 17/11 (ĐÃ triển khai trên main)

**Khung đã lên main.** Sau đó mới tạo nhánh `feature/fast-read-rsvp` làm Fast Read window (xem dưới).

**Tên gọi — CHÍNH THỨC (Manh chốt 2026-11-18, thay cách gọi panel 1/2/3
trong doc): panel left** (panel 1, shortcuts — tạm thời), **panel center**
(panel 2, dịch — CHỐT), **panel right** (panel 3, Fast Read — tạm thời).
Nói mồm có thể nói panel 1/2/3 hay panel dịch 1/2/3. Dùng tên này trong
doc + code + trao đổi để khỏi nhầm (bài học bên mac: gọi nhầm panel nên
card "How the icon works" bị đặt nhầm vào panel center thay vì panel left).

- **Kiểu "hub":** panel có 3 tab, tab ở giữa là màn chính, 2 bên là 2 nhánh.
  **panel center (panel dịch, ở giữa, mặc định):** mở panel là thấy Dịch.
- **panel left (panel shortcut, ở trái, tạm thời)** — ô linh hoạt, sau này thay được
  (không cố định là shortcut).
- **panel right (panel Fast Read, ở phải)** — tạm trống/placeholder tới khi Fast Read ship.
- **3 chấm hiện từ đầu để trải nghiệm** (thay điều kiện cũ "chấm của panel Fast Read chỉ hiện khi tab
  có nội dung thật"; ngoại lệ chỉ áp cho panel Fast Read đã có kế hoạch cụ thể).
- **Lý do (Manh):** panel dịch ở giữa thì vuốt sang 2 bên đều 1 lần; để panel dịch ở trái
  thì sang panel Fast Read phải qua 2 lần. Có tiền lệ (Snapchat: camera ở giữa làm mặc
  định, vuốt trái sang chat, vuốt phải sang stories). Khác thứ tự research cũ
  (panel dịch từng đứng đầu) nhưng lý do chính đáng nên làm theo hướng này.
- **Nút "?" bảng shortcut nằm trong panel center (panel dịch, ở giữa, mặc định)**, ngay trước nút Pin (§3).
- **Hàng chấm — triển khai (Manh chốt vị trí 12:51): hàng MỚI (row 9), center, nằm dưới icon GitHub ở footer** (không nằm trong hàng Footer, không nằm trong hàng Actions). RootGrid 9 → 10 hàng; TitleBar (row 0) và Footer (row 8) giữ nguyên mọi tab; vùng nội dung tab = rows 1–7. Chấm 8px, cách nhau 8px, chấm đang chọn xanh #0EA5E9, còn lại xám #C8C8C8. Hàng cao ~20 (top margin 4 + chấm 8 + bottom margin 8); **bottom margin 8 = ResizeStripThickness** — dải resize native chiếm 8px dưới đáy cửa sổ, nên chấm phải nằm hẳn trên dải đó mới bấm được. **Hàng mới cộng ~20 vào chiều cao tối thiểu (ComputeMinPanelHeight cộng mọi hàng Auto), panel mở cao hơn 450 cũ — Manh duyệt tăng height (12:51).** Số chính xác đo khi test bản build. **Chuyển tab (Manh 2026-11-18):** ngoài bấm chấm, còn có **nút ‹ ›** (ChevronLeft E76B / ChevronRight E76C, đã kiểm chứng trên Microsoft Learn 18/11) ở 2 bên hàng chấm — bấm để sang tab trước/sau, vòng tròn (trái từ panel left sang panel right); và **cuộn chuột (scroll) trên hàng chấm** — xuống = tab sau, lên = tab trước (như mac). **Lăn chuột chuyển tab ở cả topbar (hàng tên app/slogan + hàng hint) và toàn bộ bottom bar — từ hàng link từ điển trở xuống, gồm cả dòng icon GitHub + version (Manh 2026-11-18, hint thêm 2026-11-19)** — hàng hint + bottom bar dùng handler `OnTabSwitchWheel`, còn hàng tên app/slogan là vùng caption native nên lăn chuột không tới được XAML — bắt bằng window subclass (`WM_MOUSEWHEEL` trong `MinSizeSubclassProc`); throttle 0.4s dùng chung nên lăn qua ranh giới vùng không chuyển 2 lần. **Sau này (Manh 2026-11-19): có thể đưa cả 3 chấm lên dưới hint cho đồng bộ mac — vùng lăn topbar (tên app/slogan/hint) đã phủ sẵn nên dời chấm lên không làm mất lăn.** **Một lần lăn dài = một lần chuyển tab (Manh 2026-11-18):** sau mỗi lần chuyển tab bằng lăn, các lượt lăn tiếp trong 0.4s bị bỏ qua, không thì một lần lăn dài lật qua mấy tab một lúc.
- **panel left — triển khai (Manh 2026-11-18, như mac): 2 card** — card **"How the icon works"** (trên) + card **shortcuts** (dưới). Title mỗi card có icon ở đầu trái: How = bóng đèn **Lightbulb EA80**, shortcuts = **KeyboardShortcut EDA7** (cả 2 đã kiểm chứng trên Microsoft Learn 18/11; mac dùng cursorarrow.click cho card How, Windows không có glyph đó). Card "How the icon works" (nội dung Manh 2026-11-18): dòng "Not just a translator — it improves your learning experience." rồi 3 dòng gesture in đậm + mũi tên → + nội dung: **Hover icon → Translate the selected text**, **Click icon → (theo Settings)**, **Alt + Click → (hành động còn lại)**; cuối dòng "Tip: swap Click and Alt + Click in Settings." ("Option + Click" của mac viết thành "Alt + Click" cho đúng Windows). **Card ĐỘNG theo Settings (Manh 2026-11-18, như mac vòng 8):** 2 dòng Click / Alt+Click đọc cài đặt "Click icon" (`IconClickAction`) mỗi lần mở panel left — Click = Fast Read thì "Click icon → Fast Read — read it faster" và "Alt + Click → Listen — hear it read aloud"; Click = Speak (mặc định) thì ngược lại. Windows chỉ có 2 lựa chọn Click (Speak / Fast Read) nên không có trường hợp Click = Translate như mac. Bấm **toàn thanh top** của mỗi card là thu gọn/mở ra (không cần bấm riêng nút góc phải); trạng thái thu gọn của card "How the icon works" được nhớ trong Settings (`HowIconWorksCollapsed`), card shortcuts mở lại mỗi lần vào tab. Card shortcuts: 10 dòng y hệt card "?" nhúng (tên + phím tắt + nút Run ▶), dựng bằng cùng hàm `BuildShortcutRows` từ cùng list `_shortcuts` nên 2 chỗ không bao giờ lệch nhau. Nội dung cuộn được (ScrollViewer) nếu 10 dòng không vừa. **Dãy share (Manh 2026-11-19, như mac d414700):** dưới card shortcuts — nhãn "Share TransPeek" (11pt, xám, căn giữa) + dãy 6 icon share (Facebook/Threads/X/Reddit/LinkedIn/Mail, như About), cả 2 căn giữa, KHÔNG card, KHÔNG Spacer đẩy xuống đáy (chỗ trống khi 2 card collapse để sau này thêm focus timer). **Đổi 2026-11-19 (giống mac):** share giờ là mục cuối trong ScrollViewer chung của panel left (cả panel left cuộn chung 1 list: How card + shortcuts + share), không còn là hàng Auto thứ 3 cố định trong Grid — hàng cố định cũ lấy ~50pt từ card shortcuts, tốn chỗ của timer focus/tomato/rest-eyes sau này. Chi tiết xem §11.3.
- **Bấm Run thì ẨN PANEL TRƯỚC rồi mới chạy (Manh 2026-11-18, như mac — sửa lỗi cũ):** trước đây bấm Run ở tab shortcut thì về lại panel dịch, bấm Run ở bảng "?" thì chỉ đóng bảng, panel vẫn ở lại che màn hình mà action cần (OCR mở khung chọn vùng, Settings mở cửa sổ). Giờ `BuildShortcutRows` gọi `HidePanel()` trước `entry.Run()` ở cả 2 chỗ; action nào cần panel (vd dịch lại) thì tự mở panel lên sau.
- **panel right — triển khai (placeholder, mô tả mirror mac 2026-11-19):** icon ReadingMode (glyph E736, Segoe Fluent Icons, đã kiểm chứng trên Microsoft Learn 17/11 — glyph gần nhất với icon `book.pages` của mac) + tiêu đề "Fast Read — coming soon" + dòng mô tả "Fast reading (RSVP) will live here once the Fast Read window is ready." (12pt, xám, căn giữa, tự xuống dòng), căn giữa. Y hệt mac (`PopupView.fastReadContent`), chỉ khác icon do Segoe không có `book.pages`.
- **Hành vi:** mở panel luôn về panel dịch (`ShowPanelNear`/`ShowInputMode` gọi `ShowTab`); chuyển tab chỉ trong phiên, không lưu; panel không đổi kích thước khi chuyển tab — trừ safety net: chuyển về panel dịch mà panel đang thấp hơn chiều cao tối thiểu của panel dịch (do kéo nhỏ ở tab khác, lúc đó các hàng dịch thu lại nên mức kẹp chỉ là 450) thì tự cao lên tới min, chỉ cao lên, không đổi rộng, để card output không tràn dưới hàng Actions (kiểu lỗi #164). **Snap-back (dọc + ngang) chỉ chạy ở panel dịch** — tab khác không có text dịch để đo nên kéo ở đó chỉ kẹp min/max. Pin/Settings chỉ ở panel dịch. **KHÔNG vuốt** — WinUI không có sẵn vuốt cho panel tự làm, phải tự chế (Manh chưa duyệt).
- **Thứ tự làm:** khung multi-tab → Fast Read window → TTS (Fast Read làm trước
  TTS, như mac). **Tab RSVP trong panel chỉ là thử nghiệm sau này**, không làm
  trong nhánh Fast Read — nhánh đó chỉ làm **window riêng chạy RSVP, UX copy y xì
  SwiftRead, CHỈ ĐỌC, không gồm TTS trong read** (SwiftRead có Listen nhưng
  TransPeek không copy phần đó; nghe là việc của TTS). Engine + luồng vào theo
  `docs/RESEARCH/tts-speechify-naturalreader/rsvp-report.md` trên repo mac
  (§6.1 + §6.3 cho Windows/C#). **Đã triển khai trên nhánh `feature/fast-read-rsvp`
  (17/11): xem `docs/FEATURES/FAST_READ.md` — engine + window + 4 đường vào
  (Alt-click, menu chuột phải icon, nút "Fast Read" trong panel dịch, tray);
  phím tắt global chỉ là fallback, chưa làm.
- **"Run FastRead in" — 2 logic đã có sẵn, setting GLOBAL (kiểm tra
  2026-11-20, Manh dặn 12:57):** setting `FastReadRunLocation`
  ("NewWindow" mặc định / "RightPanel"), lưu trong settings.json, áp
  cho MỌI đường mở Fast Read. Mọi đường đều hội tụ qua 1 cửa duy nhất
  `App.RouteFastRead(text)`: "RightPanel" → `_panel.ShowPanelForReading(text)`
  (mở panel + chuyển tab phải + chạy chữ qua `FocusTimerStore.RequestPanelReading`
  → `PanelRightRequest` → panel `ShowTab(FastRead)` + `StartPanelReading` +
  tự pin); "NewWindow" → `ShowFastReadWindow(text)`. (1) Nút "Fast Read"
  ở panel center: `OnFastReadClicked` → `_fastReadRequested` →
  `OpenFastReadAsync` → `RouteFastRead` (handler KHÔNG được HidePanel vô
  điều kiện, không thì đường panel phải tự ẩn panel nó vừa mở). (2) Click
  icon A+: `TriggerIconWindow.FastReadRequested` → `OpenFastReadFromSelectionAsync`
  (lấy selection/clipboard) → `RouteFastRead`. Tray + shortcut cũng qua
  `OpenFastReadFromSelectionAsync`. Nhãn UI mới (2026-11-20, đổi tên
  2026-11-21): "FastRead Lite" / "FastRead Full Window (recommended)".
  Xem `docs/FEATURES/FAST_READ.md`.

## 11. Panel center + Panel left — thiết kế CHỐT (20/11/2026, nhánh `feature/focus-timer-decor`, CI #207)

> Mục này chốt thiết kế HIỆN TẠI của panel center (dịch) và panel left
> (shortcut) như đã ship — theo doc mac §10 (commit `937f0b6`,
> v0.1.4 / build 248). Nó bổ sung §10 ở những chỗ khác nhau, đặc biệt
> là các điểm Windows khác mac (ghi rõ ở từng mục). Các số nền tảng ở
> §1–§5 không đổi (420×450 / min 400×450 / max 600×800 / cap output
> 260 / snap-back +8).

### 11.1 Khung 3 tab (hub) — thứ tự và hàng chấm

- Thứ tự: **shortcut (trái, 1) / translate (giữa, 2, mặc định) / Fast
  Read (phải, 3)**. Panel dịch ở giữa thì sang 2 bên đều 1 lần.
- Top bar + bottom bar giống nhau trên mọi tab; chỉ nội dung giữa đổi
  theo tab. **Đổi tab có animation fade 0.15s ease-out (như mac, làm
  2026-11-19):** `ShowTab` fade-in nội dung tab mới (chỉ fade vào,
  tab cũ ẩn luôn); fade đang chạy thì dừng + trả opacity về 1 trước
  nên lật tab nhanh không để lại element mờ dở. Bỏ qua fade khi cửa
  sổ chưa load (lần mở đầu tiên) để không kẹt opacity 0.
- **Hàng chấm ở bottom bar, dưới icon GitHub (TẠM THỜI)** — vị trí +
  kích thước + chevron + lăn chuột xem §10. Sau này có thể dời lên
  dưới hint cho đồng bộ mac (Manh 19/11) — hiện tại để tạm, không đổi.
  **Pin nằm ở mép trái hàng chấm (Margin 10) từ 2026-11-20
  (feature/focus-timer-decor, mac parity)** — một nút Pin dùng chung
  cả 3 panel, thay cho nút Pin cũ ở hàng Actions (chỉ hiện ở tab
  center).
- Panel right là **lite reader** từ 2026-11-20
  (feature/focus-timer-decor): demo ("Fast Read Lite" + preview focus
  "reading" + nút "Read in panel" + radios "Run FastRead in" — nhãn
  "FastRead Lite" / "FastRead Full Window (recommended)", section
  radios căn LEFT) khi chưa đọc, và `CompactReadingView` khi đang đọc
  — cùng view model với Fast Read window. Đọc xong ở lite: câu chúc
  mừng đầy đủ + share chỉ ở full-size (≥ 540), size bé chỉ
  "Congratulations!" — ĐÓ LÀ đọc thường; **đọc nhắc nghỉ xong thì KHÔNG
  hiện chúc mừng mà hiện màn đếm ngược ở giữa** (Manh 2026-11-20 18:19,
  giống cửa sổ Rest): icon cafe EC32 32pt + tiêu đề ("Take a break" /
  "Rest your eyes") + số đếm ngược lớn 56pt Consolas Bold + "Back to Fast
  Read Lite when the timer ends." + nút Done; reader ẩn trong lúc đếm,
  hết giờ thì về lại demo. Space = play/pause ở lite (đọc do user bấm thì
  panel tự kích hoạt để nhận phím; đọc do nhắc nghỉ thì KHÔNG kích
  hoạt). Thiết kế lite ĐÃ CHỐT, không đổi nữa. Xem
  `docs/FEATURES/FAST_READ.md` (mục "Shared view model") +
  `docs/FEATURES/FOCUS_REST.md`.
- **Banner nhắc nghỉ** (feature/focus-timer-decor): overlay ở đầu vùng
  tab (mép trên sát hint dưới slogan, Manh 2026-11-21), hiện trên CẢ 3
  tab khi có nhắc đang chờ — nền cam ĐẶC, không opacity (Manh
  2026-11-21 — fill 0.25 trước đó để chữ đọc phía sau xuyên qua) — xem
  `docs/FEATURES/FOCUS_REST.md`.

### 11.2 Panel center (dịch, tab mặc định)

- **Top bar:** tên app "TransPeek" (**Bold, màu accent #0EA5E9**,
  cỡ N) + slogan "Translate anywhere, Peek instantly!" (N−1, màu Gray
  — **khác mac: Windows KHÔNG nghiêng**, mac nghiêng) + hint "Hover
  icon to translate, Click to listen" (N−4, màu tertiary), căn giữa.
  Cả hàng là vùng kéo di chuyển (caption region native). N =
  `PopupTopBarFontSize`, mặc định 17 (17 / 16 / 13, khớp mac).
- **Nội dung dịch:** ô nhập + hàng ngôn ngữ (Source ⇄ Target + Capture
  OCR) + card kết quả provider (text cap 260, loa/Copy cố định ở đáy
  card) + bảng shortcut của nút "?" nhúng ở ĐẦU ô output khi bấm +
  hàng Actions + DictionaryBar (xem §2).
- **Hàng Actions:** nút "Fast Read" ở đầu trái + nút "?" + Settings ở
  đầu phải. **Pin KHÔNG còn ở đây từ 2026-11-20** — nó ở mép trái
  hàng chấm (xem §11.1). **Viền nút (Manh 2026-11-20 17:05–17:06):**
  nút "Capture OCR" ở hàng ngôn ngữ viền **xanh blue `#0078D4` 40%
  (`#660078D4`)**, nút "Fast Read" viền **nghệ `#C7800D` 40%
  (`#66C7800D`)** — cả 2 chỉ viền 1px, nền giữ mặc định, màu không
  chiếm focus. Đổi độ đậm thì đổi alpha, không đổi màu/kiểu.
- **Bảng "?" :** nhúng ở đầu ô output (ngay trên các card provider),
  tiêu đề "Keyboard shortcuts" + nút đóng [×]. Liệt kê 10 shortcut +
  nút Run. **Khác mac: bấm Run thì ẨN PANEL TRƯỚC rồi mới chạy**
  (xem §10 — mac bấm Run thì chạy rồi về lại panel dịch; Windows ẩn
  panel trước vì action cần màn hình trống, vd OCR mở khung chọn
  vùng).
- **Bottom bar (giống nhau mọi tab):** ảnh mark GitHub 16pt + link
  "GitHub" căn giữa, text version ("Version X (Build CI #N)", ẩn số
  build khi = 1) mờ (Gray 11pt, opacity 0.7) ở bên phải.

### 11.3 Panel left (shortcut, tab tạm thời)

- Tab này là **TẠM THỜI** — sau này dành cho timer focus / tomato /
  rest-eyes. **Từ 2026-11-20 (feature/focus-timer-decor) nó đã có thẻ
  Focus & Rest ở giữa** — xem `docs/FEATURES/FOCUS_REST.md`. Triển
  khai chi tiết xem §10 (3 card + dãy share); mục này chỉ chốt lại
  thiết kế:
  1. **Thẻ "How the icon works"** — ĐỘNG theo setting Click (Hover →
     dịch; Click → Fast Read / Nghe; Alt+Click → hành động còn lại).
     Thu gọn được (bấm toàn thanh top), trạng thái lưu trong Settings
     (`HowIconWorksCollapsed`). Windows chỉ có 2 lựa chọn Click (Speak
     / Fast Read) nên không có trường hợp Click = Translate như mac.
  2. **Thẻ "Focus & Rest"** — Pomodoro (Work/Rest, 4 chấm chu kỳ,
     "Repeat automatically") + rest-eyes 20-20-20 + "Run reminder in".
     Thu gọn được (bấm toàn thanh top), trạng thái lưu trong Settings
     (`FocusTimerCollapsed`). Chấm cam ở header khi có nhắc đang chờ.
     **Bố cục CHỐT (20/11/2026, căn giữa cho đẹp):** countdown 28pt
     Consolas Bold **center**; Work/Rest picker (nhãn trái, picker phải
     88); 4 chấm chu kỳ **center** trên 1 hàng riêng; dòng "Long break
     15 min after 4 cycles" **line riêng dưới 4 chấm, center**; nút
     Start focus / Pause / Resume + Reset **center** trên 1 hàng;
     "Repeat automatically" + "Auto start focus (default: Off)" là
     **ToggleSwitch** (nút gạt — nhãn trên, gạt dưới, sát lề trái);
     nút "Start eye reminders" **center**; "Run reminder in:" nhãn trên,
     picker **xuống dòng riêng, full-width**, lựa chọn "New FastRead
     Window" / "FastRead Lite" (đổi tên 2026-11-20 18:09, KHÔNG thêm
     "(recommended)"), dòng note xám ở dưới.
  3. **Bảng "Keyboard Shortcuts"** — cùng bảng với nút "?" ở panel
     center (dựng bằng cùng hàm `BuildShortcutRows` nên 2 chỗ không
     bao giờ lệch nhau). Thu gọn được; card shortcuts mở lại mỗi lần
     vào tab.
  4. **Dãy share** — ở cuối nội dung cuộn: nhãn "Share TransPeek"
     (11pt, xám, căn giữa) + dãy 6 icon share (căn giữa). KHÔNG tạo
     card mới, KHÔNG dùng Spacer đẩy xuống đáy.
- Panel left KHÔNG ẩn bottom bar (icon GitHub + version vẫn hiện).

### 11.4 Hàng chấm — mac vs Windows

| Hạng mục | mac | Windows |
|---|---|---|
| Vị trí hàng chấm | Ngay dưới top bar (tạm thời, vòng 5) + nút chevron 2 bên | Ở bottom bar, dưới icon GitHub (tạm thời) — xem §10 |
| Chuyển tab | Bấm chấm/chevron, vuốt trên hàng chấm, vuốt trackpad (50pt = 1 tab) | Bấm chấm/chevron (E76B/E76C), **lăn chuột** trên hàng chấm + topbar + bottom bar (throttle 0.4s) — **KHÔNG vuốt** (WinUI không có sẵn vuốt cho panel, phải tự chế, Manh chưa duyệt). **KHÔNG có phím tắt ←/→ chuyển tab** (Manh 2026-11-20 11:30 — phím ←/→ ở Read lite đã dùng để tua slide, trùng; 2 nút chevron ‹ › GIỮ NGUYÊN làm fallback khi không vuốt) |
| Sau này | Chờ Manh quyết vị trí cuối | Có thể dời lên dưới hint như mac (Manh 19/11) — hiện tại để tạm |

### 11.5 Chưa chốt trong mục này (hoãn sang 0.1.5)

- Nút Fast Read ở panel center: thêm chút padding giữa icon và chữ.
- Vị trí cuối cùng của hàng chấm — Windows: có thể dời lên dưới hint
  như mac, hiện tại để tạm ở bottom bar.
- **Ngôn ngữ 20 → 249: Windows ĐÃ XONG ở 0.1.4** (249 ngôn ngữ +
  alias `pt`, dropdown tìm kiếm AutoSuggestBox — CI #191), nên không
  nằm trong danh sách hoãn của Windows. Các mục còn lại trong §10.5
  của mac (đổi tên Window Read, tăng bước đổi cỡ chữ vuốt trackpad
  dọc) không áp cho Windows: Windows đã có top bar "Rapid Serial
  Visual Presentation — RSVP" + title "TransPeek — Fast Read RSVP",
  và đổi cỡ chữ bằng lăn chuột/kéo chứ không vuốt trackpad.

### 11.6 Thiết kế CHỐT — danh sách khóa + fallback (Manh 2026-11-20 18:12)

> Mục này là **chuẩn để đối chiếu**: sửa gì về panel thì đọc lại danh
> sách này trước, sửa xong đối chiếu lại. **Không đổi mục nào dưới đây
> khi chưa có lệnh Manh.** Nếu một bản sau phá layout: quay về số +
> bố cục ở đây (baseline: nhánh `feature/focus-timer-decor`, CI #207
> xanh, head `d364f879` — cộng đợt 18:09 `53daae66` khi CI xanh),
> rồi sửa lại từ đó, không mò số.

**Khung + kích thước (§1–§2):** mở 420×450, min 400×450, max 600×800,
cap output 260, min output card 218, cap ô nguồn 120, min ô dịch 60;
RootGrid 10 hàng (Row 5 `*` duy nhất dãn); snap-back ±8 cả 2 chiều;
kéo resize viền native 8px; Settings KHÔNG có slider rộng/cao panel.

**Panel center (§11.2):** top bar (tên app Bold accent #0EA5E9 +
slogan không nghiêng + hint, caption region); hàng ngôn ngữ (picker 112
+ Swap + Capture OCR viền xanh 40%); card output 3 hàng, loa/Copy
overlay TRONG ô (không dời ra ngoài — Manh chốt 17/11); Actions (Fast
Read viền nghệ 40% trái + "?" + Settings phải, Pin đã dời sang hàng
chấm); "?" nhúng card shortcuts ở đầu output; DictionaryBar; footer
(GitHub giữa + version phải).

**Panel left (§11.3):** thứ tự How → Focus & Rest → Shortcuts → share;
How động theo Click; Focus & Rest bố cục center như §11.3 mục 2
(countdown/chấm/nút center, Long break line riêng, ToggleSwitch gạt,
"Run reminder in:" picker xuống dòng full-width, lựa chọn "New FastRead
Window" / "FastRead Lite"); Shortcuts cùng `BuildShortcutRows` với "?";
share cuối nội dung cuộn, không card, không Spacer.

**Panel right — Fast Read Lite (§11.1 + `FAST_READ.md`):** demo ("Fast
Read Lite" + preview "reading" + "Read in panel" + radios "Run FastRead
in:" căn LEFT, "FastRead Lite" lên trên) / đang đọc (`CompactReadingView`
dùng chung view model với Window Read); theme lite ĐÃ CHỐT, không đổi;
đọc xong: câu đầy đủ + share chỉ ở full-size (≥ 540), size bé chỉ
"Congratulations!" — đọc nhắc nghỉ xong thì màn đếm ngược cafe ở giữa
(cafe EC32 + tiêu đề + mm:ss 56pt + Done), reader ẩn trong lúc đếm,
hết giờ về demo; Space = play/pause (đọc do user bấm thì panel tự
kích hoạt; đọc do nhắc nghỉ cũng tự kích hoạt — từ 2026-11-22, xem §11.7); **KHÔNG phím tắt ←/→ chuyển tab**,
chevron ‹ › giữ nguyên.

**Icon float (`FLOAT_ICON.md`):** translate-first — idle LUÔN là A thường
xanh #0078D4 không bold; A+ nghệ chỉ khi giữ Alt lúc Click=Speak hoặc
linger 2.5s sau Read; loa E767 xanh khi giữ Alt lúc Click=FastRead hoặc
đang nói; bubble E8BD chỉ là mẫu dự phòng, luôn ẩn.

**Nhắc nghỉ (`FOCUS_REST.md`):** đang đọc thì nhắc CHỜ (banner cam đặc
trên cả 3 tab + chấm cam thẻ Focus-Rest + tự hiện khi dừng đọc); KHÔNG
toast hệ thống (chờ bản cài/Store); "Take a break" / "Snooze 5 min";
"Run reminder in:" là setting RIÊNG, khác "Run FastRead in:".

**Settings (`DECISIONS.md`):** cửa sổ 920×640; "Click icon for:" (trên
Hover delay); Hover delay + Auto-hide hàng inline (nhãn trái, picker
phải MinWidth 120, mô tả dưới) với nhãn giây "0.5s" / "5s" (chỉ đổi hiển
thị, giá trị 500 ms giữ nguyên); KHÔNG ô hotkey ở tab OCR (trùng General);
nhãn UI sửa cả XAML lẫn `.resw` (resw thắng lúc chạy).

### 11.7 Panel focus/activation — hành vi quan sát (Manh test CI230, 2026-11-21)

> Ghi lại để sau còn biết hướng sửa khi có lỗi liên quan (Manh
> 2026-11-21 23:28, test bản nhánh CI230 `ff33cce5`). Phân biệt rõ: phần
> nào đã xác minh bằng code, phần nào chỉ là quan sát chưa giải thích
> được — đừng đoán nguyên nhân rồi sửa theo.

**Quan sát (Manh, test máy thật):**
- Select text → hover icon → panel hiện: panel KHÔNG có viền màu 1px ở
  mép đỉnh topbar + đổ bóng nhạt (như cửa sổ đang mất focus).
- Click lần 1 vào bất kỳ chỗ nào trong panel (đã thử topbar, bottom,
  input, output, action) → vẫn vậy. Click lần 2 vào đúng/hoặc gần đúng
  vị trí click 1 → mới thấy viền màu 1px ở đỉnh mép trên cùng + đổ bóng
  đậm hơn (như panel đang được focus).
- Hover chuột ra ngoài panel → đổ bóng nhạt đi, mất viền màu 1px;
  auto-detect block text ngoài panel vẫn nhận bình thường, hover icon
  bình thường, mở panel mới được.
- Hệ quả nghi ngờ: Space play/pause không ăn vì panel chưa được focus —
  phải click vào panel mới ấn được (đợt `ff33cce5` từng làm
  `GlobalSpaceTracker` poll Space toàn cục thay cho phím tới panel —
  **đã GỠ 2026-11-22, hướng an toàn: panel tự kích hoạt khi bài đọc
  panel-right bắt đầu, Space ăn ngay không cần click trước**).
- Chưa rõ: có phải từ khi có auto-detect block text (PR #3) mới bị vậy,
  hay trước đó đã bị — Manh chưa test lại bản cũ chưa có auto-detect nên
  chưa kết luận được.

**Đã xác minh bằng code (đúng thiết kế, không phải bug):**
- Panel mở bằng `ShowWithoutActivation` (`TranslatePanelWindow.
  ShowPanelNear`): set `WS_EX_NOACTIVATE` (dính vào style của cửa sổ,
  không phải cờ 1 lần) + `SetWindowPos` hiện cửa sổ. Cửa sổ NOACTIVATE
  KHÔNG bao giờ thành foreground/active khi hiện lên hay khi click —
  nên "mở lên không có viền accent + bóng nhạt" là ĐÚNG THIẾT KẾ (panel
  không được cướp focus khỏi text user đang đọc).
- Kích hoạt là do app tự làm: `WindowHelpers.ActivateWindow` gỡ
  `WS_EX_NOACTIVATE` + `SWP_FRAMECHANGED` + `window.Activate()`; gọi
  khi user click vào panel (và các đường `ShowInputMode` /
  `ShowPanelForReading` mở panel kiểu nhập liệu). Vì vậy phím (Space,
  Ctrl+V) chỉ tới được panel SAU khi kích hoạt — đúng với quan sát
  "phải click vào panel mới bấm Space được".
- Kích hoạt chạy ở `PointerReleased`, KHÔNG phải `PointerPressed` — có
  chủ ý (test 0.1.3.53: kích hoạt giữa lúc bấm — giữa mouse-down và
  mouse-up — nuốt mất `Click` của Button, nút Settings không làm gì;
  kích hoạt lúc nhả thì chạy sau `Click`). Handler gắn bằng
  `AddHandler(..., handledEventsToo: true)` trên `contentRoot`; có guard
  `_visible` (test 0.1.3.64: nút gear ẩn panel trước rồi handler mới
  chạy, kích hoạt lại sẽ hiện panel lại ngay).

**Chưa giải thích được — KHÔNG đoán, để điều tra sau:**
- Theo thiết kế trên, 1 click (bấm + nhả) là đủ kích hoạt qua handler
  `PointerReleased`. Quan sát lại là click 1 không kích hoạt, click 2
  cùng/gần vị trí mới kích hoạt. Cơ chế chưa rõ. Hướng điều tra khi làm:
  (a) cú `PointerReleased` đầu có bị nuốt không (fade-in đổi tab 150ms?
  `ClickOutsideMonitor`? cú click bắt đầu từ cửa sổ icon?); (b)
  `SetForegroundWindow` / `window.Activate()` lần đầu có bị chặn không
  (foreground lockdown: TransPeek chưa phải tiến trình foreground lúc
  panel mở); (c) viền accent/bóng có trễ 1 chu kỳ kích hoạt không (hiện
  sau ở lần kích hoạt thứ 2).
- Chuột ra ngoài panel thì bóng nhạt + mất viền accent cũng chưa giải
  thích được: Windows chuẩn không deactivate cửa sổ khi chuột rời khỏi
  nó. Cần xác minh lại lúc đó panel có thật sự đang active không (hay
  viền/bóng lúc click 2 chỉ là trạng thái hover của khung cửa sổ).
- Câu hỏi mở: test bản cũ KHÔNG có auto-detect (vd bản v0.1.4.211,
  trước PR #3) — mở panel xem có viền accent/bóng đậm không, click 1
  có kích hoạt không. Kết quả sẽ chốt được auto-detect có liên quan
  hay không.
### 11.7 Panel focus / activation — hành vi CHỐT (2026-11-22, main `4aa959b5`)

> Panel mở KHÔNG kích hoạt là ĐÚNG THIẾT KẾ, không phải bug. Phím (Space, Ctrl+V)
> chỉ tới panel SAU khi panel được kích hoạt.

**Mở panel (không kích hoạt):** `ShowPanelNear` dùng `WindowHelpers.ShowWithoutActivation`
— gắn `WS_EX_NOACTIVATE` vào ex-style + `SetWindowPos` HWND_TOPMOST. Cửa sổ NOACTIVATE
không bao giờ thành foreground khi hiện hay khi click → lúc mở panel không có viền
accent 1px ở mép đỉnh + bóng nhạt (như mất focus) là đúng thiết kế.

**Kích hoạt (khi nào):** `WindowHelpers.ActivateWindow` (gỡ NOACTIVATE + `window.Activate()`),
gọi ở:
- handler `PointerReleased` trên contentRoot (chủ ý chạy ở lúc NHẢ chuột, không phải lúc
  bấm — test 0.1.3.53: kích hoạt giữa click nuốt mất `Click` của Button; test 0.1.3.64:
  guard `_visible` vì nút gear ẩn panel trước rồi handler mới chạy);
- `ShowInputMode` + 2 chỗ dòng 784/799;
- **khi bài đọc panel-right bắt đầu** (`OnFocusTimerStoreChanged`, nhánh `PanelRightRequest`,
  main `4aa959b5`, 2026-11-22) — Space ăn ngay không cần click trước, kể cả đường
  reminder. Đảo ngược quyết định cũ "reminder reads do NOT activate".

**Quan sát chưa giải thích được (Manh test CI230, 23:28 ngày 21/11):** theo thiết kế
1 click (bấm + nhả) là đủ kích hoạt, nhưng quan sát cần click 2 cùng/gần vị trí mới
kích hoạt; chuột ra ngoài panel thì bóng nhạt đi, mất viền 1px (trái hành vi Windows
chuẩn). Hướng điều tra: (a) cú `PointerReleased` đầu có bị nuốt không (fade-in 150ms?
`ClickOutsideMonitor`? click bắt đầu từ cửa sổ icon?); (b) `SetForegroundWindow` /
`window.Activate()` lần đầu có bị foreground lockdown chặn không; (c) viền/bóng có trễ
1 chu kỳ kích hoạt không. **Test chốt: bản cũ KHÔNG có auto-detect (vd `v0.1.4.211`,
trước PR #3) — mở panel xem viền/bóng + click 1 có kích hoạt không.**

## Liên kết

- [Icon float UX](FLOAT_ICON_UX_SPEC.md)
- [Tổng quan UX](APP_UX_OVERVIEW.md)
- [Kiến trúc](../../ARCHITECTURE.md)
- [Việc đang làm / chờ](../../TASK_QUEUE.md)
- [Trạng thái dự án](../../PROJECT_STATE.md)
- [Chỉ mục UX](../../UI_DESIGN.md)
