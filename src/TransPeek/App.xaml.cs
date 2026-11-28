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
using System.Drawing;
using System.IO;
using TransPeek.AlternativeIcon;
using TransPeek.BlockText;
using TransPeek.ContinuousListen;
using TransPeek.Core.Barcode;
using TransPeek.Core.ContinuousListen;
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
    /// <summary>Scroll-dismiss for the float icon (Manh 2026-11-27,
    /// bug 1): a wheel hook that hides the icon the moment the user
    /// scrolls — the hovered block scrolled away from the icon's place,
    /// so the icon is stale. No settings dependency, so it is created
    /// once at startup and never rebuilt on settings save (unlike the
    /// hover service above).</summary>
    private ScrollDismissMonitor? _scrollDismissMonitor;
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
    /// <summary>Continuous listen (chaining, Manh 2026-11-27): after
    /// the click-to-listen first chunk ends, keep reading the page.
    /// Created once at startup (after _iconSpeech); App starts it
    /// from StartClickToListenAsync, advances it from the SpeechEnded
    /// handler, and cancels it from every stop path (StopFloatSession).
    /// Null only before OnLaunched runs.</summary>
    private ContinuousListenController? _listenChain;
    /// <summary>The paragraph containing _lastSelection (text +
    /// rect), captured at SELECTION/HOVER time by the monitors — the
    /// continuous-listen probe anchor. Null / empty when no paragraph
    /// was captured (provider has no Paragraph unit, or the text came
    /// from a Reader selection that never captures one): then no chain
    /// starts and the session reads just the text (the old behaviour).
    /// Set together with _lastSelection/_lastPoint, on the monitors'
    /// background threads (plain fields, same pattern as _lastSelection).</summary>
    private string? _lastParagraphText;
    private Rectangle _lastParagraphRect;
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
        // asks it whether the user is reading (a Fast Read window, a
        // panel-right read, or a click-to-listen session on the page —
        // a reminder then waits instead of stealing the read), it
        // brings the panel forward for panel-right reminders, and it
        // hides the panel before a Rest window shows (the panel is
        // topmost and would float above it). The lambdas read the
        // fields at call time — the store is created before
        // _iconSpeech, like IsFastReadWindowOpen reads _fastReadWindow.
        _focusTimerStore = new FocusTimerStore
        {
            IsFastReadWindowOpen = () => _fastReadWindow is not null,
            // The float icon's session is active (playing or paused)
            // while IsSpeaking is true, and ends when the stream ends
            // or Stop is called (Manh 2026-11-27: listening on the
            // page counts as "reading" for the reminder rule).
            IsListening = () => _iconSpeech is not null && _iconSpeech.IsSpeaking,
            // When a reminder shows, the voice being listened to stops
            // first (one voice at a time, Manh 2026-11-27).
            OnReminderShowing = () => StopFloatSession(),
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
        // Continuous listen (chaining, Manh 2026-11-27): the controller
        // drives the chain — App speaks the first chunk, the controller
        // probes + speaks the next paragraphs, and tells App when the
        // chain ends on its own so the normal cleanup runs. The speak
        // callback updates _floatSessionText per chunk (so the toggle
        // and the same-text icon check track the CURRENT chunk, not the
        // first one). The DPI scale comes from the hidden window (1.0
        // at 96 DPI); it is read fresh per probe.
        _listenChain = new ContinuousListenController(
            speak: async (text, lang) =>
            {
                // Capture to a local first: the compiler only tracks
                // null-state for locals across an await, not for a
                // field that another thread could clear mid-await.
                var speech = _iconSpeech;
                var ok = speech is not null && await speech.SpeakAsync(text, lang);
                if (ok)
                    _floatSessionText = text;
                return ok;
            },
            chainEnded: EndClickToListenSession,
            getDpiScale: () => _hiddenWindow is { } w ? WindowHelpers.DpiScale(w) : 1.0);
        // The session ending on its own (the unit finished): for a chain,
        // advance to the next paragraph instead of ending the session;
        // otherwise hide the indicator and clear the session text. A
        // reminder that waited out the listening shows now, unless the
        // user is reading somewhere else (the store checks).
        _iconSpeech.SpeechEnded += (_, _) => OnIconSpeechEnded();
        // A read starting to speak stops any click-to-listen session
        // first — only one voice plays at a time (Manh 2026-11-27).
        // The view model fires this for every phrase it is about to
        // speak; stopping is a no-op when no float session is active.
        RsvpReaderViewModel.SpeechStarting += StopFloatSession;
        _speakerIndicator = new SpeakerIndicatorWindow();
        _speakerIndicator.ToggleRequested += OnSpeakerIndicatorToggleRequested;
        // The indicator auto-hiding while paused (5s, no interaction)
        // ends the session silently (the speaker law, Manh 2026-11-26).
        // A reminder that waited out the listening shows now, unless
        // the user is reading somewhere else (the store checks).
        _speakerIndicator.AutoHidden += () =>
        {
            StopFloatSession();
            _focusTimerStore?.ClickToListenEnded();
        };
        // A panel session starting replaces a float session — only
        // one TTS session runs at a time. (No `?.` + `+=` here:
        // null-conditional assignment needs C# 14, the project
        // builds with C# 12 — CI #257, CS9202.)
        if (_panel is not null)
        {
            _panel.SpeechSessionStarted += () => StopFloatSession();
        }

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
        // through the SHARED click-to-listen flow (mac 1cdaa44a: the
        // icon owns no TTS of its own — App starts the session with
        // the indicator at the icon's spot). The gate is the foreground
        // window: only the Fast Read window, or the panel on its Fast
        // Read tab, counts — never the translate input or Settings.
        // Inert unless a Read surface is foreground, so it needs no
        // settings toggle and no rebuild on settings save (sizes/delays
        // read live).
        _altIconController = new AlternativeIconController(
            IsReadSurfaceForeground,
            OnAlternativeIconTranslateRequested,
            OnAlternativeIconSpeakRequested,
            _uiQueue,
            ForegroundWindowInfo);
        _altIconController.Start();

        // Hover-to-show (feature/block-text-detection): the dwell is
        // Settings.HoverBlockDelayMs (default 400 ms) — the mouse
        // must rest that long before the icon appears.
        // Panel-up suppression (Manh 2026-11-27, mac parity): while the
        // panel is open, hovering text in another app must not
        // auto-detect — the probe + icon stole the panel's focus
        // (confirmed by Manh turning HoverBlockText off). Selection
        // still works (it is an explicit user action).
        _blockTextHoverService = new BlockTextHoverService(OnBlockTextHovered,
            dwell: TimeSpan.FromMilliseconds(Settings.HoverBlockDelayMs),
            isSuppressed: () => _panel is not null && _panel.IsPanelVisible);
        if (Settings.HoverBlockText)
            _blockTextHoverService.Start();

        // Scroll-dismiss for the float icon (Manh 2026-11-27, bug 1):
        // the hover service only re-probes when the cursor MOVES, so a
        // scroll — which moves the text, not the cursor — left the icon
        // sitting at a stale spot for its full auto-hide time. A
        // low-level wheel hook sees the scroll at once (same shape as
        // ClickOutsideMonitor) and hides the icon, like mac's
        // HoverTextDetector.onHoverStale. Only the float icon hides;
        // the speaker indicator is the player and stays while a session
        // plays. The hook runs from here on; the icon-visibility gate
        // keeps a scroll with no icon up from costing a post.
        _scrollDismissMonitor = new ScrollDismissMonitor(
            _uiQueue,
            () => _triggerIcon is not null && _triggerIcon.IsVisible,
            () => _triggerIcon?.HideIcon());
        _scrollDismissMonitor.Start();

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

    private void OnSelectionChanged(string text, (int X, int Y) point, TextParagraph? paragraph)
    {
        _lastSelection = text;
        _lastSelectionAt = DateTimeOffset.UtcNow;
        _lastPoint = point;
        // The paragraph the selection sits in, captured at detection
        // time (see the monitor's doc): the continuous-listen probe
        // anchor for when Speak is clicked. Null when the provider has
        // no Paragraph unit — then no chain starts (read the selection
        // and stop, the old behaviour).
        _lastParagraphText = paragraph?.Text;
        _lastParagraphRect = paragraph?.Rect ?? Rectangle.Empty;
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
            // Flicker investigation log (Manh 2026-11-27): selecting
            // text in the Reader around text still flickers the icon +
            // panel + read window on #264, after two guard fixes. Log
            // the guard inputs and outcome so the real cause is visible
            // in windows.log (tray -> Diagnose -> Open windows.log).
            var insideOwn = IsPointInsideOwnWindow(point);
            var fg = NativeMethods.GetForegroundWindow();
            var readGuard = IsReadSurfaceForeground();
            WindowDiagnostics.AppendLine(
                $"[Flicker] sel point=({point.X},{point.Y}) insideOwn={insideOwn} " +
                $"fg=0x{fg.ToInt64():X} fgIsFull={(_fastReadWindow is not null && fg == _fastReadWindowHwnd)} " +
                $"fgIsPanel={(_panel is not null && fg == _panelHwnd)} panelFastRead={(_panel?.IsFastReadTabActive ?? false)} " +
                $"guard={(readGuard ? "blocked" : "pass")} textLen={text.Length}");
            // Never summon the icon over our own windows (0.1.3.65 test):
            // selecting text inside the translate panel still showed the
            // icon — the UIA ProcessId exclusion in SelectionMonitor
            // misses it when the focused element reports pid 0 (fail
            // open). The same guard keeps the icon out of the fullscreen
            // capture overlay, where it sat under the mouse and broke the
            // region drag. AppWindow rects are physical pixels, same as
            // the UIA point.
            if (insideOwn)
                return;
            // Never summon the icon over a Read surface (Manh
            // 2026-11-27): the Fast Read window is NOT topmost, so the
            // rect guard above cannot cover it (a point inside its rect
            // may be in another app on top of it — the same reason
            // Settings is excluded). The foreground check is the right
            // guard here: when the user works in a Read surface, the
            // alternative icon is the one for Reader selections, and
            // the main float icon popping over the Reader flickered
            // (and stayed after a listen from the alternative icon).
            if (readGuard)
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
    private void OnBlockTextHovered(string text, (int X, int Y) point, Rectangle? paragraphRect)
    {
        _lastSelection = text;
        _lastSelectionAt = DateTimeOffset.UtcNow;
        _lastPoint = point;
        _lastTextIsHover = true;
        // The hovered block IS its paragraph (when the paragraph path
        // succeeded): the text is the paragraph text and the rect is
        // the probe anchor. Null rect = the text came from a fallback
        // (DocumentRange / Value / Name), not a paragraph — then no
        // chain starts (read the block and stop, the old behaviour).
        _lastParagraphText = paragraphRect is not null ? text : null;
        _lastParagraphRect = paragraphRect ?? Rectangle.Empty;
        // Same UI-thread marshal + own-window guard as the selection
        // path (the monitor runs on a background thread). Compact
        // icon: a hovered block gets the small (~3/4) icon, so it
        // stays out of the mouse's way — the full-size icon is only
        // for a manual selection (Manh 2026-11-21, mac parity).
        _uiQueue.TryEnqueue(() =>
        {
            // Same [Flicker] log as the selection path (see above):
            // a hovered block can summon the icon too.
            var insideOwn = IsPointInsideOwnWindow(point);
            var fg = NativeMethods.GetForegroundWindow();
            var readGuard = IsReadSurfaceForeground();
            WindowDiagnostics.AppendLine(
                $"[Flicker] hover point=({point.X},{point.Y}) insideOwn={insideOwn} " +
                $"fg=0x{fg.ToInt64():X} fgIsFull={(_fastReadWindow is not null && fg == _fastReadWindowHwnd)} " +
                $"fgIsPanel={(_panel is not null && fg == _panelHwnd)} panelFastRead={(_panel?.IsFastReadTabActive ?? false)} " +
                $"guard={(readGuard ? "blocked" : "pass")} textLen={text.Length}");
            if (insideOwn)
                return;
            // Same Read-surface guard as the selection path (the Fast
            // Read window is not topmost, so the rect guard cannot
            // cover it — see above).
            if (readGuard)
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

    /// <summary>Foreground-window info for the flicker gate-clear log:
    /// <summary>Foreground-window info for the flicker log: which window
    /// was foreground (hwnd + process id + name + TITLE + which of our
    /// windows it is), so the log names the window that stole foreground
    /// while the gate flapped. The title (GetWindowText) + role
    /// (panel/fastread) identify WHICH TransPeek window it is — the pid
    /// alone could not (log 275: fg pid 33540 TransPeek, window
    /// unknown). Called on the monitor's background thread — Win32
    /// calls only, guarded; never throws.</summary>
    private string ForegroundWindowInfo()
    {
        try
        {
            var fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero)
                return "fg=0";
            var pid = 0u;
            NativeMethods.GetWindowThreadProcessId(fg, out pid);
            string name;
            try
            {
                name = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
            }
            catch
            {
                name = "?";
            }
            // Window title: identifies which window of the process it is.
            var title = "";
            try
            {
                var sb = new System.Text.StringBuilder(64);
                if (NativeMethods.GetWindowTextW(fg, sb, sb.Capacity) > 0)
                    title = sb.ToString();
            }
            catch
            {
                title = "";
            }
            title = title.Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\'');
            if (title.Length > 40)
                title = title.Substring(0, 40);
            // Role: which of OUR windows it is (only meaningful when the
            // foreground process is TransPeek itself).
            var role = "";
            if (name == "TransPeek")
            {
                if (_fastReadWindow is not null && fg == _fastReadWindowHwnd)
                    role = " fastread";
                else if (_panel is not null && fg == _panelHwnd)
                    role = " panel";
                else
                    role = " other-transpeek";
            }
            return $"fg=0x{fg.ToInt64():X} pid={pid} {name} \"{title}\"{role}";
        }
        catch
        {
            return "fg=?";
        }
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
        if (string.IsNullOrWhiteSpace(text))
            return;
        // The indicator appears at the float icon's current position —
        // read it BEFORE hiding the icon (hiding does not move the
        // window, but the position is the indicator's anchor and must
        // come from the icon that was just clicked).
        var pos = _triggerIcon.AppWindow.Position;
        // The icon dismisses when its speak is clicked (Manh 2026-11-27,
        // bug 2 — mac parity: AppDelegate shows the indicator at the
        // icon's frame and dismisses the icon, "so the two never
        // overlap"). The indicator takes the icon's place; a later
        // hover/select shows a new icon while the session keeps
        // playing. Without this, the icon stayed UNDER the indicator
        // and was revealed when the session ended on its own — and its
        // auto-hide timer was stopped (the pointer never left it), so
        // it never hid. The toggle still works: pause/resume is the
        // indicator's job now, and a re-shown icon clicking the same
        // text still reaches the toggle branch in
        // StartClickToListenAsync.
        _triggerIcon.HideIcon();
        await StartClickToListenAsync(text, (pos.X, pos.Y), allowToggle: true);
    }

    /// <summary>Alternative icon clicked to listen (mac 1cdaa44a):
    /// the icon already hid itself and reported its position — start
    /// the shared click-to-listen session with the indicator at that
    /// spot. Clicking it again starts a NEW session (allowToggle:
    /// false), never a toggle.</summary>
    private async void OnAlternativeIconSpeakRequested(string text, (int X, int Y) position)
    {
        // Hide the float icon too (Manh 2026-11-27): it must never
        // stay visible over a Read surface after a listen starts —
        // the alternative icon hid itself, but the float icon (if it
        // had also appeared over the Reader) was never hidden by this
        // path, so it stayed after the unit finished.
        _triggerIcon?.HideIcon();
        // A Reader selection has no captured paragraph (the alternative
        // icon's monitor never captures one — it reads our own windows):
        // clear any stale paragraph context from an earlier hover/select
        // in another app, or the chain would probe the wrong page.
        _lastParagraphText = null;
        _lastParagraphRect = Rectangle.Empty;
        await StartClickToListenAsync(text, position, allowToggle: false);
    }

    /// <summary>The click-to-listen unit finished on its own: when a
    /// continuous-listen chain is active, advance it to the next
    /// paragraph instead of ending the session (the chain owns the
    /// cleanup when it ends on its own); otherwise end the session now.
    /// Async void like the other event handlers in App (the SpeechEnded
    /// event is void) — the advance awaits the UIA probe + speak.</summary>
    private async void OnIconSpeechEnded()
    {
        // SpeechEnded can fire on a background thread (MediaEnded); the
        // chain advance + cleanup touch UI, so run them on the UI thread.
        // (Crash 0x8001010E in HideIndicator, #270.)
        if (!_uiQueue.HasThreadAccess)
        {
            _uiQueue.TryEnqueue(() => OnIconSpeechEnded());
            return;
        }
        if (_listenChain is not null && _listenChain.IsActive)
        {
            await _listenChain.AdvanceAsync();
            return;
        }
        EndClickToListenSession();
    }

    /// <summary>End the click-to-listen session on its own (the unit
    /// finished and no chain is active, or the chain ended): clear the
    /// session text and hide the indicator. Hides the float icon too
    /// when it is showing the text that was just read (Manh 2026-11-27:
    /// "click icon speak không tự ẩn kết thúc phiên nghe"). The icon can
    /// re-appear during the session: the hover service re-detects the
    /// block once the click cleared the selection (it only checks "no
    /// active selection", not "no listen session"), and a re-selection
    /// re-fires the monitor. Hiding only the same-text icon keeps a
    /// genuinely new selection/hover (different text) visible, per the
    /// design that a later hover/select shows a new icon while the
    /// session plays. A reminder that waited out the listening shows
    /// now, unless the user is reading somewhere else (the store
    /// checks). Shared by the SpeechEnded handler and the chain-ended
    /// callback (the same cleanup, one place).</summary>
    private void EndClickToListenSession()
    {
        var sessionText = _floatSessionText;
        _floatSessionText = null;
        _speakerIndicator?.HideIndicator();
        if (sessionText is not null && string.Equals(_lastSelection, sessionText, StringComparison.Ordinal))
            _triggerIcon?.HideIcon();
        _focusTimerStore?.ClickToListenEnded();
    }

    /// <summary>Start a click-to-listen session through the shared
    /// float-icon speaker path (one voice at a time): pause any read,
    /// stop any other session, then play this text from the top with
    /// the indicator at <paramref name="indicatorPos"/>.
    /// <paramref name="allowToggle"/> is true only for the float icon:
    /// clicking it again for the same text toggles pause/resume. The
    /// alternative icon passes false — clicking it again starts a NEW
    /// session replacing the old one (Manh 2026-11-27 00:32).</summary>
    private async Task StartClickToListenAsync(string text, (int X, int Y) indicatorPos, bool allowToggle)
    {
        if (string.IsNullOrWhiteSpace(text) || _iconSpeech is null)
            return;
        // A read playing now pauses (keeping its position) — the
        // click-to-listen text takes the one voice, and Play/Space
        // resumes the read from where it stopped (Manh 2026-11-27).
        PauseAnyReading();
        // Same text as the running float session: the float icon
        // toggles pause/resume; the alternative icon always starts a
        // new session.
        if (allowToggle && _iconSpeech.IsSpeaking && string.Equals(_floatSessionText, text, StringComparison.Ordinal))
        {
            if (_iconSpeech.IsPaused)
                _iconSpeech.Resume();
            else
                _iconSpeech.Pause();
            _speakerIndicator?.SetPaused(_iconSpeech.IsPaused);
            return;
        }
        // A different text (or no session): stop any other session
        // first, then play this text from the top. A new session
        // replaces a running chain (only one voice, one chain) — cancel
        // it before speaking, or its SpeechEnded would advance the old
        // page under the new session (Stop() does not fire SpeechEnded,
        // so the chain would otherwise stay active and stale).
        _listenChain?.Cancel();
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
        // Continuous listen (chaining, Manh 2026-11-27): when the
        // paragraph captured at selection/hover time matches the text
        // just spoken, start a chain from it — when this chunk ends,
        // the SpeechEnded handler advances to the next paragraph below
        // it, to the end of the page. No match (stale context, Reader
        // text, provider without a Paragraph unit) = no chain: the
        // session reads just this text and stops, exactly as before.
        // Word-start: the first chunk is the EXACT text spoken here
        // (the selection, or the hovered paragraph from its start) —
        // the chain only adds the paragraphs AFTER it.
        // Chain diagnosis (CI272: Word select reads only the sentence):
        // log whether the chain starts, with the paragraph rect — a
        // whole-document rect means Paragraph degraded to Document.
        var canChain = _listenChain is not null
            && ContinuousListenChain.CanChain(_lastParagraphText, _lastParagraphRect, text,
                new Point(_lastPoint.X, _lastPoint.Y));
        WindowDiagnostics.AppendLine(
            $"[Chain] start canChain={canChain} paraLen={_lastParagraphText?.Length ?? 0} rect=({_lastParagraphRect.X},{_lastParagraphRect.Y},{_lastParagraphRect.Width}x{_lastParagraphRect.Height}) spokenLen={text.Length}");
        if (canChain)
        {
            _listenChain!.StartChain(
                new TextParagraph(_lastParagraphText!, _lastParagraphRect),
                lang,
                indicatorPos);
        }
        // The indicator appears at the spot where the user pressed
        // the speaker — the float icon's or the alternative icon's
        // current position.
        _speakerIndicator?.ShowAt(indicatorPos.X, indicatorPos.Y, paused: false);
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
        // Every stop cancels the chain too (panel session, indicator
        // auto-hide, Fast Read open, reminder showing, a Read starting):
        // the chain must never outlive the session it belongs to, or
        // its SpeechEnded would read the old page under a new session.
        // (Stop() itself does not fire SpeechEnded, so cancelling here
        // is the only thing that clears the chain on these paths.)
        _listenChain?.Cancel();
        _iconSpeech.Stop();
        _floatSessionText = null;
        _speakerIndicator?.HideIndicator();
    }

    /// <summary>Pause any read that is playing (Full window, panel
    /// Lite, Rest window) without clearing it — the user resumes with
    /// Play/Space from the same position. Called when a
    /// click-to-listen session starts, so only one voice plays at a
    /// time (Manh 2026-11-27).</summary>
    private void PauseAnyReading()
    {
        _fastReadWindow?.PauseReading();
        _focusTimerStore?.PauseAnyReading();
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
        try { _scrollDismissMonitor?.Dispose(); } catch { }
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
        // Rebuild keeps the panel-up suppression (see the initial
        // creation above): without it, a settings change would silently
        // re-enable hover while the panel is open.
        _blockTextHoverService = new BlockTextHoverService(OnBlockTextHovered,
            dwell: TimeSpan.FromMilliseconds(Settings.HoverBlockDelayMs),
            isSuppressed: () => _panel is not null && _panel.IsPanelVisible);
        if (Settings.HoverBlockText)
            _blockTextHoverService.Start();
        _panel?.RefreshDictionaryBar(Settings.DictionaryLinks, Settings.DictionaryDisplayMode);
        _panel?.RefreshTopBarFontSize();
        _panel?.RefreshPopupFontSize();
        _settingsWindow?.RefreshSettingsFontSize();
        _toast?.Show(AppStrings.Get("App.Toast.SettingsSaved", "Settings saved."));
    }
}
