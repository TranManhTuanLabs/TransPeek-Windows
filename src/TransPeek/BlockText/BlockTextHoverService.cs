// Shows the trigger icon when the mouse RESTS on a block of text —
// no manual selection needed (feature/block-text-detection, Manh
// 2026-11-20 23:58). Model A per the TTS research 16/11
// (speechify-report.md §11.1, recommendation.md §2): hit-test ONE
// point (FromPoint) per dwell session — NO scan of the UIA tree.
// Same stack as SelectionMonitor: FlaUI UIA3, background thread,
// every UIA call in try/catch, own process excluded. Only text the
// target app EXPOSES to UI Automation is detected — where nothing is
// exposed the service does nothing (Manh: don't force it; handling
// non-exposed text is a separate research task for a later session).
//
// Priority (a manual selection ALWAYS beats hover): a probe only runs
// when the FOCUSED element has NO active text selection, checked at
// probe time — so a selection's text is never overwritten by hover,
// whatever the two monitors' poll phases are. The re-show of a cached
// block is gated the same way. Hiding is NOT this service's job: the
// icon hides on its own auto-hide timer (TriggerIconWindow), which
// every ShowAt restarts — when the mouse leaves the block, no new
// show fires and the icon fades on its own.

using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using TransPeek.Core.BlockText;
using TransPeek.Interop;

namespace TransPeek.BlockText;

public sealed class BlockTextHoverService : IDisposable
{
    // Poll cadence: 100 ms. A tick only reads the cursor position
    // (GetCursorPos — a trivial call) and runs the state machine's
    // integer compares; the expensive UIA work only happens on a probe
    // (once per dwell session), so 10 wakeups/second cost nothing
    // measurable. 100 ms matters because the dwell is 150 ms
    // (BlockTextHoverState.DefaultDwellMilliseconds): with a 300 ms
    // poll like SelectionMonitor's, the icon would appear 150-450 ms
    // after the mouse stops; at 100 ms it appears ~150-250 ms after
    // stopping — the ~150 ms debounce the research recommends
    // (speechify-report.md §11.1).
    private const int DefaultPollIntervalMs = 100;

    // A single UIA text element can be a whole document (an app that
    // exposes one TextPattern for everything). Feeding the whole
    // document to translate/speak on a hover would be wrong — a hover
    // is one block — so the text is capped at roughly a long paragraph.
    // Longer blocks still show (cut at a word boundary); they just do
    // not drag the whole document along.
    private const int MaxHoverTextLength = 4000;

    private readonly Action<string, (int X, int Y), Rectangle?> _onBlockHovered;
    private readonly Func<bool>? _isSuppressed;
    private readonly TimeSpan _interval;
    private readonly BlockTextHoverState _state;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private bool _disposed;
    // The paragraph rect from the last probe (null when the paragraph
    // path failed): a cached re-show (ShowCached) has no fresh probe,
    // so it reuses this — the mouse is still inside the same block, so
    // the last probed paragraph is still the right anchor for
    // continuous listen. Cleared with the state (NoteProbe null) so a
    // stale paragraph never chains after the block changed.
    private Rectangle? _lastParagraphRect;

