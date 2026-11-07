// Windows.Media.Ocr provider (SPEC §3.3, OCR provider #1).
//
// Built into Windows 10/11 — no download, no key. Language support
// depends on the OCR language packs installed on the machine; English is
// present by default, Vietnamese needs the "Vietnamese OCR" optional
// feature (Settings > Time & language > Language > Vietnamese > Options).
// That is exactly why Tesseract is the DEFAULT for Vietnamese in v0.1:
// it brings its own language data and does not depend on optional features.

using System.Runtime.InteropServices.WindowsRuntime;
using TransPeek.Core.Ocr;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace TransPeek.Services;

public sealed class WindowsOcrProvider : IOcrProvider
{
    public string Id => OcrProviderId.WindowsOcr.ToIdString();
    public string DisplayName => OcrProviderId.WindowsOcr.DisplayName();
    public bool NeedsNetwork => false;
    public bool NeedsApiKey => false;

    public IReadOnlyList<string> SupportedLanguages =>
        OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();

    public string Status
    {
        get
        {
            var count = OcrEngine.AvailableRecognizerLanguages.Count;
            return count == 0
                ? "No OCR language installed on this machine."
                : $"Ready ({count} language(s) installed)";
        }
    }

    public async Task<string> RecognizeTextAsync(
        byte[] pngBytes, IReadOnlyList<string> languages, CancellationToken cancellationToken = default)
    {
        if (pngBytes is null || pngBytes.Length == 0)
            throw OcrException.NoTextRecognized();

        OcrEngine? engine = null;
        foreach (var code in languages)
        {
            try
            {
                var language = new Language(code);
                if (OcrEngine.IsLanguageSupported(language))
                {
                    engine = OcrEngine.TryCreateFromLanguage(language);
                    if (engine is not null)
                        break;
                }
            }
            catch
            {
                // Invalid BCP 47 code — try the next one.
            }
        }
        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            throw OcrException.EngineFailed("No Windows OCR engine is available on this machine.");

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync();
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Gray8)
            bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Gray8);

        var result = await engine.RecognizeAsync(bitmap);
        var text = result.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            throw OcrException.NoTextRecognized();
        return text;
    }
}
