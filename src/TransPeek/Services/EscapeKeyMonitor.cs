// Low-level keyboard hook that fires when the user presses Esc while
// the float panel is visible (SPEC §3.1: Esc closes the panel).
//
// The panel is a no-activate tool window: it never receives keyboard
// input, so its Esc KeyboardAccelerator is dead until the user clicks
// into the panel first (0.1.3.54 test: Esc did nothing in the
// trigger-icon / hotkey paths, where the panel was never activated).
// A WH_KEYBOARD_LL hook sees the key whatever window has focus.
// Same shape as ClickOutsideMonitor: the hook runs on a system thread
// and only posts to the UI dispatcher, never touching UI directly.
//
// The hook is only installed while the panel is visible, and Esc is
// only intercepted then — it does not swallow Esc for other apps at
// any other time (the callback always calls the next hook).

using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using TransPeek.Interop;

namespace TransPeek.Services;

public sealed class EscapeKeyMonitor : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<bool> _isActive;
    private readonly Func<int> _getEpoch;
    private readonly Action<int> _onEscape;
    private readonly NativeMethods.HookProc _hookProc;
    private IntPtr _hook;
    private bool _disposed;

    public EscapeKeyMonitor(
        DispatcherQueue dispatcher,
        Func<bool> isActive,
        Func<int> getEpoch,
        Action<int> onEscape)
    {
        _dispatcher = dispatcher;
        _isActive = isActive;
        _getEpoch = getEpoch;
        _onEscape = onEscape;
        _hookProc = HookCallback; // kept alive as a field (native callback)
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero)
            return;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _hookProc, IntPtr.Zero, 0);
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
            && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN
                || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN)
            && _isActive())
        {
            // vkCode is the first DWORD of KBDLLHOOKSTRUCT.
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == NativeMethods.VK_ESCAPE)
            {
                // Capture the epoch at KEY time (same reason as
                // ClickOutsideMonitor: a hide enqueued by this key must
                // not kill a panel that was re-shown after it).
                var epoch = _getEpoch();
                _dispatcher.TryEnqueue(() =>
                {
                    if (_isActive())
                        _onEscape(epoch);
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
