// System tray icon (SPEC §2: tray icon + Settings window).
//
// WinUI 3 has no built-in NotifyIcon, so v0.1 uses H.NotifyIcon.WinUI
// (2.3.2, MIT). 2.3.2 is the newest version targeting net8.0-windows;
// 2.4.1+ targets net10.0-windows only (NU1202 on .NET 8, CI run
// 42428017607). The icon is the mac app icon (Assets/EmbeddedIcon.cs —
// the S3 artwork converted to a multi-size .ico), the same on both
// platforms. No binary assets in the repo: the .ico lives in the code
// as base64.
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
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
    private bool _disposed;

    // The two hotkey actions, so their shortcut hints can be refreshed
    // on every menu open (see the Opening handler in the constructor).
    private readonly MenuFlyoutItem _clipboardTranslationItem;
    private readonly MenuFlyoutItem _screenshotOcrItem;

    // The .ico stream behind the tray Icon: Icon(Stream) needs the
    // stream to stay open while the Icon is used, so it lives here
    // for the lifetime of the service.
    private static MemoryStream? _iconStream;

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
        // The constructor runs on the UI thread: the menu actions are
        // deferred onto this queue (see Item()).
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        var menu = new MenuFlyout();
        // Labels come from Resources.resw (English base) via AppStrings,
        // so a future translation file changes them without touching code.
        // The header is the app name + running version, like the mac
        // menubar; disabled so it reads as a title, not an action.
        menu.Items.Add(HeaderItem(TrayHeaderText()));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(SectionItem(AppStrings.Get("Tray.Section.Capture", "Text Capture from Screen")));
        menu.Items.Add(Item("Tray.Menu.ManualTranslation", "Manual Translation", "\uE765", manualTranslation));
        _screenshotOcrItem = Item("Tray.Menu.ScreenshotOcr", "Screenshot OCR", "\uE8FE", screenshotOcr);
        menu.Items.Add(_screenshotOcrItem);
        menu.Items.Add(Item("Tray.Menu.ClipboardOcr", "Clipboard OCR", "\uE8C8", clipboardOcr));
        menu.Items.Add(Item("Tray.Menu.SelectionTranslation", "Selection Translation", "\uE70F", selectionTranslation));
        _clipboardTranslationItem = Item("Tray.Menu.ClipboardTranslation", "Clipboard Translation", "\uE77F", clipboardTranslation);
        menu.Items.Add(_clipboardTranslationItem);
        menu.Items.Add(Item("Tray.Menu.ScreenshotCopy", "Screenshot Copy", "\uE722", screenshotCopy));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(SectionItem(AppStrings.Get("Tray.Section.Barcodes", "Barcodes / QR Codes")));
        menu.Items.Add(Item("Tray.Menu.ScanBarcodes", "Scan Barcodes/QR Codes", "\uEC5A", scanBarcodes));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Tray.Menu.Settings", "Settings…", "\uE713", openSettings));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Tray.Menu.Quit", "Quit TransPeek", "\uE711", quit));

        // Shortcut hints on the right of the two hotkey actions (mac
        // menubar parity). Set once here and refreshed when Settings are
        // saved (RefreshAccelerators) — NOT on menu.Opening: the Opening
        // handler ran on every open and is suspected of breaking the
        // menu in 0.1.3.51 (items unclickable). WinUI renders
        // KeyboardAcceleratorTextOverride dimmed, like the mac labels.
        // The other actions have no hotkey yet — when the full shortcut
        // set lands in Settings, add theirs here.
        RefreshAccelerators();

        _tray = new TaskbarIcon
        {
            ToolTipText = AppStrings.Get("Tray.ToolTip", "TransPeek — Translate anywhere, peek instantly. Left-click: Settings. Right-click: menu."),
            ContextFlyout = menu,
            LeftClickCommand = new RelayCommand(openSettings),
        };
        // The icon is set separately: a bad icon must never kill
        // startup (LoadEmbeddedIcon returns null instead of throwing).
        var icon = LoadEmbeddedIcon();
        if (icon is not null)
            _tray.Icon = icon;
        // ForceCreate is required: the icon is created lazily otherwise and
        // a tray-only app would show nothing until the first interaction.
        _tray.ForceCreate();
    }

    /// <summary>Refresh the shortcut hints from the current Settings
    /// (call after Settings are saved — hotkeys can change at runtime).</summary>
    public void RefreshAccelerators()
    {
        _clipboardTranslationItem.KeyboardAcceleratorTextOverride = FormatHotkey(App.Settings.TranslateHotkey);
        _screenshotOcrItem.KeyboardAcceleratorTextOverride = FormatHotkey(App.Settings.OcrHotkey);
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

    private MenuFlyoutItem Item(string key, string fallback, string glyph, Action onClick)
    {
        var item = new MenuFlyoutItem
        {
            Text = AppStrings.Get(key, fallback),
            Icon = new FontIcon { Glyph = glyph },
        };
        // Run the action on the next dispatcher tick, AFTER the flyout
        // has closed: showing a window (Settings, panel) synchronously
        // from a MenuFlyoutItem.Click is swallowed while the flyout is
        // dismissing — the Settings item did nothing from the menu
        // (left-click on the icon worked) in the 0.1.3.47/.49 test.
        // Fall back to a direct call if the dispatcher is unavailable:
        // a dead menu is worse than a swallowed action (0.1.3.51: the
        // whole menu went unclickable, cause still unproven — the
        // Opening handler is gone too, see RefreshAccelerators).
        // A throwing menu action must never die silently: log it so
        // crash.log shows what happened.
        item.Click += (_, _) =>
        {
            // Diagnostic (0.1.3.53 test: the right-click menu is
            // "useless" — items may not even fire): log every click, so
            // the next crash.log tells whether Click fired at all.
            CrashLog.WriteLine("TrayMenu", $"Clicked '{key}'");
            try
            {
                if (_dispatcher is not null && _dispatcher.TryEnqueue(() => RunMenuAction(onClick)))
                    return;
            }
            catch { }
            RunMenuAction(onClick);
        };
        return item;
    }

    private static void RunMenuAction(Action onClick)
    {
        try { onClick(); }
        catch (Exception ex) { CrashLog.Write("TrayMenu", ex); }
    }

    /// <summary>"Control+Alt+T" → "Ctrl+Alt+T" for the menu hint.</summary>
    private static string FormatHotkey(string spec)
        => spec.Replace("Control", "Ctrl", StringComparison.OrdinalIgnoreCase);

    /// <summary>Build the tray icon from the embedded .ico bytes — the
    /// mac app icon (S3), same artwork on both platforms. new
    /// Icon(stream) reads the .ico data directly; no WinRT URI is
    /// involved, so the NullReferenceException from the old BitmapImage
    /// path (UriSource == null, crash.log 2026-11-12) cannot recur.
    /// Returns null (and logs) instead of throwing: a bad icon must
    /// never kill startup — the app runs without a tray image.</summary>
    private static System.Drawing.Icon? LoadEmbeddedIcon()
    {
        try
        {
            var bytes = Convert.FromBase64String(EmbeddedIcon.IcoBase64);
            _iconStream = new MemoryStream(bytes);
            return new System.Drawing.Icon(_iconStream);
        }
        catch (Exception ex)
        {
            CrashLog.Write("TrayIcon", ex);
            return null;
        }
    }

    /// <summary>Path of the .ico written to disk for AppWindow.SetIcon
    /// (the Settings window title-bar/taskbar icon, which needs a file
    /// path, not a stream). Written once; rewritten only if the bytes
    /// change.</summary>
    internal static string EnsureIconFile()
    {
        var dir = TransPeek.Core.Dictionary.AppPaths.LocalDataDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "app-icon.ico");
        var bytes = Convert.FromBase64String(EmbeddedIcon.IcoBase64);
        if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
            File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tray.Dispose();
    }
}
