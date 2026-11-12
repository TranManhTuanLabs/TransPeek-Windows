using TransPeek.Core.Dictionary;
using Xunit;

namespace TransPeek.Tests;

public sealed class DictionaryLinkTests
{
    [Fact]
    public void Defaults_HaveFiveLinks()
    {
        Assert.Equal(5, DictionaryLink.Defaults.Count);
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "Oxford");
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "Cambridge");
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "Google");
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "YouGlish");
        Assert.Contains(DictionaryLink.Defaults, l => l.Name == "Cambridge Vi");
        // Cambridge Vi is last, like on mac.
        Assert.Equal("Cambridge Vi", DictionaryLink.Defaults[^1].Name);
    }

    [Fact]
    public void Defaults_MatchMac()
    {
        // Same defaults as the mac app (DictionaryLink.swift).
        var oxford = DictionaryLink.Defaults.First(l => l.Name == "Oxford");
        Assert.Equal(
            "https://www.oxfordlearnersdictionaries.com/definition/english/{word}?q={word}",
            oxford.UrlTemplate);
        var youglish = DictionaryLink.Defaults.First(l => l.Name == "YouGlish");
        Assert.Equal("https://youglish.com/pronounce/{text}/english", youglish.UrlTemplate);
        var cambridgeVi = DictionaryLink.Defaults.First(l => l.Name == "Cambridge Vi");
        Assert.Equal(
            "https://dictionary.cambridge.org/dictionary/english-vietnamese/{word}",
            cambridgeVi.UrlTemplate);
    }

    [Fact]
    public void UrlFor_WordTemplate_LowercasesFirstWord()
    {
        // Oxford/Cambridge paths are case-sensitive: "Word" at a sentence
        // start must still open .../word.
        var oxford = DictionaryLink.Defaults.First(l => l.Name == "Oxford");
        var url = oxford.UrlFor("Hello world");
        Assert.NotNull(url);
        Assert.Equal(
            "https://www.oxfordlearnersdictionaries.com/definition/english/hello?q=hello",
            url!.AbsoluteUri);
    }

    [Fact]
    public void UrlFor_YouGlish_UsesWholeText()
    {
        var youglish = DictionaryLink.Defaults.First(l => l.Name == "YouGlish");
        var url = youglish.UrlFor("hello world");
        Assert.NotNull(url);
        Assert.Equal("https://youglish.com/pronounce/hello%20world/english", url!.AbsoluteUri);
    }

    [Fact]
    public void MigrateDefaults_UpgradesOldV01Links()
    {
        var links = new List<DictionaryLink>
        {
            new() { Name = "Oxford", UrlTemplate = "https://www.oxfordlearnersdictionaries.com/definition/english/{word}" },
            new() { Name = "YouGlish", UrlTemplate = "https://youglish.com/pronounce/{word}/english" },
            new() { Name = "Google Dictionary", UrlTemplate = "https://www.google.com/search?q=define+{text}" },
            new() { Name = "Custom", UrlTemplate = "https://example.com/{word}" },
        };
        Assert.True(DictionaryLink.MigrateDefaults(links));
        Assert.Equal(
            "https://www.oxfordlearnersdictionaries.com/definition/english/{word}?q={word}",
            links[0].UrlTemplate);
        Assert.Equal("https://youglish.com/pronounce/{text}/english", links[1].UrlTemplate);
        Assert.Equal("Google", links[2].Name);
        Assert.Equal("https://example.com/{word}", links[3].UrlTemplate);
        // Second run is a no-op.
        Assert.False(DictionaryLink.MigrateDefaults(links));
    }

    [Fact]
    public void MigrateDefaults_AppendsCambridgeVi_ForUneditedLists()
    {
        var links = new List<DictionaryLink>
        {
            new() { Name = "Oxford", UrlTemplate = "https://www.oxfordlearnersdictionaries.com/definition/english/{word}?q={word}" },
            new() { Name = "Cambridge", UrlTemplate = "https://dictionary.cambridge.org/dictionary/english/{word}" },
            new() { Name = "Google", UrlTemplate = "https://www.google.com/search?q=define+{text}" },
            new() { Name = "YouGlish", UrlTemplate = "https://youglish.com/pronounce/{text}/english" },
        };
        Assert.True(DictionaryLink.MigrateDefaults(links));
        Assert.Equal(5, links.Count);
        Assert.Equal("Cambridge Vi", links[^1].Name);
        Assert.Equal(
            "https://dictionary.cambridge.org/dictionary/english-vietnamese/{word}",
            links[^1].UrlTemplate);
        // Second run is a no-op (no duplicate).
        Assert.False(DictionaryLink.MigrateDefaults(links));
        Assert.Equal(5, links.Count);
    }

    [Fact]
    public void FreshDefaults_ReturnsIndependentCopies()
    {
        var a = DictionaryLink.FreshDefaults();
        var b = DictionaryLink.FreshDefaults();
        a[0].Name = "Changed";
        Assert.NotEqual("Changed", b[0].Name);
        Assert.NotEqual("Changed", DictionaryLink.Defaults[0].Name);
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
