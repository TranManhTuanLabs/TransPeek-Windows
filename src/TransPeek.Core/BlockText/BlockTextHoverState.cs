// Pure dwell + rectangle-cache state for hover-to-show-icon
// (feature/block-text-detection, Manh 2026-11-20 23:58): the mouse
// resting on a block of text shows the trigger icon without a manual
// selection. NO UIA here — the app service feeds it cursor positions
// and probe outcomes, it decides when a probe is due and whether a
// known block may show again. Pure so the test project (which only
// references Core) can unit-test the timing and cache rules; the UIA
// probing itself lives in the app service and is not tested on CI Linux.

using System.Drawing;

namespace TransPeek.Core.BlockText;

/// <summary>What the service should do on one poll tick.</summary>
public enum BlockTextHoverAction
{
    /// <summary>Nothing to do: the cursor is still moving, the dwell
    /// has not elapsed yet, or this dwell session is already handled.</summary>
    None,
    /// <summary>The cursor dwelled outside any known block: probe the
    /// element under it with UIA now.</summary>
    Probe,
    /// <summary>The cursor dwelled inside a known block that has text,
    /// in a NEW dwell session (the mouse moved since the last show):
    /// show the cached text again (e.g. the icon auto-hid while the
    /// mouse stayed in the block).</summary>
    ShowCached,
}

/// <summary>Dwell + rectangle-cache state for hover-to-show-icon. The
/// caller polls Tick on every timer tick (100 ms) with the current
/// cursor position and clock; the state answers Probe / ShowCached /
/// None, and the caller reports probe outcomes back via NoteProbe.</summary>
public sealed class BlockTextHoverState
{
    // Default dwell: how long the cursor must stay still over text
    // before the service probes it — 150 ms, the debounce the research
    // recommends for Model A hover (research TTS 16/11:
    // speechify-report.md §11.1 Windows — "only hit-test when the
    // mouse has stopped ~100-150 ms" — and recommendation.md §2:
    // "debounce ~150 ms"). This const is the PURE state machine's
    // fallback (and the tests' anchor — one test asserts it is 150);
    // the APP default is Settings.HoverBlockDelayMs (400 ms, Manh
    // 2026-11-21, mac parity: hoverBlockDelayMs), passed in by the
    // service constructor. Deliberately NOT Settings.HoverDelayMs
    // (default 500 ms): that setting governs a different gesture —
    // how long to hover the ICON before the panel opens — and reusing
    // it would make block hover feel laggy (the icon would appear
    // half a second after the mouse stops, and users would think it
    // is broken).
    public const int DefaultDwellMilliseconds = 150;

    private readonly TimeSpan _dwell;
    private (int X, int Y) _lastCursor;
    private DateTimeOffset _stillSince;
    private bool _hasCursor;
    private Rectangle _cachedRect;
    private string? _cachedText;
    private bool _shownThisDwell;

    /// <param name="dwell">How long the cursor must stay still before
    /// a probe (or a cached re-show) is due. The app passes the
    /// research default (DefaultDwellMilliseconds, 150 ms) — see that
    /// const's comment for why not Settings.HoverDelayMs.</param>
    public BlockTextHoverState(TimeSpan dwell)
    {
        _dwell = dwell;
    }

    /// <summary>Text of the cached block (null when the cached
    /// rectangle had no text, or nothing is cached yet). Read it when
    /// Tick returns ShowCached.</summary>
    public string? CachedText => _cachedText;

    /// <summary>One poll tick: decide what the service should do for
    /// this cursor position at this time.</summary>
    public BlockTextHoverAction Tick((int X, int Y) cursor, DateTimeOffset now)
    {
        // Any move restarts the dwell and opens a new dwell session
        // (so a cached block may show again after the icon auto-hid).
        // The rectangle cache survives the move: roaming inside the
        // same block must not re-probe it.
        if (!_hasCursor || cursor != _lastCursor)
        {
            _hasCursor = true;
            _lastCursor = cursor;
            _stillSince = now;
            _shownThisDwell = false;
        }
        // Not still long enough yet: nothing to do.
        if (now - _stillSince < _dwell)
            return BlockTextHoverAction.None;
        // Inside the known block: show its text once per dwell session.
        if (_cachedText is not null && _cachedRect.Contains(cursor.X, cursor.Y))
        {
            if (_shownThisDwell)
                return BlockTextHoverAction.None;
            _shownThisDwell = true;
            return BlockTextHoverAction.ShowCached;
        }
        // Outside any known block (or the known block has no text):
        // probe — unless this dwell session already probed.
        if (_shownThisDwell)
            return BlockTextHoverAction.None;
        return BlockTextHoverAction.Probe;
    }

    /// <summary>Report a probe outcome back to the state. Marks the
    /// current dwell session handled (so the same still cursor does not
    /// probe again every tick). A rectangle is cached ONLY together
    /// with text: a text-less element's rectangle can be huge (a whole
    /// window) and would hide text children elsewhere inside it from
    /// later probes — so a no-text probe caches nothing, and the next
    /// dwell session probes again. A failed probe (rect null) likewise
    /// caches nothing. A probe with no text or no rect also CLEARS any
    /// older cached block: the block is gone (or unreadable), so a
    /// later dwell inside its old rect must probe fresh instead of
    /// re-showing stale text from another window (CI272).</summary>
    public void NoteProbe(Rectangle? rect, string? text)
    {
        _shownThisDwell = true;
        if (rect is { } r && !r.IsEmpty && text is not null)
        {
            _cachedRect = r;
            _cachedText = text;
        }
        else
        {
            _cachedRect = Rectangle.Empty;
            _cachedText = null;
        }
    }
}
