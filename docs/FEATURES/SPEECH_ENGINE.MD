# Speech Engine (bản nháp)

> Trạng thái: **bản nháp** — mô tả engine TTS. Spec UX: `docs/UI/UX_DESIGN/`.

## 1. Engine đã chốt cho 0.1.4: giọng hệ thống Windows

- **Chỉ dùng giọng hệ thống** (`Windows.Media.SpeechSynthesis`) cho 0.1.4 —
  Manh chốt "Làm hệ thống theo Windows thôi" (16/11/2026).
- **KHÔNG Edge Read Aloud** (không có API công khai, edge-tts bị 403 tháng 10/2026).
- **KHÔNG Azure** giai đoạn này (chỉ hợp bring-your-own-key sau này, F0 500k ký tự/tháng).
- **KHÔNG SAPI** — Manh chốt 16/11 23:32: "Không làm SAPI hỗ trợ giọng desktop".
  Giọng Natural (Aria/Guy) là Narrator-only, không app nào dùng được.

## 2. Cách hoạt động

- `SystemSpeechService`: 1 `SpeechSynthesizer` + 1 `MediaPlayer` dùng lại.
- `InstalledVoices` = `SpeechSynthesizer.AllVoices` (chỉ đọc kho OneCore —
  `HKLM\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens`; kho SAPI Desktop
  `HKLM\SOFTWARE\Microsoft\Speech\Voices\Tokens` không đọc được qua WinRT).
- `PickVoice`: map zh-Hans→zh-CN, zh-Hant→zh-TW; ưu tiên voice Id từ Settings +
  EnglishAccent + tốc độ.
- Tốc độ **0.5x–2x**. Tab TTS Speech: Service chỉ "System built-in", giọng hệ thống
  EN/VI, auto-play.
- "Add voices" mở Language & region (`ms-settings:regionlanguage`).

## 3. Phát hiện ngôn ngữ

- `ITranslationProvider.TranslateAsync` trả `TranslationProviderResult(Text, DetectedSourceLanguage)`
  (Google parse trường `src`) — loa panel không còn "luôn giọng An".
- Click icon float phát tiếng qua `_iconSpeech` + heuristic `DetectLanguage` (VI/EN).

## Liên kết

- [Speech Player](SPEECH_PLAYER.MD)
- [Kiến trúc](../ARCHITECTURE.MD)
- [Tổng quan UX](../UI/UX_DESIGN/APP_UX_OVERVIEW.MD)
