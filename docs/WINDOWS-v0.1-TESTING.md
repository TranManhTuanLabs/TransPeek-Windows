# Windows v0.1 — build & live-test checklist

## Build (máy Windows của Manh)

- Cần: Visual Studio 2022 (17.8+) với workload **.NET desktop
  development**, .NET 8 SDK, Windows SDK 10.0.22000+ (VS2022 cài
  mặc định thường có sẵn).
- Mở `TransPeek.sln` → Build **Release | x64**.
- Chạy: `src\TransPeek\bin\x64\Release\net8.0-windows10.0.22000.0\TransPeek.exe`
  (unpackaged — chạy trực tiếp, không cần cài đặt).
- Test logic thuần (không cần Windows, chạy được cả trên Linux):
  `dotnet test tests\TransPeek.Tests\TransPeek.Tests.csproj`

## Tải bản build sẵn (không cần cài VS)

- Vào tab **Actions** của repo → mở run **Build** mới nhất đang xanh
  → kéo xuống mục **Artifacts** → tải `TransPeek-Windows-v0.1`
  → giải nén rồi chạy `TransPeek.exe` trong đó.
- Bản này là self-contained (đã gồm .NET runtime) nên chạy được trên
  máy Windows 10/11 chưa cài gì thêm; file zip hơi nặng (~100–200MB).
- Artifact chỉ lưu 30 ngày — quá hạn thì báo Muse chạy lại CI để có
  bản mới.

## CI (GitHub Actions, Windows runner)

File `.github/workflows/build.yml` đã có trong repo, chạy tự động mỗi
lần push lên `main` (cũng chạy khi mở PR hoặc bấm Run workflow tay):
Restore → Build (Release) → Test → Publish (self-contained win-x64)
→ Upload artifact `TransPeek-Windows-v0.1`.

**Checkpoint 2026-11-08: Build run #13 (commit `d08da85`) SUCCESS
toàn bộ — Restore + Build + Test đều xanh, 59/59 test pass.**
Xem tại tab Actions của repo.

## Những chỗ CI đã xác minh (Build #13 xanh)

Các điểm sau đã được kiểm chứng bằng lần biên dịch + test đầu tiên
trên runner Windows thật:

1. `ScreenCaptureService`: `GraphicsCaptureItem.TryCreateFromDisplayId`
   chỉ có từ SDK 22000 (đã nâng TargetFramework lên
   `net8.0-windows10.0.22000.0`); chuyển `Microsoft.UI.DisplayId` →
   `Windows.Graphics.DisplayId` bằng `new Windows.Graphics.DisplayId
   { Value = area.DisplayId.Value }`; `GetInterface` là hàm generic
   của CsWinRT: `GetInterface<ID3D11Texture2D>(out var texture)`.
   Trên Win10 cũ, `TryCreateFromDisplayId` sẽ throw lúc runtime và
   code rơi về đường BitBlt fallback.
2. `TrayIconService`: API `H.NotifyIcon.WinUI` 2.3.2 — `TaskbarIcon`
   có `IconSource` (BitmapImage), `ForceCreate()`,
   `LeftClickCommand` / `RightClickCommand`, gắn menu bằng
   `ContextFlyout = menu` (không có property `ContextMenu`).
3. `SelectionMonitor`: API FlaUI 5.0.0 — `FocusedElement()` (không
   có `GetFocusedElement()`), `TextRange.GetText(-1)` (không có
   property `ITextRange.Text`).
4. `Package.appxmanifest` tham chiếu logo `Assets\*.png` chưa tồn
   tại nhưng build unpackaged vẫn xanh — chỉ là warning, không lỗi.

## Live test trên máy Windows (Manh)

### 1. Khởi động
- [ ] Chạy exe → cửa sổ **Settings mở ngay** (v0.1.1: app tray-only
      nhưng phải có giao diện nhìn thấy được; Windows 11 giấu icon
      tray mới trong overflow nên v0.1 chạy mà như không chạy).
- [ ] Toast góc phải dưới báo app đang chạy ở khay hệ thống.
- [ ] Đóng Settings bằng nút ✕ → app vẫn chạy (icon tray còn).
      Bấm **trái** icon tray → Settings mở lại được (v0.1.1: v0.1
      giữ instance Settings đã đóng nên mở lại không lên).
- [ ] Right-click tray icon → menu tiếng Việt: Cài đặt / Dịch vùng
      chọn / Chụp & OCR / Thoát TransPeek.
