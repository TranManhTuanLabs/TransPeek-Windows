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
using System.Diagnostics;
using System.IO;
using TransPeek.AlternativeIcon;
using TransPeek.BlockText;
using TransPeek.Core.Barcode;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Ocr;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using TransPeek.FastRead;
using TransPeek.FocusTimer;
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
    /// <summary>Hover-to-show for blocks of text
    /// (feature/block-text-detection): the mouse resting on a block
    /// shows the trigger icon without a manual selection. Same
    /// lifecycle as the selection monitor — rebuilt when settings are
    /// saved (the on/off toggle and the dwell apply). The dwell is
    /// Settings.HoverBlockDelayMs (default 400 ms, Manh 2026-11-21),
    /// NOT Settings.HoverDelayMs — see the state's comment.</summary>
    private BlockTextHoverService? _blockTextHoverService;
    private GlobalHotkeyService? _hotkeys;
    private TriggerIconWindow? _triggerIcon;
    private TranslatePanelWindow? _panel;
    private FastReadWindow? _fastReadWindow;
    /// <summary>Focus & Rest store (feature/focus-timer-decor): one
    /// store for the whole app — the Pomodoro / rest-eyes timers, the
    /// pending reminder and the panel-right read. Created once at
    /// startup, before the panel (the panel passes it to its card,
    /// banner and lite reader).</summary>
    private FocusTimerStore? _focusTimerStore;
    private RegionSelectWindow? _regionSelectWindow;
    private ToastWindow? _toast;
    private SettingsWindow? _settingsWindow;
    private OcrFlowService? _ocrFlow;
    private ScreenshotCopyService? _screenshotCopy;
    private BarcodeScanService? _barcodeScan;
    /// <summary>Speaks the selected text when the user clicks the float
    /// icon (listen mode). Separate from the panel's own speech
    /// service — the icon speaks the selection without opening the
    /// panel, so it needs its own synthesizer/player.</summary>
    private SystemSpeechService? _iconSpeech;
    /// <summary>Speaker indicator (Manh 2026-11-26): the separate
    /// floating play/pause control for a float-icon TTS session. It
    /// shows at the spot where the user pressed the float icon's
    /// speaker, Pause while playing and Play while paused. The float
    /// icon itself never shows playback state.</summary>
    private SpeakerIndicatorWindow? _speakerIndicator;
    /// <summary>The text of the active float-icon TTS session, or
    /// null when none is active. Pressing the float icon's speaker
    /// for the same text pauses/resumes it; a different text stops
    /// it and starts a new session from the top.</summary>
    private string? _floatSessionText;
    /// <summary>Alternative icon (mac FAST_READ_SELECT_ICON, Manh
    /// 2026-11-22): the fake icon over a selection inside Fast Read.
    /// Own monitor + window + TTS path — it never touches the main
    /// float icon's logic, glyphs, or settings.</summary>
    private AlternativeIconController? _altIconController;
    /// <summary>HWNDs for the alternative icon's foreground gate,
    /// captured on the UI thread at window creation (a WinUI Window
    /// must not be touched from the monitor's background thread).</summary>
    private IntPtr _panelHwnd;
    private IntPtr _fastReadWindowHwnd;
    private string _lastSelection = "";
    private DateTimeOffset _lastSelectionAt = DateTimeOffset.MinValue;
    private (int X, int Y) _lastPoint;
    /// <summary>True when _lastSelection came from a hovered block
    /// rather than a manual selection. Hovered text never goes stale
    /// the way a selection does (the block is still under the cursor),
    /// so the clipboard re-grab in the Speak / Fast Read / translate
    /// paths is skipped for it (see IsLastTextStale).</summary>
    private bool _lastTextIsHover;

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
        // Focus & Rest store (feature/focus-timer-decor): the engine
        // asks it whether a Fast Read window is open (a reminder then
        // waits instead of stealing the read), it brings the panel
        // forward for panel-right reminders, and it hides the panel
        // before a Rest window shows (the panel is topmost and would
        // float above it).
        _focusTimerStore = new FocusTimerStore
        {
            IsFastReadWindowOpen = () => _fastReadWindow is not null,
            OnShowPanel = () => _panel?.ShowPanelNearCursor(),
            OnShowRestWindow = () => _panel?.HidePanel(),
        };
        // Auto start focus (Manh 2026-11-20, 09:47): when the toggle is
        // ON, the focus timer starts on its own at launch.
        _focusTimerStore.AutoStartFocusIfEnabled();
        _panel = new TranslatePanelWindow(TranslateAsync, () => _ocrFlow!.RunOcrAsync(), ShowSettings, BuildShortcutEntries(), text => _ = OpenFastReadAsync(text), text => ShowFastReadWindow(text), _focusTimerStore);
        _panelHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_panel);
        _triggerIcon = new TriggerIconWindow();
        _triggerIcon.OpenRequested += OnTriggerIconOpenRequested;
        _triggerIcon.FastReadRequested += () => _ = OpenFastReadFromSelectionAsync();
        _triggerIcon.SettingsRequested += ShowSettings;
        _iconSpeech = new SystemSpeechService();
        _triggerIcon.SpeakRequested += OnTriggerIconSpeakRequested;
        // The session ending on its own (the unit finished) hides the
        // indicator and clears the session text.
        _iconSpeech.SpeechEnded += (_, _) =>
        {
            _floatSessionText = null;
            _speakerIndicator?.HideIndicator();
        };
        _speakerIndicator = new SpeakerIndicatorWindow();
        _speakerIndicator.ToggleRequested += OnSpeakerIndicatorToggleRequested;
        _speakerIndicator.AutoHidden += () => StopFloatSession();
        // A panel session starting replaces a float session — only
        // one TTS session runs at a time.
        _panel?.SpeechSessionStarted += () => StopFloatSession();

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
            clipboardOcrTranslated: () => _ = _ocrFlow!.RunSilentOcrTranslatedAsync(),
            selectionTranslation: () => _ = TranslateCurrentSelectionAsync(),
            fastRead: () => _ = OpenFastReadFromSelectionAsync(),
            focusRest: () => _panel?.ShowLeftPanel(),
            clipboardTranslation: () => _ = TranslateClipboardAsync(),
            screenshotCopy: () => _ = _screenshotCopy!.RunAsync(),
            scanBarcodes: () => _ = ScanBarcodesAsync(),
            openSettings: ShowSettings,
            restart: RestartApp,
            diagnoseWindows: () =>
            {
                WindowDiagnostics.Write();
                _toast?.Show(AppStrings.Get("App.Toast.WindowsListed", "Window list saved to windows.log — send that file to diagnose the floating 'Esc' pill."));
            },
            diagnoseOpenLog: () =>
            {
                // Open windows.log in its default viewer (Notepad) so
                // Manh can read / send the panel-size diagnostic lines.
                // The log lives in %LOCALAPPDATA%\TransPeek; it does not
                // exist until something writes it (list windows, or a
                // panel fit on the diagnostic build).
                var path = Path.Combine(AppPaths.LocalDataDirectory, "windows.log");
                try
                {
                    if (File.Exists(path))
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    else
                        _toast?.Show(AppStrings.Get("App.Toast.NoWindowsLog", "windows.log does not exist yet — use Diagnose → List windows first."));
                }
                catch (Exception ex) { CrashLog.Write("TrayMenu", ex); }
            },
            quit: QuitApp);

        _selectionMonitor = new SelectionMonitor(OnSelectionChanged);
        if (Settings.WatchSelection)
            _selectionMonitor.Start();

        // Alternative icon (mac FAST_READ_SELECT_ICON, Manh 2026-11-22):
        // a selection inside a Fast Read surface shows the fake icon
        // (A in a chat bubble) — hover translates, click listens
        // through its own TTS path. The gate is the foreground window:
        // only the Fast Read window, or the panel on its Fast Read tab,
        // counts — never the translate input or Settings. Inert unless
        // a Read surface is foreground, so it needs no settings toggle
        // and no rebuild on settings save (sizes/delays read live).
        _altIconController = new AlternativeIconController(
            IsReadSurfaceForeground,
            OnAlternativeIconTranslateRequested,
            _uiQueue);
        _altIconController.Start();

        // Hover-to-show (feature/block-text-detection): the dwell is
        // Settings.HoverBlockDelayMs (default 400 ms) — the mouse
        // must rest that long before the icon appears.
        _blockTextHoverService = new BlockTextHoverService(OnBlockTextHovered,
            dwell: TimeSpan.FromMilliseconds(Settings.HoverBlockDelayMs));
        if (Settings.HoverBlockText)
            _blockTextHoverService.Start();

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
        // A manual selection is the freshest source: it wins over any
        // hovered block (the hover service already refuses to probe
        // while a selection is active; this flag keeps the priority
        // straight for the staleness re-grab below).
        _lastTextIsHover = false;
        // The monitor runs on a background thread: marshal to the UI queue
        // (DispatcherQueue.GetForCurrentThread() here would return the
        // WRONG queue — it must be the one captured in OnLaunched).
        _uiQueue.TryEnqueue(() =>
        {
            // Never summon the icon over our own windows (0.1.3.65 test):
            // selecting text inside the translate panel still showed the
            // icon — the UIA ProcessId exclusion in SelectionMonitor
            // misses it when the focused element reports pid 0 (fail
            // open). The same guard keeps the icon out of the fullscreen
            // capture overlay, where it sat under the mouse and broke the
            // region drag. AppWindow rects are physical pixels, same as
            // the UIA point.
            if (IsPointInsideOwnWindow(point))
                return;
            _triggerIcon?.ShowAt(point.X, point.Y);
        });
    }

    /// <summary>Hovered block → trigger icon
    /// (feature/block-text-detection): the mouse rested on a block of
    /// text, so the block's text becomes the icon's text — exactly like
    /// a selection (same _lastSelection / _lastPoint, same ShowAt), so
    /// hover = translate, click = Speak/Fast Read and Alt+click = the
    /// other action all work on the block. The hover service only fires
    /// when no selection is active, so this never overwrites a manual
    /// selection; the flag marks the text as hovered so the Speak /
    /// Fast Read / translate paths skip their clipboard re-grab for it
    /// (see IsLastTextStale).</summary>
    private void OnBlockTextHovered(string text, (int X, int Y) point)
    {
        _lastSelection = text;
        _lastSelectionAt = DateTimeOffset.UtcNow;
        _lastPoint = point;
        _lastTextIsHover = true;
        // Same UI-thread marshal + own-window guard as the selection
        // path (the monitor runs on a background thread). Compact
        // icon: a hovered block gets the small (~3/4) icon, so it
        // stays out of the mouse's way — the full-size icon is only
        // for a manual selection (Manh 2026-11-21, mac parity).
        _uiQueue.TryEnqueue(() =>
        {
            if (IsPointInsideOwnWindow(point))
                return;
            _triggerIcon?.ShowAt(point.X, point.Y, compact: true);
        });
    }

    /// <summary>True when the current _lastSelection may be stale: older
    /// than 10 s AND from a manual selection (a selection can change
    /// under the app — the user may have selected something else, or
    /// cleared it — so the Speak / Fast Read / translate paths re-grab
    /// it via the clipboard). Hovered text never goes stale this way:
    /// the block is still under the cursor while the icon shows it.</summary>
    private bool IsLastTextStale()
        => !_lastTextIsHover && DateTimeOffset.UtcNow - _lastSelectionAt > TimeSpan.FromSeconds(10);

    private bool IsPointInsideOwnWindow((int X, int Y) point)
    {
        // Settings is deliberately NOT in this list (Manh 2026-11-15):
        // it is a normal window, not topmost, so on launch it sits open
        // (920x640, centered) BEHIND whatever app the user works in —
        // and this rect check cannot tell "behind" from "on top", so any
        // selection inside that big centered rect was swallowed and the
        // icon missed on first launch until Settings was closed or
        // minimized. The panel, trigger icon and capture overlay ARE
        // topmost/fullscreen, so a point inside their rect really is on
        // top of them and the guard is correct there. Selecting text
        // inside Settings itself is already skipped by the monitor's
        // process-id check (it only misses when the element reports
        // pid 0, and an icon over Settings is a minor annoyance, not a
        // missed core feature).
        foreach (var window in new Window?[] { _panel, _triggerIcon, _regionSelectWindow })
        {
            if (window is null || !window.AppWindow.IsVisible)
                continue;
            var pos = window.AppWindow.Position;
            var size = window.AppWindow.Size;
            if (point.X >= pos.X && point.X < pos.X + size.Width &&
                point.Y >= pos.Y && point.Y < pos.Y + size.Height)
                return true;
        }
        return false;
    }

    private void OnTriggerIconOpenRequested()
    {
        _triggerIcon?.HideIcon();
        if (!string.IsNullOrWhiteSpace(_lastSelection))
            _panel?.ShowTranslationFlow(_lastSelection, _lastPoint.X, _lastPoint.Y);
    }

    // ----- alternative icon (Fast Read selection) -----

    /// <summary>True when the user is working in a Fast Read surface:
    /// the Fast Read window is foreground, or the panel is foreground
    /// on its Fast Read tab. The monitor polls this from its background
    /// thread, so it only uses the HWNDs captured on the UI thread at
    /// creation + plain field reads — never a WinUI Window call.</summary>
    private bool IsReadSurfaceForeground()
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return false;
        if (_fastReadWindow is not null && fg == _fastReadWindowHwnd)
            return true;
        return _panel is not null && fg == _panelHwnd && _panel.IsFastReadTabActive;
    }

    /// <summary>Alternative icon hover = translate (mac §5). Full
    /// window: the panel is closed, so open it at the selection like
    /// the main icon. Lite: the panel is already open, so translate in
    /// place — never move it to the cursor.</summary>
    private void OnAlternativeIconTranslateRequested(string text, (int X, int Y) point)
    {
        if (_fastReadWindow is not null
            && NativeMethods.GetForegroundWindow() == _fastReadWindowHwnd)
        {
            _panel?.ShowTranslationFlow(text, point.X, point.Y);
        }
        else
        {
            _panel?.TranslateInPlace(text);
        }
    }

    /// <summary>Float icon speaker pressed: play/pause/resume the
    /// TTS session for the current selection (Manh 2026-11-26).
    /// Same text as the running float session: toggle pause/resume
    /// (the indicator shows the state). A different text — or no
    /// session: stop any other session first (only one TTS session
    /// runs at a time), then play this text from the top and show
    /// the speaker indicator at the icon's spot. Re-grabs the
    /// selection via the clipboard when it is stale, like the
    /// translate hotkey path. Language: the source language setting
    /// when the user picked one; when it is "auto" the text's
    /// language is unknown, so guess from the text (Vietnamese
    /// diacritics -> vi, otherwise en — the app's two main
    /// languages). Nothing to speak -> stay silent.</summary>
    private async void OnTriggerIconSpeakRequested()
    {
        var text = _lastSelection;
        if (string.IsNullOrWhiteSpace(text)
            || IsLastTextStale())
        {
            text = await ClipboardHelper.TryGetSelectionViaClipboardAsync()
                ?? _lastSelection;
        }
        if (string.IsNullOrWhiteSpace(text) || _iconSpeech is null)
            return;
        // Same text as the running float session: toggle pause/resume.
        if (_iconSpeech.IsSpeaking && string.Equals(_floatSessionText, text, StringComparison.Ordinal))
        {
            if (_iconSpeech.IsPaused)
                _iconSpeech.Resume();
            else
                _iconSpeech.Pause();
            _speakerIndicator?.SetPaused(_iconSpeech.IsPaused);
            return;
        }
        // A different text (or no session): stop any other session
        // first, then play this text from the top.
        _panel?.StopSpeechSession();
        _iconSpeech.Stop();
        var lang = Settings.SourceLanguage == LanguageCode.Auto
            ? SystemSpeechService.DetectLanguage(text)
            : Settings.SourceLanguage;
        var started = await _iconSpeech.SpeakAsync(text, lang);
        if (!started)
        {
            _floatSessionText = null;
            _speakerIndicator?.HideIndicator();
            return;
        }
        _floatSessionText = text;
        // The indicator appears at the spot where the user pressed
        // the speaker — the float icon's current position.
        var pos = _triggerIcon.AppWindow.Position;
        _speakerIndicator?.ShowAt(pos.X, pos.Y, paused: false);
    }

    /// <summary>Speaker indicator clicked: pause (while playing) or
    /// resume (while paused) the float-icon TTS session. The
    /// indicator is the only pause/resume control for the float
    /// path — the float icon itself never shows playback state.</summary>
    private void OnSpeakerIndicatorToggleRequested()
    {
        if (_iconSpeech is null || !_iconSpeech.IsSpeaking)
            return;
        if (_iconSpeech.IsPaused)
            _iconSpeech.Resume();
        else
            _iconSpeech.Pause();
        _speakerIndicator?.SetPaused(_iconSpeech.IsPaused);
    }

    /// <summary>Stop the float-icon TTS session (a no-op when none is
    /// active) and hide its indicator. A panel session starting, the
    /// indicator auto-hiding while paused, and opening Fast Read all
    /// stop it — only one TTS session runs at a time.</summary>
    private void StopFloatSession()
    {
        if (_iconSpeech is null)
            return;
        _iconSpeech.Stop();
        _floatSessionText = null;
        _speakerIndicator?.HideIndicator();
    }

    private async Task TranslateCurrentSelectionAsync()
    {
        var text = _lastSelection;
        // The selection may be stale; if it is older than 10 s, re-grab it
        // via the clipboard fallback (Ctrl+C) instead. Hovered text
        // never re-grabs (IsLastTextStale): the block is still there.
        if (string.IsNullOrWhiteSpace(text)
            || IsLastTextStale())
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

    // ----- Fast Read (RSVP, feature/fast-read-rsvp) -----

    /// <summary>Open Fast Read with the given text (panel button: the
    /// text being translated). Empty text falls back to the current
    /// selection / clipboard, like the translate hotkey path. The run
    /// location ("Run FastRead in") decides the destination — see
    /// RouteFastRead.</summary>
    private async Task OpenFastReadAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await OpenFastReadFromSelectionAsync();
            return;
        }
        RouteFastRead(text);
    }

    /// <summary>Open Fast Read with the current selection (tray menu,
    /// Alt-click on the float icon): fresh selection, else the
    /// clipboard re-grab, else the clipboard text. Nothing to read
    /// shows a toast, like the no-selection translate case. The run
    /// location ("Run FastRead in") decides the destination — see
    /// RouteFastRead.</summary>
    private async Task OpenFastReadFromSelectionAsync()
    {
        var text = _lastSelection;
        if (string.IsNullOrWhiteSpace(text)
            || IsLastTextStale())
        {
            text = await ClipboardHelper.TryGetSelectionViaClipboardAsync()
                ?? _lastSelection;
        }
        if (string.IsNullOrWhiteSpace(text))
            text = (await ClipboardHelper.GetTextAsync())?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _toast?.Show(AppStrings.Get("App.Toast.NoTextToRead", "No text to read. Select some text first, or copy it."));
            return;
        }
        RouteFastRead(text);
    }

    /// <summary>Route a Fast Read request (feature/focus-timer-decor,
    /// mac parity: fastReadRunLocation): "RightPanel" reads in panel
    /// right (the panel stays open), anything else opens the Fast Read
    /// window (the panel hides first).</summary>
    private void RouteFastRead(string text)
    {
        // Opening Fast Read stops any click-to-listen session (the
        // old float icon stopped speech on a Fast Read click; the
        // Read mode has its own speaker, so the click-to-listen
        // audio must not keep playing under it). The panel session
        // is stopped here too when reading in the panel — HidePanel
        // only stops it when the panel hides for a separate reader
        // window.
        StopFloatSession();
        _panel?.StopSpeechSession();
        if (string.Equals(Settings.FastReadRunLocation, "RightPanel", StringComparison.OrdinalIgnoreCase))
            _panel?.ShowPanelForReading(text);
        else
            ShowFastReadWindow(text);
    }

    private void ShowFastReadWindow(string text)
    {
        // The reader is a separate window and the panel is topmost, so
        // the panel would float above the reader — hide it first. (The
        // panel button already hid it; HidePanel is a no-op then.)
        // The 2.5s A+ linger (mac §3): when the FLOAT ICON triggered
        // the Read, it shows A+ for 2.5s as a confirmation and hides
        // itself after — App must not hide it early, or the
        // confirmation never shows. Other paths (tray, panel button)
        // have no linger, so the icon hides at once there.
        if (_triggerIcon is { } icon && !icon.IsReadLingerActive)
            icon.HideIcon();
        _panel?.HidePanel();
        if (_fastReadWindow is null)
        {
            _fastReadWindow = new FastReadWindow();
            _fastReadWindowHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_fastReadWindow);
            _fastReadWindow.Closed += (_, _) =>
            {
                _fastReadWindow = null;
                _fastReadWindowHwnd = IntPtr.Zero;
                // A pending reminder shows itself now that the read
                // stopped (feature/focus-timer-decor, mac parity:
                // fastReadWindowClosed in FocusTimerStore).
                _focusTimerStore?.FastReadWindowClosed();
            };
        }
        _fastReadWindow.LoadText(text);
        _fastReadWindow.ShowReader();
    }

    /// <summary>Translate Clipboard (mac parity): read the clipboard
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

    private async Task<(string? Translation, string ProviderName, string? DetectedSourceLanguage)> TranslateAsync(string text)
    {
        try
        {
            var result = await Coordinator.TranslateAsync(
                text,
                sourceLang: Settings.SourceLanguage == LanguageCode.Auto ? null : Settings.SourceLanguage,
                targetLang: Settings.TargetLanguage);
            return (result.Text, result.ProviderName, result.DetectedSourceLanguage);
        }
        catch (Exception ex)
        {
            return ($"{AppStrings.Get("Translation.ErrorPrefix", "[Translation error: ")}{ex.Message}]", "", null);
        }
    }

    // ----- OCR region selection -----

    private async Task<RegionSelection> SelectRegionAsync()
    {
        // Capture the display BEFORE the overlay appears (0.1.3.54):
        // the overlay window renders opaque, so a capture taken while
        // it is up photographs the overlay — a flat ~222 grey board
        // with no text — instead of the desktop. The screenshot also
        // becomes the overlay's background, so the user can actually
        // see the desktop to pick a region (the 0.1.3.47 "grey frame,
        // no content" report: he was picking blind).
        var (cx, cy) = WindowHelpers.CursorPosition();
        var bounds = WindowHelpers.MonitorBounds(cx, cy);
        byte[]? screenshot = null;
        try
        {
            screenshot = await new ScreenCaptureService().CaptureRegionPngAsync(bounds);
        }
        catch (Exception ex)
        {
            // Display capture failed: fall back to the old flow
            // (overlay without a background + live region capture
            // after it closes).
            CrashLog.Write("SelectRegion.DisplayCapture", ex);
        }
        var window = new RegionSelectWindow();
        if (screenshot is not null)
            window.SetBackgroundImage(screenshot);
        // Track the overlay while it is open so the trigger icon stays
        // out of it (see IsPointInsideOwnWindow).
        _regionSelectWindow = window;
        RegionSelection selection;
        try
        {
            var region = await window.SelectRegionAsync();
            selection = new RegionSelection(region, screenshot, bounds);
        }
        finally
        {
            _regionSelectWindow = null;
        }
        return selection;
    }

    /// <summary>Quit and relaunch the app (recovery when the app
    /// misbehaves — e.g. after an OCR engine switch that crashes it,
    /// Manh 2026-11-14). Starts the new process first; only exits when
    /// the start succeeded, so a failed relaunch leaves the app
    /// running instead of quitting with nothing to come back to.</summary>
    internal static void Restart()
    {
        if (Application.Current is App app)
            app.RestartApp();
    }

    private void RestartApp()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                QuitApp();
                return;
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("RestartApp", ex);
        }
        // Relaunch failed (or no process path): stay running — quitting
        // would leave the user with no app at all.
    }

    /// <summary>Quit for real. Application.Exit() did not terminate the
    /// process on this machine (builds 0.1.3.47/.49: tray Quit left the
    /// app running, so the user could not tell 47 from 49 and could not
    /// test OCR for fear of overlapping instances). Dispose the tray
    /// icon first (otherwise Windows keeps a ghost icon until hovered),
    /// then kill the process — a tray-only app has no state to flush.</summary>
    private void QuitApp()
    {
        try { _tray?.Dispose(); } catch { }
        Environment.Exit(0);
    }

    /// <summary>The 10 shortcuts for the panel's "?" popover (Manh
    /// 2026-11-17): same names and order as Settings → Keyboard
    /// Shortcuts, same actions as the hotkeys (StartHotkeys) and the
    /// tray menu items. Bindings are read from Settings at popover
    /// open time, so a changed hotkey shows up without restarting.
    /// The lambdas only capture App methods — nothing runs at build
    /// time, so the panel can be constructed before _ocrFlow and
    /// _screenshotCopy exist (same lazy pattern as StartHotkeys).</summary>
    private IReadOnlyList<TranslatePanelWindow.PanelShortcutEntry> BuildShortcutEntries()
    {
        TranslatePanelWindow.PanelShortcutEntry Entry(string name, Func<string> binding, Action run)
            => new(name, binding, run);
        return new[]
        {
            Entry("Open translate panel", () => Settings.TranslateHotkey, () => _ = TranslateCurrentSelectionAsync()),
            Entry("Manual Translation", () => Settings.ManualHotkey, () => _panel?.ShowInputMode()),
            Entry("OCR Screen Translate", () => Settings.OcrHotkey, () => _ = _ocrFlow!.RunOcrAsync()),
            Entry("OCR to Clipboard", () => Settings.ClipboardOcrHotkey, () => _ = _ocrFlow!.RunSilentOcrAsync()),
            Entry("OCR to Clipboard - Translated", () => Settings.ClipboardOcrTranslatedHotkey, () => _ = _ocrFlow!.RunSilentOcrTranslatedAsync()),
            Entry("Translate Selection", () => Settings.SelectionHotkey, () => _ = TranslateCurrentSelectionAsync()),
            Entry("Translate Clipboard", () => Settings.ClipboardTranslationHotkey, () => _ = TranslateClipboardAsync()),
            Entry("Screenshot Copy", () => Settings.ScreenshotCopyHotkey, () => _ = _screenshotCopy!.RunAsync()),
            Entry("Scan Barcodes/QR Codes", () => Settings.ScanHotkey, () => _ = ScanBarcodesAsync()),
            Entry("Settings…", () => Settings.SettingsHotkey, () => ShowSettings()),
        };
    }

    // ----- hotkeys -----

    private void StartHotkeys()
    {
        _hotkeys?.Dispose();
        var hotkeys = new GlobalHotkeyService();
        _hotkeys = hotkeys;
        // Hotkey callbacks arrive on the hotkey thread: marshal to the UI
        // queue before touching windows or the clipboard. One action per
        // tray menu item (Manh 2026-11-12: full shortcut set in Settings).
        void TryRegister(int id, string spec, string name, Action handler)
        {
            try
            {
                // TryEnqueue takes a DispatcherQueueHandler, not an Action:
                // an Action variable does not convert to it implicitly,
                // so wrap it in a lambda (CS1503 in run #52).
                hotkeys.Register(id, spec, () => _uiQueue.TryEnqueue(() => handler()));
            }
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bad {name} hotkey {spec}: {ex.Message}");
            }
        }
        TryRegister(GlobalHotkeyService.TranslateHotkeyId, Settings.TranslateHotkey, "translate",
            () => _ = TranslateCurrentSelectionAsync());
        TryRegister(GlobalHotkeyService.OcrHotkeyId, Settings.OcrHotkey, "OCR",
            () => _ = _ocrFlow!.RunOcrAsync());
        TryRegister(GlobalHotkeyService.ManualHotkeyId, Settings.ManualHotkey, "manual translation",
            () => _panel?.ShowInputMode());
        TryRegister(GlobalHotkeyService.ClipboardOcrHotkeyId, Settings.ClipboardOcrHotkey, "clipboard OCR",
            () => _ = _ocrFlow!.RunSilentOcrAsync());
        TryRegister(GlobalHotkeyService.ClipboardOcrTranslatedHotkeyId, Settings.ClipboardOcrTranslatedHotkey, "clipboard OCR translated",
            () => _ = _ocrFlow!.RunSilentOcrTranslatedAsync());
        TryRegister(GlobalHotkeyService.SelectionHotkeyId, Settings.SelectionHotkey, "selection translation",
            () => _ = TranslateCurrentSelectionAsync());
        TryRegister(GlobalHotkeyService.ClipboardTranslationHotkeyId, Settings.ClipboardTranslationHotkey, "clipboard translation",
            () => _ = TranslateClipboardAsync());
        TryRegister(GlobalHotkeyService.ScreenshotCopyHotkeyId, Settings.ScreenshotCopyHotkey, "screenshot copy",
            () => _ = _screenshotCopy!.RunAsync());
        TryRegister(GlobalHotkeyService.ScanHotkeyId, Settings.ScanHotkey, "barcode scan",
            () => _ = ScanBarcodesAsync());
        TryRegister(GlobalHotkeyService.SettingsHotkeyId, Settings.SettingsHotkey, "settings",
            () => ShowSettings());
        hotkeys.Start();
    }

    // ----- settings -----

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            // App icon in the title bar / taskbar: same .ico as the tray.
            try { _settingsWindow.AppWindow.SetIcon(TrayIconService.EnsureIconFile()); }
            catch (Exception ex) { CrashLog.Write("SettingsWindow.SetIcon", ex); }
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
        _tray?.RefreshAccelerators(); // menu shortcut hints follow Settings
        // Restart the selection monitor if the user toggled WatchSelection.
        _selectionMonitor?.Dispose();
        _selectionMonitor = new SelectionMonitor(OnSelectionChanged);
        if (Settings.WatchSelection)
            _selectionMonitor.Start();
        // Restart the block-text hover service if the user toggled
        // HoverBlockText or changed the dwell (HoverBlockDelayMs).
        // The rebuild picks up both at once — the service reads the
        // dwell at construction.
        _blockTextHoverService?.Dispose();
        _blockTextHoverService = new BlockTextHoverService(OnBlockTextHovered,
            dwell: TimeSpan.FromMilliseconds(Settings.HoverBlockDelayMs));
        if (Settings.HoverBlockText)
            _blockTextHoverService.Start();
        _panel?.RefreshDictionaryBar(Settings.DictionaryLinks, Settings.DictionaryDisplayMode);
        _panel?.RefreshTopBarFontSize();
        _panel?.RefreshPopupFontSize();
        _settingsWindow?.RefreshSettingsFontSize();
        _toast?.Show(AppStrings.Get("App.Toast.SettingsSaved", "Settings saved."));
    }
}
