// About (Settings > About): app name + running version/build, author line
// with GitHub icon + repo link, release-notes link for the running
// version, and the simple update check (Check for Updates).
//
// Update check (Manh's decision, 2026-11-08): SIMPLE CHECK ONLY — call
// the GitHub Releases API, compare the latest tag with the running
// version, and when a newer release exists offer a link to the release
// page. The user downloads/installs by hand. No updater library, no
// auto-download/install. Velopack (auto download + install + relaunch)
// is deferred to release time.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Update;
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
            VersionText.Text = $"Version {AppInfo.Version} (build {AppInfo.Build})";
            ReleaseNotesLink.Content = $"Release notes (v{AppInfo.Version})";
        };
    }

    private async void OnRepoLinkClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(AppInfo.RepositoryUrl));
    }

    private async void OnReleaseNotesClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(AppInfo.ReleaseNotesUrl));
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = "Đang kiểm tra…";
        try
        {
            var current = UpdateChecker.ParseVersion(AppInfo.Version) ?? new Version(0, 1, 0);
            var result = await new UpdateChecker().CheckAsync(current);
            switch (result.Status)
            {
                case UpdateCheckStatus.UpToDate:
                    UpdateStatusText.Text = $"Bạn đang dùng bản mới nhất (v{AppInfo.Version}).";
                    break;
                case UpdateCheckStatus.UpdateAvailable:
                    UpdateStatusText.Text =
                        $"Có bản mới: v{result.LatestVersion} (bạn đang dùng v{AppInfo.Version}).";
                    _latestReleaseUrl = result.ReleaseUrl;
                    DownloadUpdateButton.Content = $"Tải bản mới v{result.LatestVersion}";
                    DownloadUpdateButton.Visibility = Visibility.Visible;
                    break;
                case UpdateCheckStatus.Error:
                default:
                    UpdateStatusText.Text = result.ErrorMessage ?? "Không kiểm tra được.";
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
