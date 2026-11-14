// Watches the user's text selection via UI Automation (SPEC §2).
// Primary layer: UIA TextPattern.GetSelection on the focused element,
// polled every ~300 ms (same cadence as the mac app's SelectionMonitor).
// When the selection changes to non-empty text, the trigger icon is shown
// near the selection. The clipboard Ctrl+C layer is NOT polled — it only
// runs when the user presses the manual translate hotkey, so the app never
// clobbers the clipboard unprompted (see ClipboardHelper).
//
// FlaUI.UIA3 wraps the native UIA COM API. All UIA calls run on a
// background thread and are wrapped in try/catch: any app can expose a
// broken accessibility tree, and a broken tree must never crash TransPeek.

using FlaUI.Core;
using FlaUI.UIA3;

namespace TransPeek.Services;

public sealed class SelectionMonitor : IDisposable
{
    private readonly Action<string, (int X, int Y)> _onSelectionChanged;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private string _lastText = "";
    private bool _disposed;

    /// <param name="onSelectionChanged">Invoked on a background thread with
    /// the new selected text and the point (physical pixels) to anchor the
    /// trigger icon at (bottom-right of the focused element, or the cursor
    /// when the element reports no rectangle).</param>
    public SelectionMonitor(Action<string, (int X, int Y)> onSelectionChanged,
        TimeSpan? interval = null)
    {
        _onSelectionChanged = onSelectionChanged;
        _interval = interval ?? TimeSpan.FromMilliseconds(300);
    }

    public void Start()
    {
        _loop = Task.Run(PollLoopAsync);
    }

    private async Task PollLoopAsync()
    {
        UIA3Automation? automation = null;
        try
        {
            automation = new UIA3Automation();
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var (text, point) = TryGetSelection(automation);
                    if (text is not null && !string.Equals(text, _lastText, StringComparison.Ordinal))
                    {
                        _lastText = text;
                        _onSelectionChanged(text, point);
                    }
                    else if (text is null)
                    {
                        _lastText = "";
                    }
                }
                catch
                {
                    // Any UIA failure is swallowed: a broken accessibility
                    // tree in the foreground app must not kill the monitor.
                }
                await Task.Delay(_interval, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch
        {
            // UIA3Automation construction itself failed (no desktop session,
            // etc.). The monitor stays dead; hotkeys still work.
        }
        finally
        {
            automation?.Dispose();
        }
    }

    private static (string? Text, (int X, int Y) Point) TryGetSelection(UIA3Automation automation)
    {
        var element = automation.FocusedElement();
        if (element is null)
            return (null, (0, 0));
        // Own windows (panel, Settings) must never trigger the icon:
        // selecting text inside TransPeek itself popped the floating
        // icon right under the mouse (0.1.3.59 test — hovering /
        // selecting text in the panel showed it). FlaUI reports the
        // focused element's process id; skip when it is ours. Fail open
        // (pid 0 = unknown): a missed exclusion only shows the icon,
        // a wrong one would hide the core feature.
        var pid = element.Properties.ProcessId.ValueOrDefault;
        if (pid != 0 && pid == Environment.ProcessId)
            return (null, (0, 0));
        var pattern = element.Patterns.Text.PatternOrDefault;
        if (pattern is null)
            return (null, (0, 0));
        var ranges = pattern.GetSelection();
        if (ranges is null || ranges.Length == 0)
            return (null, (0, 0));
        var text = string.Concat(ranges.Select(r => r.GetText(-1) ?? "")).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return (null, (0, 0));

        (int X, int Y) point;
        // Anchor at the SELECTION's rectangle (bottom-right of the last
        // selected range), not the focused element's: the element rect
        // is the whole editor/window, so the icon landed at the
        // screen's corner instead of near the cursor (0.1.3.47 test).
        // The mouse is where the user just selected, so the cursor is
        // the fallback when the provider reports no rectangles.
        try
        {
            var rects = ranges
                .SelectMany(r => r.GetBoundingRectangles() ?? Array.Empty<System.Drawing.Rectangle>())
                .ToArray();
            if (rects.Length > 0)
            {
                var last = rects[^1];
                point = (last.Right, last.Bottom);
            }
            else
            {
                point = Interop.WindowHelpers.CursorPosition();
            }
        }
        catch
        {
            point = Interop.WindowHelpers.CursorPosition();
        }
        return (text, point);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
