// Share targets (mac Sources/FastRead/RSVPShare.swift): one button per
// platform, each opening its share URL in the browser (Mail opens the
// mail app). The link shared is always the TransPeek-Windows repo.
// Pre-fill differs by platform: X / Threads / Reddit / Mail take the
// text; Facebook and LinkedIn take only the link. Facebook removed the
// `quote` parameter from sharer.php (silently ignored — the preview
// comes from the page's Open Graph tags and the user types their own
// caption), so there is no `quote` here on purpose (mac 2026-11-18).

namespace TransPeek.Share;

public enum SharePlatform
{
    Facebook,
    Threads,
    X,
    Reddit,
    LinkedIn,
    Mail,
}

public static class SharePlatformExtensions
{
    /// <summary>Short name shown in the tooltip.</summary>
    public static string DisplayName(this SharePlatform platform) => platform switch
    {
        SharePlatform.Facebook => "Facebook",
        SharePlatform.Threads => "Threads",
        SharePlatform.X => "X",
        SharePlatform.Reddit => "Reddit",
        SharePlatform.LinkedIn => "LinkedIn",
        SharePlatform.Mail => "Mail",
        _ => platform.ToString(),
    };

    /// <summary>Build the share URL for this platform. The text is the
    /// message shared with the link — the app tagline in About.</summary>
    public static string ShareUrl(this SharePlatform platform, string text)
    {
        var url = AppInfo.RepositoryUrl;
        return platform switch
        {
            // Link only: sharer.php takes `u` and ignores `quote`.
            SharePlatform.Facebook =>
                "https://www.facebook.com/sharer/sharer.php?u=" + Uri.EscapeDataString(url),
            // Threads has no `url=` param on its intent — the link goes
            // at the end of the text.
            SharePlatform.Threads =>
                "https://www.threads.net/intent/post?text=" + Uri.EscapeDataString(text + " " + url),
            SharePlatform.X =>
                "https://twitter.com/intent/tweet?text=" + Uri.EscapeDataString(text)
                + "&url=" + Uri.EscapeDataString(url),
            SharePlatform.Reddit =>
                "https://www.reddit.com/submit?url=" + Uri.EscapeDataString(url)
                + "&title=" + Uri.EscapeDataString(text),
            // LinkedIn's share-offsite takes only the URL, no text.
            SharePlatform.LinkedIn =>
                "https://www.linkedin.com/sharing/share-offsite/?url=" + Uri.EscapeDataString(url),
            SharePlatform.Mail =>
                "mailto:?subject=" + Uri.EscapeDataString("TransPeek")
                + "&body=" + Uri.EscapeDataString(text + "\n\n" + url),
            _ => url,
        };
    }
}
