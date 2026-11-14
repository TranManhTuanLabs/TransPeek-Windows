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
}
