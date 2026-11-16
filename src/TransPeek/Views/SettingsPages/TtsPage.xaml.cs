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

    /// <summary>Does a voice language belong to a language prefix?
    /// Matches the bare code ("en") as well as a region tag ("en-US",
    /// "en-GB"). The old StartsWith(prefix + "-") missed voices whose
    /// Language is just the bare code, so a newly installed voice did
    /// not show up after Refresh (Manh 2026-11-16).</summary>
    private static bool MatchesLanguagePrefix(string language, string prefix)
        => language.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || language.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase);

    /// <summary>English accents = the distinct Language tags of the
    /// installed English voices (en-US, en-GB, ...). The tag is both
    /// the display text and the saved value. Falls back to en-US when
    /// no English voice is installed.</summary>
    private void PopulateEnglishAccents(string selected)
    {
        var accents = SystemSpeechService.InstalledVoices
            .Where(v => MatchesLanguagePrefix(v.Language, "en"))
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
    /// (Tag ""), then the installed voices for THAT language by display
    /// name (Manh 2026-11-16: the EN and VI pickers must NOT both show
    /// the same all-voices list — that was confusing. Each picker shows
    /// only its own language; the full list lives under a separate
    /// "All voices" button below). The Tag is the voice Id, saved to
    /// settings.</summary>
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
            if (!MatchesLanguagePrefix(voice.Language, languagePrefix))
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

    /// <summary>Open Windows Settings → Language &amp; region so the
    /// user can add a voice (Manh 2026-11-16: the old target,
    /// Accessibility → Narrator, was wrong — TTS voices come from
    /// Windows language packs, so adding a voice means adding its
    /// language in Language &amp; region, then Refresh here. This is
    /// the only button for it now; the separate "Open Windows
    /// Settings" button that opened the same place is gone).</summary>
    private async void OnAddVoices(object sender, RoutedEventArgs e)
        => await Launcher.LaunchUriAsync(new Uri("ms-settings:regionlanguage"));

    /// <summary>Show (or hide) the full list of every installed voice
    /// in the AllVoicesListView. The EN / VI pickers above show only
    /// their own language; this is the separate "All voices" list
    /// (Manh 2026-11-16: the two pickers must not both show the same
    /// all-voices list). Read-only — it only shows what Windows has
    /// installed; to pick a voice, use the EN / VI picker.</summary>
    private void OnShowAllVoices(object sender, RoutedEventArgs e)
    {
        if (AllVoicesListView.Visibility == Visibility.Visible)
        {
            AllVoicesListView.Visibility = Visibility.Collapsed;
            return;
        }
        PopulateAllVoices();
        AllVoicesListView.Visibility = Visibility.Visible;
    }

    private void PopulateAllVoices()
    {
        AllVoicesListView.Items.Clear();
        foreach (var voice in SystemSpeechService.InstalledVoices)
        {
            AllVoicesListView.Items.Add(new TextBlock
            {
                Text = $"{voice.DisplayName} ({voice.Language})",
            });
        }
    }

    /// <summary>Re-read the installed voices (English accents + both
    /// voice pickers). The lists are only populated when the page is
    /// navigated to, so a voice installed via "Add voices" while this
    /// tab is open would not show up until the user clicks Refresh
    /// (Manh 2026-11-16: installed a voice, list did not update).
    /// Sets _loading while repopulating so the SelectionChanged
    /// handlers do not fire Save() (and its "settings saved" toast)
    /// for a programmatic re-select (Manh 2026-11-16: Refresh showed
    /// a save toast but the new voice was not in the list).</summary>
    private void OnRefreshVoices(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            var s = App.Settings;
            PopulateEnglishAccents(s.EnglishAccent);
            PopulateVoices(EnglishVoiceCombo, "en", s.EnglishVoiceId);
            PopulateVoices(VietnameseVoiceCombo, "vi", s.VietnameseVoiceId);
            if (AllVoicesListView.Visibility == Visibility.Visible)
                PopulateAllVoices();
        }
        finally
        {
            _loading = false;
        }
    }

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
