// Tesseract OCR provider (SPEC §3.3) via the Tesseract NuGet wrapper
// (charlesw/tesseract 5.2.0, Apache-2.0). The engine is created per call
// and disposed afterwards — TesseractEngine is not thread-safe, and
// recognition is infrequent (one capture at a time), so per-call creation
// is the safe trade-off. Language data must be installed first (see
// TesseractLanguageDataManager); the provider reports that in Status.

using Tesseract;

namespace TransPeek.Core.Ocr;

public sealed class TesseractOcrProvider : IOcrProvider
{
    public string Id => OcrProviderId.Tesseract.ToIdString();
    public string DisplayName => OcrProviderId.Tesseract.DisplayName();
    public bool NeedsNetwork => false; // After language data is downloaded.
    public bool NeedsApiKey => false;

    public IReadOnlyList<string> SupportedLanguages =>
        TesseractLanguageDataManager.InstalledCodes()
            .Select(TesseractLanguageDataManager.Bcp47ForTessCode)
            .ToList();

    public string Status
    {
        get
        {
            var installed = TesseractLanguageDataManager.InstalledCodes();
            if (installed.Count == 0)
                return "Language data not downloaded — open Settings → OCR to download vie+eng.";
            return "Ready (" + string.Join(", ", installed) + ")";
        }
    }

    /// <summary>Whether the engine can run right now: at least one language
    /// file is installed. The native DLLs ship with the NuGet package.</summary>
    public static bool IsEngineAvailable
        => TesseractLanguageDataManager.InstalledCodes().Count > 0;

    public async Task<string> RecognizeTextAsync(byte[] pngBytes, IReadOnlyList<string> languages,
        CancellationToken cancellationToken = default)
    {
        if (pngBytes is { Length: 0 })
            throw OcrException.NoTextRecognized();

        var tessCodes = languages
            .Select(TesseractLanguageDataManager.TessCodeForBcp47)
            .Where(c => c is not null)
            .Distinct()
            .ToList();
        if (tessCodes.Count == 0)
            tessCodes = ["eng"];

        var missing = tessCodes.Where(c => !TesseractLanguageDataManager.IsInstalled(c!)).ToList();
        if (missing.Count > 0)
            throw OcrException.EngineFailed(
                $"Tesseract language data missing: {string.Join(", ", missing)}. " +
                "Download it in Settings → OCR.");

        var langArg = string.Join("+", tessCodes);
        // TesseractEngine is synchronous; run it off the calling thread.
        // Upscale small captures 2x: Tesseract reads small text far better
        // upscaled (same preprocessing the mac app applies).
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var engine = new TesseractEngine(
                    TesseractLanguageDataManager.TessdataDirectory, langArg, EngineMode.Default);
                using var pix = Pix.LoadFromMemory(pngBytes);
                using var scaled = ScaleForOcr(pix);
                using var page = engine.Process(scaled);
                var text = page.GetText()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(text))
                    throw OcrException.NoTextRecognized();
                return text;
            }
            catch (OcrException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw OcrException.EngineFailed(ex.Message);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Upscale images whose longest side is under ~1200px, 2x.
    /// Always returns a NEW pix the caller must dispose: an upscaled copy,
    /// or a Clone() when the image is already big enough.</summary>
    private static Pix ScaleForOcr(Pix pix)
    {
        const int minLongSide = 1200;
        var longest = Math.Max(pix.Width, pix.Height);
        if (longest >= minLongSide)
            return pix.Clone();
        var scale = Math.Min(3.0f, minLongSide / (float)longest);
        return pix.Scale(scale, scale);
    }
}
