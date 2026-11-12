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
    private readonly Func<Task<RegionSelection>> _selectRegionAsync;
    private readonly IToastService _toast;
    private readonly ScreenCaptureService _capture = new();

    // The clipboard reads the bitmap lazily when the user pastes, so the
    // stream must outlive this call: keep the latest one in a field
    // (the previous stream is released when replaced). Disposing it
    // here would make a later paste fail.
    private InMemoryRandomAccessStream? _lastImageStream;

    public ScreenshotCopyService(Func<Task<RegionSelection>> selectRegionAsync, IToastService toast)
    {
        _selectRegionAsync = selectRegionAsync;
        _toast = toast;
    }

    public async Task RunAsync()
    {
        var selection = await _selectRegionAsync();
        if (selection.Region is null)
            return; // user cancelled the region selection

        byte[] png;
        try
        {
            // Primary: cut the region out of the pre-overlay screenshot
            // (0.1.3.54: the overlay renders opaque, so a live capture
            // photographs the overlay, not the desktop). Without a
            // screenshot, capture the region live — same behaviour on
            // Win10/Win11, with proper cancel/error feedback. The system
            // Snipping Tool (ms-screenclip:) is the fallback below, only
            // when this capture fails.
            png = await selection.CropScreenshotAsync()
                ?? await _capture.CaptureRegionPngAsync(selection.Region.Value);
        }
        catch (Exception)
        {
            // Primary capture failed: fall back to the system Snipping
            // Tool (ms-screenclip: — Snip & Sketch on Win10, Snipping
            // Tool on Win11). The system tool copies the snip to the
            // clipboard itself; the app gets no completion callback and
            // cannot tell cancel from success, so the toast says what
            // will happen instead of claiming "Copied".
            await TryOpenSystemSnippingToolAsync();
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

    /// <summary>System fallback: open the OS snipping UI. Returns after
    /// launching (or failing to launch) — the snip itself happens in the
    /// system tool, outside this app.</summary>
    private async Task TryOpenSystemSnippingToolAsync()
    {
        try
        {
            var opened = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-screenclip:"));
            _toast.Show(opened
                ? AppStrings.Get("ScreenshotCopy.SystemFallback", "Opened Windows Snipping Tool instead — the screenshot will be copied to the clipboard.")
                : AppStrings.Get("ScreenshotCopy.Failed", "Could not copy the screenshot: {0}", "Snipping Tool is not available."));
        }
        catch (Exception ex)
        {
            _toast.Show(AppStrings.Get("ScreenshotCopy.Failed", "Could not copy the screenshot: {0}", ex.Message));
        }
    }
}
