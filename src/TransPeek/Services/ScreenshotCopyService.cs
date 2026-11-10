// Screenshot Copy (mac parity: TranslationCoordinator.screenshotCopy
// via `screencapture -i -c`). Region select → capture → the IMAGE
// itself goes to the clipboard (no OCR, no panel). Toast "Copied" on
// success, error toast on failure, nothing on cancel. Must run on the
// UI thread: region select, capture and clipboard all touch UI objects.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage.Streams;

namespace TransPeek.Services;

public sealed class ScreenshotCopyService
{
    private readonly Func<Task<RectInt32?>> _selectRegionAsync;
    private readonly IToastService _toast;
    private readonly ScreenCaptureService _capture = new();

    // The clipboard reads the bitmap lazily when the user pastes, so the
    // stream must outlive this call: keep the latest one in a field
    // (the previous stream is released when replaced). Disposing it
    // here would make a later paste fail.
    private InMemoryRandomAccessStream? _lastImageStream;

    public ScreenshotCopyService(Func<Task<RectInt32?>> selectRegionAsync, IToastService toast)
    {
        _selectRegionAsync = selectRegionAsync;
        _toast = toast;
    }

    public async Task RunAsync()
    {
        var region = await _selectRegionAsync();
        if (region is null)
            return; // user cancelled the region selection

        byte[] png;
        try
        {
            png = await _capture.CaptureRegionPngAsync(region.Value);
        }
        catch (Exception ex)
        {
            _toast.Show(AppStrings.Get("Ocr.CaptureFailed", "Could not capture the screen: {0}", ex.Message));
            return;
        }

        try
        {
            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            _lastImageStream = stream;
            var package = new DataPackage();
            package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(package);
            _toast.Show(AppStrings.Get("App.Toast.Copied", "Copied"));
        }
        catch (Exception ex)
        {
            _toast.Show(AppStrings.Get("ScreenshotCopy.Failed", "Could not copy the screenshot: {0}", ex.Message));
        }
    }
}
