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

    /// <summary>Donate ("Buy me a coffee"): the author's own BIDV
    /// account. The QR card (Assets/DonateCard.png) is the full
    /// official VietQR card; its QR was verified to decode to this
    /// account before shipping (mac: same account and card).</summary>
    public const string DonateBank = "BIDV";
    public const string DonateAccountNumber = "2600786466";
    public const string DonateAccountName = "TRAN MANH TUAN";
    public static string DonateAccountLine => $"{DonateBank} — {DonateAccountNumber} — {DonateAccountName}";

    /// <summary>ms-appx URI of the donate card image (shipped as
    /// Content next to the exe).</summary>
    public const string DonateCardUri = "ms-appx:///Assets/DonateCard.png";

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

    /// <summary>CI build number (the GitHub Actions run number), stamped
    /// into the 4th version part by build.yml:
    /// <c>-p:Version=0.1.3.${{ github.run_number }}</c> turns 0.1.3.36
    /// into 36. 0 for local builds, where the csproj &lt;Version&gt; has
    /// no 4th part. The tray header shows it mac-style:
    /// "TransPeek 0.1.3 (36)".</summary>
    public static int CiBuildNumber =>
        Assembly.GetExecutingAssembly().GetName().Version?.Revision ?? 0;

    /// <summary>All releases (list page, not one tag):
    /// https://github.com/TranManhTuanLabs/TransPeek-Windows/releases
    /// — the About page links here so the user can see every release,
    /// including preview builds (Manh 2026-11-12).</summary>
    public static string ReleasesUrl => $"{RepositoryUrl}/releases";
}
