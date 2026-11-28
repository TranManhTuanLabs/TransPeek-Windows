// Tests for the continuous-listen chain (chaining, Manh 2026-11-27).
// Pure logic — no UI thread, no UIA needed. The probe offsets, the
// paragraph-change rule and the state transitions are all decided
// here; the UIA probing itself lives in the app controller and is
// verified by the Windows CI build + live test, not on Linux CI.

using System.Drawing;
using TransPeek.Core.ContinuousListen;
using Xunit;

namespace TransPeek.Tests;

public sealed class ContinuousListenChainTests
{
    private static readonly Rectangle Para = new(100, 200, 400, 60);

    private static ContinuousListenChain NewChain()
    {
        var chain = new ContinuousListenChain();
        chain.Start(new TextParagraph("First paragraph text.", Para), "en");
        return chain;
    }

    [Fact]
    public void ProbePoints_At100Percent_MatchMacOffsetsInPixels()
    {
        // Mac's [4, 24, 48, 80]pt at 96 DPI (100%) = [5, 32, 64, 107]px
        // below the paragraph's bottom edge, just inside its left edge
        // (log 274: the centre misses ragged text, the left edge is
        // where the lines below start).
        var points = ContinuousListenChain.NextProbePoints(Para, dpiScale: 1.0);
        Assert.Equal(4, points.Count);
        // Left X = 100 + 8 = 108; bottom = 200 + 60 = 260.
        Assert.Equal((108, 265), points[0]);
        Assert.Equal((108, 292), points[1]);
        Assert.Equal((108, 324), points[2]);
        Assert.Equal((108, 367), points[3]);
    }

    [Fact]
    public void ProbePoints_At150Percent_ScaleWithDpi()
    {
        // 1.5x DPI: [4, 24, 48, 80]pt = [8, 48, 96, 160]px. The left
        // inset stays 8 physical px (a few px inside the first
        // character is enough at any scale).
        var points = ContinuousListenChain.NextProbePoints(Para, dpiScale: 1.5);
        Assert.Equal((108, 268), points[0]);
        Assert.Equal((108, 308), points[1]);
        Assert.Equal((108, 356), points[2]);
        Assert.Equal((108, 420), points[3]);
    }

    [Fact]
    public void ProbePoints_NarrowRect_ClampedToCentre()
    {
        // A rect narrower than twice the inset (10px wide): the left
        // inset would probe past its middle, so the probe X clamps to
        // the centre instead of landing outside the text.
        var narrow = new Rectangle(100, 200, 10, 60);
        var points = ContinuousListenChain.NextProbePoints(narrow, dpiScale: 1.0);
        Assert.Equal((105, 265), points[0]);
        Assert.Equal((105, 292), points[1]);
        Assert.Equal((105, 324), points[2]);
        Assert.Equal((105, 367), points[3]);
    }

    [Fact]
    public void ProbePoints_EmptyRect_NoPoints()
    {
        Assert.Empty(ContinuousListenChain.NextProbePoints(Rectangle.Empty, dpiScale: 1.0));
    }

    [Fact]
    public void IsNewParagraph_SameText_NotNew()
    {
        Assert.False(ContinuousListenChain.IsNewParagraph("Hello world.", "Hello world."));
    }

    [Fact]
    public void IsNewParagraph_SameTextIgnoringSurroundingWhitespace_NotNew()
    {
        // UIA text often carries trailing newlines — trim before comparing.
        Assert.False(ContinuousListenChain.IsNewParagraph("Hello world.", "Hello world.\r\n"));
    }

    [Fact]
    public void IsNewParagraph_DifferentText_IsNew()
    {
        Assert.True(ContinuousListenChain.IsNewParagraph("First paragraph.", "Second paragraph."));
    }

    [Fact]
    public void IsNewParagraph_EmptyCandidate_NotNew()
    {
        Assert.False(ContinuousListenChain.IsNewParagraph("First paragraph.", ""));
        Assert.False(ContinuousListenChain.IsNewParagraph("First paragraph.", "   "));
        Assert.False(ContinuousListenChain.IsNewParagraph("First paragraph.", null));
    }

