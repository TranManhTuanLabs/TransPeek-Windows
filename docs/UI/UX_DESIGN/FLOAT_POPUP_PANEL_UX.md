# Float / Popup Panel — UX Spec (CHỐT)

> **Trạng thái: CHỐT — 17/11/2026, theo build #162 (commit `2053f9b`).**
> Các con số trong doc này là **spec**. Đổi bất kỳ số đã chốt nào cũng cần Manh duyệt trước.
> Spec đã bao gồm: cấu trúc card 4 hàng (#160) + hint 1 dòng (#161) + loa/Copy overlay
> trong ô output (#162) + nút "?" + popover shortcut (17/11, "?" trước Pin) +
> layout đa tab hub (§10, 17/11, chưa triển khai tab).
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

## 2. Khung layout (RootGrid — 8 hàng, Padding 12)

`RootGrid` có 8 hàng: 4 hàng `Auto`, 1 hàng `*` (output card), 3 hàng `Auto`.
Hàng `*` là hàng duy nhất dãn theo chiều cao; các hàng `Auto` chỉ cao bằng nội dung.

| Hàng | Kiểu | Nội dung | Số liệu chốt |
|---|---|---|---|
| Row 0 | Auto | **TitleBar** — tên app + slogan + hint, căn giữa | Margin `0,0,0,16` (đáy = hintSize, mặc định 13). Tên app Bold, màu `#0EA5E9`. Slogan xám. Hint: "Hover icon to translate, Click to listen". **Cả hàng là vùng kéo di chuyển** (native caption region) — panel không có title bar riêng |
| Row 1 | Auto | **SourceTextBox** — ô nhập, sửa được | `MaxHeight 120`, Margin `0,0,0,4`. Enter = dịch lại, Shift+Enter = xuống dòng. Loa + Copy **nằm trong ô** ở góc dưới-phải (overlay, Margin `0,0,6,8`) |
| Row 2 | Auto | **Hint** — 11pt, **PHẢI ở 1 dòng ở mọi chiều rộng** (xem §6) | "Select a word, then click Dictionary to look it up" (rút ngắn 17/11 #161 — câu cũ dài ~103 ký tự, xuống 2 dòng khi kéo hẹp, gây che loa) |
| Row 3 | Auto | **Hàng ngôn ngữ** — Source ⇄ Target + Capture OCR | ComboBox Source/Target **rộng 112**, nút Swap ⇄ Padding `4,2`, Spacing `4`. Nút "Capture OCR" ở bên phải |
| Row 4 | `*` | **Output card** (`ProviderCardBorder`) | Margin `0,0,0,8`, CornerRadius 6, BorderThickness 1, `MinHeight 218`. Bên trong **3 hàng**: header (tên provider 13pt SemiBold + trạng thái) / divider / TextBox (`*`, FontSize 14, MinHeight 60, MaxHeight 260) với **loa/Copy overlay trong ô TextBox** (góc dưới-phải, Margin `0,0,6,8` như ô nhập) — xem §6 |
| Row 5 | Auto | **Actions** — "?" + Pin + Settings | Căn phải, Spacing 4, Margin `0,0,0,8`. "?" (chữ "?", SemiBold 12pt, tooltip "Keyboard shortcuts") trước, Pin (glyph E718) giữa, Settings (E713) sau. "?" mở popover shortcut — xem §3 |
| Row 6 | Auto | **DictionaryBarControl** — link tra từ điển | |
| Row 7 | Auto | **Footer** — link GitHub căn giữa + build text căn phải | |

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
- **Nút "?" — popover shortcut (Manh 17/11, "?" trước Pin):** bấm mở Flyout native neo vào nút (rộng 340, mở xuống dưới, tự lật lên trên khi không đủ chỗ). Nội dung: tiêu đề "Keyboard shortcuts" + 10 dòng theo đúng thứ tự Settings → Keyboard Shortcuts (Open translate panel đầu tiên). Mỗi dòng: tên hành động + phím tắt hiện tại (lấy từ Settings lúc mở, "Control" rút gọn thành "Ctrl") + nút ▶ (glyph Play E768) chạy trực tiếp — gọi đúng code path như bấm phím tắt/tray; chỉ nút ▶ mới chạy, bấm vào dòng thì không. Chạy xong popover đóng lại. Lúc popover mở, panel tạm tắt tự ẩn khi bấm ra ngoài + Esc (popover là cửa sổ riêng ngoài khung panel); đóng popover là bật lại. Loa/Copy KHÔNG đụng — giữ trong ô output.

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

## 6. Bug che loa/Copy — nguyên nhân đã tìm ra, fix #161 chờ xác nhận

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
- **Popover shortcut:** bấm "?" → bảng hiện đủ 10 dòng theo thứ tự Settings, phím tắt đúng giá trị hiện tại; bấm nút ▶ chạy đúng hành động (vd OCR mở khung chọn vùng, Settings mở cửa sổ Settings); bấm ra ngoài/bấm lại "?" → bảng đóng, panel ở lại; Esc lúc bảng mở → chỉ đóng bảng, panel ở lại.

## 10. Layout đa tab (hub) — quyết định 17/11 (ĐÃ triển khai trên main)

**Khung đã lên main.** Sau đó mới tạo nhánh `feature/fast-read-rsvp` làm Fast Read window (xem dưới).

- **Kiểu "hub":** panel có 3 tab, tab ở giữa là màn chính, 2 bên là 2 nhánh.
  **panel dịch (ở giữa, mặc định):** mở panel là thấy Dịch.
- **panel shortcut (tạm thời, ở trái)** — ô linh hoạt, sau này thay được
  (không cố định là shortcut).
- **panel Fast Read (tên tạm, ở phải)** — tạm trống/placeholder tới khi Fast Read ship.
- **3 chấm hiện từ đầu để trải nghiệm** (thay điều kiện cũ "chấm của panel Fast Read chỉ hiện khi tab
  có nội dung thật"; ngoại lệ chỉ áp cho panel Fast Read đã có kế hoạch cụ thể).
- **Lý do (Manh):** panel dịch ở giữa thì vuốt sang 2 bên đều 1 lần; để panel dịch ở trái
  thì sang panel Fast Read phải qua 2 lần. Có tiền lệ (Snapchat: camera ở giữa làm mặc
  định, vuốt trái sang chat, vuốt phải sang stories). Khác thứ tự research cũ
  (panel dịch từng đứng đầu) nhưng lý do chính đáng nên làm theo hướng này.
- **Nút "?" bảng shortcut nằm trong panel dịch (ở giữa, mặc định)**, ngay trước nút Pin (§3).
- **Hàng chấm — triển khai (Manh chốt vị trí 12:51): hàng MỚI (row 8), center, nằm dưới icon GitHub ở footer** (không nằm trong hàng Footer, không nằm trong hàng Actions). RootGrid 8 → 9 hàng; TitleBar (row 0) và Footer (row 7) giữ nguyên mọi tab; vùng nội dung tab = rows 1–6. Chấm 8px, cách nhau 8px, chấm đang chọn xanh #0EA5E9, còn lại xám #C8C8C8. Hàng cao ~20 (top margin 4 + chấm 8 + bottom margin 8); **bottom margin 8 = ResizeStripThickness** — dải resize native chiếm 8px dưới đáy cửa sổ, nên chấm phải nằm hẳn trên dải đó mới bấm được. **Hàng mới cộng ~20 vào chiều cao tối thiểu (ComputeMinPanelHeight cộng mọi hàng Auto), panel mở cao hơn 450 cũ — Manh duyệt tăng height (12:51).** Số chính xác đo khi test bản build.
- **panel shortcut — triển khai:** tiêu đề "Keyboard shortcuts" + 10 dòng y hệt bảng "?" (tên + phím tắt + nút Run ▶), dựng bằng cùng hàm `BuildShortcutRows` từ cùng list `_shortcuts` nên 2 chỗ không bao giờ lệch nhau; bấm Run xong về lại panel dịch để thấy kết quả. Nội dung cuộn được (ScrollViewer) nếu 10 dòng không vừa.
- **panel Fast Read — triển khai (placeholder):** icon ReadingMode (glyph E736, Segoe Fluent Icons, đã kiểm chứng trên Microsoft Learn 17/11) + "Fast Read" + "Coming soon", căn giữa.
- **Hành vi:** mở panel luôn về panel dịch (`ShowPanelNear`/`ShowInputMode` gọi `ShowTab`); chuyển tab chỉ trong phiên, không lưu; panel không đổi kích thước khi chuyển tab — trừ safety net: chuyển về panel dịch mà panel đang thấp hơn chiều cao tối thiểu của panel dịch (do kéo nhỏ ở tab khác, lúc đó các hàng dịch thu lại nên mức kẹp chỉ là 450) thì tự cao lên tới min, chỉ cao lên, không đổi rộng, để card output không tràn dưới hàng Actions (kiểu lỗi #164). **Snap-back (dọc + ngang) chỉ chạy ở panel dịch** — tab khác không có text dịch để đo nên kéo ở đó chỉ kẹp min/max. Pin/Settings chỉ ở panel dịch. **KHÔNG vuốt** — WinUI không có sẵn vuốt cho panel tự làm, phải tự chế (Manh chưa duyệt).
- **Thứ tự làm:** khung multi-tab → Fast Read window → TTS (Fast Read làm trước
  TTS, như mac). **Tab RSVP trong panel chỉ là thử nghiệm sau này**, không làm
  trong nhánh Fast Read — nhánh đó chỉ làm **window riêng chạy RSVP, UX copy y xì
  SwiftRead, CHỈ ĐỌC, không gồm TTS trong read** (SwiftRead có Listen nhưng
  TransPeek không copy phần đó; nghe là việc của TTS). Engine + luồng vào theo
  `docs/RESEARCH/tts-speechify-naturalreader/rsvp-report.md` trên repo mac
  (§6.1 + §6.3 cho Windows/C#).

## Liên kết

- [Icon float UX](FLOAT_ICON_UX_SPEC.md)
- [Tổng quan UX](APP_UX_OVERVIEW.md)
- [Kiến trúc](../../ARCHITECTURE.md)
- [Việc đang làm / chờ](../../TASK_QUEUE.md)
- [Trạng thái dự án](../../PROJECT_STATE.md)
- [Chỉ mục UX](../../UI_DESIGN.md)
