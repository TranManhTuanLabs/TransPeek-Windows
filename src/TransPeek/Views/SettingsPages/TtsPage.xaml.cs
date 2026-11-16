// TTS Speech settings (Manh 2026-11-16, mirrors the mac audio tab):
// Service (system built-in), Preferences (English accent, default
// speech rate, auto-play source/translation), Voices (EN / VI — the
// exact voice per language), and a Test row to hear a voice. No
// "target accent" row: the per-language voice pickers in Voices
// already cover it (Manh cancelled it 2026-11-16).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Settings;
using TransPeek.Services;
using Windows.Media.SpeechSynthesis;
using Windows.System;

namespace TransPeek.Views.SettingsPages;

public sealed partial class TtsPage : Page
{
    private Action? _onSaved;
    private bool _loading;
    private SystemSpeechService? _testSpeech;

    public TtsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _onSaved = e.Parameter as Action;
        _loading = true;
        var s = App.Settings;
        SelectComboByTag(TtsServiceCombo, s.TtsService);
        PopulateEnglishAccents(s.EnglishAccent);
        SelectComboByTag(SpeechRateCombo, FormatRate(s.SpeechRate));
        AutoPlaySourceToggle.IsOn = s.AutoPlaySource;
        AutoPlayTranslationToggle.IsOn = s.AutoPlayTranslation;
        PopulateVoices(EnglishVoiceCombo, "en", s.EnglishVoiceId);
        PopulateVoices(VietnameseVoiceCombo, "vi", s.VietnameseVoiceId);
        TestLanguageCombo.SelectedIndex = 0;
        _loading = false;
    }

    private void Save()
    {
        if (_loading)
            return;
        SettingsStore.Save(App.Settings);
        _onSaved?.Invoke();
    }

    /// <summary>English accents = the distinct Language tags of the
    /// installed English voices (en-US, en-GB, ...). The tag is both
    /// the display text and the saved value. Falls back to en-US when
    /// no English voice is installed.</summary>
    private void PopulateEnglishAccents(string selected)
    {
        var accents = SystemSpeechService.InstalledVoices
            .Where(v => v.Language.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Language)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (accents.Count == 0)
            accents = ["en-US"];
        EnglishAccentCombo.Items.Clear();
        foreach (var accent in accents)
            EnglishAccentCombo.Items.Add(new ComboBoxItem { Content = accent, Tag = accent });
        SelectComboByTag(EnglishAccentCombo, selected);
    }

    /// <summary>Voice picker for one language: "System default" first
    /// (Tag ""), then the installed voices for that language by
    /// display name. The Tag is the voice Id, saved to settings.</summary>
    private void PopulateVoices(ComboBox combo, string languagePrefix, string? selectedId)
    {
        combo.Items.Clear();
        combo.Items.Add(new ComboBoxItem
        {
            Content = AppStrings.Get("Settings.Tts.SystemDefaultVoice", "System default"),
            Tag = "",
        });
        foreach (var voice in SystemSpeechService.InstalledVoices)
        {
            if (!voice.Language.StartsWith(languagePrefix + "-", StringComparison.OrdinalIgnoreCase))
                continue;
            combo.Items.Add(new ComboBoxItem { Content = voice.DisplayName, Tag = voice.Id });
        }
        SelectComboByTag(combo, selectedId ?? "");
    }

    private static string FormatRate(double rate)
        => rate == 1.0 ? "1" : rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private void OnServiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TtsServiceCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.TtsService = id;
            Save();
        }
    }

    private void OnEnglishAccentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EnglishAccentCombo.SelectedItem is ComboBoxItem item && item.Tag is string accent)
        {
            App.Settings.EnglishAccent = accent;
            Save();
        }
    }

    private void OnSpeechRateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeechRateCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && double.TryParse(tag, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rate))
        {
            App.Settings.SpeechRate = rate;
            Save();
        }
    }

    private void OnAutoPlaySourceToggled(object sender, RoutedEventArgs e)
    {
        App.Settings.AutoPlaySource = AutoPlaySourceToggle.IsOn;
        Save();
    }

    private void OnAutoPlayTranslationToggled(object sender, RoutedEventArgs e)
    {
        App.Settings.AutoPlayTranslation = AutoPlayTranslationToggle.IsOn;
        Save();
    }

    private void OnEnglishVoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EnglishVoiceCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.EnglishVoiceId = string.IsNullOrEmpty(id) ? null : id;
            Save();
        }
    }

    private void OnVietnameseVoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VietnameseVoiceCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.VietnameseVoiceId = string.IsNullOrEmpty(id) ? null : id;
            Save();
        }
    }

    /// <summary>Voices come from Windows (the Narrator voices) — open
    /// Accessibility → Narrator so the user can add a voice in
    /// "Narrator's voice" (the app cannot install a Windows voice).
    /// Moved here from Settings → General (Manh 2026-11-16): all
    /// voice settings live in this tab now.</summary>
    private async void OnAddVoices(object sender, RoutedEventArgs e)
        => await Launcher.LaunchUriAsync(new Uri("ms-settings:easeofaccess-narrator"));

    /// <summary>Play the test text in the chosen test language, using
    /// the voice/rate the user just picked (PickVoice reads the saved
    /// settings). A second click stops it.</summary>
    private async void OnPlayTest(object sender, RoutedEventArgs e)
    {
        var text = TestTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;
        var lang = TestLanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag
            ? tag
            : "en";
        _testSpeech ??= new SystemSpeechService();
        await _testSpeech.SpeakAsync(text, lang);
    }

    private void OnStopTest(object sender, RoutedEventArgs e)
        => _testSpeech?.Stop();

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
