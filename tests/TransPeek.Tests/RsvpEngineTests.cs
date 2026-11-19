using TransPeek.Core.FastRead;
using Xunit;

namespace TransPeek.Tests;

/// <summary>RSVP engine (Fast Read): tokenize, chunk, ORP pivot,
/// micro-pause timing, paragraph ends, pause context. Pure logic —
/// runs on Linux CI too.</summary>
public sealed class RsvpEngineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Build_EmptyText_ReturnsNoSlides(string? text)
    {
        var doc = RsvpEngine.Build(text, 275, 1);
        Assert.Equal(0, doc.TotalWords);
        Assert.Equal(0, doc.TotalSlides);
        Assert.Equal(TimeSpan.Zero, doc.TotalDuration);
    }

    [Fact]
    public void Build_ChunksWords()
    {
        var doc = RsvpEngine.Build("one two three four five", 275, 2);
        Assert.Equal(5, doc.TotalWords);
        Assert.Equal(3, doc.TotalSlides);
        Assert.Equal("one two", doc.Slides[0].Text);
        Assert.Equal("three four", doc.Slides[1].Text);
        Assert.Equal("five", doc.Slides[2].Text);
        Assert.Equal(0, doc.Slides[0].StartWordIndex);
        Assert.Equal(2, doc.Slides[1].StartWordIndex);
        Assert.Equal(4, doc.Slides[2].StartWordIndex);
    }

    [Fact]
    public void Build_SentenceEndBreaksChunkEarly()
    {
        var doc = RsvpEngine.Build("Hello world. Next sentence here", 275, 3);
        Assert.Equal(2, doc.TotalSlides);
        Assert.Equal("Hello world.", doc.Slides[0].Text);
        Assert.Equal("Next sentence here", doc.Slides[1].Text);
    }

    [Theory]
    [InlineData("hello", 2)]
    [InlineData("hi", 1)]
    [InlineData("a", 0)]
    [InlineData("", -1)]
    public void PivotCharIndex_IsMiddleCharacter(string word, int expected)
    {
        Assert.Equal(expected, RsvpEngine.PivotCharIndex(word));
    }

    [Fact]
    public void Build_ChunkLargerThanOne_PivotOnMiddleWord()
    {
        // SwiftRead (like mac): the middle word of the chunk carries
        // the pivot — "one two" pivots on the 'w' of "two" (index 5),
        // the leftover "three" pivots on its own middle char (index 2).
        var doc = RsvpEngine.Build("one two three", 275, 2);
        Assert.Equal(5, doc.Slides[0].PivotCharIndex);
        Assert.Equal(2, doc.Slides[1].PivotCharIndex);
        var three = RsvpEngine.Build("one two three", 275, 3);
        Assert.Equal(5, three.Slides[0].PivotCharIndex);
        var single = RsvpEngine.Build("hello", 275, 1);
        Assert.Equal(2, single.Slides[0].PivotCharIndex);
    }

    [Fact]
    public void ComputeSlideDelay_PlainWord_IsBaseMinusOverhead()
    {
        // WPM 275: base 60000/275 = 218.18 ms, minus 30 ms overhead.
        var delay = RsvpEngine.ComputeSlideDelay(["word"], paragraphEnd: false, wpm: 275, chunkSize: 1);
        Assert.Equal(188.2, delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void ComputeSlideDelay_CommaAddsPause()
    {
        // +0.9: 218.18 x 1.9 - 30 = 384.5 ms.
        var delay = RsvpEngine.ComputeSlideDelay(["word,"], paragraphEnd: false, wpm: 275, chunkSize: 1);
        Assert.Equal(384.5, delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void ComputeSlideDelay_SentenceEndAddsPause()
    {
        // +1.1: 218.18 x 2.1 - 30 = 428.2 ms.
        var delay = RsvpEngine.ComputeSlideDelay(["word."], paragraphEnd: false, wpm: 275, chunkSize: 1);
        Assert.Equal(428.2, delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void ComputeSlideDelay_NumberAddsPause()
    {
        // +0.5: 218.18 x 1.5 - 30 = 297.3 ms.
        var delay = RsvpEngine.ComputeSlideDelay(["2026"], paragraphEnd: false, wpm: 275, chunkSize: 1);
        Assert.Equal(297.3, delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void ComputeSlideDelay_LongWordAddsPause()
    {
        // 28 chars, +0.5: 218.18 x 1.5 - 30 = 297.3 ms.
        var delay = RsvpEngine.ComputeSlideDelay(["antidisestablishmentarianism"], paragraphEnd: false, wpm: 275, chunkSize: 1);
        Assert.Equal(297.3, delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void ComputeSlideDelay_MicroPauseOffWhenSlow()
    {
        // chunk 1 x WPM 100 <= 100: no micro-pauses, "word." is plain.
        // base 60000/100 = 600 ms, minus 30 = 570 ms.
        var delay = RsvpEngine.ComputeSlideDelay(["word."], paragraphEnd: false, wpm: 100, chunkSize: 1);
        Assert.Equal(570.0, delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void Build_ParagraphEnd_AddsParagraphPause()
    {
        // "para." at a paragraph end: sentence 1.1 + paragraph 1.1.
        // 218.18 x 3.2 - 30 = 668.2 ms.
        var doc = RsvpEngine.Build("First para.\n\nSecond para.", 275, 1);
        Assert.Equal(4, doc.TotalSlides);
        Assert.False(doc.Slides[0].IsParagraphEnd);
        Assert.True(doc.Slides[1].IsParagraphEnd);
        Assert.Equal(668.2, doc.Slides[1].Delay.TotalMilliseconds, precision: 1);
        Assert.Equal(188.2, doc.Slides[0].Delay.TotalMilliseconds, precision: 1);
    }

    [Fact]
    public void Build_ClampsWpmAndChunkSize()
    {
        var tooFast = RsvpEngine.Build("a b c", wpm: 9999, chunkSize: 0);
        Assert.Equal(3, tooFast.TotalSlides); // chunk clamped to 1
        var tooSlow = RsvpEngine.Build("a b c", wpm: 1, chunkSize: 99);
        Assert.Single(tooSlow.Slides); // chunk clamped to 6
    }

    [Fact]
    public void TotalDuration_SumsSlideDelays()
    {
        // "one" is plain (218.18 - 30 = 188.2); "two" is the paragraph's
        // last word, so it carries the +1.1 paragraph-end pause
        // (218.18 x 2.1 - 30 = 428.2). Total 188.2 + 428.2 = 616.4.
        var doc = RsvpEngine.Build("one two", 275, 1);
        Assert.Equal(616.4, doc.TotalDuration.TotalMilliseconds, precision: 1);
        Assert.Equal(428.2, doc.RemainingDuration(1).TotalMilliseconds, precision: 1);
        Assert.Equal(TimeSpan.Zero, doc.RemainingDuration(99));
    }

    [Fact]
    public void Context_ReturnsWindowAroundCurrentSlide()
    {
        var doc = RsvpEngine.Build("w1 w2 w3 w4 w5 w6 w7 w8 w9 w10", 275, 1);
        var context = doc.Context(slideIndex: 5, radius: 2);
        Assert.Equal(["w4", "w5", "w6", "w7", "w8"], context.Words);
        Assert.Equal(2, context.CurrentStart);
        Assert.Equal(1, context.CurrentCount);
    }

    [Fact]
    public void SlideIndexForWord_FindsContainingSlide()
    {
        var doc = RsvpEngine.Build("one two three four five", 275, 2);
        Assert.Equal(0, RsvpEngine.SlideIndexForWord(doc, 0));
        Assert.Equal(1, RsvpEngine.SlideIndexForWord(doc, 3));
        Assert.Equal(2, RsvpEngine.SlideIndexForWord(doc, 4));
        Assert.Equal(2, RsvpEngine.SlideIndexForWord(doc, 99));
    }
}
