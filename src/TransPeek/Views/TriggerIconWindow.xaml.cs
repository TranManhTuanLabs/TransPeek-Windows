// Trigger icon (SPEC §3.1): the small "A 文" tile that appears near the
// text selection. Hover (~300 ms, configurable) or click opens the float
// panel. v0.1: click opens the panel (original MoePeek behaviour) — there
// is NO click-to-read / TTS yet (see README: TTS comes after mac is stable).

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TransPeek.Interop;

namespace TransPeek.Views;

public sealed partial class TriggerIconWindow : Window
{
    private const int WidthPx = 48;
    private const int HeightPx = 48;

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
        IconBorder.Tapped += (_, _) => OpenRequested?.Invoke();
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
    /// (physical pixels), clamped to the display work area.</summary>
    public void ShowAt(int x, int y)
    {
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x + 8, work.X, work.X + work.Width - WidthPx);
        var py = Math.Clamp(y + 8, work.Y, work.Y + work.Height - HeightPx);
        WindowHelpers.MoveAndResize(this, px, py, WidthPx, HeightPx);
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
        WindowHelpers.Hide(this);
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
