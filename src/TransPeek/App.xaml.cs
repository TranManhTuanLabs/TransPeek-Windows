// Composition root (SPEC §2): loads settings, builds providers, and wires
// selection monitor + hotkeys + tray icon + trigger icon + panel + OCR.
//
// The app is tray-only: the main Window is hidden (it only keeps the
// WinUI app lifetime alive). All visible UI is TriggerIconWindow,
// TranslatePanelWindow, RegionSelectWindow, SettingsWindow and ToastWindow.
// Settings opens on launch (v0.1.1): a tray-only app shows nothing at
// all otherwise, and Windows 11 hides new tray icons in the overflow.
//
// NOTE — no TTS in v0.1 (SPEC §5): there is deliberately no speaker button,
// no click-to-read, no EdgeTTS. Clicking the trigger icon opens the panel.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TransPeek.Core.Barcode;
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
    private ScreenshotCopyService? _screenshotCopy;
    private BarcodeScanService? _barcodeScan;
    private string _lastSelection = "";
    private DateTimeOffset _lastSelectionAt = DateTimeOffset.MinValue;
    private (int X, int Y) _lastPoint;

    public App()
    {
        InitializeComponent();
        // Crash trap (run #40 died silently at startup with 0xc000027b in
        // Microsoft.UI.Xaml.dll — no console output, no .NET Runtime
        // event, no stack trace). Log every unhandled exception to
        // %LOCALAPPDATA%\TransPeek\crash.log and mark the WinUI one
        // handled, so a stowed WinRT exception no longer kills the app
        // before the user ever sees it. The log file carries the real
        // type/message/stack trace for the next diagnosis.
        UnhandledException += (_, e) =>
        {
            CrashLog.Write("Application.UnhandledException", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashLog.Write("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLog.Write("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
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

        _screenshotCopy = new ScreenshotCopyService(SelectRegionAsync, _toast);
        _barcodeScan = new BarcodeScanService(SelectRegionAsync);

        // Tray menu mirrors the mac menubar (MenuItemView.swift). No Smart
        // Translation and no Check for Updates: both deferred by Manh
        // (2026-11-11). No Permissions Guide: Windows has no
        // Accessibility / Screen Recording grants to walk through.
        _tray = new TrayIconService(
            manualTranslation: () => _panel?.ShowInputMode(),
            screenshotOcr: () => _ = _ocrFlow!.RunOcrAsync(),
            clipboardOcr: () => _ = _ocrFlow!.RunSilentOcrAsync(),
            selectionTranslation: () => _ = TranslateCurrentSelectionAsync(),
            clipboardTranslation: () => _ = TranslateClipboardAsync(),
            screenshotCopy: () => _ = _screenshotCopy!.RunAsync(),
            scanBarcodes: () => _ = ScanBarcodesAsync(),
            openSettings: ShowSettings,
            quit: () => Exit());

        _selectionMonitor = new SelectionMonitor(OnSelectionChanged);
        if (Settings.WatchSelection)
            _selectionMonitor.Start();

        StartHotkeys();

        // v0.1.1 — the app is tray-only, and v0.1 showed nothing at all
        // on launch: Windows 11 hides new tray icons in the overflow, so
        // the user saw no window and no menu. Open Settings right away
        // so there is always visible UI, and tell the user (toast) where
        // the app keeps running after the window is closed.
        // Startup hardening (run #40 crash 0xc000027b): if Settings or
        // the toast throws, log it and keep running in the tray instead
        // of dying silently. The tray icon already exists by now, so the
        // user can retry Settings from the tray menu.
        try
        {
            ShowSettings();
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShowSettings", ex);
        }
        try
        {
            _toast?.Show(AppStrings.Get("App.Toast.Started", "TransPeek is running — closing this window keeps the app in the system tray. Right-click the tray icon for the menu."));
        }
        catch (Exception ex)
        {
            CrashLog.Write("Toast.Show", ex);
        }
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
            _toast?.Show(AppStrings.Get("App.Toast.NoSelection", "No text selected to translate."));
            return;
        }
        var (cx, cy) = WindowHelpers.CursorPosition();
        _panel?.ShowTranslationFlow(text, cx, cy);
    }

    /// <summary>Clipboard Translation (mac parity): read the clipboard
    /// text and translate it in the panel. Empty clipboard shows a toast,
    /// like the no-selection case above.</summary>
    private async Task TranslateClipboardAsync()
    {
        var text = (await ClipboardHelper.GetTextAsync())?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _toast?.Show(AppStrings.Get("App.Toast.ClipboardEmpty", "Clipboard is empty. Copy some text first."));
            return;
        }
        var (cx, cy) = WindowHelpers.CursorPosition();
        _panel?.ShowTranslationFlow(text, cx, cy);
    }

    /// <summary>Scan Barcodes/QR Codes (mac parity): URL payloads open in
    /// the default browser, anything else is copied with a short toast
    /// preview, nothing found toasts "No barcode found".</summary>
    private async Task ScanBarcodesAsync()
    {
        var outcome = await _barcodeScan!.ScanAsync();
        switch (outcome)
        {
            case BarcodeScanOutcome.Opened:
                if (_barcodeScan.Payload is not null
                    && Uri.TryCreate(_barcodeScan.Payload, UriKind.Absolute, out var uri))
                    _ = await Windows.System.Launcher.LaunchUriAsync(uri);
                break;
            case BarcodeScanOutcome.Copied:
                await ClipboardHelper.CopyTextAsync(_barcodeScan.Payload!);
                _toast?.Show(BarcodePayload.Preview(_barcodeScan.Payload!));
                break;
            case BarcodeScanOutcome.NotFound:
                _toast?.Show(AppStrings.Get("Barcode.NoBarcode", "No barcode found"));
                break;
            case BarcodeScanOutcome.Failed:
                _toast?.Show(AppStrings.Get("Barcode.Failed", "Barcode scan failed: {0}", _barcodeScan.ErrorMessage ?? ""));
                break;
            case BarcodeScanOutcome.Cancelled:
                break;
        }
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
            return ($"{AppStrings.Get("Translation.ErrorPrefix", "[Translation error: ")}{ex.Message}]", "");
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
            // v0.1.1 — a WinUI 3 Window is destroyed when the user closes
            // it with ✕: v0.1 kept the dead instance and Activate() on it
            // silently did nothing, so Settings never opened again after
            // the first close. Drop the reference so the next open builds
            // a fresh window.
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
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
        _toast?.Show(AppStrings.Get("App.Toast.SettingsSaved", "Settings saved."));
    }
}
