// OCR settings: engine (Tesseract default for Vietnamese), OCR
// languages, post-capture mode, auto-copy, hotkey, and the Tesseract
// language-data manager (download vie/eng from tessdata_best on demand).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Ocr;
using TransPeek.Core.Settings;
using TransPeek.Services;

namespace TransPeek.Views.SettingsPages;

public sealed partial class OcrPage : Page
{
    private Action? _onSaved;
    private bool _loading;

    public OcrPage()
    {
        InitializeComponent();
    }

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
        OcrHotkeyBox.Text = s.OcrHotkey;
        RefreshTesseractStatus();
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
        var installed = TesseractLanguageDataManager.InstalledCodes();
        InstalledCodesText.Text = installed.Count == 0
            ? AppStrings.Get("Settings.Ocr.NoLanguagesInstalled", "No languages installed.")
            : AppStrings.Get("Settings.Ocr.InstalledLanguages", "Installed: {0}", string.Join(", ", installed));
    }

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OcrProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
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

    private void OnHotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        var spec = OcrHotkeyBox.Text.Trim();
        try
        {
            GlobalHotkeyService.Parse(spec);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
            App.Settings.OcrHotkey = spec;
            Save();
        }
        catch (FormatException ex)
        {
            HotkeyErrorText.Text = ex.Message;
            HotkeyErrorText.Visibility = Visibility.Visible;
            OcrHotkeyBox.Text = App.Settings.OcrHotkey;
        }
    }

    private async void OnDownloadVie(object sender, RoutedEventArgs e)
        => await DownloadLanguagesAsync(["vie"]);

    private async void OnDownloadEng(object sender, RoutedEventArgs e)
        => await DownloadLanguagesAsync(["eng"]);

    private async void OnDownloadBoth(object sender, RoutedEventArgs e)
        => await DownloadLanguagesAsync(["vie", "eng"]);

    private async Task DownloadLanguagesAsync(string[] codes)
    {
        SetDownloadButtonsEnabled(false);
        try
        {
            foreach (var code in codes)
            {
                DownloadStatusText.Text = AppStrings.Get("Settings.Ocr.DownloadingLanguage", "Downloading {0}…", code);
                await TesseractLanguageDataManager.DownloadAsync(code);
            }
            DownloadStatusText.Text = AppStrings.Get("Settings.Ocr.DownloadedLanguages", "Downloaded: {0}.", string.Join(", ", codes));
            RefreshTesseractStatus();
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
        DownloadVieButton.IsEnabled = enabled;
        DownloadEngButton.IsEnabled = enabled;
        DownloadBothButton.IsEnabled = enabled;
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
