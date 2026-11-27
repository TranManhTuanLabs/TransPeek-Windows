// Low-level mouse hook that hides the float icon when the user scrolls
// (Manh 2026-11-27, bug 1: the icon stayed its full auto-hide time
// while the hovered block scrolled away, so scrolling felt like two
// icons at once — the playing indicator plus a stale icon). The block
// the icon was anchored to has scrolled away from the icon's place, so
// the icon is stale and must go at once, not after its 5s timer — mac
// does the same via HoverTextDetector.onHoverStale (a follow poll that
// dismisses the icon when the block's frame moved). WinUI has no scroll
// event for a no-activate tool window, and the wheel goes to the app
// under the cursor, so this uses the documented WH_MOUSE_LL hook — the
// same shape as ClickOutsideMonitor. The hook runs on a system thread:
// the callback only posts to the UI dispatcher, never touches UI
// directly. Only the float icon hides here; the speaker indicator is
// the player and stays while a session plays.

using Microsoft.UI.Dispatching;
using TransPeek.Interop;

namespace TransPeek.Services;

public sealed class ScrollDismissMonitor : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<bool> _isIconVisible;
    private readonly Action _onScroll;
    private readonly NativeMethods.HookProc _hookProc;
    private IntPtr _hook;
    private bool _disposed;

    public ScrollDismissMonitor(
        DispatcherQueue dispatcher,
        Func<bool> isIconVisible,
        Action onScroll)
    {
        _dispatcher = dispatcher;
        _isIconVisible = isIconVisible;
        _onScroll = onScroll;
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
        // Any wheel notch (mouse wheel or trackpad two-finger scroll —
        // the system synthesizes WM_MOUSEWHEEL for both) while the icon
        // is up means the text under it moved: hide the icon at once.
        // The visibility check runs on the hook thread so a scroll with
        // no icon up costs no dispatcher post. Re-checked on the UI
        // thread, like ClickOutsideMonitor: a hide enqueued by this
        // scroll must not kill an icon that was re-shown after it.
        if (nCode >= 0
            && wParam == (IntPtr)NativeMethods.WM_MOUSEWHEEL
            && _isIconVisible())
        {
            _dispatcher.TryEnqueue(() =>
            {
                if (_isIconVisible())
                    _onScroll();
            });
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
