// Low-level mouse hook that fires when the user clicks anywhere outside a
// given window rectangle (SPEC §3.1: click outside closes the panel).
// WinUI has no built-in "click outside" event for a no-activate tool
// window, so this uses the documented WH_MOUSE_LL hook. The hook runs on
// a system thread: the callback only posts to the UI dispatcher, never
// touches UI directly.

using Microsoft.UI.Dispatching;
using TransPeek.Interop;

namespace TransPeek.Services;

public sealed class ClickOutsideMonitor : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<bool> _isActive;
    private readonly Func<(int X, int Y, int Width, int Height)> _getRect;
    private readonly Action _onClickOutside;
    private readonly NativeMethods.HookProc _hookProc;
    private IntPtr _hook;
    private bool _disposed;

    public ClickOutsideMonitor(
        DispatcherQueue dispatcher,
        Func<bool> isActive,
        Func<(int X, int Y, int Width, int Height)> getRect,
        Action onClickOutside)
    {
        _dispatcher = dispatcher;
        _isActive = isActive;
        _getRect = getRect;
        _onClickOutside = onClickOutside;
        _hookProc = HookCallback; // kept alive as a field (native callback)
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero)
            return;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _hookProc, IntPtr.Zero, 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
            return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0
            && (wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN
                || wParam == (IntPtr)NativeMethods.WM_RBUTTONDOWN)
            && _isActive())
        {
            var pt = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam).pt;
            var (x, y, w, h) = _getRect();
            var outside = pt.X < x || pt.X >= x + w || pt.Y < y || pt.Y >= y + h;
            if (outside)
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (_isActive())
                        _onClickOutside();
                });
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}
