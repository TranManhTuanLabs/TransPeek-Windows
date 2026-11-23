using TransPeek.Core.FastRead;
using Xunit;

namespace TransPeek.Tests;

/// <summary>Style 2 ("basic reading") lines: paragraphs split into
/// short lines of at most 12 words, so the focus can advance line by
/// line. Pure logic — runs on Linux CI too.</summary>
public sealed class RsvpStyle2LinesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void BuildLines_EmptyText_ReturnsNoLines(string? text)
    {
        Assert.Empty(RsvpEngine.BuildLines(text));
    }

    [Fact]
    public void BuildLines_ShortParagraph_IsOneLine()
    {
        var lines = RsvpEngine.BuildLines("one two three four five");
        Assert.Single(lines);
        Assert.Equal("one two three four five", lines[0]);
    }

    [Fact]
    public void BuildLines_LongParagraph_SplitsAtTwelveWords()
    {
        // 25 words: 12 + 12 + 1, so the focus moves at a readable pace.
        var text = string.Join(" ", Enumerable.Range(1, 25).Select(i => $"w{i}"));
        var lines = RsvpEngine.BuildLines(text);
        Assert.Equal(3, lines.Count);
        Assert.Equal(12, lines[0].Split(' ').Length);
        Assert.Equal(12, lines[1].Split(' ').Length);
        Assert.Equal("w25", lines[2]);
    }

    [Fact]
    public void BuildLines_BlankLine_StartsNewParagraph()
    {
        // Two paragraphs of 13 words each: each splits 12 + 1, and the
        // second paragraph never merges into the first one's last line.
        var para = string.Join(" ", Enumerable.Range(1, 13).Select(i => $"a{i}"));
        var para2 = string.Join(" ", Enumerable.Range(1, 13).Select(i => $"b{i}"));
        var lines = RsvpEngine.BuildLines(para + "\n\n" + para2);
        Assert.Equal(4, lines.Count);
        Assert.Equal("a13", lines[1]);
        Assert.StartsWith("b1", lines[2]);
    }

    [Fact]
    public void BuildLines_CustomMaxWordsPerLine_Respected()
    {
        var lines = RsvpEngine.BuildLines("one two three four five", maxWordsPerLine: 2);
        Assert.Equal(3, lines.Count);
        Assert.Equal("one two", lines[0]);
        Assert.Equal("five", lines[2]);
    }
}
