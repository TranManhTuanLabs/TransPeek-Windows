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
using TransPeek.Assets;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using TransPeek.Interop;
using TransPeek.Services;
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
    /// in DIPs. Must match the MinHeight="170" in XAML: header (~34) +
    /// one divider (1) + text box minimum (60, MinHeight on the
    /// TranslationTextBox so the output never collapses) + Speak/Copy
    /// row (~36) + margins (~19), plus 20 of breathing room (Manh
    /// 2026-11-16: at 150 the box was a touch small and clipped the
    /// Copy/speaker buttons for a one-word translation; 160 (17/11)
    /// fixed three-line text but one word still clipped, so 170).
    /// The card never collapses below this while expanded, so the
    /// output is always visible — the vertical equivalent of
    /// MinPanelWidth for the horizontal (Manh 2026-11-16: the output
    /// shrank to nothing when the panel was dragged small, even with
    /// long text). When the provider is collapsed the border
    /// MinHeight goes to 0 instead (see SetProviderCardExpanded).</summary>
    private const double MinOutputCardHeight = 170;
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
    private bool _syncingCombos;
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

    public TranslatePanelWindow(
        Func<string, Task<(string? Translation, string ProviderName, string? DetectedSourceLanguage)>> translateAsync,
        Func<Task> ocrRequested,
        Action openSettings)
    {
        _translateAsync = translateAsync;
        _ocrRequested = ocrRequested;
        _openSettings = openSettings;
        InitializeComponent();
        PopulateLangCombos();
        SyncLangCombos();
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
            contentRoot.KeyDown += (_, e) =>
            {
                if (e.Key == VirtualKey.Escape)
                {
                    HidePanel();
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
                if (epoch == _epoch && !_pinned)
                    HidePanel();
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
        RequestResizeToFit();
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
        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

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
        var minH = Math.Max(MinPanelHeight, fixedH + cardMin + 24);
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

    /// <summary>Clamp a user border-drag to the min/max frame and snap
    /// back any excess (shared panel design 2026-11-16). A drag only
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
        // box shows, keep the dragged height and raise the box cap so
        // the output grows past 260 and shows more text instead of
        // leaving the extra height empty. Cannot measure (card
        // collapsed / ScrollViewer not ready): keep the dragged height,
        // previous behaviour.
        if (TryGetTranslationTextHeights(out var fullText, out var visibleText))
        {
            var neededH = Math.Clamp(clampedH + (fullText - visibleText), minPanelH, maxH);
            if (clampedH > neededH + 8)
            {
                // Short text: the drag left white space — snap back.
                clampedH = neededH;
                TranslationTextBox.MaxHeight = TranslationTextMaxHeight;
            }
            else
            {
                // Long text (or exact fit): keep the dragged height
                // and let the output grow to the full text height.
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
        if (clampedW > defaultW + 8 && Content is FrameworkElement snapRoot)
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
    private void OnPinToggled(object sender, RoutedEventArgs e)
    {
        _pinned = PinButton.IsChecked == true;
        ToolTipService.SetToolTip(PinButton, _pinned
            ? "Unpin panel"
            : "Pin panel (keep it visible)");
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        // Hide the panel first: it is topmost (HWND_TOPMOST), so a
        // Settings window opened underneath it would be covered by it.
        HidePanel();
        _openSettings();
    }

    private void HidePanel()
    {
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

    private void PopulateLangCombos()
    {
        SourceLangCombo.Items.Add(new ComboBoxItem { Content = "Auto Detect", Tag = LanguageCode.Auto });
        foreach (var (code, name) in LanguageCode.Supported)
        {
            SourceLangCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
            TargetLangCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        }
    }

    private void SyncLangCombos()
    {
        _syncingCombos = true;
        try
        {
            SelectComboByTag(SourceLangCombo, App.Settings.SourceLanguage);
            SelectComboByTag(TargetLangCombo, App.Settings.TargetLanguage);
        }
        finally
        {
            _syncingCombos = false;
        }
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string SelectedTag(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? LanguageCode.Auto;

    private void OnSourceLangChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCombos || SourceLangCombo.SelectedItem is not ComboBoxItem)
            return;
        App.Settings.SourceLanguage = SelectedTag(SourceLangCombo);
        SettingsStore.Save(App.Settings);
        RetranslateCurrent();
    }

    private void OnTargetLangChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCombos || TargetLangCombo.SelectedItem is not ComboBoxItem)
            return;
        App.Settings.TargetLanguage = SelectedTag(TargetLangCombo);
        SettingsStore.Save(App.Settings);
        RetranslateCurrent();
    }

    private void OnSwapLanguages(object sender, RoutedEventArgs e)
    {
        var oldSource = SelectedTag(SourceLangCombo);
        var oldTarget = SelectedTag(TargetLangCombo);
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
