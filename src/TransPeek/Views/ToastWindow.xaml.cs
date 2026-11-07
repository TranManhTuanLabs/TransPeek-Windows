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
        Loaded += (_, _) =>
        {
            WindowHelpers.MakeBorderlessTopmost(this);
            WindowHelpers.MakeToolWindow(this);
        };
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
        const int height = 40;
        const int width = 320;
        var x = work.X + work.Width - width - 16;
        var y = work.Y + work.Height - height - 16;
        WindowHelpers.MoveAndResize(this, x, y, width, height);
        WindowHelpers.ShowWithoutActivation(this);
        _hideTimer.Stop();
        _hideTimer.Start();
    }
}
