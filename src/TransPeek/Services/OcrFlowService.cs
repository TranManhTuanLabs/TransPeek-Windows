// OCR flow (SPEC §3.3): region select → capture → OCR → (translate →
// panel, auto-copy translation) OR (copy source text straight + toast).
//
// The panel and toast are injected as small interfaces so this service
// stays UI-framework-free and testable. The flow mirrors the mac app:
//   Mode OpenPanel (default): OCR → translate → panel with [Copy gốc] /
//     [Copy dịch], auto-copy translation (default on, can be turned off).
//   Mode CopyOnly: OCR → copy source text, toast "Đã copy" only.
// Tesseract is the default provider for Vietnamese; on failure the flow
// falls back to Windows OCR (registry logic, see OcrProviderRegistry).

using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using TransPeek.Core.Ocr;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using Windows.Graphics;
using Windows.Graphics.Imaging;

namespace TransPeek.Services;

public interface ITranslatePanel
{
    void ShowTranslating(string sourceText);
    void ShowResult(string sourceText, string? translation, string providerName);
}

public interface IToastService
{
    void Show(string message);
}

public sealed class OcrFlowService
{
    private readonly Func<AppSettings> _settingsFactory;
    private readonly Func<TranslationCoordinator> _coordinatorFactory;
    private readonly Func<IReadOnlyList<string>, string, IOcrProvider> _resolveProvider;
    private readonly ITranslatePanel _panel;
    private readonly IToastService _toast;
    private readonly Func<Task<RegionSelection>> _selectRegionAsync;
    private readonly ScreenCaptureService _capture = new();

    public OcrFlowService(
        // Factories, not instances: App reloads Settings and rebuilds the
        // coordinator when the user saves, and the OCR flow must use the
        // current ones — a captured instance would go stale after a save.
        Func<AppSettings> settingsFactory,
        Func<TranslationCoordinator> coordinatorFactory,
        Func<IReadOnlyList<string>, string, IOcrProvider> resolveProvider,
        ITranslatePanel panel,
        IToastService toast,
        Func<Task<RegionSelection>> selectRegionAsync)
    {
        _settingsFactory = settingsFactory;
        _coordinatorFactory = coordinatorFactory;
        _resolveProvider = resolveProvider;
        _panel = panel;
        _toast = toast;
        _selectRegionAsync = selectRegionAsync;
    }

