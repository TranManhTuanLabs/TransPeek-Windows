// System tray icon (SPEC §2: tray icon + Settings window).
//
// WinUI 3 has no built-in NotifyIcon, so v0.1 uses H.NotifyIcon.WinUI
// (2.3.2, MIT). 2.3.2 is the newest version targeting net8.0-windows;
// 2.4.1+ targets net10.0-windows only (NU1202 on .NET 8, CI run
// 42428017607). The icon is the embedded base64 PNG (see
// Assets/EmbeddedIcon.cs) — no binary assets in the repo.
// Left-click opens Settings; right-click shows the context menu.

using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using TransPeek.Assets;

namespace TransPeek.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _tray;
    private bool _disposed;

    public TrayIconService(Action openSettings, Action translateSelection, Action captureOcr, Action quit)
    {
        var menu = new MenuFlyout();
        var settingsItem = new MenuFlyoutItem { Text = "Settings…" };
        settingsItem.Click += (_, _) => openSettings();
        var translateItem = new MenuFlyoutItem { Text = "Translate selection" };
        translateItem.Click += (_, _) => translateSelection();
        var ocrItem = new MenuFlyoutItem { Text = "Capture & OCR" };
        ocrItem.Click += (_, _) => captureOcr();
        menu.Items.Add(settingsItem);
        menu.Items.Add(translateItem);
        menu.Items.Add(ocrItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        var quitItem = new MenuFlyoutItem { Text = "Quit TransPeek" };
        quitItem.Click += (_, _) => quit();
        menu.Items.Add(quitItem);

        _tray = new TaskbarIcon
        {
            IconSource = LoadEmbeddedIcon(),
            ToolTipText = "TransPeek — Translate anywhere, peek instantly.",
            ContextMenuFlyout = menu,
            LeftClickCommand = new RelayCommand(openSettings),
        };
        // ForceCreate is required: the icon is created lazily otherwise and
        // a tray-only app would show nothing until the first interaction.
        _tray.ForceCreate();
    }

    private static BitmapImage LoadEmbeddedIcon()
    {
        // Synchronous on purpose: the tray icon is created on the UI thread
        // at startup, and AsRandomAccessStream() needs no async work.
        // NOTE: the stream is deliberately NOT disposed — BitmapImage
        // decodes lazily and needs the source stream to stay open.
        var bytes = Convert.FromBase64String(EmbeddedIcon.PngBase64);
        var image = new BitmapImage();
        var stream = new MemoryStream(bytes).AsRandomAccessStream();
        image.SetSource(stream);
        return image;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tray.Dispose();
    }
}
