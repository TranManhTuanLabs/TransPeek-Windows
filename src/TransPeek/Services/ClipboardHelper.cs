// Clipboard fallback for text selection (SPEC §3.1, MoePeek 3-layer
// approach adapted to Windows: UIA TextPattern first, clipboard Ctrl+C
// second — there is no AppleScript layer on Windows).
//
// The fallback is USER-INITIATED only (hotkey / tray menu): simulating
// Ctrl+C unprompted would clobber the user's clipboard. It copies the
// current selection, reads the text, then RESTORES the previous clipboard
// content so the user's clipboard is left untouched.

using TransPeek.Interop;
using Windows.ApplicationModel.DataTransfer;

namespace TransPeek.Services;

public static class ClipboardHelper
{
    /// <summary>Copy the current selection (Ctrl+C) and read it as text.
    /// Returns null when nothing usable was copied. Restores the previous
    /// clipboard content afterwards. Must run on the UI thread.</summary>
    public static async Task<string?> TryGetSelectionViaClipboardAsync()
    {
        var dataBefore = Clipboard.GetContent();
        string? textBefore = null;
        if (dataBefore is not null && dataBefore.Contains(StandardDataFormats.Text))
        {
            try { textBefore = await dataBefore.GetTextAsync(); } catch { /* ignore */ }
        }

        try
        {
            Clipboard.Clear();
            SendCtrlC();
            // Give the foreground app time to populate the clipboard.
            await Task.Delay(200);

            var data = Clipboard.GetContent();
            if (data is null || !data.Contains(StandardDataFormats.Text))
                return null;
            var text = (await data.GetTextAsync())?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
        finally
        {
            // Restore the user's clipboard.
            try
            {
                if (textBefore is not null)
                {
                    var package = new DataPackage();
                    package.SetText(textBefore);
                    Clipboard.SetContent(package);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
                // Best effort only.
            }
        }
    }

    public static async Task CopyTextAsync(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        await Task.CompletedTask;
    }

    public static async Task<string?> GetTextAsync()
    {
        try
        {
            var data = Clipboard.GetContent();
            if (data is null || !data.Contains(StandardDataFormats.Text))
                return null;
            return await data.GetTextAsync();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Simulate Ctrl+C via Win32 SendInput.</summary>
    private static void SendCtrlC()
    {
        var inputs = new NativeMethods.INPUT[]
        {
            KeyDown(NativeMethods.VK_CONTROL),
            KeyDown(NativeMethods.VK_C),
            KeyUp(NativeMethods.VK_C),
            KeyUp(NativeMethods.VK_CONTROL),
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
    }

    private static NativeMethods.INPUT KeyDown(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = vk },
        },
    };

    private static NativeMethods.INPUT KeyUp(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP },
        },
    };
}
