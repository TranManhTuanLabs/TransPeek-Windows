// Screen-region capture (SPEC §3.3).
//
// Primary path: Windows.Graphics.Capture (monitor item + Direct3D11 frame
// pool, via Vortice). This is the modern API and captures hardware
// composited content. It needs no user picker because the app captures a
// whole monitor and crops to the selected region in software.
//
// Fallback path: GDI BitBlt (GetDC + BitBlt + GetDIBits). Old but
// dependency-free and almost always works for text in browsers/documents
// — the main OCR use case (books, web, scanned PDFs). Any failure in the
// GraphicsCapture path falls through to BitBlt instead of failing the OCR.
//
// NOTE (verify first in CI): the GraphicsCapture path has several WinRT /
// D3D interop steps that cannot be checked on Linux. If CI shows a
// compile error there, the BitBlt path keeps OCR working while the
// GraphicsCapture code is fixed.

using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using TransPeek.Interop;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WinRT;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace TransPeek.Services;

public sealed class ScreenCaptureService
{
    /// <summary>Capture a screen region (physical pixels) as PNG bytes.</summary>
    public async Task<byte[]> CaptureRegionPngAsync(RectInt32 region, CancellationToken cancellationToken = default)
    {
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentException("Capture region is empty.");
        try
        {
            return await CaptureWithGraphicsCaptureAsync(region, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GraphicsCapture failed ({ex.Message}); falling back to BitBlt.");
            return await CaptureWithBitBltAsync(region, cancellationToken).ConfigureAwait(false);
        }
    }

    // ----- Primary: Windows.Graphics.Capture -----

    private static async Task<byte[]> CaptureWithGraphicsCaptureAsync(RectInt32 region, CancellationToken ct)
    {
        var center = new Windows.Graphics.PointInt32(
            region.X + region.Width / 2, region.Y + region.Height / 2);
        var area = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Nearest);
        var displayBounds = area.OuterBounds;

        // D3D11 device (BGRA support is required for capture).
        D3D11.D3D11CreateDevice(null, Vortice.Direct3D.DriverType.Hardware,
            Vortice.Direct3D11.DeviceCreationFlags.BgraSupport, null!,
            out var device, out _, out var context);
        using var d3dDevice = device ?? throw new InvalidOperationException("Could not create a D3D11 device.");
        using var d3dContext = context ?? throw new InvalidOperationException("Could not create a D3D11 context.");

        // Bridge the DXGI device to a WinRT IDirect3DDevice for the frame pool.
        using var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        var hr = NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var graphicsDevicePtr);
        if (hr != 0 || graphicsDevicePtr == IntPtr.Zero)
            throw new InvalidOperationException($"CreateDirect3D11DeviceFromDXGIDevice failed (HRESULT 0x{hr:X8}).");
        var winrtDevice = (IDirect3DDevice)Marshal.GetObjectForIUnknown(graphicsDevicePtr);
        Marshal.Release(graphicsDevicePtr);

        var item = GraphicsCaptureItem.TryCreateFromDisplayId(area.DisplayId)
            ?? throw new InvalidOperationException("Could not create a capture item for this display.");

        var poolSize = new SizeInt32(displayBounds.Width, displayBounds.Height);
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, poolSize);
        using var session = framePool.CreateCaptureSession(item);
        session.StartCapture();

        Direct3D11CaptureFrame? frame = null;
        var frameArrived = new TaskCompletionSource();
        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args) => frameArrived.TrySetResult();
        framePool.FrameArrived += OnFrameArrived;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            await frameArrived.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            frame = framePool.TryGetNextFrame();
        }
        finally
        {
            framePool.FrameArrived -= OnFrameArrived;
            session.Dispose();
        }
        if (frame is null)
            throw new InvalidOperationException("No capture frame arrived.");
        using var capturedFrame = frame;

        // Crop the full-display frame to the selected region.
        var cropX = Math.Clamp(region.X - displayBounds.X, 0, displayBounds.Width - 1);
        var cropY = Math.Clamp(region.Y - displayBounds.Y, 0, displayBounds.Height - 1);
        var cropW = Math.Min(region.Width, displayBounds.Width - cropX);
        var cropH = Math.Min(region.Height, displayBounds.Height - cropY);
        var bgra = ReadFrameBgra(capturedFrame.Surface, d3dDevice, d3dContext,
            cropX, cropY, cropW, cropH, displayBounds.Width, displayBounds.Height);
        return await ImageEncoding.EncodePngAsync(bgra, cropW, cropH).ConfigureAwait(false);
    }

    private static byte[] ReadFrameBgra(IDirect3DSurface surface, ID3D11Device device,
        ID3D11DeviceContext context, int cropX, int cropY, int cropW, int cropH,
        int frameW, int frameH)
    {
        // IDirect3DSurface -> ID3D11Texture2D via IDirect3DDxgiInterfaceAccess.
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        var texturePtr = access.GetInterface(typeof(ID3D11Texture2D).GUID);
        using var texture = new ID3D11Texture2D(texturePtr);

        var desc = texture.Description;
        var stagingDesc = new Texture2DDescription
        {
            Width = (uint)desc.Width,
            Height = (uint)desc.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        };
        using var staging = device.CreateTexture2D(stagingDesc);
        context.CopyResource(texture, staging);
        var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var bytes = new byte[cropW * cropH * 4];
            unsafe
            {
                var src = (byte*)mapped.DataPointer.ToPointer();
                for (var row = 0; row < cropH; row++)
                {
                    var srcRow = src + ((long)(cropY + row) * mapped.RowPitch) + (long)cropX * 4;
                    var dstOffset = row * cropW * 4;
                    Marshal.Copy((IntPtr)srcRow, bytes, dstOffset, cropW * 4);
                }
            }
            return bytes;
        }
        finally
        {
            context.Unmap(staging, 0);
        }
    }

    // ----- Fallback: GDI BitBlt -----

    private static async Task<byte[]> CaptureWithBitBltAsync(RectInt32 region, CancellationToken ct)
    {
        var bytes = await Task.Run(() =>
        {
            var hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
            if (hdcScreen == IntPtr.Zero)
                throw new InvalidOperationException("GetDC failed.");
            var hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
            var hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, region.Width, region.Height);
            var old = NativeMethods.SelectObject(hdcMem, hBitmap);
            try
            {
                if (!NativeMethods.BitBlt(hdcMem, 0, 0, region.Width, region.Height,
                        hdcScreen, region.X, region.Y, NativeMethods.SRCCOPY))
                    throw new InvalidOperationException("BitBlt failed.");
                var bmi = new NativeMethods.BITMAPINFO
                {
                    bmiHeader = new NativeMethods.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                        biWidth = region.Width,
                        // Negative height = top-down DIB (rows in display order).
                        biHeight = -region.Height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0, // BI_RGB
                    },
                };
                var buffer = new byte[region.Width * region.Height * 4];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var lines = NativeMethods.GetDIBits(hdcMem, hBitmap, 0, (uint)region.Height,
                        handle.AddrOfPinnedObject(), ref bmi, NativeMethods.DIB_RGB_COLORS);
                    if (lines == 0)
                        throw new InvalidOperationException("GetDIBits failed.");
                }
                finally
                {
                    handle.Free();
                }
                return buffer;
            }
            finally
            {
                NativeMethods.SelectObject(hdcMem, old);
                NativeMethods.DeleteObject(hBitmap);
                NativeMethods.DeleteDC(hdcMem);
                NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
            }
        }, ct).ConfigureAwait(false);
        return await ImageEncoding.EncodePngAsync(bytes, region.Width, region.Height).ConfigureAwait(false);
    }
}
