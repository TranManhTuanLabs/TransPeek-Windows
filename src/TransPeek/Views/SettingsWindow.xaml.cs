// Settings window (SPEC §2): NavigationView with one page per section.
// Pages are created via Frame.Navigate(type, onSaved) — the callback lets
// a page tell the app "settings changed, rebuild providers / hotkeys".
// Changes save immediately (no Apply button): every control writes to
// App.Settings + SettingsStore on change.
//
// v0.1.1: ShowSettings() sizes and centers the window the first time it
// is shown (v0.1 left the size to WinUI's default) and always brings it
// to the front. Note the window is single-use in WinUI 3: closing it
// with the ✕ button destroys it, so App recreates it on the next open
// (a closed Window cannot be re-Activated).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using TransPeek.Interop;
using TransPeek.Views.SettingsPages;
using WinRT.Interop;

namespace TransPeek.Views;

public sealed partial class SettingsWindow : Window
{
    /// <summary>Raised after a page saved settings (App rebuilds providers,
    /// hotkeys, and the panel's dictionary bar).</summary>
    public event Action? SettingsSaved;

    private bool _positioned;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = "TransPeek Settings";
        // Apply the Settings font size whenever a page is navigated to
        // (a fresh page has the XAML sizes until this runs).
        ContentFrame.Navigated += (_, _) => RefreshSettingsFontSize();
        // WinUI 3 Window has no Loaded event (Loaded lives on
        // FrameworkElement): attach to the root content element instead.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                NavView.SelectedItem = NavView.MenuItems[0];
                SelectPage("General");
            };
        }
    }

    public void ShowSettings()
    {
        RefreshSettingsFontSize();
        if (!_positioned)
        {
            _positioned = true;
            try
            {
                const int width = 960;
                const int height = 640;
                var (cx, cy) = WindowHelpers.CursorPosition();
                var work = WindowHelpers.DisplayWorkArea(cx, cy);
                var x = work.X + Math.Max(0, (work.Width - width) / 2);
                var y = work.Y + Math.Max(0, (work.Height - height) / 2);
                WindowHelpers.MoveAndResize(this, x, y, width, height);
            }
            catch
            {
                // Positioning must never stop the window from opening.
            }
        }
        // Bring it to the front as the focused window (0.1.3.53 test:
        // tray left-click while Settings sat behind other windows did
        // not focus it). Activate() alone does not restore a minimized
        // window, and a background app cannot always steal the
        // foreground — restore first, then force it forward (the tray
        // click is user input aimed at this app, so the foreground
        // grant applies).
        var hwnd = WindowNative.GetWindowHandle(this);
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
        Activate();
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            SelectPage(tag);
    }

    private void SelectPage(string tag)
    {
        Type? pageType = tag switch
        {
            "General" => typeof(GeneralPage),
            "Translation" => typeof(TranslationPage),
            "Dictionary" => typeof(DictionaryPage),
            "OCR" => typeof(OcrPage),
            "About" => typeof(AboutPage),
            _ => null,
        };
        if (pageType is not null)
            ContentFrame.Navigate(pageType, (Action)NotifySaved);
    }

    private void NotifySaved() => SettingsSaved?.Invoke();

    /// <summary>Apply the Settings window font size (AppTextSize, mac
    /// appTextSize, Manh 2026-11-15 — "Setting Text Size (chỉnh cả
    /// font chính + mô tả (-1)"). Sets FontSize on the NavigationView,
    /// which controls inherit (toggle/combo/textbox/slider labels and
    /// the nav pane text), then fixes up TextBlocks that set an
    /// explicit size: brand titles (>= 20 pt, e.g. the About
    /// "TransPeek" 24 pt) are left alone; grey/red description text
    /// goes 1 pt below the main size (the -1 rule); semibold/bold
    /// headers go 2 pt above; other text takes the main size. Called
    /// on show, on page navigation, and whenever settings are saved
    /// (App.OnSettingsSaved). Stable across calls: the base size is
    /// captured once in TextBlock.Tag (free on these TextBlocks), the
    /// role comes from Foreground/FontWeight (never changed here), and
    /// sizes are always set from the setting, never scaled from the
    /// current size, so repeated calls never compound or stick.</summary>
    public void RefreshSettingsFontSize()
    {
        var size = App.Settings.AppTextSize;
        NavView.FontSize = size;
        // Defer the TextBlock walk: when called from Navigated, the new
        // page is not in the visual tree yet, so the walk finds nothing
        // and only the General tab (the one present at show time) ever
        // got sized — Manh 2026-11-15 ("mới chỉnh đc mỗi tab general").
        // Enqueueing runs after the navigation/layout completes, so the
        // walk sees the new page's tree. Idempotent (sizes are always
        // set from the setting, never scaled), so queued repeats from a
        // fast slider drag are harmless and run in order. (this.
        // DispatcherQueue — the Window instance property, not the
        // Microsoft.UI.Dispatching type, cf. TranslatePanelWindow's
        // CS0176 note.)
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (ContentFrame.Content is DependencyObject content)
                ApplySettingsFontSize(content, size);
        });
    }

    private static void ApplySettingsFontSize(DependencyObject element, double size)
    {
        if (element is TextBlock tb
            && tb.ReadLocalValue(TextBlock.FontSizeProperty) != DependencyProperty.UnsetValue)
        {
            if (tb.Tag is not double baseSize)
            {
                baseSize = tb.FontSize;
                tb.Tag = baseSize;
            }
            if (baseSize < 20)
            {
                var isDescription = tb.Foreground is SolidColorBrush brush
                    && (brush.Color == Microsoft.UI.Colors.Gray
                        || brush.Color == Microsoft.UI.Colors.Red);
                tb.FontSize = isDescription
                    ? Math.Max(8, size - 1)
                    : tb.FontWeight == FontWeights.SemiBold || tb.FontWeight == FontWeights.Bold
                        ? size + 2
                        : size;
            }
        }
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
            ApplySettingsFontSize(VisualTreeHelper.GetChild(element, i), size);
    }
}
