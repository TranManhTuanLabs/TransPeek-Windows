// Global hotkeys via Win32 RegisterHotKey (SPEC §3.3: OCR hotkey
// Ctrl+Shift+S by default; manual translate hotkey Ctrl+Alt+T).
//
// Runs a message-only window on a dedicated thread with a GetMessage loop
// (WinUI has no built-in global-hotkey API). Hotkeys are (re)registered
// when the service starts; changing them in Settings restarts the service.
//
// The window class ("TransPeekHotkeyWindow") is registered ONCE per
// process: RegisterClassEx fails if the name is taken, and the class is
// never unregistered — so its WndProc must live for the PROCESS lifetime,
// not the service lifetime. A per-instance delegate dies with its service
// (the field only keeps it alive while the service is reachable), but the
// class keeps calling it: FailFast "callback on a garbage collected
// delegate" (0.1.3.59 test, Event Viewer 1025, after every Settings save
// re-created the service — e.g. switching the OCR engine). The WndProc
// is therefore STATIC; it forwards to the CURRENT service for that
// window handle, so a re-created service still receives its hotkeys.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TransPeek.Interop;

namespace TransPeek.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    public const int TranslateHotkeyId = 1;
    public const int OcrHotkeyId = 2;
    public const int ManualHotkeyId = 3;
    public const int ClipboardOcrHotkeyId = 4;
    public const int SelectionHotkeyId = 5;
    public const int ScreenshotCopyHotkeyId = 6;
    public const int ScanHotkeyId = 7;
    public const int SettingsHotkeyId = 8;

    private readonly Dictionary<int, (uint Modifiers, uint Vk, Action Handler)> _hotkeys = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ManualResetEventSlim _stopped = new(false);
    private IntPtr _hwnd;
    private uint _threadId; // native id of the message-loop thread (for WM_QUIT)
    private bool _disposed;

    // Process-lifetime WndProc (see the file header): the window class
    // is registered once and never unregistered, so the delegate it
    // points to must never be collected. Static fields live for the
    // process lifetime. The registry routes each window to the service
    // that owns it, so a re-created service gets its own hotkeys.
    private static readonly WndProc _staticWndProc = OnWindowMessageStatic;
    private static readonly ConcurrentDictionary<IntPtr, GlobalHotkeyService> _servicesByHwnd = new();

    // The class registration (a struct holding the function pointer).
    private NativeMethods.WNDCLASSEX _wndClass;

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public GlobalHotkeyService()
    {
        _wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_staticWndProc),
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = "TransPeekHotkeyWindow",
        };
        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "TransPeek hotkeys" };
    }

    /// <summary>Register a hotkey. Call before <see cref="Start"/>.</summary>
    public void Register(int id, string spec, Action handler)
    {
        var (modifiers, vk) = Parse(spec);
        _hotkeys[id] = (modifiers, vk, handler);
    }

    public void Start()
    {
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    public void Stop()
    {
        // PostQuitMessage would target the CALLING thread — the quit must
        // go to the message-loop thread instead.
        if (_threadId != 0)
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _stopped.Wait(TimeSpan.FromSeconds(5));
    }

    private void MessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        NativeMethods.RegisterClassEx(ref _wndClass);
        _hwnd = NativeMethods.CreateWindowEx(
            0, "TransPeekHotkeyWindow", "TransPeekHotkeyWindow", NativeMethods.WS_OVERLAPPED,
            0, 0, 0, 0, new IntPtr(NativeMethods.HWND_MESSAGE), IntPtr.Zero,
            NativeMethods.GetModuleHandle(null), IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            _ready.Set();
            return;
        }
        // Route this window's messages to this service (the static
        // WndProc looks it up here). Registered before the loop starts
        // so no message can arrive unrouted.
        _servicesByHwnd[_hwnd] = this;

        foreach (var (id, (modifiers, vk, _)) in _hotkeys)
            NativeMethods.RegisterHotKey(_hwnd, id, modifiers, vk);

        _ready.Set();
        // GetMessage returns -1 on error, 0 on WM_QUIT, > 0 otherwise.
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        foreach (var id in _hotkeys.Keys)
            NativeMethods.UnregisterHotKey(_hwnd, id);
        NativeMethods.DestroyWindow(_hwnd);
        // The window is gone: no more messages will arrive for it.
        _servicesByHwnd.TryRemove(_hwnd, out _);
        _stopped.Set();
    }

    /// <summary>Process-lifetime entry point for the window class:
    /// forwards to the service that owns the window handle, or to
    /// DefWindowProc when the window has no service (already destroyed).</summary>
    private static IntPtr OnWindowMessageStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_servicesByHwnd.TryGetValue(hWnd, out var service))
            return service.OnWindowMessage(hWnd, msg, wParam, lParam);
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private IntPtr OnWindowMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeys.TryGetValue(id, out var entry))
            {
                // Run on the thread pool — handlers marshal to the UI thread.
                _ = Task.Run(entry.Handler);
            }
            return IntPtr.Zero;
        }
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>Parse "Control+Alt+T" / "Control+Shift+S" into Win32
    /// modifiers + virtual key. Throws <see cref="FormatException"/> on
    /// unknown tokens (surfaced in Settings validation).</summary>
    public static (uint Modifiers, uint Vk) Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            throw new FormatException("Hotkey is empty.");
        uint modifiers = 0;
        uint vk = 0;
        foreach (var raw in spec.Split('+'))
        {
            var token = raw.Trim();
            switch (token.ToLowerInvariant())
            {
                case "control":
                case "ctrl": modifiers |= NativeMethods.MOD_CONTROL; break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; break;
                case "alt": modifiers |= NativeMethods.MOD_ALT; break;
                case "win":
                case "windows": modifiers |= NativeMethods.MOD_WIN; break;
                default:
                    vk = ParseKey(token);
                    break;
            }
        }
        if (vk == 0)
            throw new FormatException($"Hotkey '{spec}' has no key.");
        return (modifiers, vk);
    }

    private static uint ParseKey(string token)
    {
        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
                return c;
        }
        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(token[1..], out var f) && f is >= 1 and <= 24)
            return 0x6F + (uint)f; // VK_F1 = 0x70
        return token.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "ESCAPE" or "ESC" => 0x1B,
            "TAB" => 0x09,
            _ => throw new FormatException($"Unknown hotkey key '{token}'."),
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try { Stop(); } catch { /* best effort */ }
        _ready.Dispose();
        _stopped.Dispose();
    }
}
