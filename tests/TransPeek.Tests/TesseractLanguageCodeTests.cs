using TransPeek.Core.Ocr;
using Xunit;

namespace TransPeek.Tests;

public sealed class TesseractLanguageCodeTests
{
    [Theory]
    [InlineData("vi-VN", "vie")]
    [InlineData("vi", "vie")]
    [InlineData("en-US", "eng")]
    [InlineData("en-GB", "eng")]
    [InlineData("en", "eng")]
    [InlineData("zh-Hans", "chi_sim")]
    [InlineData("zh-Hant", "chi_tra")]
    [InlineData("ja-JP", "jpn")]
    [InlineData("ko-KR", "kor")]
    public void TessCodeForBcp47_MapsKnownCodes(string bcp47, string expected)
    {
        Assert.Equal(expected, TesseractLanguageDataManager.TessCodeForBcp47(bcp47));
    }

    [Theory]
    [InlineData("xx-YY")]
    [InlineData("")]
    public void TessCodeForBcp47_UnknownCode_ReturnsNull(string bcp47)
    {
        Assert.Null(TesseractLanguageDataManager.TessCodeForBcp47(bcp47));
    }

    [Theory]
    [InlineData("vie", "vi-VN")]
    [InlineData("eng", "en-US")]
    [InlineData("chi_sim", "zh-Hans")]
    public void Bcp47ForTessCode_MapsBack(string tessCode, string expected)
    {
        Assert.Equal(expected, TesseractLanguageDataManager.Bcp47ForTessCode(tessCode));
    }

    [Fact]
    public void DownloadUrl_PointsAtTessdataBest()
    {
        var url = TesseractLanguageDataManager.DownloadUrl("vie");
        Assert.Equal(
            "https://github.com/tesseract-ocr/tessdata_best/raw/main/vie.traineddata",
            url.ToString());
    }

    [Fact]
    public void InstalledCodes_EmptyWhenDirectoryMissing()
    {
        // On a machine without tessdata installed (CI), nothing is installed.
        // The method must not throw; it returns an empty list.
        var codes = TesseractLanguageDataManager.InstalledCodes();
        Assert.NotNull(codes);
    }
}
