// Float translate panel (SPEC §3.1): borderless, topmost, no-activate.
// Shows the source text, the translation, [Copy] (normal mode) or
// [Copy gốc] / [Copy dịch] (OCR mode), and the dictionary bar at the
// bottom. Click outside, Esc, or the ✕ button closes it.
//
// v0.1 has NO speaker button and NO click-to-read: TTS comes after the
// mac side is stable (see README).

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Translation;
using TransPeek.Interop;
using TransPeek.Services;

namespace TransPeek.Views;

public sealed partial class TranslatePanelWindow : Window, ITranslatePanel
{
    private const int PanelWidth = 440;
    private const int PanelHeight = 400;

    private readonly Func<string, Task<(string? Translation, string ProviderName)>> _translateAsync;
    private readonly Func<Task> _ocrRequested;
    private readonly ClickOutsideMonitor _clickOutside;
    private string _sourceText = "";
    private string _translationText = "";
    private bool _visible;
    private bool _ocrMode;

    public TranslatePanelWindow(
        Func<string, Task<(string? Translation, string ProviderName)>> translateAsync,
        Func<Task> ocrRequested)
    {
        _translateAsync = translateAsync;
        _ocrRequested = ocrRequested;
        InitializeComponent();
        // WinUI 3 Window is not a UIElement, so it has no Loaded event:
        // attach to the root content element instead.
        if (Content is UIElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                WindowHelpers.MakeBorderlessTopmost(this);
                WindowHelpers.MakeToolWindow(this);
            };
        }
        CloseButton.Click += (_, _) => HidePanel();
        CopyButton.Click += async (_, _) => await CopyWithFeedbackAsync(CopyButton, _translationText);
        CopySourceButton.Click += async (_, _) => await CopyWithFeedbackAsync(CopySourceButton, _sourceText);
        CopyTranslationButton.Click += async (_, _) => await CopyWithFeedbackAsync(CopyTranslationButton, _translationText);
        OcrButton.Click += (_, _) => { HidePanel(); _ = _ocrRequested(); };
        // WinUI 3 Window has no KeyDown event: Esc is a keyboard
        // accelerator on the root element instead.
        var esc = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        esc.Invoked += (_, args) =>
        {
            HidePanel();
            args.Handled = true;
        };
        contentRoot.KeyboardAccelerators.Add(esc);
        _clickOutside = new ClickOutsideMonitor(
            DispatcherQueue.GetForCurrentThread(),
            isActive: () => _visible,
            getRect: () =>
            {
                var pos = AppWindow.Position;
                var size = AppWindow.Size;
                return (pos.X, pos.Y, size.Width, size.Height);
            },
            onClickOutside: HidePanel);
        RefreshDictionaryBar(App.Settings.DictionaryLinks, App.Settings.DictionaryDisplayMode);
    }

    // ----- selection flow (App calls this) -----

    /// <summary>Show the panel near (x, y) and translate the text.</summary>
    public void ShowTranslationFlow(string text, int x, int y)
    {
        _ocrMode = false;
        ShowPanelNear(x, y);
        SetSource(text);
        TranslationText.Text = "Đang dịch…";
        ProviderText.Text = "";
        _ = TranslateAndShowAsync(text);
    }

    // ----- OCR flow (ITranslatePanel) -----

    public void ShowTranslating(string sourceText)
    {
        _ocrMode = true;
        var (cx, cy) = WindowHelpers.CursorPosition();
        ShowPanelNear(cx, cy);
        SetSource(sourceText);
        TranslationText.Text = "Đang dịch…";
        ProviderText.Text = "";
    }

    public void ShowResult(string sourceText, string? translation, string providerName)
    {
        SetSource(sourceText);
        _translationText = translation ?? "";
        TranslationText.Text = _translationText;
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
        SourceTextBox.Text = text;
        LangLabel.Text = $"{LanguageCode.DisplayName(App.Settings.SourceLanguage)} → {LanguageCode.DisplayName(App.Settings.TargetLanguage)}";
        RefreshDictionaryBar(App.Settings.DictionaryLinks, App.Settings.DictionaryDisplayMode);
    }

    private void UpdateCopyButtons()
    {
        CopyButton.Visibility = _ocrMode ? Visibility.Collapsed : Visibility.Visible;
        CopySourceButton.Visibility = _ocrMode ? Visibility.Visible : Visibility.Collapsed;
        CopyTranslationButton.Visibility = _ocrMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowPanelNear(int x, int y)
    {
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x + 8, work.X, work.X + work.Width - PanelWidth);
        var py = Math.Clamp(y + 8, work.Y, work.Y + work.Height - PanelHeight);
        WindowHelpers.MoveAndResize(this, px, py, PanelWidth, PanelHeight);
        WindowHelpers.ShowWithoutActivation(this);
        _visible = true;
        _clickOutside.Start();
        UpdateCopyButtons();
    }

    private void HidePanel()
    {
        if (!_visible)
            return;
        _visible = false;
        _clickOutside.Stop();
        WindowHelpers.Hide(this);
    }

    public void RefreshDictionaryBar(IReadOnlyList<DictionaryLink> links, string displayMode)
    {
        DictionaryBarControl.SetLinks(links, displayMode, _sourceText);
    }

    private static async Task CopyWithFeedbackAsync(Button button, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        await ClipboardHelper.CopyTextAsync(text);
        var original = button.Content;
        button.Content = "Đã copy ✓";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = original;
        };
        timer.Start();
    }
}
