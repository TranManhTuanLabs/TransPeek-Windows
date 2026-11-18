# OCR + Language Support — Feature Spec

> Purpose: the source of truth for the language numbers and OCR behaviour,
> so the README (and later a store listing) can quote them without
> re-checking code. Marketing-ready lines are in §4.
> Last verified: 2026-11-19 (v0.1.4). Code: `LanguageCode.cs`,
> `TesseractLanguageCatalog.cs`, `TesseractLanguageDataManager.cs`,
> `GoogleTranslateProvider.cs`.

## 1. Translation languages — Google Translate: 249 (+ `pt` alias)

- **249 languages**, for both source and target. Source auto-detects.
  Verified against three sources (Wikipedia "Google Translate" 249,
  Google Cloud NMT language table, `google-translate-api-x@10.7.2`
  `languages.cjs` + TranslateGemma report): the list in code matches
  all three, 0 mismatches.
- **`pt` alias (250th entry):** `pt` is NOT in Google's 249 (only
  `pt-BR` and `pt-PT` are), but the endpoint still accepts it and old
  settings may already store it — so it is kept as an alias, not dropped.
- **Picker UX:** AutoSuggestBox (native searchable picker — ComboBox
  has no live-filter event, see DECISIONS.md CI #189): 20 popular
  languages first, in this order —
  English, Vietnamese, Chinese (Simplified), Chinese (Traditional),
  Japanese, Korean, French, German, Spanish, Portuguese, Russian,
  Italian, Thai, Arabic, Hindi, Indonesian, Turkish, Dutch, Polish,
  Ukrainian — then the full list A–Z. The dropdown is searchable (type
  to filter by native name, English name or code) and capped in height,
  scrolling inside the list instead of expanding to 249 rows.
- **Free, no key:** Google Translate via the free `gtx` endpoint
  (`translate.google.com/translate_a/single?client=gtx`). Translation
  runs on Google's servers, so adding languages costs only a few KB of
  strings — the app size does not grow with the language count.
  Rate-limit (429) retries 3 times with backoff 1s / 2s / 4s.
- **Quality note:** the ~69 languages Google added in June 2024 are
  mostly low-resource languages — translations for them are weaker than
  for major languages. Do not promise equal quality across all 249.
- **Provider setup:** Google is the default; any OpenAI-compatible
  endpoint (OpenAI, DeepSeek, local Ollama/LM Studio…) can be added
  with the user's own key as primary/fallback. Google stays as the
  free safety net when all others fail.

## 2. OCR — Tesseract: 123 languages

- **Engine:** Tesseract (default for Vietnamese) with the
  `tessdata_best` models — the most accurate Tesseract models.
  Built-in Windows OCR (`Windows.Media.Ocr`) is the fallback engine.
- **Catalog: 123 languages.** `tessdata_best` ships 125 `.traineddata`
  files at its repo root; minus `equ` (math equations) and `osd`
  (orientation/script detection) — neither is a language — leaves 123.
  The ~30 script models in the `script/` subdir are excluded:
  languages only (Manh's decision). The catalog is hardcoded in the app
  so the OCR page works offline; a Refresh button fetches the live list
  from GitHub to spot languages added later.
- **Bundled: English + Vietnamese only.** OCR works out of the box with
  no download. The other 121 languages download on demand inside the
  app (tick-list + search + size warning) into
  `%LOCALAPPDATA%\TransPeek\tessdata`. There is deliberately NO
  "Download all" button.
- **Windows OCR:** its language packs are an OS feature — the app can
  neither bundle nor silently download them. The app opens Windows
  Settings (`ms-settings:regionlanguage`) for the user to install packs
  and lists the languages already installed. (Whether a Vietnamese pack
  exists for Windows OCR is still unverified — the app checks at runtime
  with `IsLanguageSupported("vi-VN")`.)
- **Flows (tray menu):** OCR Screen Translate (capture → panel with
  source + translation), OCR to Clipboard (capture → copy source text,
  toast "Đã copy"), OCR to Clipboard - Translated (OCR → translate,
  30s timeout → copy the translation; on error/timeout it opens the
  panel instead), Translate Clipboard. Captured images are saved to
  `%LOCALAPPDATA%\TransPeek\ocr-capture.png`.

## 3. Numbers at a glance

| | Count | Note |
|---|---|---|
| Translation languages (Google) | **249** (+ `pt` alias = 250 entries) | Source + target, auto-detect source |
| Popular languages first in pickers | **20** | See §1 for the order |
| OCR languages (Tesseract) | **123** | All languages `tessdata_best` ships |
| OCR languages bundled in the app | **2** (eng + vie) | Works offline out of the box |
| OCR languages downloadable in-app | **121** | One by one, no "Download all" |

## 4. README-ready lines (draft — not yet in README)

EN:
- "Translate in 249 languages — Google Translate, free, no key needed.
  Type to search, with your 20 most-used languages first."
- "Read text from any image or screenshot in 123 languages — powered by
  Tesseract. English and Vietnamese work out of the box; any other
  language downloads in one tap from inside the app."

VI:
- "Dịch 249 ngôn ngữ — Google Translate, miễn phí, không cần key.
  Gõ để tìm, 20 ngôn ngữ hay dùng nhất luôn ở trên đầu."
- "Đọc chữ từ ảnh hay ảnh chụp màn hình bằng 123 ngôn ngữ — dùng
  Tesseract. Tiếng Anh và tiếng Việt chạy ngay không cần tải; ngôn ngữ
  khác tải trong app chỉ bằng một lần bấm."

## 5. Parity note (mac)

- mac currently ships 20 translation languages (14 favorites + 6); the
  same 249-language expansion is queued for after mac 0.1.4 (Manh's
  decision 2026-11-19). mac uses Vision for OCR, not Tesseract, so the
  OCR numbers above are Windows-only.

## Links

- [OCR Text Capture (draft)](OCR_TEXT_CAPTURE.md)
- [Decisions](../DECISIONS.md) (249-language decision, CI #188)
- [Architecture](../ARCHITECTURE.md)
