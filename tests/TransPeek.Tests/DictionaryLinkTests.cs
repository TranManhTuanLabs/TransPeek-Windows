using TransPeek.Core.Dictionary;
using Xunit;

namespace TransPeek.Tests;

public sealed class DictionaryLinkTests
{
    [Fact]
    public void Defaults_HaveFourLinks()
    {
        Assert.Equal(4, DictionaryLink.Defaults.Count);
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "Oxford");
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "Cambridge");
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "Google Dictionary");
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "YouGlish");
    }

    [Fact]
    public void UrlFor_WordTemplate_UsesFirstWord()
    {
        var link = new DictionaryLink
        {
            Name = "Oxford",
            UrlTemplate = "https://www.oxfordlearnersdictionaries.com/definition/english/{word}",
        };
        var url = link.UrlFor("hello world");
        Assert.NotNull(url);
        Assert.Equal(
            "https://www.oxfordlearnersdictionaries.com/definition/english/hello",
            url!.AbsoluteUri);
    }

    [Fact]
    public void UrlFor_TextTemplate_UsesWholeText()
    {
        var link = new DictionaryLink
        {
            Name = "Google Dictionary",
            UrlTemplate = "https://www.google.com/search?q=define+{text}",
        };
        var url = link.UrlFor("hello world");
        Assert.NotNull(url);
        Assert.Equal("https://www.google.com/search?q=define+hello%20world", url!.AbsoluteUri);
    }

    [Fact]
    public void UrlFor_PercentEncodesSpecialChars()
    {
        var link = new DictionaryLink
        {
            Name = "Test",
            UrlTemplate = "https://example.com/{word}",
        };
        // "&" and "+" must not leak into the URL structure.
        var url = link.UrlFor("a&b+c");
        Assert.NotNull(url);
        Assert.DoesNotContain("&", url!.AbsoluteUri);
        Assert.Contains("a%26b%2Bc", url.AbsoluteUri);
    }

    [Fact]
    public void UrlFor_VietnameseWord_IsEncoded()
    {
        var link = new DictionaryLink
        {
            Name = "Oxford",
            UrlTemplate = "https://example.com/{word}",
        };
        var url = link.UrlFor("tiếng việt");
        Assert.NotNull(url);
        Assert.StartsWith("https://example.com/ti", url!.AbsoluteUri);
        Assert.DoesNotContain("ế", url.AbsoluteUri);
    }

    [Fact]
    public void UrlFor_EmptyText_ReturnsNull()
    {
        var link = new DictionaryLink { Name = "X", UrlTemplate = "https://example.com/{word}" };
        Assert.Null(link.UrlFor("   "));
    }

    [Fact]
    public void UrlFor_NoPlaceholder_ReturnsNull()
    {
        var link = new DictionaryLink { Name = "X", UrlTemplate = "https://example.com/search" };
        Assert.Null(link.UrlFor("hello"));
    }

    [Fact]
    public void UrlFor_NonHttpScheme_ReturnsNull()
    {
        var link = new DictionaryLink { Name = "X", UrlTemplate = "javascript:alert({word})" };
        Assert.Null(link.UrlFor("hello"));
    }

    [Fact]
    public void ValidationError_RequiresName()
    {
        Assert.NotNull(DictionaryLink.ValidationError("", "https://example.com/{word}"));
    }

    [Fact]
    public void ValidationError_RequiresPlaceholder()
    {
        Assert.NotNull(DictionaryLink.ValidationError("X", "https://example.com/search"));
    }

    [Fact]
    public void ValidationError_AcceptsGoodTemplate()
    {
        Assert.Null(DictionaryLink.ValidationError("X", "https://example.com/{word}"));
    }
}
