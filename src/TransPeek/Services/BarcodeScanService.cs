// Scan Barcodes/QR Codes (mac parity: TranslationCoordinator.scanBarcodes
// + BarcodeScanner with Vision). Region select → capture → ZXing.Net
// decode. URL payloads open in the browser, anything else is copied;
// the caller (App) owns the browser/copy/toast side effects, this
// service only reports the outcome. Must run on the UI thread: region
// select and capture both touch UI objects.
//
// Decoding uses the WinRT BitmapDecoder (PNG -> BGRA pixels) plus
// ZXing's RGBLuminanceSource — deliberately NO System.Drawing here:
// ZXing.Net 0.16 moved the Bitmap-based helpers out of the core package
// (BarcodeReader<T> is generic, Decode takes a LuminanceSource, and
// BitmapLuminanceSource is not in ZXing.Net 0.16.9 — CS0305/CS1503/
// CS0246 across runs #33-#35). RGBLuminanceSource with BGRA32 is core
// ZXing and takes raw pixels, so this path has no Drawing dependency
// and no package-downgrade fight (NU1605 in runs #31-#32).

using System.Runtime.InteropServices.WindowsRuntime;
using TransPeek.Core.Barcode;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
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
    private readonly Func<Task<RegionSelection>> _selectRegionAsync;
    private readonly ScreenCaptureService _capture = new();

    /// <summary>Payload of the first code found (Opened / Copied only).</summary>
    public string? Payload { get; private set; }

    /// <summary>Failure detail (Failed only).</summary>
    public string? ErrorMessage { get; private set; }

    public BarcodeScanService(Func<Task<RegionSelection>> selectRegionAsync)
    {
        _selectRegionAsync = selectRegionAsync;
    }

    public async Task<BarcodeScanOutcome> ScanAsync()
    {
        Payload = null;
        ErrorMessage = null;

        var selection = await _selectRegionAsync();
        if (selection.Region is null)
            return BarcodeScanOutcome.Cancelled; // user cancelled the region selection

        byte[] png;
        try
        {
            // Prefer the pre-overlay screenshot (0.1.3.54: the overlay
            // renders opaque, so a live capture photographs the overlay).
            png = await selection.CropScreenshotAsync()
                ?? await _capture.CaptureRegionPngAsync(selection.Region.Value);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return BarcodeScanOutcome.Failed;
        }

        string? payload;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyToBuffer(pixels.AsBuffer());
            var source = new RGBLuminanceSource(
                pixels, bitmap.PixelWidth, bitmap.PixelHeight,
                RGBLuminanceSource.BitmapFormat.BGRA32);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = { TryInverted = true },
            };
            var results = reader.DecodeMultiple(source);
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
