// Polls the GLOBAL Space key (GetAsyncKeyState) — the same mechanism
// the trigger icon uses to watch Alt (TriggerIconWindow, 0.1s poll):
// a Space press anywhere toggles Play/Pause while a reader is active,
// even when the reader's window is NOT the focused window (Manh
// 2026-11-21: in Lite you had to click into the panel first, because
// it opens without activation; in Full the window lost focus as soon
// as the mouse left it). The key is only OBSERVED, never swallowed —
// it still reaches the foreground app (a space typed in Notepad while
// Lite reads also pauses it). That is the trade-off of tracking Space
// globally; the tracker only runs while its window is visible, and
// only acts while the reader is active (not finished).

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TransPeek.Interop;
using WinRT.Interop;

namespace TransPeek.FastRead;

/// <summary>Global Space tracking for a reader window: polls the
/// physical Space key and invokes <paramref name="onSpacePressed"/>
/// on a fresh press, while <paramref name="shouldTrack"/> says the
/// reader is active. Skips while the window itself is foreground —
/// its own PreviewKeyDown handles Space then, and acting as well
/// would toggle twice for one press.</summary>
public sealed class GlobalSpaceTracker
{
    private readonly DispatcherTimer _timer = new();
    private readonly Window _window;
    private readonly Func<bool> _shouldTrack;
    private readonly Action _onSpacePressed;
    private bool _spaceWasDown;

    public GlobalSpaceTracker(Window window, Func<bool> shouldTrack, Action onSpacePressed)
    {
        _window = window;
        _shouldTrack = shouldTrack;
        _onSpacePressed = onSpacePressed;
        // 50 ms, not the icon's 100 ms: a Space tap is shorter than
        // an Alt hold — a quick tap must not fall between two ticks.
        _timer.Interval = TimeSpan.FromMilliseconds(50);
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        // Flush a stale "pressed since last call" bit AND record the
        // current down state: the low bit survives while the tracker
        // is stopped, so a Space pressed before Start would otherwise
        // toggle once on the first tick, and a key already held down
        // would look like a fresh press.
        var state = NativeMethods.GetAsyncKeyState(NativeMethods.VK_SPACE);
        _spaceWasDown = (state & 0x8000) != 0;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void Tick()
    {
        var state = NativeMethods.GetAsyncKeyState(NativeMethods.VK_SPACE);
        var down = (state & 0x8000) != 0;
        // Low bit (0x0001) = pressed since the last call: catches a
        // quick tap that falls between two ticks. A fresh press is
        // EITHER a down-edge (down now, up before) OR a complete tap
        // between ticks (pressed, already up again) — one physical
        // press fires once either way, and a held key never re-fires
        // (the low bit is consumed by the call, and the edge only
        // fires on the down transition).
        var pressed = (state & 0x0001) != 0;
        var freshPress = (down && !_spaceWasDown) || (pressed && !down);
        _spaceWasDown = down;
        if (!freshPress)
            return;
        if (!_shouldTrack())
            return; // consumed above: no stale press leaks in later.
        // The window's own PreviewKeyDown handles Space while it is
        // focused — skip then, or one press would toggle twice.
        var hwnd = WindowNative.GetWindowHandle(_window);
        if (hwnd != IntPtr.Zero && NativeMethods.GetForegroundWindow() == hwnd)
            return;
        _onSpacePressed();
    }
}
