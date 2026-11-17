# OCR Text Capture (bản nháp)

> Trạng thái: **bản nháp** — mô tả OCR: Windows OCR + Tesseract, các luồng capture.

## 1. Hai engine OCR

### Tesseract (mặc định cho tiếng Việt)

- **vie ĐÃ NHÚNG sẵn** trong app (bundle eng+vie trong publish, zip ~150MB)
  nên OCR tiếng Việt chạy ngay không cần tải.
- Ngôn ngữ khác: tải trong app qua tick-list (catalog **123 ngôn ngữ**,
  `TesseractLanguageCatalog.cs`, tessdata_best trừ `equ`/`osd`) + ô find +
  cảnh báo dung lượng. **KHÔNG có nút "Download all"**.
- Tải xong chỉ GỢI Ý thêm mã vào `OcrLanguages`.
- Thư mục dữ liệu: `%LOCALAPPDATA%\TransPeek\tessdata`.

### Windows OCR (`Windows.Media.Ocr`)

- **KHÔNG nhúng được, KHÔNG tải ngầm được** — gói ngôn ngữ là tính năng hệ điều hành,
  chỉ cài qua Windows Settings hoặc PowerShell admin.
- App chỉ mở thẳng Settings (`ms-settings:regionlanguage`) cho user tự cài và
  liệt kê ngôn ngữ đã cài (`RefreshWindowsOcrStatus` dùng `SupportedLanguages`).
- Chưa xác minh: Windows OCR có gói cho tiếng Việt không — app kiểm tra lúc chạy
  bằng `IsLanguageSupported("vi-VN")` mới biết chắc.

## 2. Các luồng (theo menu tray)

- **OCR Screen Translate**: chụp màn hình → panel (gốc + dịch).
- **OCR to Clipboard**: chụp → copy thẳng text gốc, chỉ toast 'Đã copy'.
- **OCR to Clipboard - Translated**: OCR → dịch (timeout 30s) → copy bản DỊCH +
  toast "Copied"; lỗi/timeout thì mở panel. Hotkey `Ctrl+Alt+R`.
- **Translate Clipboard**: hotkey `Ctrl+Alt+L`.
- Ảnh OCR lưu ở `%LOCALAPPDATA%\TransPeek\ocr-capture.png`.

## 3. Ngôn ngữ OCR

- mac Vision mặc định `["vi-VN", "en-US"]`. Windows: Tesseract + Windows OCR
  theo ngôn ngữ máy đã cài / đã tải trong app.

## Liên kết

- [Điều hướng chính](../UI/UX_DESIGN/MAIN_NAVIGATION_UX.md)
- [Kiến trúc](../ARCHITECTURE.md)
