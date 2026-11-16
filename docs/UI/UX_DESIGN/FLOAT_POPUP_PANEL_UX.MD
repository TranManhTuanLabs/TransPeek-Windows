# Float / Popup Panel — UX Spec (CHỐT)

> **Trạng thái: CHỐT — 17/11/2026, theo build #162 (commit `2053f9b`).**
> Các con số trong doc này là **spec**. Đổi bất kỳ số đã chốt nào cũng cần Manh duyệt trước.
> Spec đã bao gồm: cấu trúc card 4 hàng (#160) + hint 1 dòng (#161) + loa/Copy overlay
> trong ô output (#162).
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
| Row 5 | Auto | **Actions** — Pin + Settings | Căn phải, Spacing 4, Margin `0,0,0,8`. Pin (glyph E718) trước, Settings (E713) sau |
| Row 6 | Auto | **DictionaryBarControl** — link tra từ điển | |
| Row 7 | Auto | **Footer** — link GitHub căn giữa + build text căn phải | |

### Cỡ chữ theo Settings (chạy lúc mở panel và khi lưu Settings)

- **"App name & Slogan Font Size"** (`PopupTopBarFontSize`, mặc định **17**, slider 12–24): tên app = N, slogan = N−1 (sàn 8), hint = N−4 (sàn 8). Mặc định: 17 / 16 / 13. Đáy TitleBar = cỡ hint.
- **"Source & Translation Text Size"** (`PopupFontSize`, mặc định **14**, slider 12–24): chữ trong cả 2 ô Source + Translation. Kéo slider là panel giãn live theo (`RequestResizeToFit`).
- Slogan giữ nguyên "Translate anywhere, Peek instantly!" — chỉ hoa T và P (Manh cố ý), không đổi thành Title Case.

## 3. Hành vi (behaviour)

- **Auto-fit mỗi lần panel hiện:** đo nội dung rồi vừa khung; text dài cuộn trong ô output (cap 260), panel không tự dãn.
- **Snap-back NGANG:** kéo rộng thừa cho text ngắn → thả ra co về rộng mặc định 420; text dài thì giữ nguyên rộng đã kéo.
- **Snap-back DỌC:** kéo cao thừa → thả ra co về chiều cao nội dung.
- **Kéo tay chỉ to ra thêm trong phiên đó.** Settings chỉ đặt giá trị mặc định; **mỗi lần mở panel về đúng mặc định (420×450)**. Cỡ kéo tay KHÔNG lưu vào Settings.
- **Thu/mở provider card:** thu thì card về MinHeight 0, panel thụt theo (auto-fit lại ngay cả sau khi đã kéo tay).
- **Kéo di chuyển:** bằng vùng caption là TitleBar (Row 0). **Kéo resize:** viền native (BottomBorder 7, RightBorder 8) + grip trang trí 12×12 ở góc dưới-phải (chỉ trang trí, không phải tay nắm thật).
- **Ẩn panel:** click chuột ra chỗ khác (ClickOutsideMonitor) hoặc Esc. **KHÔNG có nút đóng tay, và cũng KHÔNG CẦN** — workflow của Manh là tra xong click ra chỗ khác, rồi tra cái mới.
- **Mở panel:** mặc định ở kích thước Settings (420×450), không mở compact, không nhớ cỡ kéo cũ.

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

- `src/TransPeek/Views/TranslatePanelWindow.xaml` — layout (RootGrid 8 hàng, card 4 hàng trong, loa/Copy ở hàng Auto riêng)
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

## Liên kết

- [Icon float UX](FLOAT_ICON_UX_SPEC.MD)
- [Tổng quan UX](APP_UX_OVERVIEW.MD)
- [Kiến trúc](../../ARCHITECTURE.MD)
- [Việc đang làm / chờ](../../TASK_QUEUE.MD)
- [Trạng thái dự án](../../PROJECT_STATE.MD)
- [Chỉ mục UX](../../UI_DESIGN.MD)
