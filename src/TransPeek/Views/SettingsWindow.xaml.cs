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
using Microsoft.UI.Xaml.Input;
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

    // Pane resize drag (Manh 2026-11-14): the overlay strip drags
    // NavigationView.OpenPaneLength. Start X + start length are captured
    // on press so the pane follows the pointer 1:1 from where the drag
    // began, instead of jumping to the pointer.
    private bool _paneResizing;
    private double _paneResizeStartX;
    private double _paneResizeStartLength;

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
                // Watch the pane toggle: when the user collapses the
                // pane to icons, the handle must hide too (see
                // UpdatePaneResizeHandle).
                NavView.RegisterPropertyChangedCallback(
                    NavigationView.IsPaneOpenProperty,
                    (_, _) => UpdatePaneResizeHandle());
                UpdatePaneResizeHandle();
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

    private void OnPaneResizePressed(object sender, PointerRoutedEventArgs e)
    {
        _paneResizing = true;
        _paneResizeStartX = e.GetCurrentPoint(RootGrid).Position.X;
        _paneResizeStartLength = NavView.OpenPaneLength;
        PaneResizeHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPaneResizeMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_paneResizing)
            return;
        var x = e.GetCurrentPoint(RootGrid).Position.X;
        // Clamp: keep the nav items readable, leave room for content.
        var length = Math.Clamp(_paneResizeStartLength + (x - _paneResizeStartX), 180, 480);
        NavView.OpenPaneLength = length;
        UpdatePaneResizeHandle();
        e.Handled = true;
    }

    private void OnPaneResizeReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_paneResizing)
            return;
        _paneResizing = false;
        PaneResizeHandle.ReleasePointerCapture(e.Pointer);
    }

    private void UpdatePaneResizeHandle()
    {
        // The handle only makes sense while the pane is open. When the
        // user collapses the pane with the built-in toggle, the pane
        // shrinks to icons-only (CompactPaneLength, 48 px) — but the
        // handle is positioned by OpenPaneLength, so if it stays visible
        // it floats in the content as a stray vertical stripe at the old
        // dragged position (Manh 2026-11-14, 0.1.3.85 test). Hide it
        // while collapsed; expanding the pane brings it back at the
        // remembered OpenPaneLength (which is preserved across
        // collapse/expand, so the dragged width is not lost).
        if (!NavView.IsPaneOpen)
        {
            PaneResizeHandle.Visibility = Visibility.Collapsed;
            return;
        }
        PaneResizeHandle.Visibility = Visibility.Visible;
        // Center the 8 px handle on the pane's right edge.
        PaneResizeHandle.Margin = new Thickness(NavView.OpenPaneLength - 4, 0, 0, 0);
    }

    private void SelectPage(string tag)
    {
        Type? pageType = tag switch
        {
            "General" => typeof(GeneralPage),
            "Appearance" => typeof(AppearancePage),
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
