// Float translate panel (SPEC §3.1): borderless, topmost, no-activate.
// Shows the source text, the translation, [Copy] (normal mode) or
// [Copy gốc] / [Copy dịch] (OCR mode), and the dictionary bar at the
// bottom. Click outside, Esc, or the ✕ button closes it.
//
// Panel speaker buttons (source + translation) are wired to the
// Windows system voices (SystemSpeechService, Manh 2026-11-16).
// The float icon's click-to-listen (click = speak the selection)
// is still visual-only — that flow comes after the mac side is
// stable (see README).
//
// v0.1.2 mac parity — lookup follows the panel selection (mac PopupView):
// selecting words in the source box OR in the translation re-points the
// dictionary links at the selection; clearing the selection points them
// back at the source text that opened the panel. The source box is
// editable (Enter re-translates, Shift+Enter newlines) and the header
// has source/target pickers + swap, like the mac LanguageBarView.

using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using TransPeek.Assets;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using TransPeek.FocusTimer;
using TransPeek.Interop;
using TransPeek.Services;
using TransPeek.Share;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;

namespace TransPeek.Views;

public sealed partial class TranslatePanelWindow : Window, ITranslatePanel
{
    /// <summary>Manual-resize limits in logical pixels (DIPs), chosen
    /// for the WINDOWS panel (Manh 2026-11-15 — the mac numbers are
    /// reference only, not fixed; Windows picks its own). Min width
    /// 400: the language row needs Source 128 + Swap + Target 128 +
    /// Capture OCR (~390), so narrower would clip the pickers. Min
    /// height 260: the same floor auto-fit uses — the content (title
    /// + input + language row + output + actions + dictionary +
    /// footer) cannot fit in less. Max 800x800: roomy on a big
    /// screen, further clamped to the display work area.</summary>
    private const double MinPanelWidth = 400;
    /// <summary>Minimum panel height in DIPs — the FLOOR. The real
    /// minimum is dynamic (ComputeMinPanelHeight): the fixed rows
    /// (title + input + hint + language + actions + dictionary +
    /// footer + padding) plus a minimum output card. A fixed 450 was
    /// not enough when the input grew to several lines — the output
    /// still hid even after the snap-back (Manh 2026-11-16). At the
    /// floor the fixed rows take ~310, so 450 leaves ~140 for the
    /// output card.</summary>
    private const double MinPanelHeight = 450;
    /// <summary>Minimum height of the output card (ProviderCardBorder),
    /// in DIPs. Must match the MinHeight in XAML.
    /// 218 restored in #164 (Manh 2026-11-17): the +20 experiment (#163,
    /// 218 -> 238) did NOT clear the cover, so the shortfall was not in
    /// the card minimum — the real cause was SettleResizedSize never
    /// growing the panel after a horizontal drag (see there). The +20
    /// is reverted; 218 is the locked minimum again.
    /// History: 218 = header (~34) + one divider (1) + text box minimum (60, MinHeight on the
    /// TranslationTextBox so the output never collapses) + Speak/Copy
    /// row (~36) + margins (~19), plus 60 of breathing room (Manh
    /// 2026-11-16: at 150 the box was a touch small and clipped the
    /// Copy/speaker buttons for a one-word translation; 160 fixed
    /// three-line text but one word still clipped, 170 and 180 (17/11)
    /// still clipped one word / two lines while three lines were
    /// fine. Manh 17/11 01:55 had me MEASURE the two company-doc
    /// screenshots instead of guessing: the 3-line card is ~206 px
    /// tall (divider-to-bottom 175 + header 29 + border 2) while the
    /// 1-word card is only ~193 px (divider-to-bottom 162), i.e. the
    /// short-text card is 13 px short and the speaker row has no
    /// slack. 210 = the measured good-card height (~206) + 4 px
    /// buffer, so a 1-word translation now gets the same card height
    /// as the 3-line one. 218 = 210 + 8 (Manh 17/11 03:47, video in
    /// the company doc, build 158): the speaker/Copy buttons were
    /// STILL clipped by ~8 px at the card's bottom edge (only ~20 px
    /// of ~28 px visible) even after #157 moved them inside the box
    /// and #156 added the card's bottom margin to
    /// ComputeMinPanelHeight — because #156 ALSO removed the Actions
    /// row's top margin in the same commit, which shrank fixedH by
    /// the same 8 px, so the computed minimum never actually grew.
    /// Raising the card minimum itself by 8 grows BOTH the minimum
    /// (via cardMin) and the auto-fit measure (via the card's
    /// DesiredSize) by 8, with nothing to cancel it out. The card
    /// never collapses below this while expanded, so the output is
    /// always visible — the vertical equivalent of MinPanelWidth
    /// for the horizontal (Manh 2026-11-16: the output shrank to
    /// nothing when the panel was dragged small, even with long
    /// text). When the provider is collapsed the border MinHeight
    /// goes to 0 instead (see SetProviderCardExpanded).</summary>
    private const double MinOutputCardHeight = 218;
    /// <summary>Window-subclass delegate for the WM_GETMINMAXINFO
    /// minimum-size enforcement. Kept in a field (not a local) so the
    /// GC cannot collect it while the window lives — a collected
    /// delegate would crash the next border drag.</summary>
    private NativeMethods.WindowSubclassProc? _minSizeSubclassProc;
    private const double MaxPanelSize = 800;
    /// <summary>Maximum panel WIDTH in logical pixels (DIPs).
    /// Separate from MaxPanelSize (800, the height cap): the width
    /// cap used to be the same 800, so dragging the panel wide went
    /// to ~double the default width (440) and SettleResizedSize
    // clamped it back to 800 — Manh 2026-11-17: that double-width
    // snap-back was too wide. 600 is ~1.4x the 420 default: wide
    // enough for long text (fewer wraps) without the panel ever
    // spanning near half the screen. The height cap stays 800 so
    // long text can still grow tall.</summary>
    private const double MaxPanelWidth = 600;
    /// <summary>Thickness of the native resize strips along the bottom
    /// and right edges, in PHYSICAL pixels. They sit inside the root
    /// Grid's 12 px padding, so they never cover interactive content.
    /// SetRegionRects takes physical pixels (like the caption region).</summary>
    private const int ResizeStripThickness = 8;
    /// <summary>Auto-fit cap for the translation text box, in logical
    /// pixels (DIPs). Must match the MaxHeight="260" in XAML: auto-fit
    /// never grows the panel past (chrome + 260) for long text — the
    /// text scrolls instead. A manual drag taller than that RAISES the
    /// cap (to the full text height) so the output can grow past 260
    /// and show more text; a new show / auto-fit resets it to 260.</summary>
    private const double TranslationTextMaxHeight = 260;

    /// <summary>The _epoch at which the user last resized the panel
    /// (height). While it equals the current _epoch, ResizeToFitContent
    /// (auto-fit) is suppressed — mac's manualResizeGeneration vs
    /// translationGeneration. A new show bumps _epoch, so auto-fit runs
    /// fresh again. Null = no manual resize this show.</summary>
    private int? _manualResizeGeneration;
    /// <summary>Whether auto-fit already retried once this show.
    /// Shared design (2026-11-16): retry auto-fit once when a
    /// measurement is missing (a measure taken before the window was
    /// sized reads wrong). Reset on every new show; the flag stops a
    /// persistently bad measure from retrying forever.</summary>
    private bool _autoFitRetried;
    /// <summary>Physical size set by the last programmatic MoveAndResize
    /// (show + auto-fit). AppWindow.Changed fires for those too; when
    /// the new size equals this, it was our own resize, not the user's
    /// drag — so it must not start the drag debounce or suppress
    /// auto-fit. (A user drag is never saved to Settings anymore —
    /// shared design 2026-11-16 — it only lives for the session.)</summary>
    private Windows.Graphics.SizeInt32? _lastProgrammaticSize;
    /// <summary>Debounce for settling a user resize: AppWindow.Changed
    /// fires on every step of a border drag, so the clamp + snap-back
    /// run 300 ms after the last size change (i.e. when the drag ends),
    /// not on every pixel. (The drag is never saved to Settings —
    /// shared design 2026-11-16 — it only lives for the session.)</summary>
    private DispatcherTimer? _resizeSettleTimer;

    private readonly Func<string, Task<(string? Translation, string ProviderName, string? DetectedSourceLanguage)>> _translateAsync;
    private readonly Func<Task> _ocrRequested;
    private readonly ClickOutsideMonitor _clickOutside;
    private readonly EscapeKeyMonitor _escapeKey;
    /// <summary>True while the panel is pinned (PinButton): click-outside
    /// and Esc do NOT hide it. Only the pin toggle turns it off.</summary>
    private bool _pinned;
    private string _sourceText = "";
    private string _translationText = "";
    /// <summary>Source language the translation provider detected for
    /// the current text (BCP 47, e.g. "en"), when the source was
    /// "auto". The source speaker uses it to pick the right voice —
    /// with "auto" the setting alone cannot tell English from
    /// Vietnamese, so the default voice ("An") read everything.
    /// Null when the provider did not report one.</summary>
    private string? _detectedSourceLanguage;
    /// <summary>Text currently selected inside the panel (source or
    /// translation), or null to look up the source text. Mac parity:
    /// dictionary links follow the live selection.</summary>
    private string? _lookupText;
    private bool _visible;
    private bool _ocrMode;
    /// <summary>Whether the provider output card body is expanded
    /// (Manh 2026-11-15, mac ProviderResultCard). A new translation
    /// always re-expands it.</summary>
    private bool _providerCardExpanded = true;
    /// <summary>Show generation: incremented on every ShowPanelNear /
    /// ShowInputMode. The click-outside hook captures the epoch at
    /// click time and only hides when it is unchanged — a hide enqueued
    /// by the click that opened the panel (tray menu) must not kill
    /// the panel that click re-showed.</summary>
    private int _epoch;
    private readonly Action _openSettings;
    private readonly IReadOnlyList<PanelShortcutEntry> _shortcuts;
    /// <summary>Fast Read (feature/fast-read-rsvp): open the RSVP reader
    /// with the text being translated. Whether the panel hides first
    /// depends on the run location (see OnFastReadClicked): the Fast
    /// Read window hides it, panel-right reading keeps it open.</summary>
    private readonly Func<string, Task> _fastReadRequested;
    // Window-read request from the demo's "FastRead Full Window"
    // button (Manh 2026-11-22, mac parity): opens the Fast Read window
    // directly, bypassing the "Run FastRead in" setting — the sibling
    // _fastReadRequested routes via the setting instead.
    private readonly Action<string> _fastReadWindowRequested;
    /// <summary>Focus & Rest (feature/focus-timer-decor): the single
    /// store for the Pomodoro / rest-eyes timers, the pending reminder
    /// and the panel-right read. Created once by App and shared with
    /// the FocusTimerCard, the reminder banner and PanelFastReadView.</summary>
    private readonly FocusTimerStore _focusTimerStore;
    /// <summary>Whether the "?" shortcuts card embedded at the top of
    /// the output area is open (Manh 2026-11-18, mac parity). Persists
    /// across tab switches and panel hides — ShowTab restores it when
    /// returning to panel center.</summary>
    private bool _embeddedShortcutsVisible;
    /// <summary>Last time the mouse wheel switched tabs on the dots row:
    /// one long scroll gesture sends many wheel ticks, so ticks within
    /// 0.4s of a switch are ignored — one long scroll = one tab switch
    /// (Manh 2026-11-18).</summary>
    private DateTime _lastWheelTabSwitch;

    /// <summary>Multi-tab hub (Manh 2026-11-17): panel left = Shortcuts
    /// (temporary), panel center = Translate (default, CHỐT), panel
    /// right = Fast Read (placeholder). Names settled 2026-11-18 (mac):
    /// use "panel left / center / right" in docs, code and conversation
    /// — never "panel 1/2/3" in docs, to avoid the mac mix-up that put
    /// the "How the icon works" card in the wrong panel. Dots only for
    /// now — no swipe (WinUI has no native swipe for a panel like this;
    /// that would be custom work and Manh has not approved it). The
    /// dots row DOES scroll (mouse wheel) and has ‹ › arrows.</summary>
    private enum PanelTab { Shortcuts = 0, Translate = 1, FastRead = 2 }

