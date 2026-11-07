using TransPeek.Core.Translation;
using Xunit;

namespace TransPeek.Tests;

public sealed class GoogleTranslateResponseTests
{
    [Fact]
    public void ParseTranslation_ConcatenatesSentences()
    {
        const string body = """
            {"sentences":[{"trans":"Xin "},{"trans":"chào"}]}
            """;
        Assert.Equal("Xin chào", GoogleTranslateProvider.ParseTranslation(body));
    }

    [Fact]
    public void ParseTranslation_EmptySentences_ThrowsEmptyResult()
    {
        const string body = """{"sentences":[]}""";
        var ex = Assert.Throws<TranslationException>(
            () => GoogleTranslateProvider.ParseTranslation(body));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseTranslation_InvalidJson_ThrowsInvalidResponse()
    {
        Assert.Throws<TranslationException>(
            () => GoogleTranslateProvider.ParseTranslation("this is not json"));
    }

    [Fact]
    public void ParseTranslation_NullSentences_ThrowsEmptyResult()
    {
        const string body = """{}""";
        Assert.Throws<TranslationException>(
            () => GoogleTranslateProvider.ParseTranslation(body));
    }
}
