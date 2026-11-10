// App identity facts used by the About page (Settings > About) and
// anywhere the repo / release links are needed. Version comes from the
// <Version> in TransPeek.csproj; the release-notes URL always points at
// the tag of the version that is actually running (vX.Y.Z), so it only
// resolves once that release is published on GitHub.

using System.Reflection;

namespace TransPeek;

public static class AppInfo
{
    public const string Name = "TransPeek";
    public const string Tagline = "Translate anywhere, peek instantly.";
    public const string RepositoryUrl = "https://github.com/TranManhTuanLabs/TransPeek-Windows";
    public const string Author = "TranManhTuanLabs";

    /// <summary>Running version, e.g. "0.1.0" (from the assembly version).</summary>
    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v is null)
                return "0.1.0";
            return $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
        }
    }

    /// <summary>Full 4-part build, e.g. "0.1.0.0".</summary>
    public static string Build =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0.0";

    /// <summary>CI build number (the GitHub Actions run number), stamped
    /// into the 4th version part by build.yml:
    /// <c>-p:Version=0.1.3.${{ github.run_number }}</c> turns 0.1.3.36
    /// into 36. 0 for local builds, where the csproj &lt;Version&gt; has
    /// no 4th part. The tray header shows it mac-style:
    /// "TransPeek 0.1.3 (36)".</summary>
    public static int CiBuildNumber =>
        Assembly.GetExecutingAssembly().GetName().Version?.Revision ?? 0;

    /// <summary>Build string for the About page: just the CI number when
    /// stamped ("36"), the full 4-part version otherwise ("0.1.3.0").</summary>
    public static string BuildDisplay => CiBuildNumber > 0 ? CiBuildNumber.ToString() : Build;

    /// <summary>Release notes for the running version:
    /// https://github.com/TranManhTuanLabs/TransPeek-Windows/releases/tag/v0.1.0</summary>
    public static string ReleaseNotesUrl => $"{RepositoryUrl}/releases/tag/v{Version}";
}