- [ ] Không còn tình trạng "chạy exe mà không thấy gì".

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
- [ ] **Panel v0.1.2 (ngang mac):** sửa chữ trong ô nguồn rồi bấm
      Enter → dịch lại ngay (Shift+Enter xuống dòng); đổi picker
      ngôn ngữ nguồn/đích hoặc bấm ⇄ trong panel → dịch lại ngay
      với ngôn ngữ mới; nút "Copy gốc" luôn có.
- [ ] **Tra từ theo text chọn trong panel (v0.1.2):** bôi chọn 1 từ
      trong ô nguồn hoặc trong bản dịch → các link từ điển ở bottom
      panel đổi sang tra từ đó ngay; bỏ chọn (bấm 1 cái vào text) →
      link quay về tra text gốc mở panel.

### 3. Từ điển (bottom panel)
- [ ] Panel có 4 nút: Oxford / Cambridge / Google / YouGlish.
- [ ] Bấm Oxford → mở trình duyệt đúng trang từ của từ đã chọn.
- [ ] Bôi chọn từ viết hoa đầu câu (vd "Hello") → Oxford mở
      `.../hello` chữ thường, không mở `.../Hello` báo lỗi (v0.1.2).
- [ ] Bôi chọn cả cụm (vd "hello world") → YouGlish tra cả cụm,
      không chỉ lấy từ đầu tiên (v0.1.2, template `{text}` như mac).
- [ ] Favicon của từng trang hiện dần lên (cần mạng); mất mạng thì
      nút vẫn bấm được, icon là chữ cái đầu.
- [ ] Đổi Display mode sang "Chỉ icon" → hover từng icon hiện
      tooltip tên từ điển (v0.1.2); thêm nhiều link → bottom panel
      cuộn ngang được, không tràn/cắt.
- [ ] Settings → Dictionary: thêm link mới (vd: `https://www.merriam-webster.com/dictionary/{word}`)
      → hiện thêm nút ở panel; thêm xong 2 ô nhập tự xóa trắng;
      bấm "Reset to Defaults" → về lại 4 link mặc định (v0.1.2).

### 4. OCR
- [ ] `Ctrl+Shift+S` → màn hình tối lại, kéo chuột chọn vùng có chữ
      → panel hiện text gốc + bản dịch; có 2 nút [Copy gốc] / [Copy dịch].
- [ ] **Thoát màn hình chọn vùng (v0.1.1, lỗi Manh gặp): Esc, chuột
      phải, nút "✕ Hủy (Esc)" góc phải trên, hoặc Alt+F4 — cả 4 cách
      đều phải thoát ngay, không kẹt toàn màn hình. V0.1 chỉ có Esc
      qua KeyboardAccelerator trên Grid không focus nên Esc chết,
      không có nút Hủy, Alt+F4 làm luồng OCR treo mãi.**
- [ ] Esc khi đang chọn vùng → hủy, không làm gì.
- [ ] Bản dịch tự copy vào clipboard (paste ra kiểm tra). Tắt
      "Tự copy bản dịch sau OCR" → không tự copy nữa.
- [ ] Settings → OCR → "Tải vie + eng" → tải xong, trạng thái báo
      "Ready (eng, vie)". OCR thử ảnh tiếng Việt (sách/web) → nhận
      đúng dấu tiếng Việt.
- [ ] Đổi chế độ sau OCR sang "OCR → copy thẳng" → chụp xong chỉ
      toast "Đã copy", không mở panel; ở chế độ này toggle "Tự copy
      bản dịch" bị khóa (không có bản dịch để copy, v0.1.2).

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

## Checklist v0.1.3 — English UI (localization base)

- App opens in English everywhere: tray menu (Settings / Translate selection / Capture & OCR / Quit TransPeek), Settings nav (General / Translation / Dictionary / OCR / About), panel buttons (Copy / Copy source / Copy translation / Capture OCR), region-select hint + Cancel button.
- Startup toast, "Settings saved", "No text selected to translate" all in English.
- Settings pages: every header, hint and button in English; On/Off toggles say On/Off.
- OCR flow in English: first-run download toasts, "No text recognized in the selected region", CopyOnly toast says "Copied".
- About → Check for Updates: "Checking…", up-to-date / update-available / error states in English.
- Language picker still lists each language in its own name (Tiếng Việt, 中文 (简体), 한국어…) — that is deliberate, not a leftover.
- No Vietnamese left in the UI except language names. (Comments in code are still partly Vietnamese — dev-facing only.)