    /// <summary>Run one full OCR pass (hotkey or tray menu).</summary>
    /// <remarks>IMPORTANT — UI thread: every entry point starts on the UI
    /// thread (tray menu click, hotkey via the UI queue, panel button), and
    /// this method MUST stay there: it touches the panel, the toast and the
    /// clipboard, all of which throw when called from a thread-pool thread.
    /// That is why there is deliberately NO ConfigureAwait(false) anywhere
    /// in this method. The heavy work (capture, OCR engine, HTTP) already
    /// runs off-thread inside the providers — awaiting them still posts
    /// this method's continuations back to the UI thread.</remarks>
    public async Task RunOcrAsync()
    {
        var settings = _settingsFactory();
        var text = await CaptureAndRecognizeTextAsync(settings);
        if (text is null)
            return;

        // "CopyOnly" (OCR -> copy source text, toast only). Anything else
        // is the default OpenPanel mode.
        if (string.Equals(settings.OcrAfterCapture, "CopyOnly", StringComparison.OrdinalIgnoreCase))
        {
            await ClipboardHelper.CopyTextAsync(text);
            _toast.Show(AppStrings.Get("App.Toast.Copied", "Copied"));
            return;
        }

        // Mode OpenPanel: show the panel immediately (with a "translating"
        // state), then fill in the translation when it arrives.
        _panel.ShowTranslating(text);
        string? translation = null;
        string providerName = "";
        try
        {
            var result = await _coordinatorFactory().TranslateAsync(
                text,
                sourceLang: settings.SourceLanguage == LanguageCode.Auto ? null : settings.SourceLanguage,
                targetLang: settings.TargetLanguage);
            translation = result.Text;
            providerName = result.ProviderName;
        }
        catch (Exception ex)
        {
            // Translation failed: still show the source text in the panel
            // with the error, instead of failing silently.
            translation = $"{AppStrings.Get("Translation.ErrorPrefix", "[Translation error: ")}{ex.Message}]";
        }
        _panel.ShowResult(text, translation, providerName);
        if (settings.OcrAutoCopyTranslation && translation is not null
            && !translation.StartsWith(AppStrings.Get("Translation.ErrorPrefix", "[Translation error: "), StringComparison.Ordinal))
        {
            await ClipboardHelper.CopyTextAsync(translation);
        }
    }

    /// <summary>Silent OCR (mac "Clipboard OCR"): region select → OCR →
    /// copy the recognized text straight to the clipboard, toast "Copied".
    /// Unlike RunOcrAsync this ignores the post-OCR mode setting — the
    /// tray item always copies. Cancellation returns silently; failures
    /// were already toasted by the shared capture step.</summary>
    public async Task RunSilentOcrAsync()
    {
        var settings = _settingsFactory();
        var text = await CaptureAndRecognizeTextAsync(settings);
        if (text is null)
            return;
        await ClipboardHelper.CopyTextAsync(text);
        _toast.Show(AppStrings.Get("App.Toast.Copied", "Copied"));
    }

    /// <summary>Shared prefix of both OCR flows: region select → capture →
    /// first-run Tesseract data → recognize (Tesseract, Windows OCR on
    /// failure). Returns the trimmed text, or null when the user cancelled
    /// or a step failed (the failure was already toasted).</summary>
    private async Task<string?> CaptureAndRecognizeTextAsync(AppSettings settings)
    {
        var selection = await _selectRegionAsync();
        if (selection.Region is null)
            return null; // user cancelled the region selection

        byte[] png;
        try
        {
            // Prefer the pre-overlay screenshot (0.1.3.54): the overlay
            // renders opaque, so a live capture taken while it is up
            // photographs the overlay, not the desktop. Fall back to a
            // live region capture when there is no screenshot.
            png = await selection.CropScreenshotAsync()
                ?? await _capture.CaptureRegionPngAsync(selection.Region.Value);
        }
        catch (Exception ex)
        {
            _toast.Show(AppStrings.Get("Ocr.CaptureFailed", "Could not capture the screen: {0}", ex.Message));
            return null;
        }

        // Grey-OCR diagnostic (0.1.3.53 test: the region overlay shows
        // grey and OCR finds nothing): save the captured image and log
        // its brightness stats, so the next test can tell the two grey
        // causes apart — a flat captured image (capture pipeline broken)
        // vs a fine capture with a grey overlay (the window surface is
        // opaque and hides the desktop). The file is
        // %LOCALAPPDATA%\TransPeek\ocr-capture.png.
        await LogCaptureDiagnosticsAsync(png, selection.Region.Value);

        // First run: download Tesseract language data if the user picked
        // Tesseract (the default) and it is missing, instead of silently
        // falling back to Windows OCR (English-only on most machines).
        await EnsureTesseractDataAsync(settings);

        string text;
        try
        {
            var provider = _resolveProvider(settings.OcrLanguages, settings.OcrProviderId);
            try
            {
                text = await provider.RecognizeTextAsync(png, settings.OcrLanguages);
            }
            catch (Exception) when (provider is TesseractOcrProvider)
            {
                // Tesseract failed (engine or language data): one automatic
                // fallback to Windows OCR, then the error stands.
                text = await new WindowsOcrProvider()
                    .RecognizeTextAsync(png, settings.OcrLanguages);
            }
        }
        catch (Exception ex)
        {
            _toast.Show(AppStrings.Get("Ocr.Failed", "OCR error: {0}", ex.Message));
            return null;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            _toast.Show(AppStrings.Get("Ocr.NoText", "No text recognized in the selected region."));
            return null;
        }
        return text.Trim();
    }

    /// <summary>First-run Tesseract setup: download any missing language
    /// data for the configured OCR languages. Runs on the UI thread (see
    /// RunOcrAsync); the download itself is async I/O, so the UI stays
    /// responsive. A failed download is NOT fatal: the provider registry
    /// below falls back to Windows OCR, and the user is told why.</summary>
    private async Task EnsureTesseractDataAsync(AppSettings settings)
    {
        // Only when the user picked Tesseract (the default). An explicit
        // Windows OCR choice is respected as-is.
        if (OcrProviderIdExtensions.FromIdString(settings.OcrProviderId) != OcrProviderId.Tesseract)
            return;
        var missing = TesseractLanguageDataManager.MissingCodes(settings.OcrLanguages, TesseractLanguageDataManager.IsInstalled);
        if (missing.Count == 0)
            return;
        _toast.Show(AppStrings.Get("Ocr.DownloadingData", "Downloading OCR data for the first time ({0})…", string.Join(", ", missing)));
        foreach (var code in missing)
        {
            try
            {
                await TesseractLanguageDataManager.DownloadAsync(code);
            }
            catch (Exception ex)
            {
                _toast.Show(AppStrings.Get("Ocr.DownloadFailed", "Could not download OCR data {0}: {1} — will use Windows OCR instead.", code, ex.Message));
                return;
            }
        }
        _toast.Show(AppStrings.Get("Ocr.DownloadDone", "OCR data downloaded."));
    }

    /// <summary>Grey-OCR diagnostic: save the captured region PNG and log
    /// its brightness mean/stddev (sampled). A flat grey board has stddev
    /// near zero; real screen content has a wide spread. The saved file
    /// lets the user open the capture and see it with their own eyes.
    /// Never throws — diagnostics must not break the OCR flow.</summary>
    private static async Task LogCaptureDiagnosticsAsync(byte[] png, RectInt32 region)
    {
        try
        {
            var dir = TransPeek.Core.Dictionary.AppPaths.LocalDataDirectory;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "ocr-capture.png");
            await File.WriteAllBytesAsync(path, png);
            using var stream = new MemoryStream(png).AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            var bytes = pixelData.DetachPixelData();
            // Sample every 16th pixel (BGRA, 4 bytes each) — cheap and
            // plenty for a flat-vs-content verdict.
            double sum = 0, sumSq = 0;
            var n = 0;
            for (var i = 0; i + 2 < bytes.Length; i += 64)
            {
                var brightness = (bytes[i] + bytes[i + 1] + bytes[i + 2]) / 3.0;
                sum += brightness;
                sumSq += brightness * brightness;
                n++;
            }
            var mean = n == 0 ? 0 : sum / n;
            var stddev = n == 0 ? 0 : Math.Sqrt(Math.Max(0, sumSq / n - mean * mean));
            CrashLog.WriteLine("OcrCapture",
                $"region {region.Width}x{region.Height} at ({region.X},{region.Y}), " +
                $"png {png.Length} bytes -> {path}, brightness mean {mean:F1} stddev {stddev:F1} " +
                $"(stddev ~0 = flat grey capture; high = real content, overlay is the grey one)");
        }
        catch (Exception ex)
        {
            CrashLog.Write("OcrCapture", ex);
        }
    }
}
