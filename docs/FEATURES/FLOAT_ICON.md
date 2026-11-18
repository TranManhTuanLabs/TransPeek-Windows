# Float Icon (bản nháp)

> Trạng thái: **bản nháp** — mô tả tính năng icon float. Spec UX chi tiết:
> `docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md`.

## 1. Tính năng

- Icon tròn nhỏ hiện gần text user bôi chọn (theo dõi selection tự động).
- **Hover → mở panel dịch.**
- **Click → hành động đã chọn** trong Settings → General → "Click icon": **Speak**
  (mặc định — phát TTS của text đã chọn) hoặc **Fast Read** (mở Window Read).
- Alt-click → hành động còn lại (Click = Speak thì Alt+Click mở Fast Read;
  Click = Fast Read thì Alt+Click nghe). Chuột phải → menu có "Fast Read".
- **Style B (Manh 2026-11-18): icon luôn hiện việc click tiếp theo sẽ làm** —
  Speak = bong bóng E8BD xanh, Fast Read = A+ nghệ tự vẽ; giữ Alt thì đổi sang
  việc còn lại. Sau bất kỳ lần bấm Read nào icon hiện A+ trong **2.5s** rồi mới ẩn.

## 2. Cài đặt liên quan

- **Watch selection automatically** (General → behaviour): bật/tắt theo dõi selection.
- **How long to hover**: ComboBox 300/500/800/1000ms, mặc định 500ms.
- **Trigger icon hides after**: NumberBox (gõ số giây), mặc định 5s.

## 3. Future idea — translate-first icon (NOT implemented, Manh 2026-11-19)

The current A+ default (Style B above) has a real advantage: the user can tell at a
glance that Click is set to Fast Read, without opening Settings. A possible future
change — **not to be done now** — is to follow the translate-first concept instead.
When the Click setting is Fast Read:

- the icon shows the plain translate **A** as normal (hover = translate is the icon's
  primary job);
- a plain click shows **A+** (Fast Read);
- Alt+Click shows the **speaker** (Speak).

So the icon's idle state stays "translate", and A+ / speaker appear as the feedback
for the action that was just triggered — instead of Style B's preview of the *next*
click. Recorded here so the idea is not lost; implement only when Manh asks. Same
note as mac (`docs/UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md` §3, commit `9818f6c` —
Option on mac, Alt on Windows).

## 4. Nợ

- Icon theo dark/light system: **CHƯA LÀM**.
- Icon lúc hiện lúc không khi select text: Manh test thêm.

## Liên kết

- [Icon float UX](../UI/UX_DESIGN/FLOAT_ICON_UX_SPEC.md)
- [Speech Player](SPEECH_PLAYER.md)
- [Tổng quan UX](../UI/UX_DESIGN/APP_UX_OVERVIEW.md)
