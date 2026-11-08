// Composition root (SPEC §2): loads settings, builds providers, and wires
// selection monitor + hotkeys + tray icon + trigger icon + panel + OCR.
//
// The app is tray-only: the main Window is hidden (it only keeps the
// WinUI app lifetime alive). All visible UI is TriggerIconWindow,
// TranslatePanelWindow, RegionSelectWindow, SettingsWindow and ToastWindow.
//
// NOTE — no TTS in v0.1 (SPEC §5): there is deliberately no speaker button,
// no click-to-read, no EdgeTTS. Clicking the trigger icon opens the panel.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Ocr;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using TransPeek.Interop;
using TransPeek.Services;
using TransPeek.Views;
using Windows.Graphics;

namespace TransPeek;

public sealed partial class App : Application
{
    public static AppSettings Settings { get; private set; } = AppSettings.Default();
    public static TranslationCoordinator Coordinator { get; private set; } = null!;
    public static IReadOnlyList<ITranslationProvider> Providers { get; private set; } = [];

    private Window? _hiddenWindow;
    private DispatcherQueue _uiQueue = null!;
    private TrayIconService? _tray;
    private SelectionMonitor? _selectionMonitor;
    private GlobalHotkeyService? _hotkeys;
    private TriggerIconWindow? _triggerIcon;
    private TranslatePanelWindow? _panel;
    private ToastWindow? _toast;
    private SettingsWindow? _settingsWindow;
    private OcrFlowService? _ocrFlow;
    private string _lastSelection = "";
    private DateTimeOffset _lastSelectionAt = DateTimeOffset.MinValue;
    private (int X, int Y) _lastPoint;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _uiQueue = DispatcherQueue.GetForCurrentThread();
        AppPaths.EnsureDirectories();
        Settings = SettingsStore.Load();
        RebuildProviders();

        // Hidden main window: keeps the WinUI app alive without a visible window.
        _hiddenWindow = new Window();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_hiddenWindow);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);

        _toast = new ToastWindow();
        _panel = new TranslatePanelWindow(TranslateAsync, () => _ocrFlow!.RunOcrAsync());
        _triggerIcon = new TriggerIconWindow();
        _triggerIcon.OpenRequested += OnTriggerIconOpenRequested;

        _ocrFlow = new OcrFlowService(
            () => Settings,
            () => Coordinator,
            (languages, providerId) => ResolveOcrProvider(providerId),
            _panel,
            _toast,
            SelectRegionAsync);

        _tray = new TrayIconService(
            openSettings: ShowSettings,
            translateSelection: () => _ = TranslateCurrentSelectionAsync(),
            captureOcr: () => _ = _ocrFlow!.RunOcrAsync(),
            quit: () => Exit());

        _selectionMonitor = new SelectionMonitor(OnSelectionChanged);
        if (Settings.WatchSelection)
            _selectionMonitor.Start();

        StartHotkeys();
    }

    // ----- providers -----

    private void RebuildProviders()
    {
        var providers = new List<ITranslationProvider>
        {
            new GoogleTranslateProvider(),
        };
        foreach (var definition in Settings.CustomProviders)
            providers.Add(new OpenAiCompatibleProvider(definition));
        Providers = providers;
        Coordinator = new TranslationCoordinator(Settings, Providers);
    }

    private static IOcrProvider ResolveOcrProvider(string providerId)
    {
        var resolved = OcrProviderRegistry.Resolve(providerId, TesseractOcrProvider.IsEngineAvailable);
        return resolved == OcrProviderId.Tesseract
            ? new TesseractOcrProvider()
            : new WindowsOcrProvider();
    }

    // ----- selection → trigger icon -----

    private void OnSelectionChanged(string text, (int X, int Y) point)
    {
        _lastSelection = text;
        _lastSelectionAt = DateTimeOffset.UtcNow;
        _lastPoint = point;
        // The monitor runs on a background thread: marshal to the UI queue
        // (DispatcherQueue.GetForCurrentThread() here would return the
        // WRONG queue — it must be the one captured in OnLaunched).
        _uiQueue.TryEnqueue(() =>
        {
            _triggerIcon?.ShowAt(point.X, point.Y);
        });
    }

    private void OnTriggerIconOpenRequested()
    {
        _triggerIcon?.HideIcon();
        if (!string.IsNullOrWhiteSpace(_lastSelection))
            _panel?.ShowTranslationFlow(_lastSelection, _lastPoint.X, _lastPoint.Y);
    }

    private async Task TranslateCurrentSelectionAsync()
    {
        var text = _lastSelection;
        // The selection may be stale; if it is older than 10 s, re-grab it
        // via the clipboard fallback (Ctrl+C) instead.
        if (string.IsNullOrWhiteSpace(text)
            || DateTimeOffset.UtcNow - _lastSelectionAt > TimeSpan.FromSeconds(10))
        {
            text = await ClipboardHelper.TryGetSelectionViaClipboardAsync()
                ?? _lastSelection;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            _toast?.Show("Chưa chọn chữ nào để dịch.");
            return;
        }
        var (cx, cy) = WindowHelpers.CursorPosition();
        _panel?.ShowTranslationFlow(text, cx, cy);
    }

    private async Task<(string? Translation, string ProviderName)> TranslateAsync(string text)
    {
        try
        {
            var result = await Coordinator.TranslateAsync(
                text,
                sourceLang: Settings.SourceLanguage == LanguageCode.Auto ? null : Settings.SourceLanguage,
                targetLang: Settings.TargetLanguage);
            return (result.Text, result.ProviderName);
        }
        catch (Exception ex)
        {
            return ($"[Lỗi dịch: {ex.Message}]", "");
        }
    }

    // ----- OCR region selection -----

    private async Task<RectInt32?> SelectRegionAsync()
    {
        var window = new RegionSelectWindow();
        return await window.SelectRegionAsync();
    }

    // ----- hotkeys -----

    private void StartHotkeys()
    {
        _hotkeys?.Dispose();
        _hotkeys = new GlobalHotkeyService();
        // Hotkey callbacks arrive on the hotkey thread: marshal to the UI
        // queue before touching windows or the clipboard.
        try
        {
            _hotkeys.Register(GlobalHotkeyService.TranslateHotkeyId,
                Settings.TranslateHotkey,
                () => _uiQueue.TryEnqueue(() => _ = TranslateCurrentSelectionAsync()));
        }
        catch (FormatException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bad translate hotkey: {ex.Message}");
        }
        try
        {
            _hotkeys.Register(GlobalHotkeyService.OcrHotkeyId,
                Settings.OcrHotkey,
                () => _uiQueue.TryEnqueue(() => _ = _ocrFlow!.RunOcrAsync()));
        }
        catch (FormatException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bad OCR hotkey: {ex.Message}");
        }
        _hotkeys.Start();
    }

    // ----- settings -----

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.SettingsSaved += OnSettingsSaved;
        }
        _settingsWindow.ShowSettings();
    }

    private void OnSettingsSaved()
    {
        Settings = SettingsStore.Load();
        RebuildProviders();
        StartHotkeys(); // re-register hotkeys with the new specs
        // Restart the selection monitor if the user toggled WatchSelection.
        _selectionMonitor?.Dispose();
        _selectionMonitor = new SelectionMonitor(OnSelectionChanged);
        if (Settings.WatchSelection)
            _selectionMonitor.Start();
        _panel?.RefreshDictionaryBar(Settings.DictionaryLinks, Settings.DictionaryDisplayMode);
        _toast?.Show("Đã lưu Settings.");
    }
}
