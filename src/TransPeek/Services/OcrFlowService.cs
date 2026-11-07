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

using TransPeek.Core.Ocr;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using Windows.Graphics;

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
    private readonly Func<Task<RectInt32?>> _selectRegionAsync;
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
        Func<Task<RectInt32?>> selectRegionAsync)
    {
        _settingsFactory = settingsFactory;
        _coordinatorFactory = coordinatorFactory;
        _resolveProvider = resolveProvider;
        _panel = panel;
        _toast = toast;
        _selectRegionAsync = selectRegionAsync;
    }

    /// <summary>Run one full OCR pass (hotkey or tray menu).</summary>
    public async Task RunOcrAsync()
    {
        var settings = _settingsFactory();
        var region = await _selectRegionAsync().ConfigureAwait(false);
        if (region is null)
            return; // user cancelled the region selection

        byte[] png;
        try
        {
            png = await _capture.CaptureRegionPngAsync(region.Value).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _toast.Show($"Không chụp được màn hình: {ex.Message}");
            return;
        }

        string text;
        try
        {
            var provider = _resolveProvider(settings.OcrLanguages, settings.OcrProviderId);
            try
            {
                text = await provider.RecognizeTextAsync(png, settings.OcrLanguages).ConfigureAwait(false);
            }
            catch (Exception) when (provider is TesseractOcrProvider)
            {
                // Tesseract failed (engine or language data): one automatic
                // fallback to Windows OCR, then the error stands.
                text = await new WindowsOcrProvider()
                    .RecognizeTextAsync(png, settings.OcrLanguages).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _toast.Show($"OCR lỗi: {ex.Message}");
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            _toast.Show("Không nhận ra chữ nào trong vùng đã chọn.");
            return;
        }
        text = text.Trim();

        // "CopyOnly" (OCR -> copy source text, toast only). Anything else
        // is the default OpenPanel mode.
        if (string.Equals(settings.OcrAfterCapture, "CopyOnly", StringComparison.OrdinalIgnoreCase))
        {
            await ClipboardHelper.CopyTextAsync(text).ConfigureAwait(false);
            _toast.Show("Đã copy");
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
                sourceLanguage: settings.SourceLanguage == LanguageCode.Auto ? null : settings.SourceLanguage,
                targetLanguage: settings.TargetLanguage)
                .ConfigureAwait(false);
            translation = result.Text;
            providerName = result.ProviderName;
        }
        catch (Exception ex)
        {
            // Translation failed: still show the source text in the panel
            // with the error, instead of failing silently.
            translation = $"[Lỗi dịch: {ex.Message}]";
        }
        _panel.ShowResult(text, translation, providerName);
        if (settings.OcrAutoCopyTranslation && translation is not null
            && !translation.StartsWith("[Lỗi dịch:", StringComparison.Ordinal))
        {
            await ClipboardHelper.CopyTextAsync(translation).ConfigureAwait(false);
        }
    }
}