    /// <param name="onBlockHovered">Invoked on a background thread with
    /// the hovered block's text, the cursor point (physical pixels)
    /// to anchor the trigger icon at — same shape as the selection
    /// monitor's callback, so App feeds it into the same icon path —
    /// and the PARAGRAPH's rect (or null when the paragraph path
    /// failed and the text came from a fallback). The rect is the
    /// continuous-listen probe anchor: it is captured HERE, at probe
    /// time, because by the time Speak is clicked the hovered block is
    /// usually gone (Mac lesson fa187c83). A null rect just means "no
    /// chain, read the block and stop" (the old behaviour).</param>
    /// <param name="dwell">How long the cursor must stay still before
    /// a probe. Null = the research default
    /// (BlockTextHoverState.DefaultDwellMilliseconds, 150 ms). App
    /// passes Settings.HoverBlockDelayMs (default 400 ms, Manh
    /// 2026-11-21, mac parity: hoverBlockDelayMs) — deliberately NOT
    /// Settings.HoverDelayMs (see the const's comment: that setting
    /// governs hovering the icon, not text).</param>
    /// <param name="isSuppressed">Optional gate, evaluated on the poll
    /// thread before every probe: while it returns true, the service
    /// neither probes nor fires (and clears its cached state, so no
    /// stale icon pops up when suppression ends). App passes "the
    /// translate panel is visible" (Manh 2026-11-27, mac parity):
    /// hovering text in another app while the panel is up stole the
    /// panel's focus — confirmed by Manh turning HoverBlockText off
    /// (focus loss stopped). Keep it to thread-safe reads (a plain
    /// bool field, like IsFastReadTabActive).</param>
    public BlockTextHoverService(Action<string, (int X, int Y), Rectangle?> onBlockHovered,
        TimeSpan? dwell = null, TimeSpan? interval = null, Func<bool>? isSuppressed = null)
    {
        _onBlockHovered = onBlockHovered;
        _isSuppressed = isSuppressed;
        _state = new BlockTextHoverState(dwell ?? TimeSpan.FromMilliseconds(BlockTextHoverState.DefaultDwellMilliseconds));
        _interval = interval ?? TimeSpan.FromMilliseconds(DefaultPollIntervalMs);
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
                    Tick(automation);
                }
                catch
                {
                    // Any UIA failure is swallowed: a broken accessibility
                    // tree in the foreground app must not kill the service.
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
            // etc.). The service stays dead; hotkeys still work.
        }
        finally
        {
            automation?.Dispose();
        }
    }

    private void Tick(UIA3Automation automation)
    {
        var cursor = WindowHelpers.CursorPosition();
        var action = _state.Tick(cursor, DateTimeOffset.UtcNow);
        if (action == BlockTextHoverAction.None)
            return;
        // Selection beats hover on BOTH paths: a fresh probe and a
        // cached re-show must never overwrite a live selection's text.
        // (The check runs at probe time, not from a cached flag, so it
        // is true the moment the user selects.)
        if (HasActiveSelection(automation))
        {
            _state.NoteProbe(null, null);
            _lastParagraphRect = null;
            return;
        }
        // Panel-up suppression (Manh 2026-11-27, mac parity): while the
        // translate panel is open, do NOT probe or fire at all — the
        // probe + icon stole the panel's focus when the mouse moved to
        // another app. Clearing the state (NoteProbe null) means no
        // stale icon appears the moment the panel closes.
        if (_isSuppressed?.Invoke() == true)
        {
            _state.NoteProbe(null, null);
            _lastParagraphRect = null;
            return;
        }
        if (action == BlockTextHoverAction.ShowCached)
        {
            // New dwell session inside a known block (the icon auto-hid
            // while the mouse stayed there): show the cached text again.
            // The paragraph rect is the last probed one (same block —
            // the cursor is still inside it), or null when the block
            // never had a paragraph path.
            if (_state.CachedText is { } text)
                _onBlockHovered(text, cursor, _lastParagraphRect);
            return;
        }
        Probe(automation, cursor);
    }

    private void Probe(UIA3Automation automation, (int X, int Y) cursor)
    {
        AutomationElement? element;
        try
        {
            element = automation.FromPoint(new Point(cursor.X, cursor.Y));
        }
        catch
        {
            // FromPoint itself failed (no desktop, broken tree): mark
            // the dwell handled so the same still cursor does not
            // hammer UIA every tick.
            _state.NoteProbe(null, null);
            return;
        }
        if (element is null)
        {
            _state.NoteProbe(null, null);
            return;
        }
        try
        {
            // Own windows (panel, Settings, the icon itself) must never
            // trigger the icon: same rule as SelectionMonitor. Fail open
            // (pid 0 = unknown): a missed exclusion only shows the icon,
            // a wrong one would hide the feature.
            var pid = element.Properties.ProcessId.ValueOrDefault;
            if (pid != 0 && pid == Environment.ProcessId)
            {
                _state.NoteProbe(null, null);
                return;
            }
            // The paragraph rect is the only rect ever cached (tight):
            // a fallback's element rect can be a whole window/viewport,
            // and caching it made ShowCached re-show that text over
            // OTHER windows stacked on top (CI272: hover in Word read
            // Desktop/Chrome text). A fallback block is never cached —
            // the next dwell probes it fresh, so the icon always shows
            // the window under the cursor. A text-less container never
            // hides its text children (see NoteProbe).
            var (text, _, paragraphRect) = TryGetBlockText(element, new Point(cursor.X, cursor.Y));
            _state.NoteProbe(paragraphRect, text);
            // Remember the paragraph rect for the cached re-show path
            // (and as the continuous-listen anchor): it is only set
            // when the paragraph path itself succeeded.
            _lastParagraphRect = paragraphRect;
            if (!string.IsNullOrWhiteSpace(text))
                _onBlockHovered(text, cursor, paragraphRect);
        }
        catch
        {
            // Any UIA failure is swallowed (same rule as SelectionMonitor).
            _state.NoteProbe(null, null);
        }
    }

    /// <summary>True when the FOCUSED element currently has a non-empty
    /// text selection: while that is true, hover must stay out of the
    /// way. Fail open (false on any UIA failure): a missed selection
    /// only shows the icon, and the probe that follows would likely
    /// fail the same way.</summary>
    private static bool HasActiveSelection(UIA3Automation automation)
    {
        try
        {
            var focused = automation.FocusedElement();
            var pattern = focused?.Patterns.Text.PatternOrDefault;
            var ranges = pattern?.GetSelection();
            if (ranges is null)
                return false;
            foreach (var range in ranges)
            {
                if (!string.IsNullOrWhiteSpace(SafeGet(() => range.GetText(-1))))
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Read a block's text from the element under the cursor,
    /// richest source first, together with the rect to cache: the
    /// PARAGRAPH's rect when the provider supports it (tight — the
    /// mouse roaming inside the same paragraph re-shows the cached
    /// text), the element's rect otherwise. Text that is not exposed
    /// at all yields (null, rect) — the caller does nothing (Manh:
    /// don't force it). The third value is the paragraph's own rect,
    /// set ONLY when the paragraph path (step 1) succeeded — the
    /// continuous-listen probe anchor. The fallbacks' element rect is
    /// NOT a paragraph rect (a whole document element's rect would
    /// probe below the whole document), so they report null.</summary>
    private static (string? Text, Rectangle Rect, Rectangle? ParagraphRect) TryGetBlockText(AutomationElement element, Point cursor)
    {
        // 1. The paragraph under the cursor (research Model A:
        // speechify-report.md §11.1 — TextPattern.RangeFromPoint +
        // ExpandToEnclosingUnit(Paragraph) + GetBoundingRectangles).
        // This finds the REAL block even inside a giant document
        // element (a browser page, a Word document: one TextPattern
        // for everything) — DocumentRange alone would return the
        // whole document for any line in it.
        var paragraph = TryGetParagraph(element, cursor);
        if (paragraph is { } p)
            return (CapText(p.Text), p.Rect, p.Rect);
        // 2. Whole-element text (DocumentRange): fallback when the
        // provider has no RangeFromPoint / Paragraph support. The
        // entire text the element exposes; -1 means "all of it"
        // (same as the selection monitor).
        var elementRect = ElementRect(element);
        var textPattern = SafeGet(() => element.Patterns.Text.PatternOrDefault);
        var documentText = SafeGet(() => textPattern?.DocumentRange.GetText(-1));
        if (!string.IsNullOrWhiteSpace(documentText))
            return (CapText(documentText), elementRect, null);
        // 3. ValuePattern: single-value controls (text boxes, combo
        // boxes, anything whose "value" is its text).
        var valuePattern = SafeGet(() => element.Patterns.Value.PatternOrDefault);
        var value = SafeGet(() => valuePattern?.Value.ValueOrDefault);
        if (!string.IsNullOrWhiteSpace(value))
            return (CapText(value), elementRect, null);
        // 4. Name: buttons, links, labels — a label by nature, so no
        // cap is needed (it is short already).
        var name = SafeGet(() => element.Properties.Name.ValueOrDefault);
        if (!string.IsNullOrWhiteSpace(name))
            return (name.Trim(), elementRect, null);
        return (null, elementRect, null);
    }

    /// <summary>The paragraph under the cursor: RangeFromPoint hit-tests
    /// the document at the point, ExpandToEnclosingUnit(Paragraph)
    /// grows that range to the whole enclosing paragraph, and
    /// GetBoundingRectangles gives the paragraph's line rects (their
    /// union is the paragraph's rect). Any step may fail — providers
    /// vary (Word/Edge/Notepad support it; Chrome/Electron only
    /// partly; some degrade Paragraph up to Document, per the UIA
    /// docs) — so every step is guarded and null means "fall back to
    /// DocumentRange" rather than "no text".</summary>
    private static (string Text, Rectangle Rect)? TryGetParagraph(AutomationElement element, Point cursor)
    {
        var textPattern = SafeGet(() => element.Patterns.Text.PatternOrDefault);
        var range = SafeGet(() => textPattern?.RangeFromPoint(cursor));
        if (range is null)
            return null;
        SafeCall(() => range.ExpandToEnclosingUnit(TextUnit.Paragraph));
        var text = SafeGet(() => range.GetText(-1));
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var rects = SafeGet(() => range.GetBoundingRectangles());
        return (text, UnionRects(rects));
    }

    /// <summary>The element's own rectangle (fallback cache rect when
    /// the paragraph path is unavailable). Empty when UIA cannot
    /// provide it.</summary>
    private static Rectangle ElementRect(AutomationElement element)
    {
        try
        {
            return element.Properties.BoundingRectangle.ValueOrDefault;
        }
        catch
        {
            return default;
        }
    }

    /// <summary>Union of a range's line rects = the range's rect: the
    /// cache granularity becomes "one paragraph", not "one element"
    /// (an element can be a whole document). Empty when the provider
    /// returns no usable rects.</summary>
    private static Rectangle UnionRects(Rectangle[]? rects)
    {
        var union = Rectangle.Empty;
        if (rects is null)
            return union;
        foreach (var rect in rects)
        {
            if (!rect.IsEmpty)
                union = union.IsEmpty ? rect : Rectangle.Union(union, rect);
        }
        return union;
    }

    /// <summary>Trim to the cap at a word boundary, so a hovered block
    /// never shows a half-word at the cut.</summary>
    private static string CapText(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxHoverTextLength)
            return trimmed;
        var cut = trimmed[..MaxHoverTextLength];
        var lastSpace = cut.LastIndexOf(' ');
        return lastSpace > 0 ? cut[..lastSpace] : cut;
    }

    /// <summary>One UIA call, guarded: a failing step must not block
    /// the fallback chain (a broken TextPattern must not hide a
    /// working ValuePattern underneath).</summary>
    private static T? SafeGet<T>(Func<T?> get)
    {
        try
        {
            return get();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>One UIA call with no return value, guarded: used for
    /// ExpandToEnclosingUnit, whose failure just means "fall back to
    /// DocumentRange" (see TryGetParagraph).</summary>
    private static void SafeCall(Action call)
    {
        try
        {
            call();
        }
        catch
        {
        }
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
