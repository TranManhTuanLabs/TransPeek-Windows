// Alternative icon window (mac FAST_READ_SELECT_ICON.md): the small
// floating tile that appears near a text selection INSIDE Fast Read.
// Hover (after the usual hover delay) translates the selected word;
// click speaks it through its own system-TTS path. NO Read action, NO
// context menu, NO Alt preview — the icon is always speaker + hover in
// Read mode, whatever "Click icon for:" says. It never touches the main
// float icon's logic, glyphs, or settings.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TransPeek.Interop;

namespace TransPeek.AlternativeIcon;

public sealed partial class AlternativeIconWindow : Window
{
    // Size follows the auto-show icon setting (Manh 2026-11-22, mac
    // parity): AutoShowFloatIconSize (default 24 pt) — the same size as
    // the hover icon, so the alternative icon reads as the same family.
    // The window is the setting, the circle 2 pt smaller, glyphs scale
    // with the circle: bubble ~80%, A = half, speaker ~60%. ShowAt
    // re-applies the sizes on every show (the XAML values are
    // first-paint defaults only).

    private readonly DispatcherTimer _hoverTimer = new();
    private readonly DispatcherTimer _autoHideTimer = new();
    private bool _visible;
    private bool _isSpeaking;

    /// <summary>Raised when the hover delay elapses: App translates the selected text.</summary>
    public event Action? OpenRequested;

    /// <summary>Raised when the user clicks the icon to listen.</summary>
    public event Action? SpeakRequested;

    /// <summary>Raised when the user clicks again to stop listening.</summary>
    public event Action? SpeakStopped;

    public AlternativeIconWindow()
    {
        InitializeComponent();
        // NOTE: WinUI 3 Window has NO Width/Height properties and no
        // Loaded event (Loaded lives on FrameworkElement) — same as
        // the main icon window: attach to the root content element.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                // Truly transparent + frameless (TransparentWindow,
                // Action-Wheel pattern) — without it the window is an
                // opaque white square (same lesson as the main icon,
                // Manh 2026-11-14, build 78).
                TransparentWindow.MakeTransparent(this);
            };
        }
        IconBorder.PointerEntered += OnPointerEntered;
        IconBorder.PointerExited += OnPointerExited;
        // Click = listen, toggled (mac §6: its own TTS path, second
        // click stops). PointerPressed, not Tapped — same reason as the
        // main icon: the press is the interaction, and a click must
        // cancel the pending hover so hover and click never both fire.
        // Left button only.
        IconBorder.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(IconBorder).Properties.IsLeftButtonPressed)
                return;
            e.Handled = true;
            if (_isSpeaking)
            {
                SetSpeaking(false);
                SpeakStopped?.Invoke();
                RestartAutoHide();
            }
            else
            {
                _hoverTimer.Stop();
                SetSpeaking(true);
                SpeakRequested?.Invoke();
                RestartAutoHide();
            }
        };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            OpenRequested?.Invoke();
        };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            HideIcon();
        };
    }

    /// <summary>Show the icon just below-right of the selection point
    /// (physical pixels), clamped to the display work area — same
    /// placement as the main icon, so it lands next to the word the
    /// user just selected. The window is sized in PHYSICAL pixels
    /// (AppWindow.MoveAndResize) but the content is laid out in
    /// effective pixels (DIPs): the window is scaled by the display
    /// DPI, or the circle overflows (is clipped by) the window on a
    /// scaled display (same lesson as the main icon, 0.1.3.68).</summary>
    public void ShowAt(int x, int y)
    {
        SetSpeaking(false); // fresh selection: back to the A-in-bubble mark.
        // Sizes from the auto-show setting, applied on every show:
        // window = setting, circle = setting − 2, bubble ~80% of the
        // circle, A = half, speaker ~60%.
        var windowPx = App.Settings.AutoShowFloatIconSize;
        var circlePx = windowPx - 2;
        var bubbleSize = (int)Math.Round(circlePx * 0.8);
        var aSize = (int)Math.Round(circlePx * 0.5);
        var speakerSize = (int)Math.Round(circlePx * 0.6);
        IconBorder.Width = circlePx;
        IconBorder.Height = circlePx;
        IconBorder.CornerRadius = new CornerRadius(circlePx / 2.0);
        BubbleMark.FontSize = bubbleSize;
        AMark.FontSize = aSize;
        SpeakerMark.FontSize = speakerSize;
        var scale = WindowHelpers.DpiScale(this);
        var w = Math.Max(1, (int)Math.Round(windowPx * scale));
        var h = Math.Max(1, (int)Math.Round(windowPx * scale));
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x + 8, work.X, work.X + work.Width - w);
        var py = Math.Clamp(y + 8, work.Y, work.Y + work.Height - h);
        WindowHelpers.MoveAndResize(this, px, py, w, h);
        // Cut the window into a circle (Manh 2026-11-15): on machines
        // where DWM blur-behind fails, the transparent backdrop shows a
        // grey square around the white circle (Lenovo T460S). A window
        // region does not depend on DWM blur, so the icon is round even
        // then. Same as the main icon.
        ApplyCircularRegion(w, h);
        WindowHelpers.ShowWithoutActivation(this);
        _visible = true;
        _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
        _autoHideTimer.Start();
    }

    /// <summary>Clip the icon window to a circle inscribed in its
    /// (physical-pixel) bounds. SetWindowRgn takes ownership of the
    /// region on success — only DeleteObject it when the call fails.</summary>
    private void ApplyCircularRegion(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
            return;
        var region = NativeMethods.CreateEllipticRgn(0, 0, width + 1, height + 1);
        if (region == IntPtr.Zero)
            return;
        if (NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
            NativeMethods.DeleteObject(region);
    }

    public void HideIcon()
    {
        if (!_visible)
            return;
        _visible = false;
        _hoverTimer.Stop();
        _autoHideTimer.Stop();
        SetSpeaking(false);
        WindowHelpers.Hide(this);
    }

    /// <summary>True while the speaker mark shows (click = listen mode).</summary>
    public bool IsSpeaking => _isSpeaking;

    /// <summary>Switch the mark to the speaker glyph (while speaking)
    /// or back to the A-in-bubble mark. Audio is the controller's job —
    /// this is only the icon state.</summary>
    public void SetSpeaking(bool speaking)
    {
        if (_isSpeaking == speaking)
            return;
        _isSpeaking = speaking;
        UpdateGlyph();
    }

    private void UpdateGlyph()
    {
        // Same mark normally and on hover (mac §3): only speaking
        // swaps it, to the speaker.
        var showSpeaker = _isSpeaking;
        BubbleMark.Visibility = showSpeaker ? Visibility.Collapsed : Visibility.Visible;
        AMark.Visibility = showSpeaker ? Visibility.Collapsed : Visibility.Visible;
        SpeakerMark.Visibility = showSpeaker ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RestartAutoHide()
    {
        _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
        _autoHideTimer.Start();
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _autoHideTimer.Stop(); // stay while the user is hovering
        // While speaking, hover must not open the translate panel —
        // the click already consumed the interaction (same guard as
        // the main icon, mac startHoverTimer !isSpeaking).
        if (_isSpeaking)
            return;
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(App.Settings.HoverDelayMs);
        _hoverTimer.Start();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoverTimer.Stop();
        RestartAutoHide();
    }
}
