// OCR settings: engine (Tesseract default for Vietnamese), OCR
// languages, post-capture mode, auto-copy, and the Tesseract
// language-data manager (download vie/eng from tessdata_best on demand).
// (The capture hotkey used to be edited here too — removed 2026-11-20,
// it lives in General and edits the same setting.)

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Ocr;
using TransPeek.Core.Settings;
using TransPeek.Services;
using Windows.System;

namespace TransPeek.Views.SettingsPages;

public sealed partial class OcrPage : Page
{
    private Action? _onSaved;
    private bool _loading;

    public OcrPage()
    {
        InitializeComponent();
    }

    private readonly HashSet<string> _checkedCodes = new(StringComparer.Ordinal);
    private List<TesseractLanguageCatalog.Language> _extraLanguages = [];

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _onSaved = e.Parameter as Action;
        _loading = true;
        var s = App.Settings;
        SelectComboByTag(OcrProviderCombo, s.OcrProviderId);
        OcrLanguagesBox.Text = string.Join(", ", s.OcrLanguages);
        SelectComboByTag(PostModeCombo, s.OcrAfterCapture);
        AutoCopyToggle.IsOn = s.OcrAutoCopyTranslation;
        UpdateAutoCopyEnabled();
        RefreshTesseractStatus();
        RefreshWindowsOcrStatus();
        PopulateLanguagesList();
        _loading = false;
    }

    private void Save()
    {
        if (_loading)
            return;
        SettingsStore.Save(App.Settings);
        _onSaved?.Invoke();
    }

    private void RefreshTesseractStatus()
    {
        var provider = new TesseractOcrProvider();
        TesseractStatusText.Text = provider.Status;
        // Available = bundled with the app OR downloaded, so eng+vie
        // show up here on first run (they ship with the app).
        var installed = TesseractLanguageDataManager.AvailableCodes();
        InstalledCodesText.Text = installed.Count == 0
            ? AppStrings.Get("Settings.Ocr.NoLanguagesInstalled", "No languages installed.")
            : AppStrings.Get("Settings.Ocr.InstalledLanguages", "Installed: {0}", string.Join(", ", installed));
    }

    /// <summary>Windows OCR languages come from the OS, not from the
    /// app — list what this machine has (OcrEngine.
    /// AvailableRecognizerLanguages). If the language the user needs
    /// is missing, the button below opens Windows Settings so they
    /// can add it (the app cannot install a Windows language pack).</summary>
    private void RefreshWindowsOcrStatus()
    {
        var provider = new WindowsOcrProvider();
        var installed = provider.SupportedLanguages;
        WindowsOcrLanguagesText.Text = installed.Count == 0
            ? AppStrings.Get("Settings.Ocr.NoWindowsLanguages", "No OCR language installed on this machine.")
            : AppStrings.Get("Settings.Ocr.WindowsLanguagesInstalled", "Installed: {0}", string.Join(", ", installed));
    }

    private async void OnOpenWindowsSettings(object sender, RoutedEventArgs e)
        => await Launcher.LaunchUriAsync(new Uri("ms-settings:regionlanguage"));

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OcrProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            // Diagnostic (0.1.3.59 test: switching Tesseract -> Windows
            // OCR exits the app): timestamp the switch in crash.log, so
            // the next log shows whether the exit follows the switch.
            CrashLog.WriteLine("OcrProvider", $"Switched to '{id}'");
            App.Settings.OcrProviderId = id;
            Save();
        }
    }

    private void OnLanguagesLostFocus(object sender, RoutedEventArgs e)
    {
        var codes = OcrLanguagesBox.Text.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => c.Length > 0).ToList();
        if (codes.Count == 0)
            codes = ["vi-VN", "en-US"];
        App.Settings.OcrLanguages = codes;
        Save();
    }

    private void OnPostModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PostModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string mode)
        {
            App.Settings.OcrAfterCapture = mode;
            Save();
            UpdateAutoCopyEnabled();
        }
    }

    /// <summary>Mac parity: auto-copy needs a translation, so the toggle
    /// is disabled in CopyOnly mode (OCR copies the source text instead).</summary>
    private void UpdateAutoCopyEnabled()
    {
        var copyOnly = string.Equals(App.Settings.OcrAfterCapture, "CopyOnly", StringComparison.OrdinalIgnoreCase);
        AutoCopyToggle.IsEnabled = !copyOnly;
    }

    private void OnAutoCopyToggled(object sender, RoutedEventArgs e)
    {
        App.Settings.OcrAutoCopyTranslation = AutoCopyToggle.IsOn;
        Save();
    }

    /// <summary>The language list: one CheckBox per catalog language
    /// (plus any extra languages found live by Refresh), showing the
    /// name, the Tesseract code, and whether it is already installed.
    /// Checked state lives in <see cref="_checkedCodes"/> so typing in
    /// the find box does not lose the selection. Installed languages
    /// can still be ticked to re-download them.</summary>
    private void PopulateLanguagesList()
    {
        var query = LanguageFindBox.Text.Trim();
        var installed = TesseractLanguageDataManager.AvailableCodes().ToHashSet(StringComparer.Ordinal);
        var languages = TesseractLanguageCatalog.Languages.Concat(_extraLanguages)
            .DistinctBy(l => l.Code, StringComparer.Ordinal)
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Where(l => query.Length == 0
                || l.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || l.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        LanguagesExpander.Header = AppStrings.Get("Settings.Ocr.AllLanguages",
            "All Tesseract languages ({0})", TesseractLanguageCatalog.Languages.Count + _extraLanguages.Count);
        LanguagesListView.Items.Clear();
        foreach (var lang in languages)
        {
            var box = new CheckBox
            {
                Content = installed.Contains(lang.Code)
                    ? $"{lang.Name} ({lang.Code}) — installed"
                    : $"{lang.Name} ({lang.Code})",
                Tag = lang.Code,
                IsChecked = _checkedCodes.Contains(lang.Code),
            };
            box.Checked += OnLanguageChecked;
            box.Unchecked += OnLanguageChecked;
            LanguagesListView.Items.Add(box);
        }
        UpdateSizeWarning();
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && box.Tag is string code)
        {
            if (box.IsChecked == true)
                _checkedCodes.Add(code);
            else
                _checkedCodes.Remove(code);
            UpdateSizeWarning();
        }
    }

    private void OnLanguageFindChanged(object sender, TextChangedEventArgs e)
        => PopulateLanguagesList();

    /// <summary>Size warning (Manh: warn about download size, no
    /// "Download all"). Files vary a lot — eng ~15 MB, vie ~12 MB —
    /// so the warning names the count and the typical range instead
    /// of a fake exact total.</summary>
    private void UpdateSizeWarning()
    {
        if (_checkedCodes.Count == 0)
        {
            SizeWarningText.Visibility = Visibility.Collapsed;
            return;
        }
        SizeWarningText.Text = AppStrings.Get("Settings.Ocr.SizeWarning",
            "{0} language(s) selected. Language files are large (eng ~15 MB, vie ~12 MB) — downloading takes a while and uses significant disk space.",
            _checkedCodes.Count);
        SizeWarningText.Visibility = Visibility.Visible;
    }

    /// <summary>Refresh: fetch the live language list from GitHub and
    /// add any codes the hardcoded catalog does not know yet (shown
    /// with the code as their name). A failure (no network, GitHub
    /// rate limit) keeps the hardcoded list and says so.</summary>
    private async void OnRefreshLanguages(object sender, RoutedEventArgs e)
    {
        RefreshLanguagesButton.IsEnabled = false;
        try
        {
            var live = await TesseractLanguageCatalog.FetchLiveCodesAsync();
            var known = TesseractLanguageCatalog.Languages.Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
            _extraLanguages = live.Where(c => !known.Contains(c))
                .Select(c => new TesseractLanguageCatalog.Language(c, c))
                .ToList();
            PopulateLanguagesList();
            DownloadStatusText.Text = _extraLanguages.Count == 0
                ? AppStrings.Get("Settings.Ocr.RefreshNoNew", "Language list is up to date.")
                : AppStrings.Get("Settings.Ocr.RefreshNew", "Found {0} new language(s).", _extraLanguages.Count);
        }
        catch (Exception ex)
        {
            DownloadStatusText.Text = AppStrings.Get("Settings.Ocr.RefreshError", "Could not refresh the language list: {0}", ex.Message);
        }
        finally
        {
            RefreshLanguagesButton.IsEnabled = true;
        }
    }

    private async void OnDownloadSelected(object sender, RoutedEventArgs e)
        => await DownloadLanguagesAsync(_checkedCodes.OrderBy(c => c, StringComparer.Ordinal).ToArray());

    private async Task DownloadLanguagesAsync(string[] codes)
    {
        if (codes.Length == 0)
        {
            DownloadStatusText.Text = AppStrings.Get("Settings.Ocr.NothingSelected", "Tick one or more languages first.");
            return;
        }
        SetDownloadButtonsEnabled(false);
        try
        {
            var progress = new Progress<TesseractLanguageDataManager.DownloadProgress>(p =>
                DownloadStatusText.Text = AppStrings.Get("Settings.Ocr.DownloadingLanguage",
                    "Downloading {0}… ({1}/{2})", p.Code, p.Done + 1, p.Total));
            var result = await TesseractLanguageDataManager.DownloadManyAsync(codes, progress);
            var parts = new List<string>();
            if (result.Downloaded.Count > 0)
                parts.Add(AppStrings.Get("Settings.Ocr.DownloadedLanguages", "Downloaded: {0}.", string.Join(", ", result.Downloaded)));
            foreach (var (code, error) in result.Failed)
                parts.Add(AppStrings.Get("Settings.Ocr.DownloadFailedOne", "{0} failed: {1}", code, error));
            if (result.Downloaded.Count > 0)
            {
                // Auto-add the new languages to the OCR Languages
                // setting (BCP 47, Manh 2026-11-21) so the user can
                // OCR with them right away — no hand-typing codes
                // (easy to mistype, e.g. "vie" instead of "vi-VN").
                // The setting and the textbox above update together.
                var missing = result.Downloaded
                    .Select(TesseractLanguageDataManager.Bcp47ForTessCode)
                    .Where(b => !App.Settings.OcrLanguages.Contains(b, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (missing.Count > 0)
                {
                    App.Settings.OcrLanguages = App.Settings.OcrLanguages.Concat(missing).ToList();
                    OcrLanguagesBox.Text = string.Join(", ", App.Settings.OcrLanguages);
                    Save();
                    parts.Add(AppStrings.Get("Settings.Ocr.SuggestLanguages",
                        "Added to OCR languages: {0}.", string.Join(", ", missing)));
                }
            }
            DownloadStatusText.Text = string.Join(" ", parts);
            _checkedCodes.Clear();
            RefreshTesseractStatus();
            PopulateLanguagesList();
        }
        catch (Exception ex)
        {
            DownloadStatusText.Text = AppStrings.Get("Settings.Ocr.DownloadError", "Download failed: {0}", ex.Message);
        }
        finally
        {
            SetDownloadButtonsEnabled(true);
        }
    }

    private void SetDownloadButtonsEnabled(bool enabled)
    {
        DownloadSelectedButton.IsEnabled = enabled;
        RefreshLanguagesButton.IsEnabled = enabled;
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
