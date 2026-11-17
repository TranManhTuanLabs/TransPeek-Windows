# Build From Zero — TransPeek-Windows

> Hướng dẫn build app từ máy trắng (chưa cài gì), từ lúc clone tới lúc chạy được.
> Checklist test sau khi build xong: `WINDOWS-v0.1-TESTING.md`.
> Kiến trúc tổng: `ARCHITECTURE.md`. Quy tắc làm việc: `AGENTS.md`.

## 1. Cần gì (máy Windows)

- **Windows 10 (1809+) / Windows 11**, 64-bit.
- **Visual Studio 2022 (17.8+)** với workload **.NET desktop development**.
- **.NET 8 SDK** (đi kèm VS2022 khi chọn workload trên).
- **Windows SDK 10.0.22000+** (VS2022 cài mặc định thường có sẵn).
- **Git**.
- Không cần cài thêm gì khác để build: app self-contained, publish kèm .NET runtime.

## 2. Clone + build

```powershell
git clone https://github.com/TranManhTuanLabs/TransPeek-Windows.git
cd TransPeek-Windows
```

- Mở `TransPeek.sln` bằng Visual Studio 2022 → Build **Release | x64**.
- Hoặc bằng lệnh (cần .NET 8 SDK):

```powershell
dotnet build TransPeek.sln -c Release
```

## 3. Chạy

```
src\TransPeek\bin\x64\Release\net8.0-windows10.0.22000.0\TransPeek.exe
```

- App **unpackaged** — chạy trực tiếp, không cần cài đặt.
- Lần đầu chạy: cửa sổ Settings mở ngay; icon tray hiện ở khay hệ thống
  (Windows 11 có thể giấu icon mới trong overflow — bấm mũi tên hiện khay để thấy).
- Thoát hẳn bản cũ trước khi mở bản mới (tránh test nhầm bản cũ còn chạy ở tray):

```powershell
taskkill /F /IM TransPeek.exe
```

## 4. Không muốn build? Tải bản có sẵn

- **Trang Releases** của repo: bản pre-release mới nhất `v0.1.3.<số run>`
  (file `TransPeek-Windows-<run>.zip`, ~150MB vì đã nhúng dữ liệu OCR eng+vie).
  Giải nén rồi chạy `TransPeek.exe` trong đó.
- **Tab Actions → run Build mới nhất đang xanh → Artifacts**:
  `TransPeek-Windows-v0.1`. Artifact chỉ lưu 7 ngày.
- Bản build sẵn là self-contained (đã gồm .NET runtime) nên chạy được trên
  máy Windows 10/11 chưa cài gì thêm.
- Chi tiết CI (khi nào chạy, khi nào build app, đọc run đỏ):
  `TEST-BUILD-WORKFLOW-GITHUB/GIT-CI-BUILD-SETUP.md` + `TEST-BUILD-WORKFLOW-GITHUB/BUILD-TEST-WORKFLOW-GITHUB-CI.md`.
- Bản build sẵn là self-contained (đã gồm .NET runtime) nên chạy được trên
  máy Windows 10/11 chưa cài gì thêm.

## 5. Test logic thuần (không cần Windows)

```bash
dotnet test tests\TransPeek.Tests\TransPeek.Tests.csproj
```

- Chỉ test logic Core trên `net8.0` — chạy được cả trên Linux, không cần máy Windows.
- Test UI/Windows-only kiểm chứng bằng CI + test live trên máy thật.

## 6. CI tự động làm gì

- File `.github/workflows/build.yml`: chạy mỗi lần push lên `main`
  (cũng chạy khi mở PR hoặc bấm Run workflow tay):
  Restore → Build (Release) → Test → Publish (self-contained win-x64) →
  Upload artifact.
- **Chỉ build app + up artifact khi commit có `[build-app]`** trong message.
  Commit chỉ sửa docs (`docs/**`, `*.md`) không trigger CI.
- Mỗi build `[build-app]` tạo thêm **pre-release** (tag `v0.1.3.<số run>`,
  chỉ giữ mới nhất; bản cũ tự xoá). Stable sau này là 0.1.4, tạo thủ công.
- Bước CI tải bundle Tesseract eng+vie vào `publish/tessdata`
  (fail build nếu file < 1MB).

## 7. Xử lý lỗi thường gặp

- **SmartScreen / "unidentified developer":** app chưa ký bằng cert thương mại
  (đang ký CI bằng cert tự ký; SignPath Foundation $0 đang chờ làm cho 0.1.4).
  Bấm "More info" → "Run anyway" để chạy.
- **Chạy exe mà không thấy gì:** icon tray bị giấu trong overflow (xem §3);
  hoặc bản cũ còn chạy ở tray — tắt hẳn bằng `taskkill` rồi mở lại.
- **App crash lúc khởi động:** kiểm tra Event Viewer → Windows Logs →
  Application, tìm lỗi `.NET Runtime`; crash log ở
  `%LOCALAPPDATA%\TransPeek\crash.log`.
- **Build lỗi thiếu SDK 22000:** `ScreenCaptureService` cần
  `GraphicsCaptureItem.TryCreateFromDisplayId` (chỉ có từ SDK 22000) —
  cài Windows SDK 10.0.22000+ qua Visual Studio Installer.
- **Lỗi 429 lúc dịch lần đầu:** Google Translate giới hạn request — app tự retry
  tối đa 3 lần (backoff 1s/2s/4s); hết retry vẫn 429 thì báo rõ, thử lại sau ít phút.

## Liên kết

- [Setup git, CI test + build workflow](TEST-BUILD-WORKFLOW-GITHUB/GIT-CI-BUILD-SETUP.md)
- [CI workflow chi tiết](TEST-BUILD-WORKFLOW-GITHUB/BUILD-TEST-WORKFLOW-GITHUB-CI.md)
- [Checklist build + test live](WINDOWS-v0.1-TESTING.md)
- [Kiến trúc](ARCHITECTURE.md)
- [Quy tắc làm việc](AGENTS.md)
- [Trạng thái dự án](PROJECT_STATE.md)
- [Bản đồ docs](STRUCTURE_MAP.md)
