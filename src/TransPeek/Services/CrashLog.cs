// Crash log (%LOCALAPPDATA%\TransPeek\crash.log).
//
// Run #40 died at startup with 0xc000027b in Microsoft.UI.Xaml.dll and
// the app had NO UnhandledException handler, so it vanished silently:
// no console output, no .NET Runtime event, no stack trace — only an
// Event Viewer entry naming WinUI itself. The handler in App writes
// every unhandled exception here (type, message, stack trace) so the
// next crash can be diagnosed from this file instead of Event Viewer.

namespace TransPeek.Services;

internal static class CrashLog
{
    public static void Write(string source, Exception? exception)
    {
        try
        {
            var text = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {source}: " +
                $"{exception?.GetType().FullName}: {exception?.Message}" +
                $"{Environment.NewLine}{exception?.StackTrace}{Environment.NewLine}";
            Append(text);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    /// <summary>Plain diagnostic line (no exception): tray-menu clicks,
    /// OCR capture stats — evidence for the next debugging round.</summary>
    public static void WriteLine(string source, string message)
    {
        try
        {
            Append($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {source}: {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void Append(string text)
    {
        var dir = TransPeek.Core.Dictionary.AppPaths.LocalDataDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "crash.log");
        File.AppendAllText(path, text);
    }
}
