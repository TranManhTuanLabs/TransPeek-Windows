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
    {
        var spec = TranslateHotkeyBox.Text.Trim();
        try
        {
            GlobalHotkeyService.Parse(spec); // validate
            HotkeyErrorText.Visibility = Visibility.Collapsed;
            App.Settings.TranslateHotkey = spec;
            Save();
        }
        catch (FormatException ex)
        {
            HotkeyErrorText.Text = ex.Message;
            HotkeyErrorText.Visibility = Visibility.Visible;
            TranslateHotkeyBox.Text = App.Settings.TranslateHotkey;
        }
    }

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
