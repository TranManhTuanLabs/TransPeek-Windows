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
            // "Release Ver 0.1.3 (Build CI #51)" — the version line lives
            // here now that the separate Version line is gone (Manh
            // 2026-11-12). Local builds (no CI number) show no build part.
            ReleaseNotesLink.Content = AppInfo.CiBuildNumber > 0
                ? $"Release Ver {AppInfo.Version} (Build CI #{AppInfo.CiBuildNumber})"
                : $"Release Ver {AppInfo.Version}";
            GithubMarkImage.Source = GitHubMark.Image();
            AppIconImage.Source = EmbeddedIcon.Image();
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
}
