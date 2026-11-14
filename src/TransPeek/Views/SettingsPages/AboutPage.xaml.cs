// About (Settings > About): app icon + name + slogan, GitHub icon +
// repo link, link to all releases (with the running version + CI build
// in its text), and the simple update check (Check for Updates).
//
// Update check (Manh's decision, 2026-11-08): SIMPLE CHECK ONLY — call
// the GitHub Releases API, compare the latest tag with the running
// version, and when a newer release exists offer a link to the release
// page. The user downloads/installs by hand. No updater library, no
// auto-download/install. Velopack (auto download + install + relaunch)
// is deferred to release time.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TransPeek.Assets;
using TransPeek.Core.Update;
using TransPeek.Services;
using Windows.System;

namespace TransPeek.Views.SettingsPages;

public sealed partial class AboutPage : Page
{
    private string? _latestReleaseUrl;

    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // "Release 0.1.3 (Build CI #59)" — the version line lives
            // here now that the separate Version line is gone (Manh
            // 2026-11-12). Template (Manh 2026-11-13, mirrors mac
            // option B): "Release" without "Ver", and the build number
            // only inside "(Build CI #...)". Local builds (no CI
            // number) show no build part. The line is a link to the
            // Releases page (OnReleaseNotesClick).
            ReleaseNotesLink.Content = AppInfo.CiBuildNumber > 0
                ? $"Release {AppInfo.Version} (Build CI #{AppInfo.CiBuildNumber})"
                : $"Release {AppInfo.Version}";
            GithubMarkImage.Source = GitHubMark.Image();
            AppIconImage.Source = EmbeddedIcon.Image();
            SupportAccountText.Text = AppInfo.DonateAccountBlock;
        };
    }

    private async void OnRepoLinkClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(AppInfo.RepositoryUrl));
    }

    private async void OnReleaseNotesClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(AppInfo.ReleasesUrl));
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = AppStrings.Get("Settings.About.Checking", "Checking…");
        try
        {
            var current = UpdateChecker.ParseVersion(AppInfo.Version) ?? new Version(0, 1, 0);
            var result = await new UpdateChecker().CheckAsync(current);
            switch (result.Status)
            {
                case UpdateCheckStatus.UpToDate:
                    UpdateStatusText.Text = AppStrings.Get("Settings.About.UpToDate", "You're on the latest version (v{0}).", AppInfo.Version);
                    break;
                case UpdateCheckStatus.UpdateAvailable:
                    UpdateStatusText.Text = AppStrings.Get("Settings.About.UpdateAvailable", "New version available: v{0} (you're on v{1}).", result.LatestVersion, AppInfo.Version);
                    _latestReleaseUrl = result.ReleaseUrl;
                    DownloadUpdateButton.Content = AppStrings.Get("Settings.About.DownloadUpdate", "Download v{0}", result.LatestVersion);
                    DownloadUpdateButton.Visibility = Visibility.Visible;
                    break;
                case UpdateCheckStatus.Error:
                default:
                    UpdateStatusText.Text = result.ErrorMessage ?? AppStrings.Get("Settings.About.CheckFailed", "Could not check.");
                    break;
            }
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_latestReleaseUrl is not null)
            await Launcher.LaunchUriAsync(new Uri(_latestReleaseUrl));
    }

    /// <summary>Donate QR dialog (mirrors mac's popover): the full BIDV
    /// VietQR card at 280px wide (the QR inside is ~165px, scannable)
    /// plus the account line underneath. ContentDialog, not a flyout:
    /// a modal dialog stays put while the user scans it with a phone.</summary>
    private async void OnShowQrClick(object sender, RoutedEventArgs e)
    {
        var image = new Image
        {
            Source = DonateCardImage(),
            Width = 280,
            Stretch = Stretch.Uniform,
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(image);
        content.Children.Add(new TextBlock
        {
            Text = AppInfo.DonateAccountBlock,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Content = content,
            CloseButtonText = AppStrings.Get("Settings.About.Support.QrDialog.Close", "Close"),
        };
        await dialog.ShowAsync();
    }

    private async void OnCopyAccountClick(object sender, RoutedEventArgs e)
    {
        await ClipboardHelper.CopyTextAsync(AppInfo.DonateAccountNumber);
        // Brief inline feedback (the About page has no toast): show
        // "Copied" on the button for 2 seconds, then restore the label.
        CopyAccountButton.IsEnabled = false;
        var original = CopyAccountText.Text;
        CopyAccountText.Text = AppStrings.Get("Settings.About.Support.Copied", "Copied");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            CopyAccountText.Text = original;
            CopyAccountButton.IsEnabled = true;
        };
        timer.Start();
    }

    /// <summary>Load the donate card image. File path first (unpackaged
    /// app: the card ships as a loose file next to the exe): ms-appx
    /// only resolves when the Content file actually reached the publish
    /// folder — in 0.1.3.59 it did not (no CopyToOutputDirectory), so
    /// the dialog showed text only. Log a missing file so the next
    /// crash.log tells whether the file shipped.</summary>
    private static BitmapImage DonateCardImage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "DonateCard.png");
        if (File.Exists(path))
            return new BitmapImage(new Uri(path));
        CrashLog.WriteLine("DonateCard", $"Card file missing at {path}; falling back to ms-appx.");
        return new BitmapImage(new Uri(AppInfo.DonateCardUri));
    }

    private void OnRestartClick(object sender, RoutedEventArgs e) => App.Restart();
}
