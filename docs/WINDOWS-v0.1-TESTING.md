# Windows v0.1 — build & live-test checklist

## Build (máy Windows của Manh)

- Cần: Visual Studio 2022 (17.8+) với workload **.NET desktop
  development**, .NET 8 SDK, Windows SDK 10.0.19041+.
- Mở `TransPeek.sln` → Build **Release | x64**.
- Chạy: `src\TransPeek\bin\x64\Release\net8.0-windows10.0.19041.0\TransPeek.exe`
  (unpackaged — chạy trực tiếp, không cần cài đặt).
- Test logic thuần (không cần Windows, chạy được cả trên Linux):
  `dotnet test tests\TransPeek.Tests\TransPeek.Tests.csproj`

## CI (GitHub Actions, Windows runner)

File `.github/workflows/build.yml` **không có trong repo** — GitHub App
không đẩy được file workflow (403). Manh tự tạo trên web GitHub:

1. Mở repo → **Add file → Create new file**.
2. Tên file: `.github/workflows/build.yml`
3. Dán nội dung dưới đây → **Commit changes** (commit thẳng lên `main`).

```yaml
name: Build
on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore
        run: dotnet restore TransPeek.sln
      - name: Build
        run: dotnet build TransPeek.sln -c Release --no-restore
      - name: Test
        run: dotnet test tests/TransPeek.Tests/TransPeek.Tests.csproj -c Release --no-build
```

Sau khi commit, vào tab **Actions** xem run `Build` xanh hay đỏ.
Nếu `dotnet build` lỗi trên runner, báo lại cho Muse (có thể cần
chuyển sang `msbuild` qua `microsoft/setup-msbuild`).

## Những chỗ cần CI xác minh đầu tiên

Code trên VM Linux không build được nên các điểm sau CHƯA được kiểm
chứng — CI sẽ là lần kiểm tra đầu tiên:

1. `ScreenCaptureService`: `GraphicsCaptureItem.TryCreateFromDisplayId(area.DisplayId)`
   (chuyển `Microsoft.UI.DisplayId` → WinRT `DisplayId`) và
   `surface.As<IDirect3DDxgiInterfaceAccess>()` + `GetInterface(Guid, out …)`.
   Nếu lỗi biên dịch, đường BitBlt fallback vẫn giữ OCR chạy được.
2. `TrayIconService`: API `H.NotifyIcon.WinUI` (`TaskbarIcon`,
   `ForceCreate()`, `LeftClickCommand`, `ContextMenu`).
3. `SelectionMonitor`: API FlaUI (`GetFocusedElement()`,
   `Patterns.Text.PatternOrDefault`, `GetSelection()`).
4. `Package.appxmanifest` tham chiếu logo `Assets\*.png` chưa tồn tại
   (v0.1 không có binary asset). Với build unpackaged, manifest
   không được đóng gói nên thường chỉ là warning — nếu build báo lỗi
   về asset, cần thêm PNG placeholder hoặc bỏ thuộc tính Logo.

## Live test trên máy Windows (Manh)

### 1. Khởi động
- [ ] Chạy exe → icon TransPeek hiện ở system tray (khay hệ thống),
      không có cửa sổ chính nào mở ra.
- [ ] Right-click tray icon → menu: Settings / Translate selection /
      Capture & OCR / Quit.

### 2. Dịch từ vùng chọn (base mirror)
- [ ] Bôi chọn chữ trong Notepad → icon `A 文` hiện gần chỗ chọn.
- [ ] Hover icon ~300 ms → panel dịch mở ra, có bản dịch tiếng Việt,
      dòng "via Google Translate".
- [ ] Click icon → panel mở ngay (không đọc to — v0.1 chưa có TTS).
- [ ] Bấm ra ngoài panel → panel đóng. Bấm ✕ → đóng. Esc → đóng.
- [ ] Bấm nút Copy → clipboard có bản dịch.
- [ ] `Ctrl+Alt+T` khi đang bôi chọn → panel mở với bản dịch.
- [ ] Tắt "Theo dõi vùng chọn tự động" trong Settings → bôi chọn
      không hiện icon nữa; `Ctrl+Alt+T` vẫn dịch được.

### 3. Từ điển (bottom panel)
- [ ] Panel có 4 nút: Oxford / Cambridge / Google Dictionary / YouGlish.
- [ ] Bấm Oxford → mở trình duyệt đúng trang từ của từ đã chọn.
- [ ] Favicon của từng trang hiện dần lên (cần mạng); mất mạng thì
      nút vẫn bấm được, icon là chữ cái đầu.
- [ ] Settings → Dictionary: thêm link mới (vd: `https://www.merriam-webster.com/dictionary/{word}`)
      → hiện thêm nút ở panel; đổi Display mode sang "Chỉ icon" →
      panel chỉ còn icon.

### 4. OCR
- [ ] `Ctrl+Shift+S` → màn hình tối lại, kéo chuột chọn vùng có chữ
      → panel hiện text gốc + bản dịch; có 2 nút [Copy gốc] / [Copy dịch].
- [ ] Bản dịch tự copy vào clipboard (paste ra kiểm tra). Tắt
      "Tự copy bản dịch sau OCR" → không tự copy nữa.
- [ ] Settings → OCR → "Tải vie + eng" → tải xong, trạng thái báo
      "Ready (eng, vie)". OCR thử ảnh tiếng Việt (sách/web) → nhận
      đúng dấu tiếng Việt.
- [ ] Đổi chế độ sau OCR sang "OCR → copy thẳng" → chụp xong chỉ
      toast "Đã copy", không mở panel.
- [ ] Esc khi đang chọn vùng → hủy, không làm gì.

### 5. Translation providers
- [ ] Settings → Translation: đổi provider chính sang OpenAI (chưa
      nhập key) → dịch thử → panel báo lỗi "API key not configured"
      rồi fallback sang Google (bản dịch vẫn hiện).
- [ ] Thêm custom provider DeepSeek + nhập key thật → dịch thử →
      dòng "via DeepSeek".

### 6. Settings → About + Check for Updates
- [ ] About hiện "TransPeek", "Version 0.1.0 (build 0.1.0.0)".
- [ ] Link repo mở đúng `github.com/TranManhTuanLabs/TransPeek-Windows`.
- [ ] Link "Release notes (v0.1.0)" trỏ tới
      `.../releases/tag/v0.1.0` (404 là ĐÚNG cho tới khi bản v0.1.0
      được phát hành lần đầu).
- [ ] Bấm **Check for Updates** khi chưa có release nào → báo
      "Chưa có bản release nào trên GitHub." (không crash).
- [ ] Tắt mạng rồi bấm Check for Updates → báo lỗi nhẹ, không crash.
- [ ] Sau khi có release v0.2.0 (lúc phát hành sau): bấm Check for
      Updates → báo "Có bản mới: v0.2.0" + nút "Tải bản mới v0.2.0"
      mở trang release trên trình duyệt.

### 7. Khác
- [ ] Settings → General: bật "Mở cùng Windows" → khởi động lại máy
      → app tự chạy (icon tray hiện). Tắt đi → không tự chạy nữa.
- [ ] Quit từ tray menu → app thoát hẳn (icon tray biến mất).

## Báo lỗi về cho Muse

Chụp màn hình lỗi + ghi lại bước làm trước đó. Nếu app crash lúc khởi
động, kiểm tra Event Viewer → Windows Logs → Application, tìm lỗi
`.NET Runtime` và gửi kèm.
