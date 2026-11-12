using TransPeek.Core.Settings;
using Xunit;

namespace TransPeek.Tests;

// The full shortcut set (Manh 2026-11-12): one global hotkey per tray
// menu action. The defaults must all be distinct — Win32 RegisterHotKey
// silently drops the second registration of a duplicate chord, so a
// copy-paste duplicate would leave an action with no working hotkey and
// no error anywhere. (Parse itself lives in the WinUI project, so the
// format check happens at runtime in Settings validation.)
public sealed class AppSettingsHotkeyTests
{
    [Fact]
    public void DefaultHotkeys_AreDistinctAndNonEmpty()
    {
        var s = AppSettings.Default();
        var specs = new[]
        {
            s.TranslateHotkey,
            s.OcrHotkey,
            s.ManualHotkey,
            s.ClipboardOcrHotkey,
            s.SelectionHotkey,
            s.ScreenshotCopyHotkey,
            s.ScanHotkey,
            s.SettingsHotkey,
        };
        Assert.All(specs, spec => Assert.False(string.IsNullOrWhiteSpace(spec)));
        Assert.Equal(specs.Length, specs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void DefaultHotkeys_MatchExpectedDefaults()
    {
        var s = AppSettings.Default();
        Assert.Equal("Control+Alt+T", s.TranslateHotkey);
        Assert.Equal("Control+Shift+S", s.OcrHotkey);
        Assert.Equal("Control+Alt+M", s.ManualHotkey);
        Assert.Equal("Control+Alt+C", s.ClipboardOcrHotkey);
        Assert.Equal("Control+Alt+S", s.SelectionHotkey);
        Assert.Equal("Control+Alt+P", s.ScreenshotCopyHotkey);
        Assert.Equal("Control+Alt+B", s.ScanHotkey);
        Assert.Equal("Control+Alt+O", s.SettingsHotkey);
    }
}