    [Fact]
    public void IsNewParagraph_NoCurrentParagraph_AnyTextIsNew()
    {
        Assert.True(ContinuousListenChain.IsNewParagraph(null, "Some text."));
        Assert.True(ContinuousListenChain.IsNewParagraph("", "Some text."));
    }

    [Fact]
    public void CanChain_SelectionInsideParagraph_Chains()
    {
        // A selection is a substring of its paragraph.
        Assert.True(ContinuousListenChain.CanChain(
            "The quick brown fox jumps over the lazy dog.", new Rectangle(0, 0, 100, 20), "brown fox"));
    }

    [Fact]
    public void CanChain_HoveredTextEqualsParagraph_Chains()
    {
        // A hovered block's text IS its paragraph text.
        Assert.True(ContinuousListenChain.CanChain(
            "A whole paragraph.", new Rectangle(0, 0, 100, 20), "A whole paragraph."));
    }

    [Fact]
    public void CanChain_StaleParagraph_NoChain()
    {
        // The spoken text is not in the captured paragraph: the context
        // belongs to a different selection/hover — never chain on it.
        Assert.False(ContinuousListenChain.CanChain(
            "Text from another app.", new Rectangle(0, 0, 100, 20), "Reader text here."));
    }

    [Fact]
    public void CanChain_MissingParagraphOrRectOrText_NoChain()
    {
        Assert.False(ContinuousListenChain.CanChain(null, new Rectangle(0, 0, 100, 20), "word"));
        Assert.False(ContinuousListenChain.CanChain("", new Rectangle(0, 0, 100, 20), "word"));
        Assert.False(ContinuousListenChain.CanChain("Paragraph.", Rectangle.Empty, "word"));
        Assert.False(ContinuousListenChain.CanChain("Paragraph.", new Rectangle(0, 0, 100, 20), ""));
    }

    [Fact]
    public void Start_ActivatesChainWithParagraphAndLanguage()
    {
        var chain = new ContinuousListenChain();
        Assert.False(chain.IsActive);
        chain.Start(new TextParagraph("Para.", Para), "vi");
        Assert.True(chain.IsActive);
        Assert.Equal("Para.", chain.CurrentParagraph?.Text);
        Assert.Equal(Para, chain.CurrentParagraph?.Rect);
        Assert.Equal("vi", chain.Language);
    }

    [Fact]
    public void AcceptCandidate_NewParagraph_ReturnsTextAndMovesCurrent()
    {
        var chain = NewChain();
        var next = new TextParagraph("Second paragraph.", new Rectangle(100, 280, 400, 60));
        Assert.Equal("Second paragraph.", chain.AcceptCandidate(next));
        Assert.Equal("Second paragraph.", chain.CurrentParagraph?.Text);
        Assert.True(chain.IsActive);
    }

    [Fact]
    public void AcceptCandidate_SameParagraph_ReturnsNullAndKeepsCurrent()
    {
        var chain = NewChain();
        Assert.Null(chain.AcceptCandidate(new TextParagraph("First paragraph text.", new Rectangle(100, 280, 400, 60))));
        Assert.Equal("First paragraph text.", chain.CurrentParagraph?.Text);
        Assert.True(chain.IsActive);
    }

    [Fact]
    public void AcceptCandidate_NullCandidate_ReturnsNull()
    {
        var chain = NewChain();
        Assert.Null(chain.AcceptCandidate(null));
        Assert.True(chain.IsActive);
    }

    [Fact]
    public void AcceptCandidate_WhenIdle_ReturnsNull()
    {
        var chain = new ContinuousListenChain();
        Assert.Null(chain.AcceptCandidate(new TextParagraph("Any.", Para)));
    }

    [Fact]
    public void Cancel_DeactivatesAndClears()
    {
        var chain = NewChain();
        chain.Cancel();
        Assert.False(chain.IsActive);
        Assert.Null(chain.CurrentParagraph);
        Assert.Equal("", chain.Language);
        // Cancelling twice is safe (every stop path calls it).
        chain.Cancel();
        Assert.False(chain.IsActive);
    }
}
