using TransPeek.Core.Barcode;
using Xunit;

namespace TransPeek.Tests;

/// <summary>Barcode/QR payload routing: URLs open, everything else copies,
/// empty finds nothing; long payloads truncate in the toast preview.
/// Pure logic — runs on Linux CI too.</summary>
public sealed class BarcodePayloadTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?q=1")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void Classify_HttpUrl_ReturnsOpenUrl(string payload)
    {
        Assert.Equal(BarcodePayloadAction.OpenUrl, BarcodePayload.Classify(payload));
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("tel:+84123456789")]
    [InlineData("WIFI:S:Home;T:WPA;P:secret;;")]
    [InlineData("BEGIN:VCARD\nFN:Someone\nEND:VCARD")]
    [InlineData("plain text, not a URL")]
    public void Classify_NonHttpPayload_ReturnsCopy(string payload)
    {
        Assert.Equal(BarcodePayloadAction.Copy, BarcodePayload.Classify(payload));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_Empty_ReturnsNotFound(string? payload)
    {
        Assert.Equal(BarcodePayloadAction.NotFound, BarcodePayload.Classify(payload));
    }

    [Fact]
    public void Preview_ShortPayload_ReturnsItWhole()
    {
        Assert.Equal("hello", BarcodePayload.Preview("hello"));
    }

    [Fact]
    public void Preview_LongPayload_TruncatesTo80PlusEllipsis()
    {
        var payload = new string('x', 100);
        var preview = BarcodePayload.Preview(payload);
        Assert.Equal(81, preview.Length);
        Assert.EndsWith("…", preview);
        Assert.Equal(new string('x', 80), preview[..80]);
    }
}
