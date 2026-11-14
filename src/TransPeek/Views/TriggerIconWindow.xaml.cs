// Trigger icon (SPEC §3.1): the small bubble tile that appears near
// the text selection. Hover (~300 ms, configurable) opens the float
// panel (translate); click switches the mark to a speaker (listen/TTS
// mode) and does NOT open the panel — the mac model
// (TriggerIconController: click-to-speak cancels the hover timer and
// never opens the panel; while speaking, hover must not re-trigger
// translation). TTS audio itself is NOT built yet (see README: TTS
// comes after mac is stable), so the click is only the speaker visual
// for now; the second click turns it back. The two paths must not
// conflict: a click cancels the pending hover, and hover is blocked
// while the speaker shows (0.1.3.70 test: the speaker never appeared
// because the click also opened the panel, which hid the icon at once).

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TransPeek.Interop;

namespace TransPeek.Views;

public sealed partial class TriggerIconWindow : Window
{
    // Mac parity (TriggerIconPanel): 32 px.
    private const int WidthPx = 32;
    private const int HeightPx = 32;

    private readonly DispatcherTimer _hoverTimer = new();
    private readonly DispatcherTimer _autoHideTimer = new();
    private bool _visible;
    /// <summary>True while the speaker mark shows (click = listen mode).
    /// While speaking, hover must not open the translate panel (mac:
    /// startHoverTimer has guard !isSpeaking).</summary>
    private bool _isSpeaking;

    /// <summary>Raised on click, or when the hover delay elapses.</summary>
    public event Action? OpenRequested;

    public TriggerIconWindow()
    {
        InitializeComponent();
        // Fixed 32x32 DIP size (mac parity): the window sizes itself to
        // content otherwise, and on a scaled display the content (30 DIP
        // circle) can end up off-size against the window frame. ShowAt
        // still resizes in physical pixels (32 * DpiScale) for the exact
        // screen position.
        Width = WidthPx;
        Height = HeightPx;
        // WinUI 3 Window has no Loaded event (Loaded lives on
        // FrameworkElement): attach to the root content element instead.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                WindowHelpers.MakeBorderlessTopmost(this);
                WindowHelpers.MakeToolWindow(this);
            };
        }
        IconBorder.PointerEntered += OnPointerEntered;
        IconBorder.PointerExited += OnPointerExited;
        // Click = listen mode (mac handleIconClick, .speak): toggle the
        // speaker mark. It must NOT open the panel (the old code raised
        // OpenRequested here, and opening the panel hid the icon at once,
        // so the speaker never showed). A click also cancels the pending
        // hover timer, so hover and click never both fire. A second click
        // while speaking turns the speaker back off.
        IconBorder.Tapped += (_, _) =>
        {
            if (_isSpeaking)
            {
                SetSpeaking(false);
                _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
                _autoHideTimer.Start();
            }
            else
            {
                _hoverTimer.Stop();
                SetSpeaking(true);
                // No TTS audio yet: keep the speaker visible for the
                // auto-hide duration, then hide (the mac keeps it until
                // playback stops; here there is no playback to stop it).
                _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
                _autoHideTimer.Start();
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
    /// (physical pixels), clamped to the display work area. The window
    /// is sized in PHYSICAL pixels (AppWindow.MoveAndResize) but the
    /// content is laid out in effective pixels (DIPs): the 32 DIP
    /// window is scaled by the display DPI, or the 30 DIP circle
    /// overflows (is clipped by) the window on a scaled display — the
    /// "abnormal size" Manh saw in 0.1.3.68.</summary>
    public void ShowAt(int x, int y)
    {
        SetSpeaking(false); // fresh selection: back to the bubble mark.
        var scale = WindowHelpers.DpiScale(this);
        var w = Math.Max(1, (int)Math.Round(WidthPx * scale));
        var h = Math.Max(1, (int)Math.Round(HeightPx * scale));
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x + 8, work.X, work.X + work.Width - w);
        var py = Math.Clamp(y + 8, work.Y, work.Y + work.Height - h);
        WindowHelpers.MoveAndResize(this, px, py, w, h);
        WindowHelpers.ShowWithoutActivation(this);
        _visible = true;
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(App.Settings.HoverDelayMs);
        _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
        _autoHideTimer.Start();
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

    /// <summary>Switch the mark to the speaker glyph (click = listen
    /// mode) or back to the "A". TTS audio is not built yet — this
    /// is only the icon state for now.</summary>
    private void SetSpeaking(bool speaking)
    {
        if (_isSpeaking == speaking)
            return;
        _isSpeaking = speaking;
        LetterMark.Visibility = speaking ? Visibility.Collapsed : Visibility.Visible;
        SpeakerMark.Visibility = speaking ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _autoHideTimer.Stop(); // stay while the user is hovering
        // While speaking, hover must not open the translate panel (mac:
        // startHoverTimer has guard !isSpeaking) — the click already
        // consumed the interaction.
        if (_isSpeaking)
            return;
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(App.Settings.HoverDelayMs);
        _hoverTimer.Start();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoverTimer.Stop();
        _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
        _autoHideTimer.Start();
    }
}
