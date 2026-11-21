using TransPeek.Core.FastRead;
using Xunit;

namespace TransPeek.Tests;

/// <summary>Continuous-reading phrases (phương án 1, Manh 2026-11-21):
/// the text split into sentence/clause utterances, so the voice never
/// stutters between words. Pure logic — runs on Linux CI too.</summary>
public sealed class RsvpPhrasesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void BuildPhrases_EmptyText_ReturnsNoPhrases(string? text)
    {
        Assert.Empty(RsvpEngine.BuildPhrases(text));
    }

    [Fact]
    public void BuildPhrases_OneSentence_IsOnePhrase()
    {
        var phrases = RsvpEngine.BuildPhrases("Hello world, this is a test.");
        Assert.Single(phrases);
        Assert.Equal("Hello world, this is a test.", phrases[0].Text);
        Assert.Equal(0, phrases[0].StartWordIndex);
        Assert.Equal(6, phrases[0].WordCount);
    }

    [Fact]
    public void BuildPhrases_TwoSentences_SplitsAtSentenceEnd()
    {
        // A phrase ends at a natural pause — the gap between utterances
        // lands where a speaker would pause anyway.
        var phrases = RsvpEngine.BuildPhrases("First sentence here. Second sentence here!");
        Assert.Equal(2, phrases.Count);
        Assert.Equal("First sentence here.", phrases[0].Text);
        Assert.Equal(0, phrases[0].StartWordIndex);
        Assert.Equal("Second sentence here!", phrases[1].Text);
        Assert.Equal(3, phrases[1].StartWordIndex);
    }

    [Fact]
    public void BuildPhrases_Comma_SplitsAtClause()
    {
        var phrases = RsvpEngine.BuildPhrases("Apples, oranges, and bananas.");
        Assert.Equal(3, phrases.Count);
        Assert.Equal("Apples,", phrases[0].Text);
        Assert.Equal("oranges,", phrases[1].Text);
        Assert.Equal("and bananas.", phrases[2].Text);
        Assert.Equal(2, phrases[2].StartWordIndex);
    }

    [Fact]
    public void BuildPhrases_ParagraphBreak_SplitsPhrases()
    {
        var phrases = RsvpEngine.BuildPhrases("First paragraph words\n\nSecond paragraph words");
        Assert.Equal(2, phrases.Count);
        Assert.Equal("First paragraph words", phrases[0].Text);
        Assert.Equal("Second paragraph words", phrases[1].Text);
        Assert.Equal(3, phrases[1].StartWordIndex);
    }

    [Fact]
    public void BuildPhrases_LongSentenceWithoutPunctuation_SplitsAtCap()
    {
        // 25 words, no punctuation: 20 + 5, so the focus keeps moving
        // even through a comma-less monster sentence.
        var text = string.Join(" ", Enumerable.Range(1, 25).Select(i => $"w{i}"));
        var phrases = RsvpEngine.BuildPhrases(text);
        Assert.Equal(2, phrases.Count);
        Assert.Equal(20, phrases[0].WordCount);
        Assert.Equal(0, phrases[0].StartWordIndex);
        Assert.Equal(5, phrases[1].WordCount);
        Assert.Equal(20, phrases[1].StartWordIndex);
    }
}
