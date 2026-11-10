// Scan Barcodes/QR Codes (mac parity: TranslationCoordinator.scanBarcodes
// + BarcodeScanner with Vision). Region select → capture → ZXing.Net
// decode. URL payloads open in the browser, anything else is copied;
// the caller (App) owns the browser/copy/toast side effects, this
// service only reports the outcome. Must run on the UI thread: region
// select and capture both touch UI objects.

using System.Drawing;
using TransPeek.Core.Barcode;
using Windows.Graphics;
using ZXing;

namespace TransPeek.Services;

public enum BarcodeScanOutcome
{
    Opened,
    Copied,
    NotFound,
    Cancelled,
    Failed,
}

public sealed class BarcodeScanService
{
    private readonly Func<Task<RectInt32?>> _selectRegionAsync;
    private readonly ScreenCaptureService _capture = new();

    /// <summary>Payload of the first code found (Opened / Copied only).</summary>
    public string? Payload { get; private set; }

    /// <summary>Failure detail (Failed only).</summary>
    public string? ErrorMessage { get; private set; }

    public BarcodeScanService(Func<Task<RectInt32?>> selectRegionAsync)
    {
        _selectRegionAsync = selectRegionAsync;
    }

    public async Task<BarcodeScanOutcome> ScanAsync()
    {
        Payload = null;
        ErrorMessage = null;

        var region = await _selectRegionAsync();
        if (region is null)
            return BarcodeScanOutcome.Cancelled; // user cancelled the region selection

        byte[] png;
        try
        {
            png = await _capture.CaptureRegionPngAsync(region.Value);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return BarcodeScanOutcome.Failed;
        }

        string? payload;
        try
        {
            // ZXing.Net 0.16: BarcodeReader<T> is generic (the plain
            // `new BarcodeReader()` of older versions is CS0305 now).
            // BarcodeReaderGeneric is the concrete reader for
            // System.Drawing.Bitmap, with Decode/DecodeMultiple(Bitmap).
            using var stream = new MemoryStream(png);
            using var bitmap = new Bitmap(stream);
            var reader = new BarcodeReaderGeneric { AutoRotate = true, TryInverted = true };
            var results = reader.DecodeMultiple(bitmap);
            payload = results
                ?.Select(r => r?.Text?.Trim())
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return BarcodeScanOutcome.Failed;
        }

        if (string.IsNullOrWhiteSpace(payload))
            return BarcodeScanOutcome.NotFound;

        Payload = payload;
        return BarcodePayload.Classify(payload) == BarcodePayloadAction.OpenUrl
            ? BarcodeScanOutcome.Opened
            : BarcodeScanOutcome.Copied;
    }
}
