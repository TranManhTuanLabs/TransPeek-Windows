// General settings: selection watching, hover delay, trigger auto-hide,
// translate hotkey, launch at login (registry Run key — the standard
// approach for unpackaged apps; no admin rights needed).

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Win32;
using TransPeek.Core.Settings;
using TransPeek.Services;
using Windows.System;
using Windows.UI.Core;

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
        FontSizeSlider.Value = s.PopupTopBarFontSize;
        FontSizeValueText.Text = FontSizeLabel(s.PopupTopBarFontSize);
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

    // Top-bar font size (Manh 2026-11-15): the slider sets the app-name
    // size; the tagline follows at 1 pt smaller (applied by the panel).
    // The label shows both so the -1 rule is visible in Settings.
    private void OnFontSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        App.Settings.PopupTopBarFontSize = e.NewValue;
        FontSizeValueText.Text = FontSizeLabel(e.NewValue);
        Save();
    }

    private static string FontSizeLabel(double size)
        => $"{size:0} pt — app name; tagline {Math.Max(8, size - 1):0} pt";

    private void OnTranslateHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(TranslateHotkeyBox, HotkeyErrorText, "Translate",
            s => App.Settings.TranslateHotkey = s, () => App.Settings.TranslateHotkey);

    private void OnOcrHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(OcrHotkeyBox, OcrHotkeyErrorText, "OCR from Screen",
            s => App.Settings.OcrHotkey = s, () => App.Settings.OcrHotkey);

    private void OnManualHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(ManualHotkeyBox, ManualHotkeyErrorText, "Manual Translation",
            s => App.Settings.ManualHotkey = s, () => App.Settings.ManualHotkey);

    private void OnClipboardOcrHotkeyLostFocus(object sender, RoutedEventArgs e)
        => TrySaveHotkey(ClipboardOcrHotkeyBox, ClipboardOcrHotkeyErrorText, "OCR to clipboard",
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

    // Hotkey recording (Manh 2026-11-14): click a hotkey box, then press
    // the key combination — the box records it, no typing "Control+Alt+T"
    // by hand. The text before recording is restored if the user cancels
    // (Esc) or clicks away mid-combination.
    private string _recordingOriginal = "";

    private void OnHotkeyBoxGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            _recordingOriginal = box.Text;
    }

    private void OnHotkeyBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        // Never type into the box: the keys ARE the hotkey.
        e.Handled = true;
        if (e.Key == VirtualKey.Escape)
        {
            box.Text = _recordingOriginal;
            Focus(FocusState.Programmatic);
            return;
        }
        var modifiers = HeldModifiers();
        if (IsModifierKey(e.Key))
        {
            // Modifier(s) only so far: show them + "…" and keep recording.
            box.Text = modifiers.Count == 0 ? "…" : string.Join("+", modifiers) + "+…";
            return;
        }
        if (modifiers.Count == 0)
        {
            // A bare key with no modifier would hijack normal typing as a
            // global hotkey — require at least one modifier, keep recording.
            box.Text = KeyToken(e.Key) + " (needs a modifier…)";
            return;
        }
        box.Text = string.Join("+", modifiers) + "+" + KeyToken(e.Key);
        // Save via the box's own LostFocus path (validates, checks
        // duplicates, restores on failure), then move focus away so the
        // next keypress does not start a new recording.
        SaveHotkeyForBox(box);
        Focus(FocusState.Programmatic);
    }

    private void SaveHotkeyForBox(TextBox box)
    {
        if (box == TranslateHotkeyBox)
            TrySaveHotkey(box, HotkeyErrorText, "Translate",
                s => App.Settings.TranslateHotkey = s, () => App.Settings.TranslateHotkey);
        else if (box == OcrHotkeyBox)
            TrySaveHotkey(box, OcrHotkeyErrorText, "OCR from Screen",
                s => App.Settings.OcrHotkey = s, () => App.Settings.OcrHotkey);
        else if (box == ManualHotkeyBox)
            TrySaveHotkey(box, ManualHotkeyErrorText, "Manual Translation",
                s => App.Settings.ManualHotkey = s, () => App.Settings.ManualHotkey);
        else if (box == ClipboardOcrHotkeyBox)
            TrySaveHotkey(box, ClipboardOcrHotkeyErrorText, "OCR to clipboard",
                s => App.Settings.ClipboardOcrHotkey = s, () => App.Settings.ClipboardOcrHotkey);
        else if (box == SelectionHotkeyBox)
            TrySaveHotkey(box, SelectionHotkeyErrorText, "Selection Translation",
                s => App.Settings.SelectionHotkey = s, () => App.Settings.SelectionHotkey);
        else if (box == ScreenshotCopyHotkeyBox)
            TrySaveHotkey(box, ScreenshotCopyHotkeyErrorText, "Screenshot Copy",
                s => App.Settings.ScreenshotCopyHotkey = s, () => App.Settings.ScreenshotCopyHotkey);
        else if (box == ScanHotkeyBox)
            TrySaveHotkey(box, ScanHotkeyErrorText, "Scan Barcodes",
                s => App.Settings.ScanHotkey = s, () => App.Settings.ScanHotkey);
        else if (box == SettingsHotkeyBox)
            TrySaveHotkey(box, SettingsHotkeyErrorText, "Settings",
                s => App.Settings.SettingsHotkey = s, () => App.Settings.SettingsHotkey);
    }

    private static bool IsModifierKey(VirtualKey key) => key is VirtualKey.Control
        or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
        or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
        or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static List<string> HeldModifiers()
    {
        var list = new List<string>();
        if (IsDown(VirtualKey.Control)) list.Add("Control");
        if (IsDown(VirtualKey.Shift)) list.Add("Shift");
        if (IsDown(VirtualKey.Menu)) list.Add("Alt");
        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) list.Add("Win");
        return list;
    }

    private static bool IsDown(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    /// <summary>VirtualKey to the token GlobalHotkeyService.Parse accepts
    /// (A-Z, 0-9, F1-F24, Space, Enter, Tab; anything else falls back to
    /// the key name and Parse rejects it with a clear error).</summary>
    private static string KeyToken(VirtualKey key)
    {
        if (key is >= VirtualKey.A and <= VirtualKey.Z)
            return ((char)key).ToString();
        if (key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
            return ((char)('0' + (key - VirtualKey.Number0))).ToString();
        if (key is >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9)
            return ((char)('0' + (key - VirtualKey.NumberPad0))).ToString();
        if (key is >= VirtualKey.F1 and <= VirtualKey.F24)
            return "F" + ((int)key - (int)VirtualKey.F1 + 1);
        return key switch
        {
            VirtualKey.Space => "Space",
            VirtualKey.Enter => "Enter",
            VirtualKey.Tab => "Tab",
            _ => key.ToString(),
        };
    }

    /// <summary>Validate one hotkey box (format + not already used by
    /// another action — Win32 would silently drop the second registration),
    /// save it, or show the error and restore the old text.</summary>
    private void TrySaveHotkey(TextBox box, TextBlock error, string actionName,
        Action<string> set, Func<string> get)
    {
        var spec = box.Text.Trim();
        // A half-recorded combination ("Control+…" / "T (needs a
        // modifier…)") is not a spec: restore the old text silently,
        // no error.
        if (spec.Contains('…'))
        {
            error.Visibility = Visibility.Collapsed;
            box.Text = get();
            return;
        }
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
        ("OCR from Screen", () => App.Settings.OcrHotkey),
        ("Manual Translation", () => App.Settings.ManualHotkey),
        ("OCR to clipboard", () => App.Settings.ClipboardOcrHotkey),
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
