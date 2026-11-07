// Screenshot-OCR engines behind a common provider interface (SPEC §3.3).
// Mirrors the mac app's OcrProvider protocol, adapted for Windows:
// input is PNG bytes (platform-neutral) instead of a CGImage, so the
// Tesseract provider can live in the platform-neutral Core library and
// only the Windows.Media.Ocr provider needs WinRT (app project).

namespace TransPeek.Core.Ocr;

/// <summary>Identifies an OCR engine. New providers add a value here.</summary>
public enum OcrProviderId
{
    /// <summary>Windows.Media.Ocr — built-in, offline, no install needed.
    /// Vietnamese quality depends on the OCR language pack installed on
    /// the machine, usually weaker than Tesseract for diacritics.</summary>
    WindowsOcr,
    /// <summary>Tesseract via the Tesseract NuGet wrapper — default for
    /// Vietnamese (SPEC §3.3). Language data downloads on demand.</summary>
    Tesseract,
}

public static class OcrProviderIdExtensions
{
    public static string ToIdString(this OcrProviderId id) => id switch
    {
        OcrProviderId.WindowsOcr => "windows-ocr",
        OcrProviderId.Tesseract => "tesseract",
        _ => "windows-ocr",
    };

    public static OcrProviderId FromIdString(string? id) => id switch
    {
        "tesseract" => OcrProviderId.Tesseract,
        _ => OcrProviderId.WindowsOcr,
    };

    public static string DisplayName(this OcrProviderId id) => id switch
    {
        OcrProviderId.WindowsOcr => "Windows OCR (built-in)",
        OcrProviderId.Tesseract => "Tesseract (best for Vietnamese)",
        _ => "Windows OCR (built-in)",
    };
}

/// <summary>A screenshot-OCR engine behind the common interface.</summary>
public interface IOcrProvider
{
    string Id { get; }
    string DisplayName { get; }
    bool NeedsNetwork { get; }
    bool NeedsApiKey { get; }
    IReadOnlyList<string> SupportedLanguages { get; }
    string Status { get; }

    /// <summary>Recognize text in a PNG image. Throws
    /// <see cref="OcrException"/> when nothing is found or the engine fails.
    /// <paramref name="languages"/> are BCP 47 codes, e.g. ["vi-VN", "en-US"].</summary>
    Task<string> RecognizeTextAsync(byte[] pngBytes, IReadOnlyList<string> languages,
        CancellationToken cancellationToken = default);
}

public sealed class OcrException : Exception
{
    public OcrException(string message) : base(message) { }
    public OcrException(string message, Exception inner) : base(message, inner) { }

    public static OcrException NoTextRecognized()
        => new("No text recognized in this image.");

    public static OcrException EngineFailed(string detail)
        => new($"OCR failed: {detail}");
}
