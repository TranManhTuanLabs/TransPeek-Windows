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

    /// <summary>Release notes for the running version:
    /// https://github.com/TranManhTuanLabs/TransPeek-Windows/releases/tag/v0.1.0</summary>
    public static string ReleaseNotesUrl => $"{RepositoryUrl}/releases/tag/v{Version}";
}
