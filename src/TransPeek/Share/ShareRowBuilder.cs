// A row of share buttons — one per platform (Facebook, Threads, X,
// Reddit, LinkedIn, Mail) — the same row as the Fast Read completion
// window on mac (Manh 2026-11-18) and the About tab (Manh 2026-11-19).
// Each opens its share URL in the browser (Mail opens the mail app).
// The link shared is always the TransPeek-Windows repo; `text` is the
// message shared with it — the app tagline in About.
//
// Circular badges with the platform's letter (Segoe Fluent Icons has
// no brand icons), mirroring mac ShareRowView: brand colors where they
// read on both schemes; Threads and X use the adaptive theme colors
// because a black circle would vanish on the dark scheme.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace TransPeek.Share;

public static class ShareRowBuilder
{
    /// <summary>Fill <paramref name="panel"/> with one circular share
    /// button per platform, sharing <paramref name="text"/> with the
    /// repo link. Call once (e.g. in AboutPage Loaded).</summary>
    public static void Populate(StackPanel panel, string text)
    {
        panel.Children.Clear();
        foreach (var platform in Enum.GetValues<SharePlatform>())
            panel.Children.Add(BuildButton(platform, text));
    }

    private static Button BuildButton(SharePlatform platform, string text)
    {
        // 28px circle (mac ShareRowView), letter centered.
        var badge = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = BadgeBackground(platform),
            Child = BadgeContent(platform),
        };
        var button = new Button
        {
            Content = badge,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(button, "Share on " + platform.DisplayName());
        button.Click += async (_, _) =>
        {
            var url = platform.ShareUrl(text);
            await Launcher.LaunchUriAsync(new Uri(url));
        };
        return button;
    }

    private static Brush BadgeBackground(SharePlatform platform) => platform switch
    {
        // Brand blue (mac: 0.09, 0.47, 0.95).
        SharePlatform.Facebook => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x17, 0x78, 0xF2)),
        // Adaptive: the circle is the theme text color, the letter the
        // theme page color — a black circle would vanish on dark.
        SharePlatform.Threads => ThemeBrush("TextFillColorPrimaryBrush", Microsoft.UI.Colors.Black),
        SharePlatform.X => ThemeBrush("TextFillColorPrimaryBrush", Microsoft.UI.Colors.Black),
        // Brand orange-red (mac: 1.0, 0.27, 0.0).
        SharePlatform.Reddit => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xFF, 0x45, 0x00)),
        // Brand blue (mac: 0.04, 0.4, 0.76).
        SharePlatform.LinkedIn => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x0A, 0x66, 0xC2)),
        // Secondary text color at 20% (mac: secondaryColor.opacity(0.2)).
        SharePlatform.Mail => SecondaryAtOpacity(0.2),
        _ => ThemeBrush("TextFillColorPrimaryBrush", Microsoft.UI.Colors.Black),
    };

    private static UIElement BadgeContent(SharePlatform platform)
    {
        // Mail is the only icon: envelope (Segoe Fluent Icons E715,
        // Microsoft Learn). The rest are letters (mac ShareRowView).
        if (platform == SharePlatform.Mail)
        {
            return new FontIcon
            {
                Glyph = "\uE715",
                FontSize = 13,
                Foreground = ThemeBrush("TextFillColorPrimaryBrush", Microsoft.UI.Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        var (label, fontSize) = platform switch
        {
            SharePlatform.Facebook => ("f", 15.0),
            SharePlatform.Threads => ("@", 15.0),
            SharePlatform.X => ("X", 13.0),
            SharePlatform.Reddit => ("R", 15.0),
            SharePlatform.LinkedIn => ("in", 11.0),
            _ => ("?", 13.0),
        };
        var foreground = platform is SharePlatform.Threads or SharePlatform.X
            ? ThemeBrush("ApplicationPageBackgroundThemeBrush", Microsoft.UI.Colors.White)
            : new SolidColorBrush(Microsoft.UI.Colors.White);
        return new TextBlock
        {
            Text = label,
            FontSize = fontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = foreground,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>Resolve a theme brush by name, falling back to a solid
    /// color when the resource is missing (e.g. in a test host).</summary>
    private static Brush ThemeBrush(string name, Microsoft.UI.Color fallback)
    {
        if (Application.Current.Resources.TryGetValue(name, out var value) && value is Brush brush)
            return brush;
        return new SolidColorBrush(fallback);
    }

    /// <summary>The theme secondary text color at the given opacity.</summary>
    private static Brush SecondaryAtOpacity(double opacity)
    {
        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var value)
            && value is SolidColorBrush secondary)
        {
            var c = secondary.Color;
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(
                (byte)(opacity * 255), c.R, c.G, c.B));
        }
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(
            (byte)(opacity * 255), 0x80, 0x80, 0x80));
    }
}
