# Float / Popup Panel — UX Spec (CHỐT)

> **Trạng thái: CHỐT — 17/11/2026, theo build #159 (commit `93bbd8e`).**
> Các con số trong doc này là **spec**. Đổi bất kỳ số đã chốt nào cũng cần Manh duyệt trước.
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
| Row 2 | Auto | **Hint** — 11pt | "Enter: translate again • Esc: close • Select a word in the panel to look it up in the dictionary" |
| Row 3 | Auto | **Hàng ngôn ngữ** — Source ⇄ Target + Capture OCR | ComboBox Source/Target **rộng 112**, nút Swap ⇄ Padding `4,2`, Spacing `4`. Nút "Capture OCR" ở bên phải |
| Row 4 | `*` | **Output card** (`ProviderCardBorder`) | Margin `0,0,0,8`, CornerRadius 6, BorderThickness 1, `MinHeight 218`. Bên trong 3 hàng: header (tên provider 13pt SemiBold + trạng thái) / divider / TextBox (FontSize 14, MinHeight 60, MaxHeight 260) + **loa/Copy overlay cùng ô TextBox** ở góc dưới-phải (Margin `0,0,6,8`, Spacing 4) |
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

## 6. Vấn đề đang mở (OPEN — không đổi size để sửa)

**Bug che loa/Copy (từ #149, vẫn mở ở #159):** nút loa/Copy ở góc dưới-phải ô output
bị cắt ở mép dưới card khi tra 1–5 line; từ 6 line trở lên không bị.

- Đã đo từ video build 158 (1 word "lea", panel 424×550): đáy card ở y≈417,
  nút bị cắt đúng ở y=417 — chỉ hiện ~20px trên ~28px của nút, thiếu ~8px.
  Nút nằm sát đáy card thay vì cách 8px như Margin bottom 8.
- **Giả thuyết "thiếu cố định ~8px" ĐÃ BỊ BÁC BỎ** bởi dữ kiện 17/11 04:31:
  giờ 3-line cũng bị, chỉ từ 6-line trở lên mới không bị. Nếu chỉ thiếu 8px cố định
  thì 3-line (trước đây không bị) không thể tự nhiên bị, và 6-line không thể đặc biệt hơn.
- **Nguyên nhân thật CHƯA RA.** Đang chờ video/ảnh so sánh 3-line (bị) vs 6-line (không bị)
  trên cùng một bản để điều tra. **Fix phải nằm trong khung size đã chốt ở §1 — không đổi
  số để mò.**

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

- `src/TransPeek/Views/TranslatePanelWindow.xaml` — layout (RootGrid 8 hàng, card, overlay loa/Copy)
- `src/TransPeek/Views/TranslatePanelWindow.xaml.cs` — hằng số (~dòng 50–121),
  `ComputeMinPanelHeight` (~940), `ResizeToFitContent` (~705), `SettleResizedSize` (~1070),
  `RefreshTopBarFontSize` / `RefreshPopupFontSize` (~355–385)
- `src/TransPeek.Core/Settings/AppSettings.cs` — `PanelWidth = 420`, `PanelHeight = 450`,
  migration `SettingsVersion` (v1 reset 440×450, v2 rộng 440→420)
- `src/TransPeek/Views/TranslatePanelWindow.xaml.cs` — `SetProviderCardExpanded`
  (thu/mở card, auto-fit lại)

## Liên kết

- [Icon float UX](FLOAT_ICON_UX_SPEC.MD)
- [Tổng quan UX](APP_UX_OVERVIEW.MD)
- [Kiến trúc](../../ARCHITECTURE.MD)
- [Việc đang làm / chờ](../../TASK_QUEUE.MD)
- [Trạng thái dự án](../../PROJECT_STATE.MD)
- [Chỉ mục UX](../../UI_DESIGN.MD)
