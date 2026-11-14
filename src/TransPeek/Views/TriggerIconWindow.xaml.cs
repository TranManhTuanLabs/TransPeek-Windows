// Trigger icon (SPEC §3.1): the small "A" tile that appears near the
// text selection. Hover (~300 ms, configurable) or click opens the float
// panel. v0.1: click opens the panel (original MoePeek behaviour) — there
// is NO click-to-read / TTS yet (see README: TTS comes after mac is stable).
// The click switches the mark to a speaker glyph (visual state only).

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

    /// <summary>Raised on click, or when the hover delay elapses.</summary>
    public event Action? OpenRequested;

    public TriggerIconWindow()
    {
        InitializeComponent();
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
        IconBorder.Tapped += (_, _) =>
        {
            ShowSpeaker(); // click = listen mode: show the speaker mark.
            OpenRequested?.Invoke();
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
        ShowLetter(); // fresh selection: back to the "A" mark.
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
        ShowLetter();
        WindowHelpers.Hide(this);
    }

    /// <summary>Switch the mark to the speaker glyph (click = listen
    /// mode). TTS audio is not built yet — this is only the icon
    /// state for now.</summary>
    public void ShowSpeaker()
    {
        LetterMark.Visibility = Visibility.Collapsed;
        SpeakerMark.Visibility = Visibility.Visible;
    }

    /// <summary>Back to the "A" mark (new selection / icon hidden).</summary>
    public void ShowLetter()
    {
        SpeakerMark.Visibility = Visibility.Collapsed;
        LetterMark.Visibility = Visibility.Visible;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _autoHideTimer.Stop(); // stay while the user is hovering
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
