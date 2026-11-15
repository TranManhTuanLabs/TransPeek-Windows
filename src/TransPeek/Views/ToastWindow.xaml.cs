// In-app toast (SPEC §3.3: "Đã copy" after OCR copy-only mode).
// A custom window, NOT AppNotifications: unpackaged apps have no app
// identity, so system toasts are unreliable. Same approach as the mac
// app's CopyToast.

using Microsoft.UI.Xaml;
using TransPeek.Interop;
using TransPeek.Services;

namespace TransPeek.Views;

public sealed partial class ToastWindow : Window, IToastService
{
    private readonly DispatcherTimer _hideTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2.5),
    };

    public ToastWindow()
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
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            WindowHelpers.Hide(this);
        };
    }

    public void Show(string message)
    {
        MessageText.Text = message;
        // Bottom-right of the display containing the cursor.
        var (cx, cy) = WindowHelpers.CursorPosition();
        var work = WindowHelpers.DisplayWorkArea(cx, cy);
        // Measure: fixed height, width from content (cap at 480).
        // DPI: the 320x40 size is logical (DIPs) but MoveAndResize takes
        // PHYSICAL pixels — scale it, or at 150% the toast is 1.5x too
        // small and the text is clipped (Lenovo T460S, 2026-11-15).
        // At 100% the factor is 1.0, so the behaviour there is unchanged.
        var scale = WindowHelpers.DpiScale(this);
        const int height = 40;
        const int width = 320;
        const int margin = 16;
        var w = Math.Max(1, (int)Math.Round(width * scale));
        var h = Math.Max(1, (int)Math.Round(height * scale));
        var m = Math.Max(1, (int)Math.Round(margin * scale));
        var x = work.X + work.Width - w - m;
        var y = work.Y + work.Height - h - m;
        WindowHelpers.MoveAndResize(this, x, y, w, h);
        WindowHelpers.ShowWithoutActivation(this);
        _hideTimer.Stop();
        _hideTimer.Start();
    }
}
