// Window-list diagnostic (%LOCALAPPDATA%\TransPeek\windows.log).
//
// A floating white "Esc" pill keeps appearing above the panel and in
// the OCR capture screen (Manh, 2026-11-14), but an exhaustive search
// of the code finds NO "Esc" button, tooltip or label in TransPeek:
// the only "Esc" strings are comments and the panel/Settings hint
// texts, the Cancel button was removed in 0.1.3.63, the hint chip was
// removed in 0.1.3.65, and the toast is a dark pill at the bottom-right
// that never shows "Esc". So the pill may belong to another app — or
// to a TransPeek window nobody recognised. This lists every visible
// top-level window with its HWND, title, size/position and owning
// process, so the pill's owner can be identified for certain.
//
// How to use: when the "Esc" pill is visible, right-click the tray
// icon -> "Diagnose: list windows", then send windows.log. The pill's
// window will be the small (~40x28 px) one near the top-center of the
// screen; its process name settles whether it is TransPeek.exe or
// something else (e.g. a screenshot tool, Edge's fullscreen hint, or
// an old TransPeek build still running in the background).
//
// Temporary: remove this file, its NativeMethods P/Invokes and the
// tray menu item once the pill's owner is known.

using System.Diagnostics;
using System.Text;
using TransPeek.Interop;

namespace TransPeek.Services;

internal static class WindowDiagnostics
{
    public static void Write()
    {
        try
        {
            var lines = new StringBuilder();
            lines.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Visible top-level windows:");
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                try
                {
                    if (!NativeMethods.IsWindowVisible(hWnd))
                        return true;
                    var title = new StringBuilder(256);
                    NativeMethods.GetWindowTextW(hWnd, title, title.Capacity);
                    NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
                    string process = "?";
                    try { process = Process.GetProcessById((int)pid).ProcessName; }
                    catch { /* process may have exited */ }
                    var rect = NativeMethods.GetWindowRect(hWnd, out var r)
                        ? $"{r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top}"
                        : "?";
                    lines.AppendLine($"  0x{hWnd.ToInt64():X} pid={pid} [{process}] rect={rect} title=\"{title}\"");
                }
                catch
                {
                    // One bad window must not stop the enumeration.
                }
                return true;
            }, IntPtr.Zero);
            var dir = TransPeek.Core.Dictionary.AppPaths.LocalDataDirectory;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "windows.log"), lines.ToString());
        }
        catch
        {
            // Diagnostics must never crash the app.
        }
    }
}
