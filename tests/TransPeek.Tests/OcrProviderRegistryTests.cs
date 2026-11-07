using TransPeek.Core.Ocr;
using Xunit;

namespace TransPeek.Tests;

public sealed class OcrProviderRegistryTests
{
    [Fact]
    public void Resolve_TesseractSelectedAndAvailable_ReturnsTesseract()
    {
        Assert.Equal(OcrProviderId.Tesseract, OcrProviderRegistry.Resolve("tesseract", tesseractAvailable: true));
    }

    [Fact]
    public void Resolve_TesseractSelectedButUnavailable_FallsBackToWindowsOcr()
    {
        Assert.Equal(OcrProviderId.WindowsOcr, OcrProviderRegistry.Resolve("tesseract", tesseractAvailable: false));
    }

    [Fact]
    public void Resolve_WindowsOcrSelected_ReturnsWindowsOcr()
    {
        Assert.Equal(OcrProviderId.WindowsOcr, OcrProviderRegistry.Resolve("windows-ocr", tesseractAvailable: true));
    }

    [Fact]
    public void Resolve_UnknownId_ReturnsWindowsOcr()
    {
        Assert.Equal(OcrProviderId.WindowsOcr, OcrProviderRegistry.Resolve("something-else", tesseractAvailable: true));
        Assert.Equal(OcrProviderId.WindowsOcr, OcrProviderRegistry.Resolve(null, tesseractAvailable: true));
    }

    [Fact]
    public void IdStrings_RoundTrip()
    {
        Assert.Equal("tesseract", OcrProviderId.Tesseract.ToIdString());
        Assert.Equal("windows-ocr", OcrProviderId.WindowsOcr.ToIdString());
        Assert.Equal(OcrProviderId.Tesseract, OcrProviderIdExtensions.FromIdString("tesseract"));
        Assert.Equal(OcrProviderId.WindowsOcr, OcrProviderIdExtensions.FromIdString("windows-ocr"));
    }
}
