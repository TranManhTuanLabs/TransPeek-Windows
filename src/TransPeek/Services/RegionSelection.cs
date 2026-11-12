// Result of a region selection (SPEC §3.3): the rectangle the user
// drew, plus the screenshot of the display it was drawn on.
//
// The screenshot is captured BEFORE the overlay appears (0.1.3.54
// finding): the overlay window renders opaque, so a capture taken
// while it is up photographs the overlay — a flat ~222 grey board
// with no text — instead of the desktop. The screenshot also becomes
// the overlay's background, so the user can actually see the desktop
// to pick a region (the 0.1.3.47 "grey frame, no content" report).

using Windows.Graphics;

namespace TransPeek.Services;

/// <summary>Region the user selected (absolute screen coords, physical
/// pixels; null when cancelled) + the display screenshot it was drawn
/// on (null when the display capture failed — callers fall back to
/// capturing the region live after the overlay closes) + the display
/// bounds the screenshot covers (the crop origin).</summary>
public sealed record RegionSelection(RectInt32? Region, byte[]? ScreenshotPng, RectInt32 DisplayBounds)
{
    /// <summary>Cut the selected region out of the pre-overlay
    /// screenshot. Null when there is no screenshot (or no region) —
    /// the caller falls back to a live region capture.</summary>
    public async Task<byte[]?> CropScreenshotAsync()
    {
        if (Region is null || ScreenshotPng is null)
            return null;
        var crop = new RectInt32(
            Region.Value.X - DisplayBounds.X,
            Region.Value.Y - DisplayBounds.Y,
            Region.Value.Width, Region.Value.Height);
        return await ImageEncoding.CropPngAsync(ScreenshotPng, crop);
    }
}