    private PanelTab _activeTab = PanelTab.Translate;

    /// <summary>Panel left: the shortcuts card's collapsed state within
    /// this panel session (in-memory only — unlike the "How the icon
    /// works" card, whose state is remembered in Settings).</summary>
    private bool _shortcutsCardCollapsed;

    private static readonly SolidColorBrush DotSelectedBrush =
        new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x0E, 0xA5, 0xE9));
    private static readonly SolidColorBrush DotUnselectedBrush =
        new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xC8, 0xC8, 0xC8));
    /// <summary>Windows system TTS (Windows.Media.SpeechSynthesis):
    /// the panel speaker buttons speak through this. One instance for
    /// the window's lifetime; it owns a MediaPlayer + synthesizer.</summary>
    private readonly SystemSpeechService _speech = new();
    /// <summary>Auto-play chain (TTS Speech tab): when auto-play spoke
    /// the source and Auto-play translation is also on, speak the
    /// translation when the source finishes (SpeechEnded). Cleared
    /// whenever the user toggles a speaker manually or a new result
    /// arrives, so a manual speak never chains into a translation.</summary>
    private bool _autoPlayPendingTranslation;

    /// <summary>One row in the shortcuts table ("?" button, Manh
    /// 2026-11-17): display name, current key binding (read fresh from
    /// Settings every time the card opens), and the action to run —
    /// the same code path as the hotkey and the tray menu item, so the
    /// user never needs a physical key press. Built by App, which owns
    /// all ten handlers.</summary>
    public sealed record PanelShortcutEntry(string Name, Func<string> Binding, Action Run);

    public TranslatePanelWindow(
        Func<string, Task<(string? Translation, string ProviderName, string? DetectedSourceLanguage)>> translateAsync,
        Func<Task> ocrRequested,
        Action openSettings,
        IReadOnlyList<PanelShortcutEntry> shortcuts,
        Func<string, Task> fastReadRequested,
        Action<string> fastReadWindowRequested,
        FocusTimerStore focusTimerStore)
    {
        _translateAsync = translateAsync;
        _ocrRequested = ocrRequested;
        _openSettings = openSettings;
        _shortcuts = shortcuts;
        _fastReadRequested = fastReadRequested;
        _fastReadWindowRequested = fastReadWindowRequested;
        _focusTimerStore = focusTimerStore;
        InitializeComponent();
        PopulateLangCombos();
        SyncLangCombos();
        // Share row at the bottom of panel left (Manh 2026-11-19,
        // mirrors mac d414700): the same 6 buttons as About, built
        // once here — the row itself is in XAML (LeftShareRowPanel).
        ShareRowBuilder.Populate(LeftShareRowPanel, AppInfo.Tagline);
        // Focus & Rest (feature/focus-timer-decor): the card, the
        // reminder banner and the Fast Read lite view all read from
        // the same store (created once by App). The panel pins itself
        // when a read starts from the right tab, and toggles its size
        // for the lite reader's Maximize button.
        FocusTimerCardControl.Store = _focusTimerStore;
        ReminderBanner.Store = _focusTimerStore;
        FastReadTabPanel.Store = _focusTimerStore;
        FastReadTabPanel.OnStartReading = () => SetPinned(true);
        FastReadTabPanel.OnOpenInWindow = () => _fastReadWindowRequested(FastReadTabPanel.SourceText);
        FastReadTabPanel.OnToggleMaximize = TogglePanelSize;
        _focusTimerStore.PropertyChanged += OnFocusTimerStoreChanged;
        // Auto-play chain: when the auto-played source finishes and
        // Auto-play translation is also on, speak the translation.
        _speech.SpeechEnded += (_, _) =>
        {
            if (!_autoPlayPendingTranslation)
                return;
            _autoPlayPendingTranslation = false;
            _ = _speech.SpeakAsync(_translationText, App.Settings.TargetLanguage);
        };
        // WinUI 3 Window has no Loaded event (Loaded lives on
        // FrameworkElement): attach to the root content element instead.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                // resizable: true (Manh 2026-11-15 — user-resizable
                // panel): only flips OverlappedPresenter.IsResizable so
                // the OS allows user resizing; the grab areas are the
                // native resize strips (UpdateResizeRegions), and the
                // size is tracked in OnAppWindowChanged.
                WindowHelpers.MakeBorderlessTopmost(this, resizable: true);
                WindowHelpers.MakeToolWindow(this);
                // Minimum-size enforcement (WM_GETMINMAXINFO, Manh
                // 2026-11-16): the OS stops the border drag at the
                // minimum, so the panel can never be dragged small
                // enough to hide the output. The post-hoc clamp in
                // SettleResizedSize stays as a backstop.
                var hwnd = WindowNative.GetWindowHandle(this);
                InstallMinSizeSubclass(hwnd);
                // Native drag region (see UpdateCaptionRegion): set once
                // the layout is known, and again whenever the title bar
                // resizes (font size change).
                UpdateCaptionRegion();
                TitleBar.SizeChanged += (_, _) => UpdateCaptionRegion();
                // Native resize strips (bottom + right edges) + size
                // tracking: re-declared whenever the window size
                // changes, so the strips stay on the edges.
                UpdateResizeRegions();
                AppWindow.Changed += OnAppWindowChanged;
            };
            // A click into the panel activates it (see ActivateWindow):
            // until then the NOACTIVATE window never receives keyboard
            // shortcuts like Ctrl+V in the source box.
            // Activation runs on PointerRELEASED, not PointerPressed
            // (0.1.3.53 test): activating mid-click — between the
            // mouse-down and the mouse-up — swallowed the Button's
            // Click, so the Settings gear did nothing in every
            // ShowPanelNear path (trigger icon, translate hotkey,
            // selection translation) but worked in Manual Translation,
            // where the panel was already active. The Button's Click
            // fires during its own release handling, before the event
            // bubbles here, so release-activation runs after the Click.
            // handledEventsToo: the Button may mark the release handled.
            contentRoot.AddHandler(
                UIElement.PointerReleasedEvent,
                new PointerEventHandler((_, _) =>
                {
                    // Do not re-activate a hidden panel (0.1.3.64 test):
                    // the gear's Click hides the panel first, and this
                    // handler runs after the Click (the comment above),
                    // so activating here showed the panel again right
                    // after HidePanel hid it. Click-outside and Esc hide
                    // without a pointer release, so they were never
                    // affected.
                    if (_visible)
                        WindowHelpers.ActivateWindow(this);
                }),
                handledEventsToo: true);
            // WinUI 3 Window has no KeyDown event, so Esc is handled by
            // KeyDown on the root element (below) plus the low-level
            // EscapeKeyMonitor hook (which sees Esc whatever window has
            // focus). There used to be a KeyboardAccelerator for Esc here
            // as well, but it is REMOVED (2026-11-14): WinUI renders a
            // KeyboardAccelerator's key as a floating "Esc" key-tip pill
            // (a PopupHost window, 46x41 px) whenever the mouse hovers
            // the element — that pill is the "Esc" hint Manh kept seeing
            // above the panel and in the capture screen. The accelerator
            // was only a backup (it needs a focused element); KeyDown +
            // the hook already cover Esc.
            // PreviewKeyDown (tunnel), NOT KeyDown (bubble): Space must
            // be caught before a focused button sees it — KeyDown
            // reaches this handler only after the button already
            // handled Space (the repo link at the bottom got activated
            // this way, intermittently, depending on which button held
            // focus). The read keys go to the lite reader while the
            // Fast Read tab is open.
            contentRoot.PreviewKeyDown += (_, e) =>
            {
                // Esc is two-stage while a panel-right read is running
                // (feature/focus-timer-decor, mac parity: the first Esc
                // stops the read, the second hides the panel). The read
                // keys (Space / arrows / R) go to the lite reader while
                // the Fast Read tab is open.
                if (e.Key == VirtualKey.Escape)
                {
                    HandleEscape();
                    e.Handled = true;
                }
                else if (_activeTab == PanelTab.FastRead
                    && _focusTimerStore.PanelReadingViewModel is not null
                    && FastReadTabPanel.HandleKey(e.Key, FocusManager.GetFocusedElement()))
                {
                    e.Handled = true;
                }
            };
        }
        CopyButton.Click += async (_, _) => await CopyWithFeedbackAsync(CopyButton, _translationText);
        CopySourceButton.Click += async (_, _) => await CopyWithFeedbackAsync(CopySourceButton, _sourceText);
        CopyTranslationButton.Click += async (_, _) => await CopyWithFeedbackAsync(CopyTranslationButton, _translationText);
        OcrButton.Click += (_, _) => { HidePanel(); _ = _ocrRequested(); };
        _clickOutside = new ClickOutsideMonitor(
            // Fully qualified: the unqualified name binds to Window's
            // instance DispatcherQueue property (CS0176) instead of the
            // Microsoft.UI.Dispatching type.
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(),
            isActive: () => _visible,
            getRect: () =>
            {
                var pos = AppWindow.Position;
                var size = AppWindow.Size;
                return (pos.X, pos.Y, size.Width, size.Height);
            },
            getEpoch: () => _epoch,
            onClickOutside: epoch =>
            {
                if (epoch == _epoch && !_pinned)
                    HidePanel();
            });
        // Esc while the panel is visible (0.1.3.54): the panel is a
        // no-activate window, so its Esc accelerator is dead until the
        // user clicks into it first — a low-level keyboard hook sees
        // Esc whatever window has focus.
        _escapeKey = new EscapeKeyMonitor(
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(),
            isActive: () => _visible,
            getEpoch: () => _epoch,
            onEscape: epoch =>
            {
                // Two-stage Esc (feature/focus-timer-decor): the hook
                // path follows the same rule as the KeyDown path —
                // first stop a panel-right read, then hide (unless
                // pinned). The old code hid unconditionally here while
                // the KeyDown path also hid — the pin check below is
                // now inside HandleEscape, shared by both paths.
                if (epoch == _epoch)
                    HandleEscape();
            });
        RefreshDictionaryBar(App.Settings.DictionaryLinks, App.Settings.DictionaryDisplayMode);
        // Title + footer (AppInfo is the single source for name,
        // slogan, hint, repo URL and build number).
        AppNameText.Text = AppInfo.Name;
        TaglineText.Text = AppInfo.Tagline;
        HintText.Text = AppInfo.Hint;
        RefreshTopBarFontSize();
        RefreshPopupFontSize();
        FooterBuildText.Text = AppInfo.CiBuildNumber > 0
            ? $"{AppInfo.Version} (build {AppInfo.CiBuildNumber})"
            : AppInfo.Version;
        GithubMarkFooterImage.Source = GitHubMark.Image();
        GithubLink.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri(AppInfo.RepositoryUrl));
    }

    /// <summary>Apply the top-bar font size from settings: the app name
    /// takes the setting, the tagline is always 1 pt smaller (Manh
    /// 2026-11-14), and the hint line is 4 pt smaller than the app
    /// name (Manh 2026-11-15, mirrors mac — a few pt smaller than the
    /// slogan and dimmer). The gap between the hint (now the last line)
    /// and the input is exactly 1 line: the TitleBar's bottom margin
    /// is set to the hint size, so the space scales with the font
    /// instead of staying a fixed 6 px (Manh 2026-11-15 — "slogan chưa
    /// cách 1 line với input"). Called on open and whenever settings
    /// are saved (App.OnSettingsSaved). The 8 pt floor mirrors mac's
    /// slogan floor; with the 12 pt slider minimum it never triggers
    /// in practice.</summary>
    public void RefreshTopBarFontSize()
    {
        var size = App.Settings.PopupTopBarFontSize;
        AppNameText.FontSize = size;
        var taglineSize = Math.Max(8, size - 1);
        TaglineText.FontSize = taglineSize;
        var hintSize = Math.Max(8, size - 4);
        HintText.FontSize = hintSize;
        TitleBar.Margin = new Thickness(0, 0, 0, hintSize);
        RequestResizeToFit();
    }

    /// <summary>Apply the main content font size from settings (the
    /// source + translation text, mac popupFontSize, default 14).
    /// Called on open and whenever settings are saved
    /// (App.OnSettingsSaved), like RefreshTopBarFontSize. Resizes the
    /// window to fit the new text size, so dragging the Text Size
    /// slider shows the change live instead of clipping the bigger
    /// text (Manh 2026-11-15 — dragging Text Size showed no change
    /// inside the panel).</summary>
    public void RefreshPopupFontSize()
    {
        var size = App.Settings.PopupFontSize;
        SourceTextBox.FontSize = size;
        TranslationTextBox.FontSize = size;
        RequestResizeToFit();
    }

    // ----- selection flow (App calls this) -----

    /// <summary>Show the panel near (x, y) and translate the text.</summary>
    public void ShowTranslationFlow(string text, int x, int y)
    {
        _ocrMode = false;
        ShowPanelNear(x, y);
        SetSource(text);
        TranslationTextBox.Text = AppStrings.Get("Panel.Translating", "Translating…");
        ProviderStatusText.Text = "";
        SetProviderCardExpanded(true);
        _ = TranslateAndShowAsync(text);
    }

    /// <summary>True when the panel is visible on the Fast Read tab —
    /// the alternative icon's Lite surface (mac FAST_READ_SELECT_ICON
    /// §7). App's gate reads this from the monitor's background
    /// thread: a plain bool/enum field read, atomic and safe to poll.</summary>
    public bool IsFastReadTabActive => _visible && _activeTab == PanelTab.FastRead;

    /// <summary>Translate in place (alternative icon, Lite — mac
    /// FAST_READ_SELECT_ICON §5): the panel is ALREADY open on the
    /// Fast Read tab, so only switch it to the Translate tab and
    /// translate the text. It must NOT call ShowPanelNear — that would
    /// move the panel to the cursor. Unlike HidePanel, switching tabs
    /// does not stop the panel-right read: the read keeps running
    /// behind the translate tab.</summary>
    public void TranslateInPlace(string text)
    {
        _ocrMode = false;
        SetSource(text);
        TranslationTextBox.Text = AppStrings.Get("Panel.Translating", "Translating…");
        ProviderStatusText.Text = "";
        SetProviderCardExpanded(true);
        ShowTab(PanelTab.Translate);
        _ = TranslateAndShowAsync(text);
    }

    /// <summary>Manual translation (mac parity: prepareInputMode + panel
    /// at screen center). Empty source box, centered on the primary
    /// display, ACTIVATED with the keyboard focus in the source box —
    /// typing is the point, unlike the no-activate selection/OCR flows.
    /// Enter in the source box re-translates (existing handler).</summary>
    public void ShowInputMode()
    {
        _ocrMode = false;
        _epoch++;
        // New show: auto-fit runs fresh again (the manual-resize
        // suppression only lasts for the show the user resized in).
        _manualResizeGeneration = null;
        _autoFitRetried = false;
        // Shared panel design (2026-11-16): the panel opens at the
        // DEFAULT size in Settings (420x450) every show. A manual drag
        // only enlarges it for the session and is NOT saved back to
        // Settings, so PanelWidth/PanelHeight here ARE the default —
        // opening at them is opening at the default. ResizeToFitContent
        // grows the window if the content is tall.
        var scale = WindowHelpers.DpiScale(this);
        var w = Math.Max(1, (int)Math.Round(App.Settings.PanelWidth * scale));
        var h = Math.Max(1, (int)Math.Round(App.Settings.PanelHeight * scale));
        var work = WindowHelpers.PrimaryWorkArea();
        var px = work.X + Math.Max((work.Width - w) / 2, 0);
        var py = work.Y + Math.Max((work.Height - h) / 2, 0);
        _lastProgrammaticSize = new Windows.Graphics.SizeInt32(w, h);
        WindowHelpers.MoveAndResize(this, px, py, w, h);
        SetSource("");
        _translationText = "";
        TranslationTextBox.Text = "";
        ProviderStatusText.Text = "";
        SetProviderCardExpanded(true);
        UpdateCopyButtons();
        // Typing mode needs a real active window (Activate() alone does
        // nothing on a NOACTIVATE window — Ctrl+V would not work).
        WindowHelpers.ActivateWindow(this);
        _visible = true;
        _clickOutside.Start();
        _escapeKey.Start();
        // Multi-tab: every show opens on the translate tab.
        ShowTab(PanelTab.Translate);
        RequestResizeToFit();
        SourceTextBox.Focus(FocusState.Keyboard);
    }

    // ----- OCR flow (ITranslatePanel) -----

    public void ShowTranslating(string sourceText)
    {
        _ocrMode = true;
        var (cx, cy) = WindowHelpers.CursorPosition();
        ShowPanelNear(cx, cy);
        SetSource(sourceText);
        TranslationTextBox.Text = AppStrings.Get("Panel.Translating", "Translating…");
        ProviderStatusText.Text = "";
        SetProviderCardExpanded(true);
    }

    public void ShowResult(string sourceText, string? translation, string providerName)
    {
        SetSource(sourceText);
        _translationText = translation ?? "";
        TranslationTextBox.Text = _translationText;
        // The provider name heads the output card (the old "via …" line
        // under the pickers is gone, Manh 2026-11-15).
        ProviderNameText.Text = string.IsNullOrEmpty(providerName) ? "Google Translate" : providerName;
        ProviderStatusText.Text = "";
        SetProviderCardExpanded(true);
        UpdateCopyButtons();
        // A new result always re-fits, even if the user dragged the
        // panel during the previous result (Manh 2026-11-16, CI #145:
        // the panel stayed super tall for a 1-word translation). The
        // manual-resize suppression (_manualResizeGeneration == _epoch)
        // only means "don't fight the drag for the result that was
        // showing when the user dragged" — a new result is a new
        // fit, so clear it here. Without this, a tall drag stuck for
        // the whole time the panel stayed open, because _epoch only
        // bumps when the panel is re-shown (ShowPanelNear), not when
        // a new translation arrives while it is already visible.
        _manualResizeGeneration = null;
        RequestResizeToFit();
        AutoPlayIfEnabled();
    }

    /// <summary>Auto-play (Settings -> TTS Speech -> Preferences):
    /// read the source and/or the translation aloud when a result
    /// arrives. Both on: speak the source, then the translation when
    /// the source finishes (the SpeechEnded chain above). A new
    /// result clears any pending chain from the previous one.</summary>
    private void AutoPlayIfEnabled()
    {
        _autoPlayPendingTranslation = false;
        var playSource = App.Settings.AutoPlaySource && !string.IsNullOrWhiteSpace(_sourceText);
        var playTranslation = App.Settings.AutoPlayTranslation && !string.IsNullOrWhiteSpace(_translationText);
        if (!playSource && !playTranslation)
            return;
        if (playSource)
        {
            _autoPlayPendingTranslation = playTranslation;
            _ = _speech.SpeakAsync(_sourceText, _detectedSourceLanguage ?? App.Settings.SourceLanguage);
        }
        else
        {
            _ = _speech.SpeakAsync(_translationText, App.Settings.TargetLanguage);
        }
    }

    // ----- internals -----

    private async Task TranslateAndShowAsync(string text)
    {
        var (translation, providerName, detectedSourceLanguage) = await _translateAsync(text);
        // The user may have closed the panel while translating.
        if (!_visible)
            return;
        _detectedSourceLanguage = detectedSourceLanguage;
        ShowResult(text, translation, providerName);
    }

    private void SetSource(string text)
    {
        _sourceText = text;
        _lookupText = null;
        SourceTextBox.Text = text;
        // The right tab's demo reads this text ("Read in panel") —
        // keep it in sync so the button enables/disables with it.
        FastReadTabPanel.SourceText = text;
        SyncLangCombos();
        RefreshDictionaryBar(App.Settings.DictionaryLinks, App.Settings.DictionaryDisplayMode);
    }

    private void UpdateCopyButtons()
    {
        CopyButton.Visibility = _ocrMode ? Visibility.Collapsed : Visibility.Visible;
        // Copy source is always available (mac has it in the source area).
        CopySourceButton.Visibility = Visibility.Visible;
        CopyTranslationButton.Visibility = _ocrMode ? Visibility.Visible : Visibility.Collapsed;
    }

    // ----- provider output card (mac ProviderResultCard) -----

    /// <summary>Expand/collapse the provider card body. Collapsed, the
    /// card shrinks to its header and the output row goes back to Auto
    /// (so it does not sit as a tall empty row); expanded, the row is
    /// * again so the card stretches and the translation fills the
    /// remaining space. The window resizes to fit via
    /// RequestResizeToFit.</summary>
    private void OnProviderCardHeaderClicked(object sender, RoutedEventArgs e)
        => SetProviderCardExpanded(!_providerCardExpanded);

    private void SetProviderCardExpanded(bool expanded)
    {
        _providerCardExpanded = expanded;
        var bodyVisibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ProviderCardBodyDivider.Visibility = bodyVisibility;
        TranslationTextBox.Visibility = bodyVisibility;
        ProviderCardSpeakerRow.Visibility = bodyVisibility;
        ProviderChevronText.Text = expanded ? "⌄" : "›";
        ProviderCardBorder.VerticalAlignment = expanded
            ? VerticalAlignment.Stretch
            : VerticalAlignment.Top;
        // The card's MinHeight must follow the expanded state too:
        // a collapsed card (header only) still kept MinHeight 150,
        // so clicking the provider header never shrank the output
        // box — and with many providers later, every collapsed card
        // would keep 150 of height (Manh 2026-11-16). Collapsed the
        // border may shrink to just the header (0 floor); expanded
        // it goes back to the minimum output height (160).
        ProviderCardBorder.MinHeight = expanded ? MinOutputCardHeight : 0;
        // The output row is * when expanded (the card stretches with
        // the window so the output grows) and Auto when collapsed
        // (the row only takes the header height — a * row would keep
        // its share of the window height and leave empty space below
        // the header, VerticalAlignment.Top alone does not prevent
        // that because the row itself still takes the space).
        OutputCardRow.Height = expanded
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Auto);
        // A collapse/expand is a new fit, like a new translation
        // (ShowResult clears the flag the same way): after a manual
        // drag the flag suppresses auto-fit, so without this the panel
        // stayed at the dragged height when the provider was hidden
        // and the bottom bar sat on the output box with an empty gap
        // below it instead of the panel shrinking to fit (Manh
        // 2026-11-17). Clearing it lets the RequestResizeToFit below
        // actually run. Low risk: the flag only ever blocks a fit
        // this method already requests.
        _manualResizeGeneration = null;
        RequestResizeToFit();
    }

    // ----- speaker buttons (Windows system TTS, Manh 2026-11-16) -----

    /// <summary>Speaker icons (E767 Volume, same glyph as the float
    /// icon's speaker state): one at the bottom-right of the input
    /// area, one at the bottom-right of the output card body — both
    /// mirroring mac, where Speak + Copy sit on the right below the
    /// text in SourceInputView and in ProviderResultCard. Wired to
    /// the Windows system voices (SystemSpeechService): the source
    /// button speaks the source text in the source language, the
    /// translation button speaks the translation in the target
    /// language. Click while speaking stops. A second click on the
    /// same button therefore toggles speak/stop.</summary>
    private async void OnSpeakSourceClicked(object sender, RoutedEventArgs e)
        // Source language for the voice: the provider's detected
        // language when the source was "auto" (that is what the text
        // actually is), else the source language the user picked.
        // Without the detected language, "auto" fell back to the
        // system default voice ("An") for every language.
        => await SpeakAsync(_sourceText, _detectedSourceLanguage ?? App.Settings.SourceLanguage);

    private async void OnSpeakTranslationClicked(object sender, RoutedEventArgs e)
        => await SpeakAsync(_translationText, App.Settings.TargetLanguage);

    private async Task SpeakAsync(string text, string languageCode)
    {
        // A manual speaker click cancels any pending auto-play chain
        // (the chain belongs to the auto-played source, not to a
        // speaker the user pressed by hand).
        _autoPlayPendingTranslation = false;
        if (_speech.IsSpeaking)
        {
            _speech.Stop();
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
            return;
        await _speech.SpeakAsync(text, languageCode);
    }

    private void ShowPanelNear(int x, int y)
    {
        _epoch++;
        // New show: auto-fit runs fresh again (see ShowInputMode).
        _manualResizeGeneration = null;
        _autoFitRetried = false;
        // DPI: (x, y) and the work area are physical pixels, but
        // PanelWidth/PanelHeight are logical (DIPs) — scale them before
        // MoveAndResize, which takes physical pixels (see
        // ResizeToFitContent). At 100% the factor is 1.0 (no change).
        // Shared panel design (2026-11-16): the panel opens at the
        // DEFAULT size in Settings (420x450) every show — a manual drag
        // is not saved back, so these ARE the default. ResizeToFitContent
        // grows it if the content is tall.
        var scale = WindowHelpers.DpiScale(this);
        var w = Math.Max(1, (int)Math.Round(App.Settings.PanelWidth * scale));
        var h = Math.Max(1, (int)Math.Round(App.Settings.PanelHeight * scale));
        var gap = Math.Max(1, (int)Math.Round(8 * scale));
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x + gap, work.X, work.X + work.Width - w);
        var py = Math.Clamp(y + gap, work.Y, work.Y + work.Height - h);
        _lastProgrammaticSize = new Windows.Graphics.SizeInt32(w, h);
        WindowHelpers.MoveAndResize(this, px, py, w, h);
        WindowHelpers.ShowWithoutActivation(this);
        _visible = true;
        _clickOutside.Start();
        _escapeKey.Start();
        UpdateCopyButtons();
        // Multi-tab: every show opens on the translate tab.
        ShowTab(PanelTab.Translate);
        RequestResizeToFit();
    }

    /// <summary>Show the panel near the cursor (public so the Focus
    /// store can bring it forward when a reminder is routed to panel
    /// right). Already open: leave it where it is — the consume
    /// handler already switched it to the Fast Read tab, and moving
    /// a panel the user is looking at would be jarring.</summary>
    public void ShowPanelNearCursor()
    {
        if (_visible)
            return;
        var (x, y) = WindowHelpers.CursorPosition();
        ShowPanelNear(x, y);
    }

    /// <summary>Read a text in panel right (public so App can route a
    /// Fast Read request here when "Run FastRead in" is FastRead Lite):
    /// show the panel if it is hidden, then hand the text to the
    /// store — the consume handler below switches to the Fast Read
    /// tab, starts the read and pins.</summary>
    public void ShowPanelForReading(string text)
    {
        if (!_visible)
            ShowPanelNearCursor();
        // A user-started panel read must receive keys (Space to
        // play/pause, arrows to step): the panel shows without
        // activation, so activate it now that the user asked to read.
        // Reminder reads do NOT come through here (they request the
        // panel right directly), so they still never steal focus.
        WindowHelpers.ActivateWindow(this);
        _focusTimerStore.RequestPanelReading(text, null);
    }

    /// <summary>Show the panel on the left tab (Shortcuts — the Focus
    /// & Rest card lives there), public so the tray menu can jump
    /// straight to it (Manh 2026-11-21, "Focus & Rest" tray item):
    /// show the panel if it is hidden, switch to the Shortcuts tab,
    /// and activate it — the user asked for it from the tray, so it
    /// may take focus (same as a user-started panel read).</summary>
    public void ShowLeftPanel()
    {
        if (!_visible)
            ShowPanelNearCursor();
        ShowTab(PanelTab.Shortcuts);
        WindowHelpers.ActivateWindow(this);
    }

    /// <summary>Toggle the panel between its default size and its
    /// maximum (feature/focus-timer-decor, mac parity:
    /// togglePanelSize in PopupView): the lite reader's Maximize
    /// button calls this. The max is clamped to the visible work
    /// area (so the panel never covers the Dock / taskbar — the mac
    /// 08:11 fix), the top edge stays put, and the toggle counts as
    /// a manual resize so auto-fit does not fight it.</summary>
    public void TogglePanelSize()
    {
        var scale = WindowHelpers.DpiScale(this);
        var size = AppWindow.Size;
        var centerX = AppWindow.Position.X + size.Width / 2;
        var centerY = AppWindow.Position.Y + size.Height / 2;
        var work = WindowHelpers.DisplayWorkArea(centerX, centerY);
        var effectiveMaxW = Math.Min((int)Math.Round(MaxPanelWidth * scale), work.Width);
        var effectiveMaxH = Math.Min((int)Math.Round(MaxPanelSize * scale), work.Height);
        var isMax = size.Width >= effectiveMaxW - 1 && size.Height >= effectiveMaxH - 1;
        int targetW, targetH;
        if (isMax)
        {
            targetW = (int)Math.Round(App.Settings.PanelWidth * scale);
            targetH = (int)Math.Round(App.Settings.PanelHeight * scale);
        }
        else
        {
            targetW = effectiveMaxW;
            targetH = effectiveMaxH;
        }
        var minW = (int)Math.Round(MinPanelWidth * scale);
        var minH = (int)Math.Round(MinPanelHeight * scale);
        targetW = Math.Clamp(targetW, Math.Min(minW, effectiveMaxW), effectiveMaxW);
        targetH = Math.Clamp(targetH, Math.Min(minH, effectiveMaxH), effectiveMaxH);
        var x = Math.Clamp(AppWindow.Position.X, work.X, Math.Max(work.X, work.X + work.Width - targetW));
        var y = AppWindow.Position.Y;
        if (y + targetH > work.Y + work.Height)
            y = Math.Max(work.Y, work.Y + work.Height - targetH);
        _lastProgrammaticSize = new Windows.Graphics.SizeInt32(targetW, targetH);
        WindowHelpers.MoveAndResize(this, x, y, targetW, targetH);
        _manualResizeGeneration = _epoch;
    }

    /// <summary>Focus store changes (feature/focus-timer-decor): consume
    /// a panel-right request (switch to the Fast Read tab, start the
    /// read, pin — mac's consumePanelRightRequest) and freeze auto-fit
    /// while a panel-right read is running (mac sets
    /// manualResizeGeneration while reading, so the panel size stays
    /// put for the reader; auto-fit resumes when the read stops).</summary>
    private void OnFocusTimerStoreChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FocusTimerStore.PanelRightRequest))
        {
            var request = _focusTimerStore.PanelRightRequest;
            if (request is not null)
            {
                ShowTab(PanelTab.FastRead);
                // Safe direction (Manh 2026-11-22, mac parity): a
                // panel-right read starts KEYED — the panel activates
                // itself, so Space (Play/Pause) works at once, no
                // click into the panel first. This covers the reminder
                // path (ShowPanelNearCursor shows without activation)
                // as well as the manual paths (which already
                // activated). No global key capture: once the user
                // clicks to another app, the keys go there.
                WindowHelpers.ActivateWindow(this);
                _focusTimerStore.StartPanelReading(request.Text, request.CountdownSeconds, request.Title);
                SetPinned(true);
            }
        }
        else if (e.PropertyName == nameof(FocusTimerStore.PanelReadingViewModel))
        {
            if (_focusTimerStore.PanelReadingViewModel is not null)
                _manualResizeGeneration = _epoch;
            else
            {
                _manualResizeGeneration = null;
                RequestResizeToFit();
            }
        }
    }

    /// <summary>Grow/shrink the window to fit its content, like mac's
    /// PopupPanelSizing.frameToFit. All rows are Auto and the
    /// translation box is capped by MaxHeight (260), so the desired
    /// height is bounded; beyond the screen the text scrolls instead
    /// of the window growing off-screen. The top stays where
    /// ShowPanelNear / ShowInputMode put it; if the bottom would run
    /// off-screen the whole window shifts up. Deferred via the
    /// dispatcher so layout has finished before measuring. Only
    /// resizes when the height actually changed, so a resize never
    /// re-triggers itself. User-resizable (Manh 2026-11-15): the width
    /// is never changed here (mac frameToFit keeps currentFrame.width)
    /// and auto-fit sizes to the CONTENT, with no saved-height floor
    /// (shared design 2026-11-16 — a tall drag for long text must not
    /// keep the panel tall for a later one-word translation); after a
    /// manual resize in this show auto-fit is skipped entirely
    /// (_manualResizeGeneration == _epoch) so it cannot fight the
    /// user's drag.</summary>
    private void RequestResizeToFit()
    {
        DispatcherQueue.TryEnqueue(ResizeToFitContent);
    }

    /// <summary>Find the first descendant of a given type in the visual
    /// tree (used to reach the ScrollViewer inside the translation
    /// TextBox — GetTemplateChild is protected, so the Window cannot
    /// call it on the TextBox directly).</summary>
    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found)
                return found;
            var nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    /// <summary>Full vs visible height of the translation text, in
    /// logical pixels (DIPs), from the TextBox's ScrollViewer:
    /// ExtentHeight is the height of ALL the text, ViewportHeight the
    /// height currently visible. Returns false when the card is
    /// collapsed or the ScrollViewer is not ready (template not
    /// applied yet) — callers fall back to the previous behaviour
    /// (no snap-back, no cap change) instead of guessing.</summary>
    private bool TryGetTranslationTextHeights(out double full, out double visible)
    {
        full = 0;
        visible = 0;
        if (!_providerCardExpanded)
            return false;
        var sv = FindDescendant<ScrollViewer>(TranslationTextBox);
        if (sv is null)
            return false;
        full = sv.ExtentHeight;
        visible = sv.ViewportHeight;
        return !(double.IsNaN(full) || double.IsNaN(visible) || full <= 0 || visible <= 0);
    }

    private void ResizeToFitContent()
    {
        if (!_visible || Content is not FrameworkElement root)
            return;
        // Manual resize this show: do not override the user's size.
        if (_manualResizeGeneration == _epoch)
            return;
        // Reset the output cap (a manual drag may have raised it so the
        // box could grow past 260) and force layout, so the measure
        // below and the ScrollViewer read are both with the cap — the
        // ScrollViewer only updates on arrange, and ResizeToFitContent
        // runs from the dispatcher where layout may not have run yet
        // for the new text.
        TranslationTextBox.MaxHeight = TranslationTextMaxHeight;
        root.UpdateLayout();
        // DPI: XAML measures in logical pixels (DIPs) but AppWindow
        // MoveAndResize takes PHYSICAL pixels (Microsoft Learn, "Manage
        // app windows": AppWindow.Resize/ResizeClient take physical
        // pixels). At 150% scale the unscaled height made the window 1.5x
        // too short, cutting off the bottom row (Lenovo T460S, 2026-11-15).
        // Scale logical -> physical here; at 100% the factor is 1.0, so
        // the behaviour there is unchanged.
        var scale = WindowHelpers.DpiScale(this);
        // Measure at the CURRENT width (the user's width) — a wider
        // panel wraps the text into fewer lines and fits shorter.
        // The width itself is only snapped back below (for short text
        // that gains nothing from the extra width); it is never
        // widened by auto-fit, and long text keeps its width.
        var currentWidth = Math.Max(1.0, AppWindow.Size.Width / scale);
        root.Measure(new Windows.Foundation.Size(currentWidth, double.PositiveInfinity));
        var desired = root.DesiredSize.Height;
        if (desired <= 0 || double.IsNaN(desired) || double.IsInfinity(desired)
            || currentWidth < MinPanelWidth)
        {
            // Missing measurement — retry auto-fit once (shared design
            // 2026-11-16; mac had the same miss: a measure taken before
            // the window was sized reads wrong, e.g. width still ~0 so
            // the text wraps into a tall column). currentWidth below the
            // 400 minimum means the window has not been sized yet, so
            // the measure at that width is not the content's real size.
            // _autoFitRetried is per-show (reset in ShowPanelNear /
            // ShowInputMode), so a persistently bad measure cannot loop.
            if (!_autoFitRetried)
            {
                _autoFitRetried = true;
                RequestResizeToFit();
            }
            return;
        }
        var pos = AppWindow.Position;
        var work = WindowHelpers.DisplayWorkArea(pos.X, pos.Y);
        var minHeight = Math.Max(1, (int)Math.Round(ComputeMinPanelHeight() * scale));
        var maxHeight = Math.Max(minHeight, work.Height - 16);
        // Auto-fit to the CONTENT height (Manh 2026-11-16: the panel
        // must NOT stay tall for short text). The old baseline held
        // the persisted PanelHeight as a floor, so once the user
        // dragged the panel tall it opened tall for every later show
        // — even a one-word translation — and the user read that as
        // "the vertical still remembers the long position" (CI #143).
        // Shared design 2026-11-16: a drag is never saved to Settings
        // anymore and auto-fit has no saved-height floor — it sizes to
        // what the text needs, not to the tallest drag ever made.
        var baseline = desired;
        // The output box stays capped at 260 (TranslationTextMaxHeight)
        // during auto-fit — it scrolls for longer text instead of
        // growing the panel super tall. (The old longText raise set
        // MaxHeight to the full ExtentHeight after sizing, which left
        // the box uncapped and let the output expand super long on
        // the next show/measure — Manh 2026-11-16, #147 "output siêu
        // expand dài". Dragging the window taller by hand still grows
        // the box — SettleResizedSize raises the cap for manual drags.)
        var height = (int)Math.Clamp(Math.Ceiling(baseline * scale), minHeight, maxHeight);
        // Panel-size diagnostic (Manh 2026-11-22 — covered speaker, frame
        // hypothesis): log the outer vs client size on every fit, so the
        // leftover window frame is MEASURED instead of guessed. Temporary:
        // remove once the speaker bug is fixed.
        LogPanelSize(minHeight, baseline, height);
        // Horizontal snap-back on a NEW translation (Manh 2026-11-16):
        // auto-fit used to keep the current width, so after a long
        // text had widened the panel, a one-word translation left it
        // wide — no horizontal snap-back for short text, unlike the
        // drag-release snap-back in SettleResizedSize. Same rule here:
        // if the content is no meaningfully taller at the default
        // width (420) than at the current width, the extra width is
        // white space — snap back to 420. Long text keeps its width
        // (fewer wraps = a shorter panel). Only runs when the panel
        // is wider than the default, so a 420-wide panel is untouched.
        var widthDip = Math.Max(1.0, AppWindow.Size.Width / scale);
        // Never wider than MaxPanelWidth (600): a drag past it is
        // clamped back by SettleResizedSize, but a new translation
        // arriving mid-drag (before the 300 ms settle) must not keep
        // an over-wide panel either.
        widthDip = Math.Clamp(widthDip, MinPanelWidth, Math.Max(MinPanelWidth, MaxPanelWidth));
        var defaultW = App.Settings.PanelWidth; // 420
        if (widthDip > defaultW + 8)
        {
            var hAtDefault = MeasureContentHeight(root, defaultW);
            var hAtCurrent = MeasureContentHeight(root, widthDip);
            if (hAtDefault > 0 && hAtCurrent > 0 && hAtDefault <= hAtCurrent + 8)
                widthDip = defaultW;
        }
        var width = Math.Max(1, (int)Math.Round(widthDip * scale));
        var y = pos.Y;
        if (y + height > work.Y + work.Height)
            y = Math.Max(work.Y, work.Y + work.Height - height);
        if (AppWindow.Size.Height != height || AppWindow.Size.Width != width || pos.Y != y)
        {
            _lastProgrammaticSize = new Windows.Graphics.SizeInt32(width, height);
            WindowHelpers.MoveAndResize(this, pos.X, y, width, height);
        }
    }

    /// <summary>Panel-size diagnostic (Manh 2026-11-22 — covered speaker):
    /// log the outer window size vs the client size on every auto-fit, so
    /// the leftover window frame (WS_DLGFRAME left by
    /// SetBorderAndTitleBar — see TransparentWindow) is measured in
    /// physical pixels instead of guessed. The frame eats a fixed amount
    /// from the client area, so a shortfall that survives every cardMin
    /// increase points at the frame, not at the card. Temporary: remove
    /// once the speaker bug is fixed. Diagnostics must never break the
    /// panel, so every call is guarded.</summary>
    private void LogPanelSize(int minHeightPhysical, double baselineLogical, int heightPhysical)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var scale = WindowHelpers.DpiScale(this);
            var outer = AppWindow.Size;
            var clientW = 0;
            var clientH = 0;
            if (NativeMethods.GetClientRect(hwnd, out var client))
            {
                clientW = client.Right - client.Left;
                clientH = client.Bottom - client.Top;
            }
            WindowDiagnostics.AppendLine(
                $"[PanelSize] outer={outer.Width}x{outer.Height} client={clientW}x{clientH} " +
                $"frame={outer.Width - clientW}x{outer.Height - clientH} scale={scale:0.##} " +
                $"minH={minHeightPhysical} baseline={baselineLogical:0.#} height={heightPhysical} " +
                $"minParts=({_diagMinParts}) expanded={_providerCardExpanded} tab={_activeTab}");
        }
        catch
        {
            // Diagnostics must never break the panel.
        }
    }

    /// <summary>The input box grows as the user types (up to MaxHeight):
    /// grow the window with it, like the input side (Manh 2026-11-15).
    /// The box width is fixed and its height is content-driven, so a
    /// window resize never changes it back — no loop.</summary>
    private void OnSourceTextBoxSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Height != e.PreviousSize.Height)
            RequestResizeToFit();
    }

    /// <summary>Declare the title bar a NATIVE caption (drag) region
    /// (0.1.3.65 test): the OS moves the window itself — left button
    /// only, smooth, exactly like the Settings window's title bar. The
    /// manual AppWindow.Move drag was jerky and fired for right/middle
    /// buttons too; the system move (SendMessage WM_NCLBUTTONDOWN) never
    /// engaged for this no-activate window. InputNonClientPointerSource
    /// is the WinUI 3 native way to declare a drag region on a
    /// borderless window. The title bar holds only text (the gear button
    /// is in the header row below it), so the whole row is the region.
    /// Recomputed when the row's size changes (font size). SetRegionRects
    /// takes RectInt32 (PHYSICAL pixels, not DIPs — CS1503 in CI #66),
    /// so the bounds are scaled by the window's DPI factor.</summary>
    private void UpdateCaptionRegion()
    {
        if (TitleBar.ActualWidth <= 0 || TitleBar.ActualHeight <= 0)
            return;
        if (Content is not UIElement root)
            return;
        var bounds = TitleBar.TransformToVisual(root).TransformBounds(
            new Windows.Foundation.Rect(0, 0, TitleBar.ActualWidth, TitleBar.ActualHeight));
        var scale = WindowHelpers.DpiScale(this);
        var physical = new Windows.Graphics.RectInt32(
            (int)Math.Round(bounds.X * scale),
            (int)Math.Round(bounds.Y * scale),
            Math.Max(1, (int)Math.Round(bounds.Width * scale)),
            Math.Max(1, (int)Math.Round(bounds.Height * scale)));
        var source = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        source.SetRegionRects(NonClientRegionKind.Caption, new[] { physical });
    }

    /// <summary>Declare the native resize strips along the bottom and
    /// right edges (Manh 2026-11-15 — user-resizable panel). The OS
    /// shows the resize cursor there and handles the drag itself
    /// (including the correct cursor, snap, and DPI — the resize is
    /// done in physical pixels by the OS, so it is right at 150% with
    /// no manual logical/physical conversion). NonClientRegionKind
    /// (Microsoft Learn, Windows App SDK): BottomBorder / RightBorder
    /// are the border areas; there are no corner values, so the
    /// bottom-right corner is where the two strips overlap and the OS
    /// treats it as a diagonal resize. The strips are 8 physical px
    /// inside the root Grid's 12 px padding, so they never cover
    /// interactive content. Re-declared whenever the window size
    /// changes (OnAppWindowChanged) so they stay on the edges.
    /// Requires OverlappedPresenter.IsResizable = true (set in the
    /// Loaded handler via MakeBorderlessTopmost(resizable: true)).</summary>
    private void UpdateResizeRegions()
    {
        var size = AppWindow.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;
        var t = ResizeStripThickness;
        var source = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        source.SetRegionRects(NonClientRegionKind.BottomBorder, new[]
        {
            new Windows.Graphics.RectInt32(0, Math.Max(0, size.Height - t), size.Width, Math.Min(t, size.Height)),
        });
        source.SetRegionRects(NonClientRegionKind.RightBorder, new[]
        {
            new Windows.Graphics.RectInt32(Math.Max(0, size.Width - t), 0, Math.Min(t, size.Width), size.Height),
        });
    }

    /// <summary>Install the WM_GETMINMAXINFO subclass that enforces
    /// the panel's minimum size while the user drags a border. Uses
    /// SetWindowSubclass (comctl32, the safe subclass API — it chains
    /// to the previous proc and cleans up with the window). Idempotent
    /// for this window: the delegate is created once and kept in the
    /// _minSizeSubclassProc field.</summary>
    private void InstallMinSizeSubclass(IntPtr hwnd)
    {
        if (_minSizeSubclassProc is not null)
            return;
        _minSizeSubclassProc = MinSizeSubclassProc;
        NativeMethods.SetWindowSubclass(hwnd, _minSizeSubclassProc, new UIntPtr(2), UIntPtr.Zero);
    }

    /// <summary>Subclass proc: on WM_GETMINMAXINFO, set the minimum
    /// tracking size to (MinPanelWidth, ComputeMinPanelHeight) in
    /// PHYSICAL pixels. The OS then refuses to drag the window any
    /// smaller — the resize cursor simply stops at the minimum, and
    /// the output can never be hidden. Everything else is passed to
    /// the previous proc (DefSubclassProc).</summary>
    private IntPtr MinSizeSubclassProc(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == NativeMethods.WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
            var scale = WindowHelpers.DpiScale(this);
            var minW = Math.Max(1, (int)Math.Round(MinPanelWidth * scale));
            var minH = Math.Max(1, (int)Math.Round(ComputeMinPanelHeight() * scale));
            mmi.ptMinTrackSize = new NativeMethods.POINT { X = minW, Y = minH };
            Marshal.StructureToPtr(mmi, lParam, false);
        }
        if (uMsg == NativeMethods.WM_MOUSEWHEEL)
        {
            // Wheel over the title bar switches tabs (Manh 2026-11-19):
            // the title bar is the native caption region (drag to
            // move), and the OS swallows XAML pointer input there, so
            // the XAML PointerWheelChanged on TitleBar never fires —
            // the wheel only reaches the app as a window message.
            // WM_MOUSEWHEEL carries the cursor in screen coordinates
            // (lParam) and the delta in the high word of wParam. Only
            // the title bar rect is handled here; wheel anywhere else
            // falls through to the normal proc (XAML input, e.g.
            // scrolling the output).
            var delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            if (delta != 0 && TryGetTitleBarScreenRect(hWnd, out var titleBar))
            {
                var x = (short)(lParam.ToInt64() & 0xFFFF);
                var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                if (x >= titleBar.Left && x < titleBar.Right && y >= titleBar.Top && y < titleBar.Bottom)
                {
                    var now = DateTime.UtcNow;
                    if (now - _lastWheelTabSwitch >= TimeSpan.FromMilliseconds(400))
                    {
                        _lastWheelTabSwitch = now;
                        CycleTab(delta < 0 ? 1 : -1);
                    }
                    return IntPtr.Zero; // handled — do not pass on.
                }
            }
        }
        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>Title bar bounds in PHYSICAL screen pixels (for the
    /// WM_MOUSEWHEEL branch above): the window rect from GetWindowRect
    /// plus the title bar's bounds within the window (same DIP-to-
    /// physical math as UpdateCaptionRegion). False when the row has
    /// not been laid out yet.</summary>
    private bool TryGetTitleBarScreenRect(IntPtr hWnd, out NativeMethods.RECT rect)
    {
        rect = default;
        if (TitleBar.ActualWidth <= 0 || TitleBar.ActualHeight <= 0)
            return false;
        if (Content is not UIElement root)
            return false;
        if (!NativeMethods.GetWindowRect(hWnd, out var windowRect))
            return false;
        var bounds = TitleBar.TransformToVisual(root).TransformBounds(
            new Windows.Foundation.Rect(0, 0, TitleBar.ActualWidth, TitleBar.ActualHeight));
        var scale = WindowHelpers.DpiScale(this);
        rect = new NativeMethods.RECT
        {
            Left = windowRect.Left + (int)Math.Round(bounds.X * scale),
            Top = windowRect.Top + (int)Math.Round(bounds.Y * scale),
            Right = windowRect.Left + (int)Math.Round((bounds.X + bounds.Width) * scale),
            Bottom = windowRect.Top + (int)Math.Round((bounds.Y + bounds.Height) * scale),
        };
        return true;
    }

    // Diagnostic parts for the panel-size log (Manh 2026-11-22): filled
    // by ComputeMinPanelHeight on every call, read by LogPanelSize on
    // every fit. Temporary: remove with the log once the speaker bug
    // is fixed.
    private string _diagMinParts = "";

    /// <summary>Minimum panel height in DIPs, from the CURRENT content:
    /// the fixed rows' measured heights (title + input + hint +
    /// language + actions + dictionary + footer) plus the minimum
    /// output card plus the root Grid's padding (12 top + 12 bottom),
    /// floored at MinPanelHeight (450). This is what makes the
    /// minimum follow a multi-line input or a larger font size — a
    /// fixed 450 was not enough when the input grew (Manh
    /// 2026-11-16). RowDefinition.ActualHeight is the last layout's
    /// value; during a drag the input height does not change, so the
    /// stale value is correct. Capped at the display work area so a
    /// huge font size cannot make the minimum taller than the screen.</summary>
    private double ComputeMinPanelHeight()
    {
        // Force layout first: RowDefinition.ActualHeight is the LAST
        // layout's value, and MinSizeSubclassProc (WM_GETMINMAXINFO)
        // can call this before any layout has run for the current
        // content — a stale/tall ActualHeight would inflate the OS
        // minimum and clamp the window tall (Manh 2026-11-16, #149:
        // panel opened ~800 tall with a huge empty output on first
        // open). UpdateLayout is a no-op when nothing is dirty, so
        // calling it here is cheap on the ResizeToFitContent path
        // (which already laid out) and corrective on the OS path.
        RootGrid.UpdateLayout();
        var fixedH = 0.0;
        foreach (var row in RootGrid.RowDefinitions)
        {
            // Skip the * output row by TYPE as well as by reference:
            // the * row takes the leftover height, so its ActualHeight
            // is the window height, not content — including it would
            // make the minimum grow with the window itself.
            if (row.Height.GridUnitType == GridUnitType.Star
                || ReferenceEquals(row, OutputCardRow))
                continue;
            fixedH += row.ActualHeight;
        }
        // The output card only counts toward the minimum when it is
        // expanded: a collapsed card is just the header (~30), so
        // adding the full 160 minimum would keep the OS minimum (and
        // the panel) tall even with the provider collapsed — and with
        // many providers later, every collapsed card would hold the
        // panel up (Manh 2026-11-16). Collapsed, the header is small
        // enough that the 450 floor covers it.
        var cardMin = _providerCardExpanded ? MinOutputCardHeight : 0;
        // The card Border also carries a vertical margin (Margin
        // "0,0,0,8" in XAML) between the card and the Actions row —
        // it is NOT part of MinHeight, so it must be counted here too.
        // Without it the minimum (and the auto-fit floor) came out 8
        // short, the * row squeezed the card by 8, and the speaker row
        // at the bottom of the card lost its bottom ~8 px — which is
        // why raising MinOutputCardHeight 170 -> 180 -> 210 never
        // fixed the covered speaker (Manh 2026-11-17): the shortfall
        // was a missing margin, not a too-small minimum. Long text was
        // fine because its height came from the full content measure,
        // which does include the margin. Read the margin from the
        // Border itself so it stays right if the XAML margin changes.
        // NOTE 2026-11-22: the margin fix did NOT end the covering —
        // Manh still sees it on build 238, so the "missing margin"
        // conclusion above is stale. Current suspect: the leftover
        // window frame (see LogPanelSize); the diagnostic log measures
        // it instead of guessing.
        var cardMargin = _providerCardExpanded
            ? ProviderCardBorder.Margin.Top + ProviderCardBorder.Margin.Bottom
            : 0;
        var minH = Math.Max(MinPanelHeight, fixedH + cardMin + cardMargin + 24);
        // Diagnostic parts for the panel-size log (see LogPanelSize):
        // the fit logs these alongside the outer/client sizes.
        _diagMinParts = $"fixedH={fixedH:0.#} cardMin={cardMin} cardMargin={cardMargin} minH={minH:0.#}";
        var work = WindowHelpers.DisplayWorkArea(AppWindow.Position.X, AppWindow.Position.Y);
        var maxH = Math.Min(MaxPanelSize, work.Height / WindowHelpers.DpiScale(this));
        return Math.Min(minH, Math.Max(MinPanelHeight, maxH));
    }

    /// <summary>Track window size changes (Microsoft Learn, "Manage app
    /// windows": AppWindow.Changed + AppWindowChangedEventArgs.
    /// DidSizeChange). Two jobs: (1) keep the native resize strips on
    /// the edges (UpdateResizeRegions); (2) tell a user border-drag
    /// apart from our own programmatic MoveAndResize (show + auto-fit),
    /// so a manual resize suppresses auto-fit for the rest of this
    /// show (mac manualResizeGeneration) while our own resizes do
    /// neither. (A manual resize is never saved to Settings anymore —
    /// shared design 2026-11-16 — it only lives for the session.) The
    /// distinction is the size itself: a programmatic resize sets
    /// exactly _lastProgrammaticSize, a user drag lands on any other
    /// size.</summary>
    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange)
            return;
        UpdateResizeRegions();
        var size = sender.Size;
        // A programmatic MoveAndResize sets exactly _lastProgrammaticSize
        // — but the OS may adjust it by a pixel or two (DPI rounding,
        // min/max clamping), so match within a small tolerance instead
        // of exactly. An exact match misread a 1 px OS adjustment as a
        // USER drag, which suppressed auto-fit for the whole show
        // (_manualResizeGeneration = _epoch) and left the panel stuck
        // at the adjusted size (Manh 2026-11-16, #149 first-open tall
        // panel). A real user drag moves by far more than 2 px.
        const int programmaticTolerancePx = 2;
        if (_lastProgrammaticSize is { } expected
            && Math.Abs(size.Width - expected.Width) <= programmaticTolerancePx
            && Math.Abs(size.Height - expected.Height) <= programmaticTolerancePx)
        {
            // Our own MoveAndResize (show / auto-fit): not a user drag.
            _lastProgrammaticSize = null;
            return;
        }
        // User border-drag: suppress auto-fit for the rest of this show
        // (mac: manualResizeGeneration = translationGeneration) so it
        // cannot shrink the panel back while the user is resizing it.
        _manualResizeGeneration = _epoch;
        // Settle (debounced): Changed fires on every step of a drag,
        // so the clamp + snap-back run 300 ms after the last size change.
        // The Tick handler is attached once, at creation — attaching
        // it on every restart would stack a handler per size change
        // and run SettleResizedSize() N times.
        _resizeSettleTimer?.Stop();
        if (_resizeSettleTimer is null)
        {
            _resizeSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _resizeSettleTimer.Tick += (_, _) =>
            {
                _resizeSettleTimer?.Stop();
                SettleResizedSize();
            };
        }
        _resizeSettleTimer.Start();
    }

    /// <summary>Clamp a user border-drag to the min/max frame, snap
    /// back any excess, and grow to fit text that wrapped taller
    /// (shared panel design 2026-11-16; grow added 2026-11-17, #164). A drag only
    /// enlarges the panel for the SESSION — it is NOT saved to
    /// Settings anymore, so the next show opens at the default size
    /// (420x450) again. The window clamp below still applies (the OS
    /// does not enforce min/max on a borderless window). Called 300 ms
    /// after the last size change of a border drag (OnAppWindowChanged
    /// debounce).</summary>
    private void SettleResizedSize()
    {
        // No _visible guard: the timer only starts on a user resize
        // (which implies the panel was visible), and AppWindow.Size
        // still holds the resized size even if the user hid the panel
        // within the 300 ms debounce — that resize must still apply
        // to the window (it just is not saved anywhere).
        var scale = WindowHelpers.DpiScale(this);
        var size = AppWindow.Size;
        var w = size.Width / scale;
        var h = size.Height / scale;
        var work = WindowHelpers.DisplayWorkArea(AppWindow.Position.X, AppWindow.Position.Y);
        // Width caps at MaxPanelWidth (600), not MaxPanelSize (800):
        // the height may grow to 800 for long text, but the width
        // must not — 800 was ~double the default and the drag clamp
        // snapped back to it (Manh 2026-11-17).
        var maxW = Math.Min(MaxPanelWidth, work.Width / scale);
        var maxH = Math.Min(MaxPanelSize, work.Height / scale);
        var clampedW = Math.Clamp(w, MinPanelWidth, Math.Max(MinPanelWidth, maxW));
        var minPanelH = ComputeMinPanelHeight();
        var clampedH = Math.Clamp(h, minPanelH, Math.Max(minPanelH, maxH));
        // Vertical snap-back + output growth (Manh 2026-11-15 — full
        // Windows approach): the output card row is * and the text box
        // row inside it is *, so the box grows 1:1 with the window.
        // The window height needed to show ALL the translation text is
        // the current height plus (full text height - visible text
        // height), from the box's ScrollViewer. If the drag left the
        // window taller than that (short text), shrink it back — the
        // extra height was white space. If the text is longer than the
        // box shows, GROW the window to the needed height (Manh
        // 2026-11-17, #164): a horizontal drag wraps the text taller
        // while auto-fit is suppressed, so keeping the dragged height
        // left the card overflowing under the Actions row and covered
        // the speaker/Copy buttons — raising the box cap alone could
        // not help, the box had no room to grow into. Cannot measure
        // (card collapsed / ScrollViewer not ready): keep the dragged
        // height, previous behaviour. Snap-back only applies on the
        // translate tab: the other tabs have no translation text to
        // measure (the shortcuts list is fixed-height rows), so there
        // is nothing to snap back to — a drag there just clamps.
        if (_activeTab == PanelTab.Translate && TryGetTranslationTextHeights(out var fullText, out var visibleText))
        {
            var neededH = Math.Clamp(clampedH + (fullText - visibleText), minPanelH, maxH);
            if (clampedH > neededH + 8)
            {
                // Short text: the drag left white space — snap back.
                clampedH = neededH;
                TranslationTextBox.MaxHeight = TranslationTextMaxHeight;
            }
            else if (neededH > clampedH)
            {
                // Wrapped text needs MORE height than the panel has
                // (Manh 2026-11-17, #164 — see the comment above):
                // grow the panel to the needed height (already
                // clamped to the screen max) so the card stops
                // overflowing under the Actions row.
                clampedH = neededH;
                TranslationTextBox.MaxHeight = Math.Max(TranslationTextMaxHeight, fullText);
            }
            else
            {
                // Exact fit: keep the dragged height and let the output
                // grow to the full text height.
                TranslationTextBox.MaxHeight = Math.Max(TranslationTextMaxHeight, fullText);
            }
        }
        // Horizontal snap-back (shared design 2026-11-16): if the
        // dragged width does not meaningfully reduce the content
        // height, the extra width was white space (short text already
        // fits at the default width) — snap back to the default width
        // (420) on release. Long text keeps the dragged width (fewer
        // wraps = a shorter panel). The +8 threshold mirrors the
        // vertical snap-back: a width that saves more than 8 pt of
        // height is genuinely useful. Cannot measure: keep the dragged
        // width, previous behaviour.
        var defaultW = App.Settings.PanelWidth; // 420
        if (_activeTab == PanelTab.Translate && clampedW > defaultW + 8 && Content is FrameworkElement snapRoot)
        {
            var hAtDefault = MeasureContentHeight(snapRoot, defaultW);
            var hAtDragged = MeasureContentHeight(snapRoot, clampedW);
            if (hAtDefault > 0 && hAtDragged > 0 && hAtDefault <= hAtDragged + 8)
                clampedW = defaultW;
        }
        // Shared design 2026-11-16: the dragged size is NOT saved to
        // Settings (no PanelWidth/PanelHeight write, no Save) — the
        // next show opens at the default size again. The clamp below
        // only fixes the live window (the OS does not enforce min/max
        // on a borderless window): record it as programmatic so
        // OnAppWindowChanged ignores it.
        var cw = Math.Max(1, (int)Math.Round(clampedW * scale));
        var ch = Math.Max(1, (int)Math.Round(clampedH * scale));
        if (cw != size.Width || ch != size.Height)
        {
            var pos = AppWindow.Position;
            var y = pos.Y;
            if (y + ch > work.Y + work.Height)
                y = Math.Max(work.Y, work.Y + work.Height - ch);
            _lastProgrammaticSize = new Windows.Graphics.SizeInt32(cw, ch);
            WindowHelpers.MoveAndResize(this, pos.X, y, cw, ch);
        }
    }

    /// <summary>Content height (DIPs) if the panel were this wide,
    /// measured with unlimited height. Used by the horizontal
    /// snap-back to tell whether a wider drag actually helps (long
    /// text wraps into fewer lines) or is just empty width (short
    /// text). A failed measure returns 0 and the caller keeps the
    /// dragged width. Measure only computes a desired size — it does
    /// not move or resize the live window.</summary>
    private static double MeasureContentHeight(FrameworkElement root, double width)
    {
        if (width <= 0)
            return 0;
        root.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        var h = root.DesiredSize.Height;
        return double.IsNaN(h) || double.IsInfinity(h) || h <= 0 ? 0 : h;
    }

    /// <summary>Pin toggle: while pinned the panel stays put — click-outside
    /// and Esc do not hide it. Toggle again to unpin. (The panel is
    /// already topmost; pin is about not auto-hiding, not z-order.)</summary>
    /// <summary>Switch tabs (Manh 2026-11-17): show one tab's content,
    /// hide the others, and repaint the dots. The panel size does NOT
    /// change on a switch — the overlays span rows 1-6, the same area
    /// the translate tab uses. One exception (safety net): a drag on
    /// another tab can leave the panel shorter than the translate
    /// tab's minimum (its rows were collapsed, so the clamp floor was
    /// the 450 floor, not the translate minimum) — switching back to
    /// Translate grows the panel to the minimum so the output card
    /// never overflows under the Actions row (the #164 pattern). Only
    /// grows, never shrinks, width untouched. The panel always opens
    /// on the Translate tab (ShowPanelNear / ShowInputMode call this).</summary>
    private void ShowTab(PanelTab tab)
    {
        _activeTab = tab;
        var showTranslate = tab == PanelTab.Translate;
        var translateVisibility = showTranslate ? Visibility.Visible : Visibility.Collapsed;
        SourceTextBox.Visibility = translateVisibility;
        SourceOverlayPanel.Visibility = translateVisibility;
        HintTextBlock.Visibility = translateVisibility;
        LanguageBarGrid.Visibility = translateVisibility;
        ProviderCardBorder.Visibility = translateVisibility;
        ActionsPanel.Visibility = translateVisibility;
        DictionaryBarControl.Visibility = translateVisibility;
        // The embedded "?" card lives on panel center too: it shows
        // only on the Translate tab, and only while it is open — its
        // open state survives tab switches (see the field comment).
        EmbeddedShortcutsCard.Visibility = showTranslate && _embeddedShortcutsVisible ? Visibility.Visible : Visibility.Collapsed;
        ShortcutsTabPanel.Visibility = tab == PanelTab.Shortcuts ? Visibility.Visible : Visibility.Collapsed;
        FastReadTabPanel.Visibility = tab == PanelTab.FastRead ? Visibility.Visible : Visibility.Collapsed;
        if (tab == PanelTab.FastRead)
            // The demo's "Read in panel" reads the text being
            // translated — sync it now so the button state is right
            // even if the source changed while another tab was open.
            FastReadTabPanel.SourceText = _sourceText;
        if (tab == PanelTab.Shortcuts)
        {
            BuildShortcutRows(ShortcutsTabRowsPanel, static () => { });
            ApplyPanelLeftCardStates();
        }
        TabDotShortcuts.Background = tab == PanelTab.Shortcuts ? DotSelectedBrush : DotUnselectedBrush;
        TabDotTranslate.Background = tab == PanelTab.Translate ? DotSelectedBrush : DotUnselectedBrush;
        TabDotFastRead.Background = tab == PanelTab.FastRead ? DotSelectedBrush : DotUnselectedBrush;
        if (showTranslate && _visible)
        {
            RootGrid.UpdateLayout();
            var minH = ComputeMinPanelHeight();
            var scale = WindowHelpers.DpiScale(this);
            var currentH = AppWindow.Size.Height / scale;
            if (currentH < minH)
            {
                var w = AppWindow.Size.Width;
                var h = Math.Max(1, (int)Math.Round(minH * scale));
                _lastProgrammaticSize = new Windows.Graphics.SizeInt32(w, h);
                WindowHelpers.MoveAndResize(this, AppWindow.Position.X, AppWindow.Position.Y, w, h);
            }
        }

        // Fade the incoming tab in (Manh 2026-11-19, like mac).
        if (showTranslate)
            FadeInTabContent(SourceTextBox, SourceOverlayPanel, HintTextBlock, LanguageBarGrid,
                ProviderCardBorder, ActionsPanel, DictionaryBarControl, EmbeddedShortcutsCard);
        else if (tab == PanelTab.Shortcuts)
            FadeInTabContent(ShortcutsTabPanel);
        else
            FadeInTabContent(FastReadTabPanel);
    }

    /// <summary>Tab-switch fade (Manh 2026-11-19, like mac): the
    /// incoming tab's content fades in over 0.15s, ease-out. Only a
    /// fade-in — the outgoing tab just hides (its elements are
    /// Collapsed by ShowTab). A running fade is stopped and every tab
    /// element's opacity restored first, so a fast double-switch never
    /// leaves an element half-transparent. Skipped while the window
    /// is not loaded yet (first show) — the content then just
    /// appears, and the opacities are never touched, so nothing can
    /// get stuck invisible.</summary>
    private Storyboard? _tabFadeStoryboard;

    private void FadeInTabContent(params UIElement[] elements)
    {
        // Window has no IsLoaded of its own (it is not a
        // FrameworkElement — CS0103, CI #193-#196) — ask the content
        // root instead.
        if (Content is not FrameworkElement root || !root.IsLoaded)
            return;
        _tabFadeStoryboard?.Stop();
        foreach (var element in new UIElement[]
        {
            SourceTextBox, SourceOverlayPanel, HintTextBlock, LanguageBarGrid,
            ProviderCardBorder, ActionsPanel, DictionaryBarControl,
            EmbeddedShortcutsCard, ShortcutsTabPanel, FastReadTabPanel,
        })
            element.Opacity = 1;
        var storyboard = new Storyboard();
        foreach (var element in elements)
        {
            element.Opacity = 0;
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);
        }
        _tabFadeStoryboard = storyboard;
        storyboard.Begin();
    }

    private void OnTabDotClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button dot && int.TryParse(dot.Tag?.ToString(), out var index)
            && index >= 0 && index <= 2)
            ShowTab((PanelTab)index);
    }

    /// <summary>‹ › arrows beside the dots (Manh 2026-11-18): switch
    /// tabs by click. Wraps around (left from panel left goes to panel
    /// right, right from panel right goes to panel left) so the arrows
    /// are always clickable.</summary>
    private void OnTabPrevClicked(object sender, RoutedEventArgs e) => CycleTab(-1);

    private void OnTabNextClicked(object sender, RoutedEventArgs e) => CycleTab(1);

    /// <summary>Mouse wheel to switch tabs (Manh 2026-11-18, like mac):
    /// scroll down = next tab, scroll up = previous tab. Attached to
    /// the hint row, the whole bottom bar (DictionaryBar row down
    /// through the footer with the GitHub icon + version), and the
    /// dots row — so the wheel switches tabs from the top and bottom
    /// edges, not only over the dots. The title bar (app name +
    /// slogan) is the native caption region, so its wheel never
    /// reaches XAML — it is caught in the window subclass instead
    /// (WM_MOUSEWHEEL in MinSizeSubclassProc) and shares this same
    /// 400 ms throttle, so scrolling across a zone boundary cannot
    /// double-switch. One long scroll gesture sends many wheel ticks,
    /// so ticks within 0.4s of a switch are ignored — one long scroll
    /// = one tab switch, instead of flipping through several tabs at
    /// once. The throttle is shared across all zones.</summary>
    private void OnTabSwitchWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        var now = DateTime.UtcNow;
        if (now - _lastWheelTabSwitch < TimeSpan.FromMilliseconds(400))
            return;
        _lastWheelTabSwitch = now;
        e.Handled = true;
        CycleTab(delta < 0 ? 1 : -1);
    }

    /// <summary>Horizontal wheel over the Fast Read tab switches tabs
    /// (feature/focus-timer-decor, mac parity: the panel's trackpad
    /// monitor passes horizontal swipes through so they switch tabs).
    /// Vertical wheel never reaches here: the lite reader marks it
    /// handled (font size on the reading area, seek on the progress
    /// slider), and the XAML handler only sees unhandled events.</summary>
    private void OnFastReadTabWheel(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(FastReadTabPanel).Properties.IsHorizontalMouseWheel)
            return;
        OnTabSwitchWheel(sender, e);
    }

    private void CycleTab(int direction)
    {
        const int count = 3;
        var next = ((int)_activeTab + direction % count + count) % count;
        ShowTab((PanelTab)next);
    }

    /// <summary>Panel left card states (Manh 2026-11-18, mac parity):
    /// the "How the icon works" card's collapsed state is remembered
    /// in Settings; the shortcuts card's state lives only for this
    /// panel session. Chevron ⌄ = expanded, › = collapsed.</summary>
    private void ApplyPanelLeftCardStates()
    {
        var howCollapsed = App.Settings.HowIconWorksCollapsed;
        HowIconWorksBody.Visibility = howCollapsed ? Visibility.Collapsed : Visibility.Visible;
        HowIconWorksChevronText.Text = howCollapsed ? "›" : "⌄";
        ShortcutsCardBody.Visibility = _shortcutsCardCollapsed ? Visibility.Collapsed : Visibility.Visible;
        ShortcutsCardChevronText.Text = _shortcutsCardCollapsed ? "›" : "⌄";
        ApplyHowIconWorksFlow();
    }

    /// <summary>Card "How the icon works" is DYNAMIC (Manh 2026-11-18,
    /// mac round 8, FlowIntroCard): the Click / Alt+Click rows read the
    /// Settings "Click icon" choice (IconClickAction) and show what a
    /// click actually does right now — never a hardcoded mapping that
    /// goes stale when the default changes (mac's card used to say
    /// "Click → Listen" even after the default became Read). Windows
    /// only has 2 click actions (Speak / FastRead — no Translate
    /// option, unlike mac's triggerClickAction), so: FastRead → Click
    /// = Fast Read, Alt+Click = Listen; Speak (default) → Click =
    /// Listen, Alt+Click = Fast Read. Runs from
    /// ApplyPanelLeftCardStates every time panel left shows, so the
    /// card is always fresh — including after a Settings change (the
    /// panel hides when Settings opens, and panel left refreshes on
    /// the next show).</summary>
    private void ApplyHowIconWorksFlow()
    {
        var clickReads = string.Equals(App.Settings.IconClickAction, "FastRead", StringComparison.OrdinalIgnoreCase);
        HowClickActionText.Text = clickReads ? "Fast Read — read it faster" : "Listen — hear it read aloud";
        HowAltClickActionText.Text = clickReads ? "Listen — hear it read aloud" : "Fast Read — read it faster";
    }

    /// <summary>"How the icon works" header = the whole top bar: click
    /// it to collapse/expand the card. The state is remembered in
    /// Settings, so once Manh knows the flow the shortcuts card is the
    /// main one.</summary>
    private void OnHowIconWorksHeaderClicked(object sender, RoutedEventArgs e)
    {
        App.Settings.HowIconWorksCollapsed = !App.Settings.HowIconWorksCollapsed;
        SettingsStore.Save(App.Settings);
        ApplyPanelLeftCardStates();
    }

    /// <summary>Shortcuts card header = the whole top bar: click it to
    /// close/open the card (in-memory for this panel session).</summary>
    private void OnShortcutsCardHeaderClicked(object sender, RoutedEventArgs e)
    {
        _shortcutsCardCollapsed = !_shortcutsCardCollapsed;
        ApplyPanelLeftCardStates();
    }

    private void OnPinToggled(object sender, RoutedEventArgs e)
        => SetPinned(PinButton.IsChecked == true);

    /// <summary>Pin state (feature/focus-timer-decor): the pin lives
    /// in the dots row now and pins ALL three panels. The tooltip
    /// follows the state.</summary>
    public void SetPinned(bool pinned)
    {
        _pinned = pinned;
        PinButton.IsChecked = pinned;
        ToolTipService.SetToolTip(PinButton, pinned
            ? "Unpin panel"
            : "Pin panel (keep it visible)");
    }

    /// <summary>Two-stage Esc (feature/focus-timer-decor, mac parity):
    /// while a panel-right read is running the first Esc stops the
    /// read (and a pending reminder then shows itself), the second
    /// hides the panel. Pinned: Esc never hides. Shared by the
    /// KeyDown path and the low-level hook path.</summary>
    private void HandleEscape()
    {
        if (_focusTimerStore.PanelReadingViewModel is not null)
        {
            _focusTimerStore.StopPanelReading();
            return;
        }
        if (!_pinned)
            HidePanel();
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        // Hide the panel first: it is topmost (HWND_TOPMOST), so a
        // Settings window opened underneath it would be covered by it.
        HidePanel();
        _openSettings();
    }

    private void OnFastReadClicked(object sender, RoutedEventArgs e)
    {
        // The run location decides (feature/focus-timer-decor): the
        // Fast Read window hides the panel first (the topmost panel
        // would float above the reader), panel-right reading keeps it
        // open. App routes via RouteFastRead — this handler must NOT
        // hide unconditionally, or the panel-right path would hide
        // the panel it is about to read in.
        _ = _fastReadRequested(_sourceText);
    }

    /// <summary>"?" button (Manh 2026-11-18, mac parity): toggles the
    /// shortcuts table embedded at the top of the output area — the
    /// old Flyout popover is gone (a popover lost focus to the panel's
    /// click-outside auto-hide on mac, and the embedded card keeps the
    /// table inside the panel). While the card is open the "?" shows
    /// in the accent colour.</summary>
    private void OnShortcutsButtonClicked(object sender, RoutedEventArgs e)
        => SetEmbeddedShortcutsVisible(!_embeddedShortcutsVisible);

    /// <summary>Embedded "?" card header = the whole top bar: click it
    /// (or the × at its right end, or "?" again) to hide the card.</summary>
    private void OnEmbeddedShortcutsHeaderClicked(object sender, RoutedEventArgs e)
        => SetEmbeddedShortcutsVisible(false);

    private void SetEmbeddedShortcutsVisible(bool visible)
    {
        _embeddedShortcutsVisible = visible;
        if (visible)
        {
            // Rebuild so the key bindings are the current Settings values.
            BuildShortcutRows(EmbeddedShortcutsRowsPanel, () => SetEmbeddedShortcutsVisible(false));
        }
        EmbeddedShortcutsCard.Visibility = visible && _activeTab == PanelTab.Translate ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
            ShortcutsButtonText.Foreground = DotSelectedBrush;
        else
            ShortcutsButtonText.ClearValue(TextBlock.ForegroundProperty);
        // A collapse/expand is a new fit (same as SetProviderCardExpanded):
        // after a manual drag the flag suppresses auto-fit, so without
        // this the panel would not grow for the card and the * output
        // row would pay for it instead.
        _manualResizeGeneration = null;
        // Auto-fit grows the panel for the card (its Auto row) and
        // shrinks it back when the card closes.
        RequestResizeToFit();
    }

    /// <summary>Build the 10 shortcut rows (Settings order) into a
    /// StackPanel — shared by the embedded "?" card and the shortcuts
    /// tab, so the two can never drift apart. Only the Run button runs
    /// the action; clicking the row itself does nothing, so reading the
    /// list cannot trigger anything. Run HIDES THE PANEL FIRST, then
    /// runs the action (Manh 2026-11-18, mac parity): the panel would
    /// otherwise cover the screen the action needs (OCR opens the
    /// region picker, Settings opens its window). afterRun runs after
    /// the action: the embedded card hides itself. The tab passes a
    /// no-op — it used to switch back to the translate tab, but the
    /// panel is hidden now, so there is nothing to switch back to.</summary>
    private void BuildShortcutRows(StackPanel panel, Action afterRun)
    {
        panel.Children.Clear();
        foreach (var entry in _shortcuts)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = entry.Name,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(name, 0);
            row.Children.Add(name);

            var binding = new TextBlock
            {
                Text = DisplayBinding(entry.Binding()),
                FontSize = 12,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(binding, 1);
            row.Children.Add(binding);

            // Run (Segoe Fluent Icons Play E768, verified on Microsoft
            // Learn 2026-11-17): same code path as the hotkey and the
            // tray menu item — no physical key press needed. Only this
            // button runs the action; clicking the row itself does
            // nothing, so reading the list cannot trigger anything.
            // The embedded card closes after running: the action takes over
            // (OCR opens the region picker, Settings opens its window,
            // a translation re-shows the panel with the result).
            var run = new Button
            {
                Padding = new Thickness(6, 2, 6, 2),
            };
            // Attached property: in code it needs SetToolTip — the XAML
            // attribute syntax "ToolTipService.ToolTip = ..." is NOT
            // valid in an object initializer (CS0117 + CS0747, CI #165).
            ToolTipService.SetToolTip(run, "Run this action");
            run.Content = new FontIcon { Glyph = "\uE768", FontSize = 10 };
            run.Click += (_, _) =>
            {
                // Hide the panel FIRST (Manh 2026-11-18): the action
                // takes over the screen (OCR opens the region picker,
                // Settings opens its window, a translation re-shows
                // the panel with the result) — the topmost panel must
                // not cover it. The flyout closes via afterRun below.
                HidePanel();
                entry.Run();
                afterRun();
            };
            Grid.SetColumn(run, 2);
            row.Children.Add(run);

            panel.Children.Add(row);
        }
    }

    /// <summary>Hotkey spec ("Control+Alt+T") as shown in the shortcuts
    /// "Control" shortens to "Ctrl", like the tray's accelerator hints.</summary>
    private static string DisplayBinding(string spec)
        => spec.Replace("Control", "Ctrl", StringComparison.Ordinal);

    /// <summary>Hide the panel (public so App can hide it when opening
    /// the Fast Read window — the panel is topmost and would otherwise
    /// float above the reader). Idempotent: safe to call when already
    /// hidden (0.1.3.54 — the early return on !_visible once stuck the
    /// panel visible).</summary>
    public void HidePanel()
    {
        // Closing the panel stops a panel-right read (feature/
        // focus-timer-decor, mac parity: hiding the panel while
        // reading counts as stopping the read — a pending reminder
        // then shows itself). StopPanelReading with showPending=true
        // (the default) re-routes the reminder: to the Rest window,
        // or back to panel right — in which case a NEW read is now
        // running and the reminder wins: leave the panel open with
        // it instead of hiding (the consume handler already switched
        // to the Fast Read tab and pinned).
        if (_focusTimerStore.PanelReadingViewModel is not null)
        {
            _focusTimerStore.StopPanelReading();
            if (_focusTimerStore.PanelReadingViewModel is not null)
                return;
        }
        // No early return on !_visible (0.1.3.54): the panel got stuck
        // visible — HidePanel had run (setting _visible = false) while
        // the window stayed up, and every later HidePanel was then a
        // no-op, so neither the gear nor Esc could close it. Always
        // hide the window; the state flags are idempotent.
        _visible = false;
        _clickOutside.Stop();
        _escapeKey.Stop();
        // AppWindow.Hide() (the WinUI-native hide) instead of
        // ShowWindow(SW_HIDE): the HWND hide left this window up.
        AppWindow.Hide();
    }

    public void RefreshDictionaryBar(IReadOnlyList<DictionaryLink> links, string displayMode)
    {
        DictionaryBarControl.SetLinks(links, displayMode, _lookupText ?? _sourceText);
    }

    // ----- dictionary lookup follows the panel selection (mac parity) -----

    private void OnSourceSelectionChanged(object sender, RoutedEventArgs e)
        => OnPanelSelectionChanged((TextBox)sender);

    private void OnTranslationSelectionChanged(object sender, RoutedEventArgs e)
        => OnPanelSelectionChanged((TextBox)sender);

    private void OnPanelSelectionChanged(TextBox box)
    {
        _lookupText = string.IsNullOrEmpty(box.SelectedText) ? null : box.SelectedText;
        RefreshDictionaryBar(App.Settings.DictionaryLinks, App.Settings.DictionaryDisplayMode);
    }

    // ----- editable source: Enter re-translates -----

    private void OnSourceTextChanged(object sender, TextChangedEventArgs e)
    {
        _sourceText = SourceTextBox.Text;
    }

    private void OnSourceKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;
        var shiftDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (shiftDown)
        {
            // Shift+Enter: insert a newline at the caret. AcceptsReturn
            // is False (see XAML), so the box never inserts one itself.
            e.Handled = true;
            var pos = SourceTextBox.SelectionStart;
            var length = SourceTextBox.SelectionLength;
            SourceTextBox.Text = SourceTextBox.Text.Remove(pos, length).Insert(pos, "\n");
            SourceTextBox.SelectionStart = pos + 1;
            SourceTextBox.SelectionLength = 0;
            return;
        }
        // Plain Enter: translate again. (With AcceptsReturn=True the
        // TextBox consumed Enter in its own OnKeyDown and this handler
        // never fired — Enter only made a newline, 0.1.3.68.)
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(_sourceText))
            return;
        _lookupText = null;
        TranslationTextBox.Text = AppStrings.Get("Panel.Translating", "Translating…");
        ProviderStatusText.Text = "";
        SetProviderCardExpanded(true);
        _ = TranslateAndShowAsync(_sourceText);
    }

    // ----- language pickers + swap (mac LanguageBarView) -----

    private LanguageSuggestBoxHelper? _sourceLangHelper;
    private LanguageSuggestBoxHelper? _targetLangHelper;

    private void PopulateLangCombos()
    {
        // Searchable pickers (Manh 2026-11-19): the helper owns the
        // items, the popular-first order, and the live filter. The
        // onPicked callbacks below save + retranslate on pick.
        _sourceLangHelper ??= new LanguageSuggestBoxHelper(
            SourceLangCombo, includeAuto: true,
            () => App.Settings.SourceLanguage, "Auto Detect",
            code =>
            {
                if (string.Equals(code, App.Settings.SourceLanguage, StringComparison.Ordinal))
                    return;
                App.Settings.SourceLanguage = code;
                SettingsStore.Save(App.Settings);
                RetranslateCurrent();
            });
        _targetLangHelper ??= new LanguageSuggestBoxHelper(
            TargetLangCombo, includeAuto: false,
            () => App.Settings.TargetLanguage, string.Empty,
            code =>
            {
                if (string.Equals(code, App.Settings.TargetLanguage, StringComparison.Ordinal))
                    return;
                App.Settings.TargetLanguage = code;
                SettingsStore.Save(App.Settings);
                RetranslateCurrent();
            });
    }

    private void SyncLangCombos()
    {
        _sourceLangHelper?.Refresh(App.Settings.SourceLanguage);
        _targetLangHelper?.Refresh(App.Settings.TargetLanguage);
    }

    private void OnSwapLanguages(object sender, RoutedEventArgs e)
    {
        var oldSource = _sourceLangHelper?.CommittedTag ?? App.Settings.SourceLanguage;
        var oldTarget = _targetLangHelper?.CommittedTag ?? App.Settings.TargetLanguage;
        // Target can never be Auto: swapping out of Auto targets English.
        App.Settings.SourceLanguage = oldTarget;
        App.Settings.TargetLanguage = oldSource == LanguageCode.Auto ? LanguageCode.English : oldSource;
        SettingsStore.Save(App.Settings);
        SyncLangCombos();
        RetranslateCurrent();
    }

    private void RetranslateCurrent()
    {
        if (!_visible || string.IsNullOrWhiteSpace(_sourceText))
            return;
        _lookupText = null;
        TranslationTextBox.Text = AppStrings.Get("Panel.Translating", "Translating…");
        ProviderStatusText.Text = "";
        SetProviderCardExpanded(true);
        _ = TranslateAndShowAsync(_sourceText);
    }

    private static async Task CopyWithFeedbackAsync(Button button, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        await ClipboardHelper.CopyTextAsync(text);
        var original = button.Content;
        button.Content = AppStrings.Get("Panel.Copied", "Copied ✓");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = original;
        };
        timer.Start();
    }
}
