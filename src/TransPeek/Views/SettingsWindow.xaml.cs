// Settings window (SPEC §2): NavigationView with one page per section.
// Pages are created via Frame.Navigate(type, onSaved) — the callback lets
// a page tell the app "settings changed, rebuild providers / hotkeys".
// Changes save immediately (no Apply button): every control writes to
// App.Settings + SettingsStore on change.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Views.SettingsPages;

namespace TransPeek.Views;

public sealed partial class SettingsWindow : Window
{
    /// <summary>Raised after a page saved settings (App rebuilds providers,
    /// hotkeys, and the panel's dictionary bar).</summary>
    public event Action? SettingsSaved;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = "TransPeek Settings";
        // WinUI 3 Window has no Loaded event (Loaded lives on
        // FrameworkElement): attach to the root content element instead.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                NavView.SelectedItem = NavView.MenuItems[0];
                SelectPage("General");
            };
        }
    }

    public void ShowSettings()
    {
        Activate();
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            SelectPage(tag);
    }

    private void SelectPage(string tag)
    {
        Type? pageType = tag switch
        {
            "General" => typeof(GeneralPage),
            "Translation" => typeof(TranslationPage),
            "Dictionary" => typeof(DictionaryPage),
            "OCR" => typeof(OcrPage),
            "About" => typeof(AboutPage),
            _ => null,
        };
        if (pageType is not null)
            ContentFrame.Navigate(pageType, (Action)NotifySaved);
    }

    private void NotifySaved() => SettingsSaved?.Invoke();
}
