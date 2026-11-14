// Float translate panel (SPEC §3.1): borderless, topmost, no-activate.
// Shows the source text, the translation, [Copy] (normal mode) or
// [Copy gốc] / [Copy dịch] (OCR mode), and the dictionary bar at the
// bottom. Click outside, Esc, or the ✕ button closes it.
//
// v0.1 has NO speaker button and NO click-to-read: TTS comes after the
// mac side is stable (see README).
//
// v0.1.2 mac parity — lookup follows the panel selection (mac PopupView):
// selecting words in the source box OR in the translation re-points the
// dictionary links at the selection; clearing the selection points them
// back at the source text that opened the panel. The source box is
// editable (Enter re-translates, Shift+Enter newlines) and the header
// has source/target pickers + swap, like the mac LanguageBarView.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TransPeek.Assets;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using TransPeek.Interop;
using TransPeek.Services;
using Windows.System;
using Windows.UI.Core;

namespace TransPeek.Views;

public sealed partial class TranslatePanelWindow : Window, ITranslatePanel
{
    private const int PanelWidth = 440;
    private const int PanelHeight = 480;

    private readonly Func<string, Task<(string? Translation, string ProviderName)>> _translateAsync;
    private readonly Func<Task> _ocrRequested;
    private readonly ClickOutsideMonitor _clickOutside;
    private readonly EscapeKeyMonitor _escapeKey;
    private string _sourceText = "";
    private string _translationText = "";
    /// <summary>Text currently selected inside the panel (source or
    /// translation), or null to look up the source text. Mac parity:
    /// dictionary links follow the live selection.</summary>
    private string? _lookupText;
    private bool _visible;
    private bool _ocrMode;
    private bool _syncingCombos;
    /// <summary>Show generation: incremented on every ShowPanelNear /
    /// ShowInputMode. The click-outside hook captures the epoch at
    /// click time and only hides when it is unchanged — a hide enqueued
    /// by the click that opened the panel (tray menu) must not kill
    /// the panel that click re-showed.</summary>
    private int _epoch;
    private readonly Action _openSettings;

    public TranslatePanelWindow(
        Func<string, Task<(string? Translation, string ProviderName)>> translateAsync,
        Func<Task> ocrRequested,
        Action openSettings)
    {
        _translateAsync = translateAsync;
        _ocrRequested = ocrRequested;
        _openSettings = openSettings;
        InitializeComponent();
        PopulateLangCombos();
        SyncLangCombos();
        // WinUI 3 Window has no Loaded event (Loaded lives on
        // FrameworkElement): attach to the root content element instead.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                WindowHelpers.MakeBorderlessTopmost(this);
                WindowHelpers.MakeToolWindow(this);
                // Native drag region (see UpdateCaptionRegion): set once
                // the layout is known, and again whenever the title bar
                // resizes (font size change).
                UpdateCaptionRegion();
                TitleBar.SizeChanged += (_, _) => UpdateCaptionRegion();
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
            // WinUI 3 Window has no KeyDown event: Esc is a keyboard
            // accelerator on the root element instead. Same caveat as
            // RegionSelectWindow (v0.1.1): an accelerator needs a focused
            // element, so KeyDown on the root is the primary Esc path.
            var esc = new KeyboardAccelerator { Key = VirtualKey.Escape };
            esc.Invoked += (_, args) =>
            {
                HidePanel();
                args.Handled = true;
            };
            contentRoot.KeyboardAccelerators.Add(esc);
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
                if (epoch == _epoch)
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
                if (epoch == _epoch)
                    HidePanel();
            });
        RefreshDictionaryBar(App.Settings.DictionaryLinks, App.Settings.DictionaryDisplayMode);
        // Title + footer (AppInfo is the single source for name,
        // slogan, repo URL and build number).
        AppNameText.Text = AppInfo.Name;
        TaglineText.Text = AppInfo.Tagline;
        FooterBuildText.Text = AppInfo.CiBuildNumber > 0
            ? $"{AppInfo.Version} (build {AppInfo.CiBuildNumber})"
            : AppInfo.Version;
        GithubMarkFooterImage.Source = GitHubMark.Image();
        GithubLink.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri(AppInfo.RepositoryUrl));
    }

    // ----- selection flow (App calls this) -----

    /// <summary>Show the panel near (x, y) and translate the text.</summary>
    public void ShowTranslationFlow(string text, int x, int y)
    {
        _ocrMode = false;
        ShowPanelNear(x, y);
        SetSource(text);
        TranslationTextBox.Text = AppStrings.Get("Panel.Translating", "Translating…");
        ProviderText.Text = "";
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
        // Win32 only: DisplayArea.Primary.WorkArea threw on this machine
        // (same WinRT display bug as FindAll()) and the tray Manual
        // Translation click died silently — the panel never appeared.
        var work = WindowHelpers.PrimaryWorkArea();
        var px = work.X + Math.Max((work.Width - PanelWidth) / 2, 0);
        var py = work.Y + Math.Max((work.Height - PanelHeight) / 2, 0);
        WindowHelpers.MoveAndResize(this, px, py, PanelWidth, PanelHeight);
        SetSource("");
        _translationText = "";
        TranslationTextBox.Text = "";
        ProviderText.Text = "";
        UpdateCopyButtons();
        // Typing mode needs a real active window (Activate() alone does
        // nothing on a NOACTIVATE window — Ctrl+V would not work).
        WindowHelpers.ActivateWindow(this);
        _visible = true;
        _clickOutside.Start();
        _escapeKey.Start();
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
        ProviderText.Text = "";
    }

    public void ShowResult(string sourceText, string? translation, string providerName)
    {
        SetSource(sourceText);
        _translationText = translation ?? "";
        TranslationTextBox.Text = _translationText;
        ProviderText.Text = string.IsNullOrEmpty(providerName) ? "" : $"via {providerName}";
        UpdateCopyButtons();
    }

    // ----- internals -----

    private async Task TranslateAndShowAsync(string text)
    {
        var (translation, providerName) = await _translateAsync(text);
        // The user may have closed the panel while translating.
        if (!_visible)
            return;
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

    private void ShowPanelNear(int x, int y)
    {
        _epoch++;
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x + 8, work.X, work.X + work.Width - PanelWidth);
        var py = Math.Clamp(y + 8, work.Y, work.Y + work.Height - PanelHeight);
        WindowHelpers.MoveAndResize(this, px, py, PanelWidth, PanelHeight);
        WindowHelpers.ShowWithoutActivation(this);
        _visible = true;
        _clickOutside.Start();
        _escapeKey.Start();
        UpdateCopyButtons();
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
        ProviderText.Text = "";
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
        ProviderText.Text = "";
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
