using TransPeek.Core.Ocr;
using Xunit;

namespace TransPeek.Tests;

/// <summary>First-run OCR setup: which Tesseract language files still need
/// downloading for the configured OCR languages. Pure logic — no disk,
/// no network — so it runs on Linux CI too.</summary>
public sealed class TesseractMissingCodesTests
{
    [Fact]
    public void MissingCodes_DefaultLanguagesNothingInstalled_ReturnsVieAndEng()
    {
        var missing = TesseractLanguageDataManager.MissingCodes(["vi-VN", "en-US"], _ => false);
        Assert.Equal(["vie", "eng"], missing);
    }

    [Fact]
    public void MissingCodes_VieInstalled_ReturnsOnlyEng()
    {
        var missing = TesseractLanguageDataManager.MissingCodes(
            ["vi-VN", "en-US"], code => code == "vie");
        Assert.Equal(["eng"], missing);
    }

    [Fact]
    public void MissingCodes_AllInstalled_ReturnsEmpty()
    {
        var missing = TesseractLanguageDataManager.MissingCodes(["vi-VN", "en-US"], _ => true);
        Assert.Empty(missing);
    }

    [Fact]
    public void MissingCodes_UnknownCodeDropped_DuplicatesRemoved()
    {
        var missing = TesseractLanguageDataManager.MissingCodes(
            ["vi-VN", "xx-YY", "vi-VN", "en-US"], _ => false);
        Assert.Equal(["vie", "eng"], missing);
    }

    [Fact]
    public void MissingCodes_NoUsableCode_FallsBackToEngLikeTheProvider()
    {
        var missing = TesseractLanguageDataManager.MissingCodes(["xx-YY"], _ => false);
        Assert.Equal(["eng"], missing);
        var installed = TesseractLanguageDataManager.MissingCodes(["xx-YY"], code => code == "eng");
        Assert.Empty(installed);
    }
}
