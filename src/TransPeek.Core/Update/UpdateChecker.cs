// Simple update check (SPEC §5, Settings > About > Check for Updates).
//
// DELIBERATE v0.1 LIMIT (Manh's decision, 2026-11-08): this only CHECKS.
// It calls the GitHub Releases API for the latest release, compares the
// tag with the running version, and — when a newer release exists —
// offers a link that opens the release page in the browser. The user
// downloads and installs by hand. NO updater library, NO auto-download,
// NO auto-install. Automatic download + install + relaunch (Velopack)
// comes later, when there is a real release pipeline.
//
// Failure modes are gentle by design: no network, no releases yet, or an
// unparseable tag all return an Error result with a plain message — the
// button never crashes the app.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TransPeek.Core.Update;

public enum UpdateCheckStatus
{
    /// <summary>Running version is the latest (or newer).</summary>
    UpToDate,
    /// <summary>A newer release exists on GitHub.</summary>
    UpdateAvailable,
    /// <summary>Could not check (network, no releases yet, bad response).</summary>
    Error,
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string? LatestVersion,
    string? ReleaseUrl,
    string? ReleaseName,
    string? ErrorMessage)
{
    public static UpdateCheckResult UpToDate(string latestVersion) =>
        new(UpdateCheckStatus.UpToDate, latestVersion, null, null, null);

    public static UpdateCheckResult Available(string latestVersion, string releaseUrl, string? releaseName) =>
        new(UpdateCheckStatus.UpdateAvailable, latestVersion, releaseUrl, releaseName, null);

    public static UpdateCheckResult Failed(string message) =>
        new(UpdateCheckStatus.Error, null, null, null, message);
}

public sealed class UpdateChecker
{
    public const string Repository = "TranManhTuanLabs/TransPeek-Windows";
    public const string LatestReleaseApiUrl =
        "https://api.github.com/repos/TranManhTuanLabs/TransPeek-Windows/releases/latest";

    private static readonly Regex VersionPrefix = new(
        @"^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>Check GitHub for a newer release than
    /// <paramref name="currentVersion"/>. Never throws.</summary>
    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            // GitHub API requires a User-Agent; without it the call is rejected.
            request.Headers.UserAgent.ParseAdd("TransPeek-Windows");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return UpdateCheckResult.Failed("Chưa có bản release nào trên GitHub.");
            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.Failed(
                    $"GitHub trả lỗi HTTP {(int)response.StatusCode}. Thử lại sau.");

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tag) || tag.GetString() is not { } tagName)
                return UpdateCheckResult.Failed("GitHub trả về dữ liệu lạ, không đọc được tag.");
            var latest = ParseVersion(tagName);
            if (latest is null)
                return UpdateCheckResult.Failed($"Không hiểu được tag mới nhất: {tagName}");
            var releaseUrl = root.TryGetProperty("html_url", out var url)
                ? url.GetString() : null;
            var releaseName = root.TryGetProperty("name", out var name)
                ? name.GetString() : null;

            var latestText = $"{latest.Major}.{latest.Minor}.{latest.Build}";
            if (latest <= currentVersion)
                return UpdateCheckResult.UpToDate(latestText);
            if (string.IsNullOrEmpty(releaseUrl))
                return UpdateCheckResult.Failed("Tìm thấy bản mới nhưng không có link tải.");
            return UpdateCheckResult.Available(latestText, releaseUrl, releaseName);
        }
        catch (HttpRequestException)
        {
            return UpdateCheckResult.Failed("Không có mạng hoặc không tới được GitHub. Kiểm tra mạng rồi thử lại.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Failed("Kiểm tra quá lâu (timeout). Thử lại sau.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed($"Không kiểm tra được: {ex.Message}");
        }
    }

    /// <summary>Parse a release tag ("v0.2.0", "0.2", "v1.0.0-beta") into a
    /// Version. Only the leading numeric parts matter; suffixes are
    /// ignored. Returns null when the tag has no leading number.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var match = VersionPrefix.Match(tag.Trim());
        if (!match.Success)
            return null;
        var major = int.Parse(match.Groups[1].Value);
        var minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        return new Version(major, minor, build);
    }
}
