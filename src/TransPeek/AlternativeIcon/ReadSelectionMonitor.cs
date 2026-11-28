// Watches the user's text selection INSIDE TransPeek's own Read
// surfaces (alternative icon, mac FAST_READ_SELECT_ICON.md §7). The
// global SelectionMonitor deliberately skips our own process, so it
// can never see a selection inside the Fast Read window or the panel's
// Fast Read tab — this monitor is the separate path for that case.
//
// Same poll pattern (UIA TextPattern.GetSelection, ~300 ms) as
// SelectionMonitor, but with the filter INVERTED: it ONLY reads our
// own process, and only while the caller's gate says a Read surface is
// foreground. Polling (not a mouse-up hook) also catches keyboard
// selections (Shift+arrows), which a mouse-up hook would miss.
//
// The gate runs on this monitor's background thread, so it must only
// use thread-safe reads (Win32 + plain fields) — App's gate compares
// GetForegroundWindow against HWNDs captured on the UI thread.

using FlaUI.Core;
using FlaUI.UIA3;
using TransPeek.Services;

namespace TransPeek.AlternativeIcon;

public sealed class ReadSelectionMonitor : IDisposable
{
    private readonly Func<bool> _isReadSurfaceActive;
    private readonly Action<string, (int X, int Y)> _onSelectionChanged;
    private readonly Action _onSelectionCleared;
    private readonly TimeSpan _interval;
    // Foreground-window info for the gate-clear log (CI272 flicker):
    // which window was foreground when the gate flapped. Supplied by
    // App (it owns the HWNDs); null when no provider was given.
    private readonly Func<string>? _getForegroundInfo;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private string _lastText = "";
    private bool _disposed;
    // Debounce for "no valid selection" (Manh 2026-11-27, [Flicker]
    // log): the foreground gate (or a null UIA read) can flap for a
    // single poll while a selection is active in the Reader — clearing
    // _lastText on one flap made the same selection re-fire as "new" on
    // the next poll (85 altSel lines for one 6-char selection in
    // windows.log), re-showing the icon and flickering it. Only treat
    // "no valid selection" as real after this many consecutive polls;
    // a single flap is ignored and the icon stays put.
    private const int NoSelectionDebouncePolls = 2;
    private int _noSelectionStreak;

