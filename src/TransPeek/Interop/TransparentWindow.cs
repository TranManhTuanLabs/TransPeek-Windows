// Truly transparent floating window (WinUI 3) — the Action-Wheel pattern.
//
// WinUI 3 has NO built-in transparent window background. A "Transparent"
// XAML background just renders as an opaque box (white in Light mode), so
// the trigger icon's 30 px white circle was invisible against its own
// window and the icon looked like a white square with a drop shadow
// (Manh 2026-11-14, build 78). The earlier fix (MakeToolWindow:
// WS_EX_TOOLWINDOW + SWP_FRAMECHANGED) did not remove the shadow either —
// in WinUI 3 the shadow/outline comes from the frame bits, not from the
// extended style.
//
// The fix that actually works (Action-Wheel, a WinUI 3 radial menu with
// real per-pixel alpha) needs ALL of these together; drop any one and the
// window renders as an opaque box:
//   1. The presenter attached FIRST (AppWindow.SetPresenter) — style
//      changes made before it are silently reverted (measured).
//   2. WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE (no taskbar / Alt+Tab, never
//      steals focus).
//   3. RemoveWindowFrame: strip WS_CAPTION | WS_BORDER | WS_DLGFRAME |
//      WS_THICKFRAME from GWL_STYLE and the edge bits from GWL_EXSTYLE.
//      OverlappedPresenter.SetBorderAndTitleBar(false, false) clears
//      WS_EX_WINDOWEDGE but LEAVES WS_DLGFRAME — that is what the white
//      outline/shadow was drawn from. Also DWMWA_WINDOW_CORNER_PREFERENCE
//      = DONOTROUND (Win11 rounds top-level windows) and
//      DWMWA_BORDER_COLOR = COLOR_NONE (the faint 1 px accent border).
//   4. DwmEnableBlurBehindWindow(fEnable: true, null region) so DWM
//      composites the window with alpha over the whole client area.
//   5. A custom SystemBackdrop holding a transparent composition brush
//      (the same extension point Mica/Acrylic use) — this is what makes
//      WinUI's swap chain transparent instead of opaque.
//   6. The native client surface cleared to BLACK: WinUI clears it with
//      the theme background (white in Light mode) before drawing XAML,
//      and that clear stays visible through transparent XAML pixels.
//      A window subclass fills WM_ERASEBKGND with a black brush; with
//      DWM alpha composition those untouched pixels become fully
//      transparent instead of a white box.
//
// Only the trigger icon uses this (it is the only window that must be a
// bare circle). The panel and toast are opaque cards and keep their
// normal window setup (MakeBorderlessTopmost + MakeToolWindow).

using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
// Alias rather than "using Windows.UI": both Microsoft.UI and Windows.UI
// declare a Colors class, and importing the whole namespace makes every
// Colors.X reference ambiguous (Action-Wheel). And Windows.UI.Colors
// itself does not resolve in this project (net8.0-windows10.0.22000.0 +
// WinAppSDK 1.8, CS0234 in CI #80) — so the transparent color is built
// directly with Color.FromArgb instead of Colors.Transparent.
using Color = Windows.UI.Color;

namespace TransPeek.Interop;

internal static class TransparentWindow
{
    // Per-HWND state, kept alive for the window's lifetime: the black
    // brush (native background clear) and the subclass proc delegate.
    // A collected delegate would crash the next WM_ERASEBKGND, so both
    // are rooted here, not in a local.
    private sealed class State
    {
        public IntPtr Brush;
        public NativeMethods.WindowSubclassProc? Proc;
    }

    private static readonly Dictionary<IntPtr, State> _states = new();
    private static readonly UIntPtr BackgroundSubclassId = new(1);

