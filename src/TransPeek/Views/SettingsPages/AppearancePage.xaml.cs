// Appearance settings: text size for the popup panel's top bar
// (app name + slogan). More appearance options can join later.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Settings;

namespace TransPeek.Views.SettingsPages;

public sealed partial class AppearancePage : Page
{
    private Action? _onSaved;
    private bool _loading;

    public AppearancePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _onSaved = e.Parameter as Action;
        _loading = true;
        var s = App.Settings;
        FontSizeSlider.Value = s.PopupTopBarFontSize;
        FontSizeValueText.Text = FontSizeLabel(s.PopupTopBarFontSize);
        _loading = false;
    }

    private void Save()
    {
        if (_loading)
            return;
        SettingsStore.Save(App.Settings);
        _onSaved?.Invoke();
    }

    // Top-bar font size (Manh 2026-11-14): the slider sets the app-name
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
}
