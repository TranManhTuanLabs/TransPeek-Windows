// Small window helpers shared by the floating windows (trigger icon,
// translate panel, toast). WinUI's AppWindow APIs work in physical
// pixels; the Win32 helpers below match that coordinate space.

using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TransPeek.Interop;
using WinRT.Interop;

namespace TransPeek.Interop;

internal static class WindowHelpers
{
    /// <summary>Make a WinUI window invisible to the taskbar and Alt+Tab
    /// (tool window). The translate panel / trigger icon / toast must not
    /// appear as separate apps.</summary>
    public static void MakeToolWindow(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        var next = new IntPtr(exStyle.ToInt64() | (long)NativeMethods.WS_EX_TOOLWINDOW);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, next);
    }

    /// <summary>Show a window without stealing keyboard focus (the panel
    /// must not yank focus out of the app the user is translating in).</summary>
    public static void ShowWithoutActivation(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        var next = new IntPtr(exStyle.ToInt64() | (long)NativeMethods.WS_EX_NOACTIVATE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, next);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
    }

    public static void Hide(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    /// <summary>Move + resize a window in physical pixels.</summary>
    public static void MoveAndResize(Window window, int x, int y, int width, int height)
    {
        window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
    }

    /// <summary>Configure an AppWindow as a borderless, always-on-top,
    /// non-resizable floating window.</summary>
    public static void MakeBorderlessTopmost(Window window)
    {
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
    }

    /// <summary>Current cursor position in physical pixels.</summary>
    public static (int X, int Y) CursorPosition()
    {
        NativeMethods.GetCursorPos(out var pt);
        return (pt.X, pt.Y);
    }

    /// <summary>DPI scale factor for a window (1.0 = 96 DPI).</summary>
    public static double DpiScale(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    /// <summary>Work area of the display containing a point, in physical
    /// pixels. Used to clamp floating windows on-screen.
    /// Win32 only, deliberately: DisplayArea.FindAll() throws
    /// InvalidCastException ("NamedResource Not Found") when its WinRT
    /// list is enumerated on some machines — crash.log 2026-11-12,
    /// builds 0.1.3.40/.42 (toast at startup). MonitorFromPoint +
    /// GetMonitorInfo return the same work area with no WinRT call.
    /// </summary>
    public static Windows.Graphics.RectInt32 DisplayWorkArea(int x, int y)
    {
        var monitor = NativeMethods.MonitorFromPoint(
            new NativeMethods.POINT { X = x, Y = y },
            NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>(),
            };
            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                var w = info.rcWork;
                return new Windows.Graphics.RectInt32(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);
            }
        }
        // Fallback: primary monitor work area.
        if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETWORKAREA, 0, out var rc, 0))
            return new Windows.Graphics.RectInt32(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
        return new Windows.Graphics.RectInt32(0, 0, 1920, 1080);
    }
}