    /// <summary>Make a WinUI window truly transparent (per-pixel alpha)
    /// with no frame, outline or shadow. Idempotent per HWND — a second
    /// call for the same window is a no-op. Returns false when the
    /// window has no handle yet (the caller may retry on Loaded).</summary>
    public static bool MakeTransparent(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
            return false;
        lock (_states)
        {
            if (_states.ContainsKey(hwnd))
                return true;
        }

        try
        {
            // 1. Presenter FIRST — style changes before it are reverted.
            var appWindow = window.AppWindow;
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }
            appWindow.IsShownInSwitchers = false;

            // 2. Tool window + no-activate.
            var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= (long)(NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            // 3. Strip the frame bits (the real shadow/outline source).
            RemoveWindowFrame(hwnd);

            // 4. DWM alpha composition over the whole client area.
            var blurBehind = new NativeMethods.DWM_BLURBEHIND
            {
                dwFlags = NativeMethods.DWM_BB_ENABLE,
                fEnable = true,
                hRgnBlur = IntPtr.Zero,
            };
            NativeMethods.DwmEnableBlurBehindWindow(hwnd, ref blurBehind);

            // Suppress the faint 1 px accent border Win11 draws around
            // top-level windows.
            int noBorder = NativeMethods.DWMWA_COLOR_NONE;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref noBorder, sizeof(int));

            // 5. Transparent composition brush as the system backdrop.
            window.SystemBackdrop = new TransparentSystemBackdrop();

            // 6. Clear the native surface to black so untouched pixels
            // are transparent instead of theme-white.
            var brush = NativeMethods.CreateSolidBrush(0);
            var proc = new NativeMethods.WindowSubclassProc(TransparentWindowProc);
            lock (_states)
            {
                _states[hwnd] = new State { Brush = brush, Proc = proc };
            }
            NativeMethods.SetWindowSubclass(hwnd, proc, BackgroundSubclassId, UIntPtr.Zero);
            var hdc = NativeMethods.GetDC(hwnd);
            if (hdc != IntPtr.Zero)
            {
                ClearNativeBackground(hwnd, hdc, brush);
                NativeMethods.ReleaseDC(hwnd, hdc);
            }

            // Topmost without showing — the caller shows the window.
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Strip every frame bit from the window — this is what
    /// actually removes the outline/shadow. Only the frame bits are
    /// cleared (deliberately with &= ~): overwriting GWL_STYLE wholesale
    /// drops WS_CLIPCHILDREN/WS_CLIPSIBLINGS, which makes content render
    /// blurry after animations (Action-Wheel).</summary>
    private static void RemoveWindowFrame(IntPtr hwnd)
    {
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
        style &= ~(long)(NativeMethods.WS_CAPTION | NativeMethods.WS_BORDER |
            NativeMethods.WS_DLGFRAME | NativeMethods.WS_THICKFRAME);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));

        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle &= ~(long)(NativeMethods.WS_EX_WINDOWEDGE | NativeMethods.WS_EX_CLIENTEDGE |
            NativeMethods.WS_EX_STATICEDGE | NativeMethods.WS_EX_DLGMODALFRAME);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

        // Windows 11 rounds top-level windows; on a transparent overlay
        // the rounding is visible as curved corners of the outline.
        int corners = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));

        // The cached frame is only recomputed when told it changed.
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    private static IntPtr TransparentWindowProc(
        IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData)
    {
        if (message == NativeMethods.WM_ERASEBKGND)
        {
            IntPtr brush;
            lock (_states)
            {
                brush = _states.TryGetValue(hwnd, out var s) ? s.Brush : IntPtr.Zero;
            }
            if (ClearNativeBackground(hwnd, wParam, brush))
                return new IntPtr(1);
        }
        return NativeMethods.DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private static bool ClearNativeBackground(IntPtr hwnd, IntPtr hdc, IntPtr brush)
    {
        if (hdc == IntPtr.Zero || brush == IntPtr.Zero)
            return false;
        if (!NativeMethods.GetClientRect(hwnd, out var rect))
            return false;
        return NativeMethods.FillRect(hdc, ref rect, brush) != 0;
    }

    /// <summary>A SystemBackdrop (the same extension point Mica/Acrylic
    /// use) that hands the window a fully transparent composition brush
    /// instead of a material. ICompositionSupportsSystemBackdrop expects
    /// a Windows.UI.Composition.CompositionBrush from the OS-level
    /// compositor — not the Microsoft.UI.Composition.Compositor used for
    /// XAML element visuals — so a dedicated Compositor is created here
    /// (the standard pattern for custom system backdrops).</summary>
    private sealed class TransparentSystemBackdrop : SystemBackdrop
    {
        private WindowsSystemDispatcherQueueHelper? _dispatcherQueueHelper;
        private Windows.UI.Composition.Compositor? _compositor;

        protected override void OnTargetConnected(
            Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);
            _dispatcherQueueHelper = new WindowsSystemDispatcherQueueHelper();
            _dispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();
            _compositor = new Windows.UI.Composition.Compositor();
            // Fully transparent (A=0). Built with FromArgb, not
            // Colors.Transparent — Windows.UI.Colors does not resolve in
            // this project (CS0234, CI #80).
            connectedTarget.SystemBackdrop = _compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        protected override void OnTargetDisconnected(
            Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop disconnectedTarget)
        {
            disconnectedTarget.SystemBackdrop = null;
            _compositor?.Dispose();
            _compositor = null;
            base.OnTargetDisconnected(disconnectedTarget);
        }
    }

    /// <summary>Ensures the current thread has a DispatcherQueue, which
    /// Windows.UI.Composition.Compositor requires to construct. The app's
    /// UI thread already has one by the time a window is created, so in
    /// practice this is a cheap no-op guard.</summary>
    private sealed class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController(
            [In] DispatcherQueueOptions options,
            [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object? dispatcherQueueController);

        private object? _dispatcherQueueController;

        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
                return;

            if (_dispatcherQueueController == null)
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                    threadType = 2,    // DQTYPE_THREAD_CURRENT
                    apartmentType = 2, // DQTAT_COM_STA
                };
                CreateDispatcherQueueController(options, ref _dispatcherQueueController);
            }
        }
    }
}
