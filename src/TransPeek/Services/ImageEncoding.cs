// PNG encoding for captured frames. Input is raw BGRA bytes (B, G, R, A
// per pixel); the encoder writes a standard PNG. Alpha is forced opaque:
// GDI BitBlt frames carry a zero alpha byte, which would make the PNG
// fully transparent.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace TransPeek.Services;

public static class ImageEncoding
{
    /// <summary>Encode raw BGRA bytes as a PNG. Alpha is forced to 255.</summary>
    public static async Task<byte[]> EncodePngAsync(byte[] bgra, int width, int height)
    {
        if (bgra.Length < width * height * 4)
            throw new ArgumentException("BGRA buffer is smaller than width*height*4.");
        ForceOpaque(bgra);
        using var stream = new InMemoryRandomAccessStream();
        var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            bgra.AsBuffer(), BitmapPixelFormat.Bgra8, width, height);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        stream.Seek(0);
        var bytes = new byte[stream.Size];
        var buffer = await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
        return buffer.ToArray();
    }

    /// <summary>Decode PNG bytes to raw BGRA (for OCR engines that want
    /// raw pixels). Returns (bgra, width, height).</summary>
    public static async Task<(byte[] Bgra, int Width, int Height)> DecodePngAsync(byte[] pngBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var width = (int)bitmap.PixelWidth;
        var height = (int)bitmap.PixelHeight;
        var buffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
        bitmap.CopyToBuffer(buffer);
        return (buffer.ToArray(), width, height);
    }

    /// <summary>Crop a PNG to a rectangle (relative to the PNG's origin):
    /// decode → copy the crop rows → re-encode. Used to cut the selected
    /// region out of the pre-overlay display screenshot (the overlay is
    /// opaque, so a live capture taken after it appears photographs the
    /// overlay, not the desktop — 0.1.3.54).</summary>
    public static async Task<byte[]> CropPngAsync(byte[] pngBytes, RectInt32 crop)
    {
        var (bgra, width, height) = await DecodePngAsync(pngBytes);
        var x = Math.Clamp(crop.X, 0, width - 1);
        var y = Math.Clamp(crop.Y, 0, height - 1);
        var w = Math.Min(crop.Width, width - x);
        var h = Math.Min(crop.Height, height - y);
        if (w <= 0 || h <= 0)
            throw new ArgumentException("Crop rectangle is empty.");
        var cropped = new byte[w * h * 4];
        for (var row = 0; row < h; row++)
            System.Buffer.BlockCopy(bgra, ((y + row) * width + x) * 4, cropped, row * w * 4, w * 4);
        return await EncodePngAsync(cropped, w, h);
    }

    private static void ForceOpaque(byte[] bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
            bgra[i] = 0xFF;
    }
}
