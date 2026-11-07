// Dictionary settings: add/edit/delete lookup links (SPEC §3.2, mirror
// of mac Phase 1) and the Icon+Text / Icon-only display mode.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Settings;

namespace TransPeek.Views.SettingsPages;

public sealed partial class DictionaryPage : Page
{
    private Action? _onSaved;
    private bool _loading;

    public DictionaryPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _onSaved = e.Parameter as Action;
        _loading = true;
        RefreshLinks();
        foreach (var item in DisplayModeCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, App.Settings.DictionaryDisplayMode, StringComparison.Ordinal))
                DisplayModeCombo.SelectedItem = item;
        }
        _loading = false;
    }

    private void Save()
    {
        if (_loading)
            return;
        SettingsStore.Save(App.Settings);
        _onSaved?.Invoke();
    }

    private void RefreshLinks()
    {
        LinksList.ItemsSource = null;
        LinksList.ItemsSource = App.Settings.DictionaryLinks.ToList();
    }

    private void OnLinkSelected(object sender, SelectionChangedEventArgs e)
    {
        if (LinksList.SelectedItem is DictionaryLink link)
        {
            LinkNameBox.Text = link.Name;
            LinkTemplateBox.Text = link.UrlTemplate;
        }
    }

    private void OnAddLink(object sender, RoutedEventArgs e)
    {
        var error = DictionaryLink.ValidationError(LinkNameBox.Text, LinkTemplateBox.Text);
        if (error is not null)
        {
            LinkErrorText.Text = error;
            LinkErrorText.Visibility = Visibility.Visible;
            return;
        }
        LinkErrorText.Visibility = Visibility.Collapsed;
        App.Settings.DictionaryLinks.Add(new DictionaryLink
        {
            Name = LinkNameBox.Text.Trim(),
            UrlTemplate = LinkTemplateBox.Text.Trim(),
        });
        Save();
        RefreshLinks();
    }

    private void OnUpdateLink(object sender, RoutedEventArgs e)
    {
        if (LinksList.SelectedItem is not DictionaryLink link)
            return;
        var error = DictionaryLink.ValidationError(LinkNameBox.Text, LinkTemplateBox.Text);
        if (error is not null)
        {
            LinkErrorText.Text = error;
            LinkErrorText.Visibility = Visibility.Visible;
            return;
        }
        LinkErrorText.Visibility = Visibility.Collapsed;
        link.Name = LinkNameBox.Text.Trim();
        link.UrlTemplate = LinkTemplateBox.Text.Trim();
        Save();
        RefreshLinks();
    }

    private void OnDeleteLink(object sender, RoutedEventArgs e)
    {
        if (LinksList.SelectedItem is not DictionaryLink link)
            return;
        App.Settings.DictionaryLinks.Remove(link);
        Save();
        RefreshLinks();
    }

    private void OnDisplayModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DisplayModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string mode)
        {
            App.Settings.DictionaryDisplayMode = mode;
            Save();
        }
    }
}
