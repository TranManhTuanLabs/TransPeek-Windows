# Main Navigation — UX Spec (bản nháp)

> Trạng thái: **bản nháp** — ghi lại thứ tự menu tray + cấu trúc Settings đã chốt,
> chờ Manh duyệt rồi mới thành spec chốt.

## 1. Menu tray (thứ tự đã chốt)

Header (TransPeek + version + build) →
"Text Capture from Screen" →
Manual Translation (E765) →
OCR Screen Translate (E8FE) →
OCR to Clipboard (E8C8) →
OCR to Clipboard - Translated (E774) →
Translate Selection (E70F) →
Translate Clipboard (E77F) →
Screenshot Copy (E722) →
"Barcodes / QR Codes" →
Scan Barcodes/QR Codes (EC5A) →
Settings… (E713) →
Diagnose: list windows (E946) →
Restart TransPeek (E72C) →
Quit TransPeek (E711).

- Gợi ý phím chỉ ở 3 mục: OCR Screen Translate, OCR to Clipboard - Translated,
  Translate Clipboard.
- "Restart TransPeek": Manh nhận xét "hơi vô nghĩa" nhưng **chưa bảo bỏ** — giữ nguyên.
- "Diagnose: list windows": giữ, nằm TRÊN "Restart TransPeek".

## 2. Cửa sổ Settings

- `NavigationView` (PaneDisplayMode Left, `OpenPaneLength="250"`).
- Tab: **General, Translation, Dictionary, OCR, TTS Speech, About** — **không có tab
  Appearance** (Appearance là mục trong trang General).
- Nav pane 250: Manh **chưa phán quyết** (nếu chật → bỏ `OpenPaneLength` về 320).
- Trang General, thứ tự: đầu mục **Keyboard shortcuts** → HotkeyHint → 10 ô hotkey →
  behaviour (Launch with Windows → Watch selection automatically → How long to hover →
  Trigger icon hides after) → **Appearance** → **Setting Text Size** → **Popup Panel** →
  **Source & Translation Text Size** → **App name & Slogan Font Size** → footer đường dẫn settings.
- 10 ô hotkey = tên item tray (xem `FEATURES/SETTINGS.MD` khi viết xong).
- Chi tiết từng trang Settings: xem `SETTING_UX.MD` — **CHƯA VIẾT**.
