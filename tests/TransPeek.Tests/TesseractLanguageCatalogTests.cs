using System.Net;
using System.Text;
using TransPeek.Core.Ocr;
using Xunit;

namespace TransPeek.Tests;

public sealed class TesseractLanguageCatalogTests
{
    [Fact]
    public void Catalog_Has123Languages_NoDuplicates_NoSpecialModels()
    {
        var codes = TesseractLanguageCatalog.Languages.Select(l => l.Code).ToList();
        Assert.Equal(123, codes.Count);
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("equ", codes);
        Assert.DoesNotContain("osd", codes);
        Assert.DoesNotContain("Arabic", codes);
        Assert.DoesNotContain("Latin", codes);
    }

    [Fact]
    public void Catalog_ContainsCoreLanguages()
    {
        var codes = TesseractLanguageCatalog.Languages.Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("vie", codes);
        Assert.Contains("eng", codes);
        Assert.Contains("fra", codes);
        Assert.Contains("chi_sim", codes);
        Assert.Contains("jpn", codes);
    }

    [Fact]
    public void Catalog_SortedByCode()
    {
        var codes = TesseractLanguageCatalog.Languages.Select(l => l.Code).ToList();
        var sorted = codes.OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, codes);
    }

    [Fact]
    public void DisplayName_KnownCode_ReturnsName()
    {
        Assert.Equal("Vietnamese", TesseractLanguageCatalog.DisplayName("vie"));
        Assert.Equal("French", TesseractLanguageCatalog.DisplayName("fra"));
    }

    [Fact]
    public void DisplayName_UnknownCode_ReturnsCode()
    {
        Assert.Equal("xx_new", TesseractLanguageCatalog.DisplayName("xx_new"));
    }

    [Theory]
    [InlineData("nl-NL", "nld")]
    [InlineData("sv-SE", "swe")]
    [InlineData("pl-PL", "pol")]
    [InlineData("cs-CZ", "ces")]
    [InlineData("tr-TR", "tur")]
    [InlineData("uk-UA", "ukr")]
    [InlineData("hi-IN", "hin")]
    [InlineData("id-ID", "ind")]
    [InlineData("he-IL", "heb")]
    [InlineData("el-GR", "ell")]
    public void TessCodeForBcp47_ExtendedMap_MapsNewLanguages(string bcp47, string expected)
    {
        Assert.Equal(expected, TesseractLanguageDataManager.TessCodeForBcp47(bcp47));
    }

    [Theory]
    [InlineData("nld", "nl-NL")]
    [InlineData("swe", "sv-SE")]
    [InlineData("pol", "pl-PL")]
    [InlineData("tur", "tr-TR")]
    [InlineData("hin", "hi-IN")]
    public void Bcp47ForTessCode_ExtendedMap_MapsBack(string tessCode, string expected)
    {
        Assert.Equal(expected, TesseractLanguageDataManager.Bcp47ForTessCode(tessCode));
    }

    [Fact]
    public async Task FetchLiveCodesAsync_ParsesListing_ExcludesNonLanguages()
    {
        var json = """
            [
              {"name": "vie.traineddata", "type": "file"},
              {"name": "fra.traineddata", "type": "file"},
              {"name": "equ.traineddata", "type": "file"},
              {"name": "osd.traineddata", "type": "file"},
              {"name": "script", "type": "dir"},
              {"name": "README.md", "type": "file"}
            ]
            """;
        using var http = new HttpClient(new FakeHandler(json));
        var codes = await TesseractLanguageCatalog.FetchLiveCodesAsync(http);
        Assert.Equal(["fra", "vie"], codes);
    }

    [Fact]
    public async Task FetchLiveCodesAsync_HttpError_Throws()
    {
        using var http = new HttpClient(new FakeHandler("{}", HttpStatusCode.Forbidden));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            TesseractLanguageCatalog.FetchLiveCodesAsync(http));
    }

    private sealed class FakeHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
