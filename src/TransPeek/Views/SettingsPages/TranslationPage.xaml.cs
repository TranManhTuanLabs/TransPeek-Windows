// Translation settings: primary/fallback provider, source/target
// language, per-provider enable toggles, and custom OpenAI-compatible
// endpoints (add/edit/delete). No Apple Translation on Windows — it does
// not exist there (see README).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using TransPeek.Services;

namespace TransPeek.Views.SettingsPages;

public sealed partial class TranslationPage : Page
{
    private Action? _onSaved;
    private bool _loading;
    private CustomProviderDefinition? _editing;

    public TranslationPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _onSaved = e.Parameter as Action;
        _loading = true;
        BuildLanguageCombos();
        BuildProviderCombos();
        BuildProviderToggles();
        BuildCustomList();
        _loading = false;
    }

    private void Save()
    {
        if (_loading)
            return;
        SettingsStore.Save(App.Settings);
        _onSaved?.Invoke();
    }

    // ----- languages -----

    private void BuildLanguageCombos()
    {
        SourceLanguageCombo.Items.Clear();
        SourceLanguageCombo.Items.Add(new ComboBoxItem { Content = AppStrings.Get("Settings.Translation.Auto", "Automatic"), Tag = LanguageCode.Auto });
        foreach (var (code, name) in LanguageCode.Supported)
            SourceLanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        SelectComboByTag(SourceLanguageCombo, App.Settings.SourceLanguage);

        TargetLanguageCombo.Items.Clear();
        foreach (var (code, name) in LanguageCode.Supported)
            TargetLanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        SelectComboByTag(TargetLanguageCombo, App.Settings.TargetLanguage);
    }

    // ----- providers -----

    private void BuildProviderCombos()
    {
        FillProviderCombo(PrimaryProviderCombo, App.Settings.PrimaryProviderId);
        FillProviderCombo(FallbackProviderCombo, App.Settings.FallbackProviderId);
    }

    private static void FillProviderCombo(ComboBox combo, string selectedId)
    {
        combo.Items.Clear();
        foreach (var p in App.Providers)
        {
            combo.Items.Add(new ComboBoxItem
            {
                Content = p.DisplayName + (p.NeedsApiKey ? AppStrings.Get("Settings.Translation.RequiresKey", " (requires key)") : ""),
                Tag = p.Id,
            });
        }
        SelectComboByTag(combo, selectedId);
    }

    private void BuildProviderToggles()
    {
        ProviderToggles.Children.Clear();
        foreach (var p in App.Providers)
        {
            var check = new CheckBox
            {
                Content = p.DisplayName,
                IsChecked = App.Settings.IsProviderEnabled(p.Id),
                Tag = p.Id,
            };
            check.Checked += (_, _) => ToggleProvider(check, true);
            check.Unchecked += (_, _) => ToggleProvider(check, false);
            ProviderToggles.Children.Add(check);
        }
    }

    private void ToggleProvider(CheckBox check, bool enabled)
    {
        if (check.Tag is string id)
        {
            App.Settings.SetProviderEnabled(id, enabled);
            Save();
        }
    }

    private void OnPrimaryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PrimaryProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.PrimaryProviderId = id;
            Save();
        }
    }

    private void OnFallbackChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FallbackProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.FallbackProviderId = id;
            Save();
        }
    }

    private void OnSourceLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceLanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.SourceLanguage = id;
            Save();
        }
    }

    private void OnTargetLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetLanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.TargetLanguage = id;
            Save();
        }
    }

    // ----- custom providers -----

    private void BuildCustomList()
    {
        CustomList.ItemsSource = null;
        CustomList.ItemsSource = App.Settings.CustomProviders
            .Select(d => $"{d.Name} — {d.BaseUrl} ({d.Model})").ToList();
    }

    private void OnCustomSelected(object sender, SelectionChangedEventArgs e)
    {
        var index = CustomList.SelectedIndex;
        _editing = index >= 0 && index < App.Settings.CustomProviders.Count
            ? App.Settings.CustomProviders[index]
            : null;
        if (_editing is not null)
        {
            CustomNameBox.Text = _editing.Name;
            CustomBaseUrlBox.Text = _editing.BaseUrl;
            CustomModelBox.Text = _editing.Model;
            CustomApiKeyBox.Password = _editing.ApiKey;
        }
        else
        {
            CustomNameBox.Text = "";
            CustomBaseUrlBox.Text = "";
            CustomModelBox.Text = "";
            CustomApiKeyBox.Password = "";
        }
    }

    private void OnCustomEdited(object sender, RoutedEventArgs e)
    {
        if (_editing is null)
            return;
        _editing.Name = CustomNameBox.Text.Trim();
        _editing.BaseUrl = CustomBaseUrlBox.Text.Trim();
        _editing.Model = CustomModelBox.Text.Trim();
        _editing.ApiKey = CustomApiKeyBox.Password;
        Save();
        BuildCustomList();
        BuildProviderCombos();
        BuildProviderToggles();
    }

    private void OnAddOpenAi(object sender, RoutedEventArgs e) => AddCustom(CustomProviderDefinition.OpenAiDefault());

    private void OnAddDeepSeek(object sender, RoutedEventArgs e) => AddCustom(CustomProviderDefinition.DeepSeekDefault());

    private void AddCustom(CustomProviderDefinition definition)
    {
        App.Settings.CustomProviders.Add(definition);
        Save();
        BuildCustomList();
        BuildProviderCombos();
        BuildProviderToggles();
        CustomList.SelectedIndex = App.Settings.CustomProviders.Count - 1;
    }

    private void OnDeleteCustom(object sender, RoutedEventArgs e)
    {
        var index = CustomList.SelectedIndex;
        if (index < 0 || index >= App.Settings.CustomProviders.Count)
            return;
        App.Settings.CustomProviders.RemoveAt(index);
        _editing = null;
        Save();
        BuildCustomList();
        BuildProviderCombos();
        BuildProviderToggles();
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }
}
