using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TransPeek.Interop;
using Windows.Foundation;

namespace TransPeek.Views;

/// <summary>Speaker indicator — the separate floating play/pause
/// control (Manh 2026-11-26, speaker indicator split from the float
/// icon, following Speechify's Pill Player and NaturalReader's
/// minibar). It reuses the float icon's floating-window machinery
/// (TransparentWindow, circular region, show without activation) and
/// the same size (FloatIconSize, default 24 pt), so it looks like
/// the float icon but shows playback state instead of the trigger
/// marks: Pause (E769) while playing, Play (E768) while paused.
/// The glyph shows what the next press will do.
///
/// Lifecycle: it appears at the spot where the user pressed a
/// speaker (the float icon's position for the float path — the
/// panel buttons are their own control, so no indicator there).
/// While playing it stays (no auto-hide, so the user can always
/// press to pause). While paused it auto-hides after the usual
/// delay (TriggerIconAutoHideSeconds, default 5s); hovering pauses
/// the timer. When it hides while paused the session ends quietly
/// (stop) — that is the only way a paused session ends, since
/// there is no Stop button and no Esc to stop it. Resume works
/// while it is still visible.</summary>
public sealed partial class SpeakerIndicatorWindow : Window
{
    private readonly DispatcherTimer _autoHideTimer = new();
    private bool _visible;
    private bool _paused;

    /// <summary>Raised when the user clicks the indicator: App
    /// pauses (while playing) or resumes (while paused) the TTS
    /// session.</summary>
    public event Action? ToggleRequested;

    /// <summary>Raised when the auto-hide timer fires while paused:
    /// App ends the session quietly (stop), since the resume
    /// control is gone.</summary>
    public event Action? AutoHidden;

    public bool IsVisible => _visible;

    public SpeakerIndicatorWindow()
    {
        InitializeComponent();
        // Truly transparent + frameless (TransparentWindow, the
        // same machinery as the float icon): the presenter, the
        // frame strip, DWM blur-behind, the transparent backdrop
        // and the black native clear in one place. Loaded runs while
        // the window is first shown, so the handle is ready.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) => TransparentWindow.MakeTransparent(this);
        }
        IndicatorBorder.PointerPressed += (_, _) => ToggleRequested?.Invoke();
        IndicatorBorder.PointerEntered += (_, _) => _autoHideTimer.Stop();
        IndicatorBorder.PointerExited += (_, _) =>
        {
            // Hover pauses the auto-hide timer; leaving restarts it
            // only while paused (while playing there is no timer).
            if (_visible && _paused)
                StartAutoHideTimer();
        };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            var wasVisible = _visible;
            HideIndicator();
            if (wasVisible)
                AutoHidden?.Invoke();
        };
    }

    /// <summary>Show the indicator at the spot where the user
    /// pressed a speaker (physical pixels), at the float icon's
    /// size, with the glyph for the current state. The position is
    /// the press spot itself (not offset like the float icon's
    /// +8, since the indicator replaces the pressed icon), clamped
    /// to the work area.</summary>
    public void ShowAt(int x, int y, bool paused)
    {
        _paused = paused;
        UpdateGlyph();
        // Same sizing as the float icon (FloatIconSize, default
        // 24 pt): the window is the setting, the circle 2 pt
        // smaller, the glyph scales with the circle.
        var windowPx = App.Settings.FloatIconSize;
        var circlePx = windowPx - 2;
        var glyphSize = (int)Math.Round(circlePx * 0.44);
        IndicatorBorder.Width = circlePx;
        IndicatorBorder.Height = circlePx;
        IndicatorBorder.CornerRadius = new CornerRadius(circlePx / 2.0);
        IndicatorGlyph.FontSize = glyphSize;
        var scale = WindowHelpers.DpiScale(this);
        var w = Math.Max(1, (int)Math.Round(windowPx * scale));
        var h = Math.Max(1, (int)Math.Round(windowPx * scale));
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x, work.X, work.X + work.Width - w);
        var py = Math.Clamp(y, work.Y, work.Y + work.Height - h);
        WindowHelpers.MoveAndResize(this, px, py, w, h);
        // Cut the window into a circle (same as the float icon): on
        // machines where DWM blur-behind fails, the transparent
        // backdrop shows a grey square around the white circle — a
        // window region does not depend on DWM blur.
        ApplyCircularRegion(w, h);
        WindowHelpers.ShowWithoutActivation(this);
        _visible = true;
        if (_paused)
            StartAutoHideTimer();
        else
            _autoHideTimer.Stop();
    }

    /// <summary>Update the state (playing/paused) of a visible
    /// indicator: the glyph flips, and the auto-hide timer stops
    /// (playing — stay so the user can press to pause) or starts
    /// (paused — hide after the usual delay).</summary>
    public void SetPaused(bool paused)
    {
        _paused = paused;
        UpdateGlyph();
        if (!_visible)
            return;
        if (_paused)
            StartAutoHideTimer();
        else
            _autoHideTimer.Stop();
    }

    /// <summary>Hide the indicator (a no-op when already hidden).
    /// App calls this when the session ends (natural end, a new
    /// session replacing it, or the panel session taking over).</summary>
    public void HideIndicator()
    {
        if (!_visible)
            return;
        _visible = false;
        _autoHideTimer.Stop();
        WindowHelpers.Hide(this);
    }

    /// <summary>Glyph shows what the next press will do: Pause
    /// (E769) while playing, Play (E768) while paused. The same
    /// glyphs the Read mode transport and the panel speaker
    /// buttons use.</summary>
    private void UpdateGlyph()
    {
        IndicatorGlyph.Glyph = _paused ? "\uE768" : "\uE769";
    }

    private void StartAutoHideTimer()
    {
        _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
        _autoHideTimer.Start();
    }

    /// <summary>Clip the indicator window to a circle inscribed in
    /// its (physical-pixel) bounds. SetWindowRgn takes ownership of
    /// the region on success — only DeleteObject it when the call
    /// fails.</summary>
    private void ApplyCircularRegion(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
            return;
        // Right/bottom are exclusive in CreateEllipticRgn, so +1
        // covers the full width/height.
        var region = NativeMethods.CreateEllipticRgn(0, 0, width + 1, height + 1);
        if (region == IntPtr.Zero)
            return;
        if (NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
            NativeMethods.DeleteObject(region);
    }
}
