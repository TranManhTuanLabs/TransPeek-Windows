# Float Icon (bản nháp)

> Trạng thái: **bản nháp** — mô tả tính năng icon float. Spec UX chi tiết:
> `docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md`.

## 1. Tính năng

### Glyph mapping đã chốt (Manh 2026-11-19 — đừng đổi nữa)

1 glyph = 1 hành động, đã chốt cho cả mac + Windows:

| Glyph | Hành động |
|---|---|
| **A thường** (15pt, KHÔNG bold, chữ A màu xanh dương — Windows `#0078D4`) | **Translate** (icon chính) |
| **A+** (chữ A đậm + dấu + nhỏ, nghệ #C7800D) | **Fast Read** |
| **Loa** (E767 Volume) | **TTS / Speak** |

### Hai style của icon translate (Manh 2026-11-19)

- **Style 1 — mẫu chuẩn / mẫu chính:** chữ A thường, KHÔNG bold, chữ A
  màu xanh dương. "Blue" là màu của CHÍNH CHỮ A — không phải viền vòng
  tròn hay nền highlight. Ví dụ của Manh: như icon ô chat Style 2 — chữ
  A màu xanh, và viền ô chat bọc chữ A cũng màu xanh. Đây mới là icon
  translate đúng chuẩn.
- **Style 2 — dùng tạm thay chính:** icon ô chat (speech bubble E8BD).
  Ô chat KHÔNG phải icon translate — chỉ dùng tạm thay cho Style 1, giữ
  làm mẫu style dự phòng trong code (`BubbleMark`, luôn ẩn) + docs.
  Windows đã đổi nhầm A thường sang ô chat từ 1-2 bản trước, Manh test
  build 191 phát hiện, đã cho về lại A thường (2026-11-19).

Chữ A thường (Style 1) là chữ "A" vẽ bằng text (như A+, không phải
FontIcon), KHÔNG bold, chữ A màu xanh dương (Windows `#0078D4`, cùng màu
xanh với ô chat E8BD). Nó là icon chính: bản chất
của icon là "translate", còn A+ / loa cùng họ "A" thể hiện 2 hành động
còn lại.

Windows hiện tại: chữ A thường (`PlainAMark`) là icon chính — hiện lúc
bình thường (idle) bất kể Click là Speak hay Fast Read — màu xanh dương
`#0078D4`, KHÔNG bold — theo đúng mẫu chuẩn từ commit này (Manh
2026-11-19: "Click=Listen thì ... chuyển A không bold sang blue cho đúng
thiết kế"). Trước đó (từ `1ffcbb7`) nó đang là nghệ #C7800D. Vòng tròn
trắng viền xám #C8C8C8 giữ nguyên. (Phần làm rõ "blue" này sửa local từ
19:00 ngày 19/11 mà push bị hết hạn thẻ duyệt, tới nay mới đẩy được cùng
đợt đổi màu A.)

**Ô chat / speech bubble (E8BD) KHÔNG phải icon translate** — không được
thay A thường bằng ô chat. Ô chat chỉ giữ lại làm mẫu style dự phòng trong
code (`BubbleMark`, luôn ẩn) + ghi trong doc này, sau này cần thì bật lại
được. (Windows đã đổi nhầm A thường sang ô chat từ 1-2 bản trước, Manh
test build 191 phát hiện, đã cho về lại A thường ở commit `1ffcbb7` —
ghi rõ ở đây để không tái diễn.)

Lưu ý: A thường KHÔNG bold (Manh sửa lại 2026-11-19). A+ vẫn đậm như cũ.
Mac đã ghi tương tự vào `docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md` §3
(commit `7e5e4cd`).

Từ 2026-11-20 18:09 ý tưởng translate-first ở §3 ĐÃ LÀM (Manh duyệt):
idle LUÔN là A thường; A+ / loa chỉ hiện khi giữ Alt (preview hành động
Alt+click) hoặc làm feedback sau khi bấm (A+ 2.5s sau Read). Style B preview click tiếp theo đã bỏ. Loa E767 giờ màu xanh dương
`#0078D4` (Manh 2026-11-20 18:09 — loa là highlight, không phải glyph tối).

Code gap (mac, ghi để Windows biết): code mac hiện vẫn đang vẽ
`character.bubble` cho preview translate/speak (`TriggerTrackingView.draw()`)
— chưa theo mapping mới, sửa sau khi Manh duyệt. Windows đã theo mapping
này từ commit `1ffcbb7`.

- Icon tròn nhỏ hiện gần text user bôi chọn (theo dõi selection tự động).
- **Hover → mở panel dịch.**
- **Click → hành động đã chọn** trong Settings → General → "Click icon": **Speak**
  (mặc định — phát TTS của text đã chọn) hoặc **Fast Read** (mở Window Read).
- Alt-click → hành động còn lại (Click = Speak thì Alt+Click mở Fast Read;
  Click = Fast Read thì Alt+Click nghe). Chuột phải → menu có "Fast Read".
- **Translate-first (Manh 2026-11-20 18:09 — thay Style B preview click
  tiếp theo, 2026-11-18):** icon bình thường LUÔN là **A thường** xanh
  (hover = translate là việc chính của icon, bất kể Click là Speak hay
  Fast Read). Giữ Alt thì đổi sang preview hành động Alt+click: **A+**
  khi Click=Speak (Alt+click đọc), **loa E767 xanh** khi Click=Fast Read
  (Alt+click nghe). Sau bất kỳ lần bấm Read nào icon hiện A+ trong
  **2.5s** rồi mới ẩn. Icon KHÔNG hiện trạng thái phát nhạc nữa — trạng
  thái đó hiện trên indicator loa riêng (xem `SPEECH_PLAYER.md` §1,
  CHỐT 26/11 23:59). Bong bóng E8BD xanh giờ
  chỉ là **mẫu style dự phòng** — vẫn định nghĩa trong code (`BubbleMark`,
  luôn ẩn) để sau cần thì bật lại.
- Alt cho click đọc từ event con trỏ lúc bấm (`PointerPressed` + `KeyModifiers.Menu`),
  không đọc trạng thái global lúc tap (sửa 0.1.4 — Alt+click khi Click = Read trước
  đây mở Read thay vì nghe; xem spec §2). **Sửa tiếp sau test CI #191
  (Manh 2026-11-19):** nguyên nhân thật là handler click viết `altHeld ||
  clickReads` (OR) thay vì XOR — Click=Read thì Alt+click lúc nào cũng vào
  Read, dù preview (đã dùng XOR) đổi đúng. Sửa 1 dòng thành
  `altHeld != clickReads`: Alt ĐỔI hành động, không ép về Read.

## 2. Cài đặt liên quan

- **Watch selection automatically** (General → behaviour): bật/tắt theo dõi selection.
- **How long to hover**: ComboBox 300/500/800/1000ms, mặc định 500ms.
- **Hover text delay**: ComboBox 200/300/400/500/700ms, mặc định 400ms — chuột phải dừng yên chừng đó trên khối chữ thì icon hover mới hiện (ngay dưới toggle block-text).
- **Trigger icon hides after**: NumberBox (gõ số giây), mặc định 5s.
- **Kích thước icon** (Manh 2026-11-21, mac parity — user chọn trong Settings → General, ngay dưới toggle block-text + "Hover text delay"): **"Float icon size"** (icon bôi chọn, mặc định 24pt — gợi ý 20-24pt, chọn 18/20/22/24/26/28/30/32) và **"Auto-show float icon size"** (icon hover block-text, mặc định 24pt — gợi ý 20, 22pt, chọn 18/20/22/24). Cửa sổ = cỡ chọn, vòng tròn = cỡ − 2, chữ tự co theo vòng: A = 1/2 vòng, loa ≈ 44% vòng, dấu + = 1/4 vòng — ở cỡ cũ 32pt ra đúng A 15pt / + 8pt (loa 13pt, mac 14pt vì vòng mac 32 còn vòng Windows 30). Đọc setting live mỗi lần hiện icon nên đổi là có hiệu lực ngay với icon tiếp theo.

## 3. Translate-first icon — IMPLEMENTED (Manh 2026-11-20 18:09)

Style B's preview-the-next-click (above) had a real advantage: the user could tell at a
glance that Click is set to Fast Read, without opening Settings. Manh chose the
translate-first concept instead. When the Click setting is Fast Read:

- the icon shows the plain translate **A** as normal (hover = translate is the icon's
  primary job);
- a plain click reads (Fast Read) and the icon shows **A+** as feedback for 2.5s after
  the trigger;
- Alt held previews the **speaker** (Alt+Click speaks).

So the icon's idle state stays "translate", and A+ / speaker appear as the Alt preview
or as feedback for the action that was just triggered — instead of Style B's preview of
the *next* click. Same note as mac (`docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md` §3,
commit `9818f6c` — Option on mac, Alt on Windows). Implementation: `UpdateGlyph` in
`TriggerIconWindow.xaml.cs` — `showRead = linger || (!speaking && altHeld && !clickReads)`
(sẽ bỏ `!speaking` khi làm indicator — icon không còn track speaking nữa, CHỐT 26/11 23:59).

## 4. Nợ

- Icon theo dark/light system: **CHƯA LÀM**.
- Icon lúc hiện lúc không khi select text: Manh test thêm.

## Liên kết

- [Icon float UX](../UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md)
- [Speech Player](SPEECH_PLAYER.md)
- [Tổng quan UX](../UI/UX_DESIGN/APP_UX_OVERVIEW.md)
