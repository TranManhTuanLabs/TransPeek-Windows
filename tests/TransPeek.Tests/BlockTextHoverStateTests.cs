// Tests for the hover-to-show-icon dwell + rectangle cache
// (feature/block-text-detection). Pure logic — no UI thread needed.
// The clock is passed in explicitly so dwell timing is deterministic.

using System.Drawing;
using TransPeek.Core.BlockText;
using Xunit;

namespace TransPeek.Tests;

public sealed class BlockTextHoverStateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(500);

    private static BlockTextHoverState NewState() => new(Dwell);

    [Fact]
    public void CursorKeepsMoving_NeverProbes()
    {
        var state = NewState();
        // A move every tick restarts the dwell, so 500 ms never elapses.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0));
        Assert.Equal(BlockTextHoverAction.None, state.Tick((11, 10), T0 + TimeSpan.FromMilliseconds(300)));
        Assert.Equal(BlockTextHoverAction.None, state.Tick((12, 10), T0 + TimeSpan.FromMilliseconds(600)));
        Assert.Equal(BlockTextHoverAction.None, state.Tick((13, 10), T0 + TimeSpan.FromMilliseconds(900)));
    }

    [Fact]
    public void StillCursor_PastDwell_ProbesOncePerSession()
    {
        var state = NewState();
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0));
        // 300 ms still: dwell (500 ms) not reached yet.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(300)));
        // 600 ms still: probe is due.
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(600)));
        // The service probed and found text: report it, then the same
        // still cursor must not probe again this session.
        state.NoteProbe(new Rectangle(0, 0, 100, 50), "hello");
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(900)));
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(1200)));
    }

    [Fact]
    public void DwellExactlyAtThreshold_Probes()
    {
        var state = NewState();
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0));
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + Dwell));
    }

    [Fact]
    public void ProbeWithText_CachesRect_RoamingInsideShowsCachedOnce()
    {
        var state = NewState();
        state.Tick((10, 10), T0);
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(600)));
        state.NoteProbe(new Rectangle(0, 0, 100, 50), "hello");
        // A move inside the same block starts a new dwell session; the
        // block is already known, so no probe — a cached re-show instead.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((20, 20), T0 + TimeSpan.FromMilliseconds(900)));
        Assert.Equal(BlockTextHoverAction.ShowCached, state.Tick((20, 20), T0 + TimeSpan.FromMilliseconds(1500)));
        Assert.Equal("hello", state.CachedText);
        // One show per dwell session: still again is a no-op.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((20, 20), T0 + TimeSpan.FromMilliseconds(1800)));
    }

    [Fact]
    public void ProbeWithNoText_DoesNotCacheRect()
    {
        var state = NewState();
        state.Tick((10, 10), T0);
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(600)));
        // A text-less element's rectangle must NOT be cached: it could
        // be a whole window, hiding text children elsewhere inside it.
        state.NoteProbe(new Rectangle(0, 0, 1000, 800), null);
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(900)));
        // A new dwell session inside that same rectangle probes again.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((50, 50), T0 + TimeSpan.FromMilliseconds(1200)));
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((50, 50), T0 + TimeSpan.FromMilliseconds(1800)));
    }

    [Fact]
    public void CursorLeavesRect_ProbesAgain()
    {
        var state = NewState();
        state.Tick((10, 10), T0);
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(600)));
        state.NoteProbe(new Rectangle(0, 0, 100, 50), "hello");
        // Outside the cached block: a new dwell probes the new spot.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((500, 500), T0 + TimeSpan.FromMilliseconds(900)));
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((500, 500), T0 + TimeSpan.FromMilliseconds(1500)));
    }

    [Fact]
    public void FailedProbe_MarksHandledUntilMove()
    {
        var state = NewState();
        state.Tick((10, 10), T0);
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(600)));
        // FromPoint failed (no rectangle to cache): the same still
        // cursor must not hammer UIA every tick.
        state.NoteProbe(null, null);
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(900)));
        // A move starts a new dwell session, which probes again.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((11, 10), T0 + TimeSpan.FromMilliseconds(1200)));
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((11, 10), T0 + TimeSpan.FromMilliseconds(1800)));
    }

    [Fact]
    public void FailedProbe_ClearsCachedBlock()
    {
        var state = NewState();
        state.Tick((10, 10), T0);
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(600)));
        state.NoteProbe(new Rectangle(0, 0, 100, 50), "hello");
        // A failed probe (no text / no rect) clears the older cached
        // block: a later dwell inside its old rect must probe fresh
        // instead of re-showing stale text (CI272: hover in Word read
        // Desktop/Chrome text from a cached block).
        state.NoteProbe(null, null);
        Assert.Null(state.CachedText);
        Assert.Equal(BlockTextHoverAction.None, state.Tick((20, 20), T0 + TimeSpan.FromMilliseconds(900)));
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((20, 20), T0 + TimeSpan.FromMilliseconds(1500)));
    }

    // The research default dwell is 150 ms (speechify-report.md §11.1
    // + recommendation.md §2), NOT the 500 ms HoverDelayMs setting:
    // a still cursor must probe after ~150 ms, and must not probe
    // after only 100 ms. The state machine itself is
    // dwell-parameterized (the other tests use 500 ms); this test
    // locks the value the service actually ships with.
    [Fact]
    public void DefaultDwell_Is150Milliseconds()
    {
        Assert.Equal(150, BlockTextHoverState.DefaultDwellMilliseconds);

        var state = new BlockTextHoverState(TimeSpan.FromMilliseconds(BlockTextHoverState.DefaultDwellMilliseconds));
        var start = DateTimeOffset.UtcNow;
        // First sighting at +100 ms starts the dwell clock (elapsed 0).
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), start.AddMilliseconds(100)));
        // +200 ms: only 100 ms still — under the 150 ms dwell.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((10, 10), start.AddMilliseconds(200)));
        // +250 ms: 150 ms still — exactly at the dwell, probes.
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), start.AddMilliseconds(250)));
    }

    [Fact]
    public void RectEdge_RightAndBottomAreOutside()
    {
        var state = NewState();
        state.Tick((10, 10), T0);
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((10, 10), T0 + TimeSpan.FromMilliseconds(600)));
        state.NoteProbe(new Rectangle(0, 0, 100, 50), "hello");
        // (100, 10) is on the rect's right edge: outside, so a new
        // dwell there probes instead of showing the cached text.
        Assert.Equal(BlockTextHoverAction.None, state.Tick((100, 10), T0 + TimeSpan.FromMilliseconds(900)));
        Assert.Equal(BlockTextHoverAction.Probe, state.Tick((100, 10), T0 + TimeSpan.FromMilliseconds(1500)));
    }
}
