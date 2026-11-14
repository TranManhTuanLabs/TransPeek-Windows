// Fullscreen region picker for OCR (SPEC §3.3). Covers the display that
// contains the cursor, dims it, and lets the user drag a rectangle.
// Returns the rectangle in PHYSICAL pixels (capture APIs work in physical
// pixels; XAML layout uses effective pixels, so the result is scaled by
// the window's DPI factor). Esc, right-click, or Alt+F4 cancels (null).
//
// v0.1.1 fix — the v0.1 overlay could trap the user fullscreen: Esc was
// only a KeyboardAccelerator on the root Grid, and a Grid never takes
// keyboard focus, so the accelerator never fired; there was no Cancel
// button at all; and Finish() only hid the window, so closing it with
// Alt+F4 left the TaskCompletionSource uncompleted and the OCR flow
// awaiting forever. Now: the grid is focusable and focused on show,
// KeyDown handles Esc directly, RightTapped handles right-click, Closed
// completes the task, and Finish() closes (not just hides) the window
// exactly once. The Cancel button added then is gone again (2026-11-14,
// 0.1.3.59 test): it sat top-right near the mouse and an accidental
// click exited the capture; Esc on the keyboard, right-click and Alt+F4
// are the exits, and the grid keeps focus so Esc is reliable.

using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using TransPeek.Interop;
using TransPeek.Services;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;

namespace TransPeek.Views;

public sealed partial class RegionSelectWindow : Window
{
    private TaskCompletionSource<RectInt32?>? _result;
    private Point _start;
    private bool _dragging;
    private bool _finished;
    // Origin of the display this window covers (set in
    // SelectRegionAsync): the pointer math offsets by it, and it must
    // be the same display the window was placed on.
    private int _originX;
    private int _originY;

    public RegionSelectWindow()
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
                // Give the grid keyboard focus so Esc (KeyDown below and
                // the accelerator) actually reaches this window.
                RootGrid.Focus(FocusState.Programmatic);
            };
            // WinUI 3 Window has no KeyDown event: Esc is a keyboard
            // accelerator on the root element instead. The accelerator
            // alone is NOT reliable (it needs a focused element), so
            // KeyDown on the focused grid is the primary Esc path and
            // the accelerator stays only as a backup.
            var esc = new KeyboardAccelerator { Key = VirtualKey.Escape };
            esc.Invoked += (_, args) =>
            {
                Cancel();
                args.Handled = true;
            };
            contentRoot.KeyboardAccelerators.Add(esc);
            contentRoot.KeyDown += (_, e) =>
            {
                if (e.Key == VirtualKey.Escape)
                {
                    Cancel();
                    e.Handled = true;
                }
            };
        }
        // Re-focus whenever the window is activated (e.g. after the user
        // clicked another window and came back): without focus, Esc is dead.
        Activated += (_, _) => RootGrid.Focus(FocusState.Programmatic);
        // Alt+F4 / taskbar close must complete the selection instead of
        // hanging the OCR flow forever awaiting a result that never comes.
        Closed += (_, _) => Cancel();
        RootGrid.PointerPressed += OnPointerPressed;
        RootGrid.PointerMoved += OnPointerMoved;
        RootGrid.PointerReleased += OnPointerReleased;
        RootGrid.PointerCanceled += (_, _) => Cancel();
        // Right-click release: PointerPressed already handles the press,
        // but RightTapped is the reliable right-click signal in WinUI.
        RootGrid.RightTapped += (_, _) => Cancel();
    }

    public Task<RectInt32?> SelectRegionAsync()
    {
        _result = new TaskCompletionSource<RectInt32?>();
        // Cover the display containing the cursor. Win32 only:
        // DisplayArea.GetFromPoint is the same WinRT display API family
        // as the broken FindAll(), and the window could land on the
        // wrong display (0.1.3.47 test: grey overlay, no content).
        var (cx, cy) = WindowHelpers.CursorPosition();
        var bounds = WindowHelpers.MonitorBounds(cx, cy);
        _originX = bounds.X;
        _originY = bounds.Y;
        WindowHelpers.MoveAndResize(this, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        Activate();
        RootGrid.Focus(FocusState.Programmatic);
        return _result.Task;
    }

    /// <summary>Show the display screenshot behind the dim (0.1.3.54):
    /// the window renders opaque, so without it the user sees only a
    /// flat grey board and picks a region blind.</summary>
    public void SetBackgroundImage(byte[] png)
    {
        _ = LoadBackgroundAsync(png);
    }

    private async Task LoadBackgroundAsync(byte[] png)
    {
        try
        {
            var image = new BitmapImage();
            // SetSourceAsync decodes before it returns, so the stream
            // may be disposed right after (unlike SetSource, which
            // decodes lazily and needs the stream to stay open).
            using var stream = new MemoryStream(png).AsRandomAccessStream();
            await image.SetSourceAsync(stream);
            BackgroundImage.Source = image;
        }
        catch (Exception ex)
        {
            CrashLog.Write("RegionSelect.Background", ex);
        }
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
        // The window was placed at the display origin: offset by it
        // (the origin stored when the window was shown, so the rect is
        // on the same display even if the cursor moved since).
        rect.X += _originX;
        rect.Y += _originY;
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
        // Exactly once: Closed fires while closing, and Cancel() can be
        // reached from several events for the same press — a second
        // Finish must be a no-op, and Close() on an already-closed
        // window must not throw out of an event handler.
        if (_finished)
            return;
        _finished = true;
        var result = _result;
        _result = null;
        result?.TrySetResult(rect);
        try
        {
            Close();
        }
        catch
        {
            WindowHelpers.Hide(this);
        }
    }
}
