// General settings: selection watching, hover delay, trigger auto-hide,
// translate hotkey, launch at login (registry Run key — the standard
// approach for unpackaged apps; no admin rights needed).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using TransPeek.Core.Settings;
using TransPeek.Services;

namespace TransPeek.Views.SettingsPages;

public sealed partial class GeneralPage : Page
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "TransPeek";

    private Action? _onSaved;
    private bool _loading;

    public GeneralPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _onSaved = e.Parameter as Action;
        _loading = true;
        var s = App.Settings;
        WatchSelectionToggle.IsOn = s.WatchSelection;
        HoverDelayCombo.SelectedIndex = s.HoverDelayMs == 500 ? 1 : 0;
        AutoHideBox.Value = s.TriggerIconAutoHideSeconds;
        TranslateHotkeyBox.Text = s.TranslateHotkey;
        OcrHotkeyBox.Text = s.OcrHotkey;
        ManualHotkeyBox.Text = s.ManualHotkey;
        ClipboardOcrHotkeyBox.Text = s.ClipboardOcrHotkey;
        SelectionHotkeyBox.Text = s.SelectionHotkey;
        ScreenshotCopyHotkeyBox.Text = s.ScreenshotCopyHotkey;
        ScanHotkeyBox.Text = s.ScanHotkey;
        SettingsHotkeyBox.Text = s.SettingsHotkey;
        LaunchAtLoginToggle.IsOn = IsLaunchAtLoginSet();
        _loading = false;
    }

    private void Save()
    {
        if (_loading)
            return;
        SettingsStore.Save(App.Settings);
        _onSaved?.Invoke();
    }

    private void OnWatchSelectionToggled(object sender, RoutedEventArgs e)
    {
        App.Settings.WatchSelection = WatchSelectionToggle.IsOn;
        Save();
    }

    private void OnHoverDelayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HoverDelayCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var ms))
        {
            App.Settings.HoverDelayMs = ms;
            Save();
        }
    }

    private void OnAutoHideChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        App.Settings.TriggerIconAutoHideSeconds = Math.Max(1, (int)args.NewValue);
        Save();
    }

    private void OnTranslateHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(TranslateHotkeyBox, HotkeyErrorText, "Translate",
            s => App.Settings.TranslateHotkey = s, () => App.Settings.TranslateHotkey);

    private void OnOcrHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(OcrHotkeyBox, OcrHotkeyErrorText, "OCR",
            s => App.Settings.OcrHotkey = s, () => App.Settings.OcrHotkey);

    private void OnManualHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(ManualHotkeyBox, ManualHotkeyErrorText, "Manual Translation",
            s => App.Settings.ManualHotkey = s, () => App.Settings.ManualHotkey);

    private void OnClipboardOcrHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(ClipboardOcrHotkeyBox, ClipboardOcrHotkeyErrorText, "Clipboard OCR",
            s => App.Settings.ClipboardOcrHotkey = s, () => App.Settings.ClipboardOcrHotkey);

    private void OnSelectionHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(SelectionHotkeyBox, SelectionHotkeyErrorText, "Selection Translation",
            s => App.Settings.SelectionHotkey = s, () => App.Settings.SelectionHotkey);

    private void OnScreenshotCopyHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(ScreenshotCopyHotkeyBox, ScreenshotCopyHotkeyErrorText, "Screenshot Copy",
            s => App.Settings.ScreenshotCopyHotkey = s, () => App.Settings.ScreenshotCopyHotkey);

    private void OnScanHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(ScanHotkeyBox, ScanHotkeyErrorText, "Scan Barcodes",
            s => App.Settings.ScanHotkey = s, () => App.Settings.ScanHotkey);

    private void OnSettingsHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(SettingsHotkeyBox, SettingsHotkeyErrorText, "Settings",
            s => App.Settings.SettingsHotkey = s, () => App.Settings.SettingsHotkey);

    /// <summary>Validate one hotkey box (format + not already used by
    /// another action — Win32 would silently drop the second registration),
    /// save it, or show the error and restore the old text.</summary>
    private void TrySaveHotkey(TextBox box, TextBlock error, string actionName,
        Action<string> set, Func<string> get)
    {
        var spec = box.Text.Trim();
        try
        {
            var parsed = GlobalHotkeyService.Parse(spec);
            foreach (var (name, other) in AllHotkeys())
            {
                if (name == actionName)
                    continue;
                try
                {
                    if (GlobalHotkeyService.Parse(other()) == parsed)
                    {
                        error.Text = $"Already used by {name}.";
                        error.Visibility = Visibility.Visible;
                        box.Text = get();
                        return;
                    }
                }
                catch (FormatException) { /* ignore an invalid stored spec */ }
            }
            error.Visibility = Visibility.Collapsed;
            set(spec);
            Save();
        }
        catch (FormatException ex)
        {
            error.Text = ex.Message;
            error.Visibility = Visibility.Visible;
            box.Text = get();
        }
    }

    private static (string Name, Func<string> Get)[] AllHotkeys() =>
    [
        ("Translate", () => App.Settings.TranslateHotkey),
        ("OCR", () => App.Settings.OcrHotkey),
        ("Manual Translation", () => App.Settings.ManualHotkey),
        ("Clipboard OCR", () => App.Settings.ClipboardOcrHotkey),
        ("Selection Translation", () => App.Settings.SelectionHotkey),
        ("Screenshot Copy", () => App.Settings.ScreenshotCopyHotkey),
        ("Scan Barcodes", () => App.Settings.ScanHotkey),
        ("Settings", () => App.Settings.SettingsHotkey),
    ];

    private void OnLaunchAtLoginToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        try
        {
            SetLaunchAtLogin(LaunchAtLoginToggle.IsOn);
            LaunchErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            LaunchErrorText.Text = AppStrings.Get("Settings.General.RunKeyWriteFailed", "Could not write the Run key: {0}", ex.Message);
            LaunchErrorText.Visibility = Visibility.Visible;
            _loading = true;
            LaunchAtLoginToggle.IsOn = IsLaunchAtLoginSet();
            _loading = false;
        }
    }

    private static bool IsLaunchAtLoginSet()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(RunValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void SetLaunchAtLogin(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException(AppStrings.Get("Settings.General.RunKeyOpenFailed", "Could not open the Run key."));
        if (enable)
        {
            var exe = Environment.ProcessPath
                ?? throw new InvalidOperationException(AppStrings.Get("Settings.General.ExePathUnknown", "Could not find the exe path."));
            key.SetValue(RunValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }
}
