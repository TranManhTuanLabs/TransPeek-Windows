// Global hotkeys via Win32 RegisterHotKey (SPEC §3.3: OCR hotkey
// Ctrl+Shift+S by default; manual translate hotkey Ctrl+Alt+T).
//
// Runs a message-only window on a dedicated thread with a GetMessage loop
// (WinUI has no built-in global-hotkey API). Hotkeys are (re)registered
// when the service starts; changing them in Settings restarts the service.
// Keep the WndProc delegate alive as a field — the GC must never collect
// a delegate passed to native code.

using System.Runtime.InteropServices;
using TransPeek.Interop;

namespace TransPeek.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    public const int TranslateHotkeyId = 1;
    public const int OcrHotkeyId = 2;

    private readonly Dictionary<int, (uint Modifiers, uint Vk, Action Handler)> _hotkeys = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ManualResetEventSlim _stopped = new(false);
    private IntPtr _hwnd;
    private uint _threadId; // native id of the message-loop thread (for WM_QUIT)
    private bool _disposed;

    // Kept alive for the lifetime of the service (native callback).
    private readonly NativeMethods.WNDCLASSEX _wndClass;
    private readonly WndProc _wndProc;

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public GlobalHotkeyService()
    {
        _wndProc = OnWindowMessage;
        _wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
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
        _stopped.Set();
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