    /// <param name="isReadSurfaceActive">True while a Read surface is
    /// the foreground window (Fast Read window, or the panel on its
    /// Fast Read tab). Evaluated on the poll thread — keep it to
    /// thread-safe reads.</param>
    /// <param name="onSelectionChanged">Invoked on a background thread
    /// with the new selected text and the point (physical pixels) to
    /// anchor the alternative icon at (bottom-right of the last
    /// selected range, or the cursor when no rectangle is
    /// reported).</param>
    /// <param name="onSelectionCleared">Invoked on a background thread
    /// when the selection empties or the Read surface stops being
    /// foreground — the icon must hide.</param>
    public ReadSelectionMonitor(
        Func<bool> isReadSurfaceActive,
        Action<string, (int X, int Y)> onSelectionChanged,
        Action onSelectionCleared,
        TimeSpan? interval = null,
        Func<string>? getForegroundInfo = null)
    {
        _isReadSurfaceActive = isReadSurfaceActive;
        _onSelectionChanged = onSelectionChanged;
        _onSelectionCleared = onSelectionCleared;
        _interval = interval ?? TimeSpan.FromMilliseconds(300);
        _getForegroundInfo = getForegroundInfo;
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
                    // Gate first: outside a Read surface there is
                    // nothing to watch, and the icon must hide — but
                    // only after the debounce (see the field comment):
                    // a single gate flap must not clear _lastText.
                    if (!_isReadSurfaceActive())
                    {
                        _noSelectionStreak++;
                        if (_noSelectionStreak >= NoSelectionDebouncePolls && _lastText != "")
                        {
                            // Flicker diagnosis: log WHY the selection
                            // cleared (gate vs UIA read) + its length.
                            // The fg info names the window that stole
                            // foreground while the gate flapped.
                            WindowDiagnostics.AppendLine(
                                $"[Flicker] altClear reason=gate lastLen={_lastText.Length} {ForegroundInfo()}");
                            _lastText = "";
                            _onSelectionCleared();
                        }
                    }
                    else
                    {
                        var (text, point, why) = TryGetSelection(automation);
                        if (text is not null && !string.Equals(text, _lastText, StringComparison.Ordinal))
                        {
                            _noSelectionStreak = 0;
                            _lastText = text;
                            // Flicker investigation log (Manh 2026-11-27):
                            // log Reader-surface selections so windows.log
                            // shows whether the alternative icon fired.
                            // hash = stable text fingerprint: same-text
                            // re-fires share it, alternating text differs.
                            WindowDiagnostics.AppendLine(
                                $"[Flicker] altSel point=({point.X},{point.Y}) textLen={text.Length} hash={TextHash(text)}");
                            _onSelectionChanged(text, point);
                        }
                        else if (text is null)
                        {
                            // Same debounce as the gate: a single null
                            // UIA read must not clear _lastText.
                            _noSelectionStreak++;
                            if (_noSelectionStreak >= NoSelectionDebouncePolls && _lastText != "")
                            {
                                // Flicker diagnosis: log the UIA reason
                                // (no-focus / pid / no-pattern / ...).
                                WindowDiagnostics.AppendLine(
                                    $"[Flicker] altClear reason={why} lastLen={_lastText.Length}");
                                _lastText = "";
                                _onSelectionCleared();
                            }
                        }
                        else
                        {
                            // Same text still selected: the selection is
                            // alive, so the streak resets.
                            _noSelectionStreak = 0;
                        }
                    }
                }
                catch
                {
                    // Same rule as SelectionMonitor: a broken
                    // accessibility tree must never kill the monitor.
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
            // UIA3Automation construction itself failed. The monitor
            // stays dead; the rest of the app still works.
        }
        finally
        {
            automation?.Dispose();
        }
    }

    // Foreground-window info for the gate-clear log: which window was
    // foreground when the gate flapped (the flicker cause). Empty when
    // no provider was given, or when reading it fails.
    private string ForegroundInfo()
    {
        try
        {
            return _getForegroundInfo?.Invoke() ?? "";
        }
        catch
        {
            return "";
        }
    }

    // Stable text fingerprint for the flicker log (FNV-1a): tells
    // same-text re-fires apart from alternating text, without
    // logging the user's selected content.
    private static string TextHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("X8");
        }
    }

    private static (string? Text, (int X, int Y) Point, string Why) TryGetSelection(UIA3Automation automation)
    {
        var element = automation.FocusedElement();
        if (element is null)
            return (null, (0, 0), "no-focus");
        // INVERTED vs SelectionMonitor: only our own process. A
        // selection in another app is the main float icon's job; this
        // monitor only watches the Read text inside TransPeek itself.
        // Fail CLOSED here (pid 0 = unknown): showing the alternative
        // icon for another app's text would be wrong, so an unknown
        // pid reads as "not ours" (the main monitor fails open in
        // the other direction for the same reason).
        var pid = element.Properties.ProcessId.ValueOrDefault;
        if (pid == 0 || pid != Environment.ProcessId)
            return (null, (0, 0), $"pid={pid}");
        var pattern = element.Patterns.Text.PatternOrDefault;
        if (pattern is null)
            return (null, (0, 0), "no-textpattern");
        var ranges = pattern.GetSelection();
        if (ranges is null || ranges.Length == 0)
            return (null, (0, 0), "no-ranges");
        var text = string.Concat(ranges.Select(r => r.GetText(-1) ?? "")).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return (null, (0, 0), "empty-text");

        (int X, int Y) point;
        // Anchor at the SELECTION's rectangle (bottom-right of the last
        // selected range), not the focused element's: the element rect
        // is the whole window, so the icon would land at the screen's
        // corner instead of near the cursor (same lesson as
        // SelectionMonitor, 0.1.3.47). The mouse is where the user just
        // selected, so the cursor is the fallback when the provider
        // reports no rectangles.
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
        return (text, point, "ok");
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
