// Fullscreen region picker for OCR (SPEC §3.3). Covers the display that
// contains the cursor, dims it, and lets the user drag a rectangle.
// Returns the rectangle in PHYSICAL pixels (capture APIs work in physical
// pixels; XAML layout uses effective pixels, so the result is scaled by
// the window's DPI factor). Esc or right-click cancels (null).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TransPeek.Interop;
using Windows.Foundation;
using Windows.Graphics;

namespace TransPeek.Views;

public sealed partial class RegionSelectWindow : Window
{
    private TaskCompletionSource<RectInt32?>? _result;
    private Point _start;
    private bool _dragging;

    public RegionSelectWindow()
    {
        InitializeComponent();
        // WinUI 3 Window is not a UIElement, so it has no Loaded event:
        // attach to the root content element instead.
        if (Content is UIElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                WindowHelpers.MakeBorderlessTopmost(this);
                WindowHelpers.MakeToolWindow(this);
            };
        }
        RootGrid.PointerPressed += OnPointerPressed;
        RootGrid.PointerMoved += OnPointerMoved;
        RootGrid.PointerReleased += OnPointerReleased;
        RootGrid.PointerCanceled += (_, _) => Cancel();
        // WinUI 3 Window has no KeyDown event: Esc is a keyboard
        // accelerator on the root element instead.
        var esc = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        esc.Invoked += (_, args) =>
        {
            Cancel();
            args.Handled = true;
        };
        contentRoot.KeyboardAccelerators.Add(esc);
    }

    public Task<RectInt32?> SelectRegionAsync()
    {
        _result = new TaskCompletionSource<RectInt32?>();
        // Cover the display containing the cursor.
        var (cx, cy) = WindowHelpers.CursorPosition();
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
            new PointInt32(cx, cy), Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        var bounds = area.OuterBounds;
        WindowHelpers.MoveAndResize(this, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        Activate();
        return _result.Task;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(RootGrid).Properties.IsRightButtonPressed)
        {
            Cancel();
            return;
        }
        _start = e.GetCurrentPoint(RootGrid).Position;
        _dragging = true;
        RootGrid.CapturePointer(e.Pointer);
        UpdateRect(_start, _start);
        SelectionRect.Visibility = Visibility.Visible;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
            return;
        UpdateRect(_start, e.GetCurrentPoint(RootGrid).Position);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;
        var end = e.GetCurrentPoint(RootGrid).Position;
        var x = Math.Min(_start.X, end.X);
        var y = Math.Min(_start.Y, end.Y);
        var w = Math.Abs(end.X - _start.X);
        var h = Math.Abs(end.Y - _start.Y);
        if (w < 4 || h < 4)
        {
            Cancel();
            return;
        }
        // Effective px -> physical px.
        var scale = WindowHelpers.DpiScale(this);
        var rect = new RectInt32(
            (int)Math.Round(x * scale),
            (int)Math.Round(y * scale),
            (int)Math.Round(w * scale),
            (int)Math.Round(h * scale));
        // The window was placed at the display origin: offset by it.
        var (cx, cy) = WindowHelpers.CursorPosition();
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
            new PointInt32(cx, cy), Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        rect.X += area.OuterBounds.X;
        rect.Y += area.OuterBounds.Y;
        Finish(rect);
    }

    private void UpdateRect(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = Math.Abs(b.X - a.X);
        SelectionRect.Height = Math.Abs(b.Y - a.Y);
    }

    private void Cancel() => Finish(null);

    private void Finish(RectInt32? rect)
    {
        WindowHelpers.Hide(this);
        _result?.TrySetResult(rect);
        _result = null;
    }
}
