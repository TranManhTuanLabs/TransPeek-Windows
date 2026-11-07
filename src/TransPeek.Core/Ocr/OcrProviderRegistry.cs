// Selects which OCR provider runs a capture (SPEC §3.3).
// Tesseract is the default for Vietnamese, but only runs when its engine
// AND language data are actually installed; anything else falls back to
// Windows.Media.Ocr, then to a clear error. Pure selection logic, covered
// by unit tests. Mirrors the mac app's OcrProviderRegistry.

using TransPeek.Core.Settings;

namespace TransPeek.Core.Ocr;

public static class OcrProviderRegistry
{
    /// <summary>Resolve the provider id to use.
    /// Tesseract only when explicitly selected AND available; anything else
    /// (unknown id, engine/data missing) is Windows OCR.</summary>
    public static OcrProviderId Resolve(string? selectedId, bool tesseractAvailable)
    {
        if (OcrProviderIdExtensions.FromIdString(selectedId) == OcrProviderId.Tesseract
            && tesseractAvailable)
            return OcrProviderId.Tesseract;
        return OcrProviderId.WindowsOcr;
    }

    /// <summary>Whether the Tesseract -&gt; Windows OCR automatic fallback
    /// may run after a Tesseract failure.</summary>
    public static bool WindowsOcrFallbackAvailable(bool tesseractEnabled) => tesseractEnabled;
}
