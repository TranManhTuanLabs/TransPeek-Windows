// System tray icon (SPEC §2: tray icon + Settings window).
//
// WinUI 3 has no built-in NotifyIcon, so v0.1 uses H.NotifyIcon.WinUI
// (2.3.2, MIT). 2.3.2 is the newest version targeting net8.0-windows;
// 2.4.1+ targets net10.0-windows only (NU1202 on .NET 8, CI run
// 42428017607). The icon is the embedded base64 PNG (see
// Assets/EmbeddedIcon.cs) — no binary assets in the repo.
// Left-click opens Settings; right-click shows the context menu.
//
// The menu mirrors the mac menubar (MenuItemView.swift): header with
// the running version, section headlines, one item per action, icons
// from Segoe Fluent Icons (glyph codes verified against Microsoft's
// icon list: Edit E70F, Settings E713, KeyboardClassic E765,
// Paste E77F, Copy E8C8, Scan E8FE, Camera E722, Cancel E711,
// BarcodeScanner EC5A).
//
// Deliberately NOT in this menu (Manh, 2026-11-11):
// - Smart Translation: deferred — Manh does not yet know how that
//   feature behaves, so it stays out until he decides.
// - Check for Updates: deferred — to discuss later.
// - Permissions Guide: skipped, no Windows equivalent — Windows never
//   asks for the Accessibility / Screen Recording grants the mac guide
//   walks through, so there is nothing to guide the user to grant.

using System.IO;
using H.NotifyIcon;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Assets;

namespace TransPeek.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _tray;
    private bool _disposed;

    public TrayIconService(
        Action manualTranslation,
        Action screenshotOcr,
        Action clipboardOcr,
        Action selectionTranslation,
        Action clipboardTranslation,
        Action screenshotCopy,
        Action scanBarcodes,
        Action openSettings,
        Action quit)
    {
        var menu = new MenuFlyout();
        // Labels come from Resources.resw (English base) via AppStrings,
        // so a future translation file changes them without touching code.
        // The header is the app name + running version, like the mac
        // menubar; disabled so it reads as a title, not an action.
        menu.Items.Add(HeaderItem(TrayHeaderText()));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(SectionItem(AppStrings.Get("Tray.Section.Capture", "Text Capture from Screen")));
        menu.Items.Add(Item("Tray.Menu.ManualTranslation", "Manual Translation", "\uE765", manualTranslation));
        menu.Items.Add(Item("Tray.Menu.ScreenshotOcr", "Screenshot OCR", "\uE8FE", screenshotOcr));
        menu.Items.Add(Item("Tray.Menu.ClipboardOcr", "Clipboard OCR", "\uE8C8", clipboardOcr));
        menu.Items.Add(Item("Tray.Menu.SelectionTranslation", "Selection Translation", "\uE70F", selectionTranslation));
        menu.Items.Add(Item("Tray.Menu.ClipboardTranslation", "Clipboard Translation", "\uE77F", clipboardTranslation));
        menu.Items.Add(Item("Tray.Menu.ScreenshotCopy", "Screenshot Copy", "\uE722", screenshotCopy));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(SectionItem(AppStrings.Get("Tray.Section.Barcodes", "Barcodes / QR Codes")));
        menu.Items.Add(Item("Tray.Menu.ScanBarcodes", "Scan Barcodes/QR Codes", "\uEC5A", scanBarcodes));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Tray.Menu.Settings", "Settings…", "\uE713", openSettings));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Tray.Menu.Quit", "Quit TransPeek", "\uE711", quit));

        _tray = new TaskbarIcon
        {
            Icon = LoadEmbeddedIcon(),
            ToolTipText = AppStrings.Get("Tray.ToolTip", "TransPeek — Translate anywhere, peek instantly. Left-click: Settings. Right-click: menu."),
            ContextFlyout = menu,
            LeftClickCommand = new RelayCommand(openSettings),
        };
        // ForceCreate is required: the icon is created lazily otherwise and
        // a tray-only app would show nothing until the first interaction.
        _tray.ForceCreate();
    }

    /// <summary>Header: app name + version, plus the CI build number in
    /// parentheses when the build was stamped by CI — mac parity
    /// ("TransPeek 0.1.3 (36)"). Local builds show no number.</summary>
    private static string TrayHeaderText()
    {
        var text = $"TransPeek {AppInfo.Version}";
        if (AppInfo.CiBuildNumber > 0)
            text += $" ({AppInfo.CiBuildNumber})";
        return text;
    }

    /// <summary>Header / section headline: a disabled item reads as a
    /// title, not an action (mac uses a dimmed, non-clickable Text).</summary>
    private static MenuFlyoutItem HeaderItem(string text) => new()
    {
        Text = text,
        IsEnabled = false,
        FontWeight = FontWeights.SemiBold,
    };

    private static MenuFlyoutItem SectionItem(string text) => new()
    {
        Text = text,
        IsEnabled = false,
    };

    private static MenuFlyoutItem Item(string key, string fallback, string glyph, Action onClick)
    {
        var item = new MenuFlyoutItem
        {
            Text = AppStrings.Get(key, fallback),
            Icon = new FontIcon { Glyph = glyph },
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    /// <summary>Build the tray icon from the embedded PNG as a
    /// System.Drawing.Icon — NOT a BitmapImage. H.NotifyIcon loads
    /// IconSource through a WinRT Uri (ToStream(Uri)), and a
    /// stream-sourced BitmapImage has UriSource == null, which is the
    /// NullReferenceException in crash.log (2026-11-12, builds
    /// 0.1.3.40/.42). The Icon property takes the handle straight to
    /// Shell_NotifyIcon with no WinRT URI resolution — the path the
    /// library itself recommends for unpackaged apps, where ms-appx
    /// URIs cannot resolve either (no package identity).
    /// The HICON is deliberately leaked for the app lifetime:
    /// Icon.FromHandle does not take ownership, GetHicon() returns a
    /// new handle (disposing the Bitmap does not destroy it), and the
    /// tray icon needs the handle alive as long as the app runs.</summary>
    private static System.Drawing.Icon LoadEmbeddedIcon()
    {
        var bytes = Convert.FromBase64String(EmbeddedIcon.PngBase64);
        using var stream = new MemoryStream(bytes);
        using var bitmap = new System.Drawing.Bitmap(stream);
        var hicon = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(hicon);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tray.Dispose();
    }
}
