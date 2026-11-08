// Dictionary lookup links at the bottom of the float panel (SPEC §3.2).
// Direct mirror of the mac app's DictionaryLink: same defaults, same
// {word} / {text} templates, same URL building and validation rules.
// Persistence is JSON via AppSettings (not UserDefaults — this is Windows).

using System.Text;
using System.Text.Json;

namespace TransPeek.Core.Dictionary;

/// <summary>One dictionary lookup link shown at the bottom of the float panel.</summary>
public sealed class DictionaryLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string UrlTemplate { get; set; } = "";
    /// <summary>Optional icon glyph (Segoe Fluent icon name) shown while no
    /// favicon has been fetched yet. Null falls back to the first letter of
    /// the name in a rounded tile.</summary>
    public string? IconGlyph { get; set; }

    public static IReadOnlyList<DictionaryLink> Defaults { get; } =
    [
        new() { Name = "Oxford", UrlTemplate = "https://www.oxfordlearnersdictionaries.com/definition/english/{word}" },
        new() { Name = "Cambridge", UrlTemplate = "https://dictionary.cambridge.org/dictionary/english/{word}" },
        new() { Name = "Google Dictionary", UrlTemplate = "https://www.google.com/search?q=define+{text}" },
        new() { Name = "YouGlish", UrlTemplate = "https://youglish.com/pronounce/{word}/english" },
    ];

    /// <summary>Host of the link's site, used as the favicon cache key.</summary>
    public string? Host => UrlFor("test")?.Host.ToLowerInvariant();

    /// <summary>Builds the URL to open for <paramref name="text"/>, or null
    /// when the template has neither placeholder, the text is empty, or
    /// substitution does not yield a valid http(s) URL.</summary>
    public Uri? UrlFor(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return null;

        var hasWord = UrlTemplate.Contains("{word}", StringComparison.Ordinal);
        var hasText = UrlTemplate.Contains("{text}", StringComparison.Ordinal);
        if (!hasWord && !hasText)
            return null;

        var substituted = UrlTemplate;
        if (hasWord)
        {
            var firstWord = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (firstWord is null)
                return null;
            var encoded = PercentEncode(firstWord);
            if (encoded is null)
                return null;
            substituted = substituted.Replace("{word}", encoded, StringComparison.Ordinal);
        }
        if (hasText)
        {
            var encoded = PercentEncode(trimmed);
            if (encoded is null)
                return null;
            substituted = substituted.Replace("{text}", encoded, StringComparison.Ordinal);
        }

        if (!Uri.TryCreate(substituted, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        if (string.IsNullOrEmpty(uri.Host))
            return null;
        return uri;
    }

    /// <summary>Validates a user-entered link (Settings add/edit). Returns
    /// an error message, or null when the link is usable.</summary>
    public static string? ValidationError(string name, string urlTemplate)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Enter a name for the link.";
        var template = urlTemplate.Trim();
        if (!template.Contains("{word}", StringComparison.Ordinal)
            && !template.Contains("{text}", StringComparison.Ordinal))
            return "The URL template must contain {word} or {text}.";
        var probe = new DictionaryLink { Name = name, UrlTemplate = template };
        if (probe.UrlFor("hello world") is null)
            return "The URL template is not a valid web address.";
        return null;
    }

    /// <summary>Percent-encodes a substituted value. Deliberately stricter
    /// than the default query encoding: only RFC 3986 unreserved characters
    /// pass through, so `&`, `+`, `=` in the text cannot corrupt query
    /// templates such as Google's `define+{text}`.</summary>
    public static string? PercentEncode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var sb = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
        {
            var c = (char)b;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                || c is '-' or '.' or '_' or '~')
                sb.Append(c);
            else
                sb.Append('%').Append(b.ToString("X2"));
        }
        return sb.ToString();
    }

    public static string Serialize(IEnumerable<DictionaryLink> links)
        => JsonSerializer.Serialize(links);

    public static List<DictionaryLink>? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<List<DictionaryLink>>(json); }
        catch { return null; }
    }
}
