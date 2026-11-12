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
    private readonly Func<int> _getEpoch;
    private readonly Action<int> _onClickOutside;
    private readonly NativeMethods.HookProc _hookProc;
    private IntPtr _hook;
    private bool _disposed;

    public ClickOutsideMonitor(
        DispatcherQueue dispatcher,
        Func<bool> isActive,
        Func<(int X, int Y, int Width, int Height)> getRect,
        Func<int> getEpoch,
        Action<int> onClickOutside)
    {
        _dispatcher = dispatcher;
        _isActive = isActive;
        _getRect = getRect;
        _getEpoch = getEpoch;
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
                // Capture the epoch at CLICK time: a hide enqueued by
                // this click must not kill a panel that was re-shown
                // after it. The tray-menu case: the menu's mouse-down
                // enqueues a hide for the open panel, the menu click
                // re-shows it, and the queued hide then kills the new
                // panel — the tray Manual/Clipboard/Selection actions
                // looked dead in the 0.1.3.47 test.
                var epoch = _getEpoch();
                _dispatcher.TryEnqueue(() =>
                {
                    if (_isActive())
                        _onClickOutside(epoch);
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
