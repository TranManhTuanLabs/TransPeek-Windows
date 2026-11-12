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
        // Same defaults as the mac app (DictionaryLink.swift): Oxford
        // carries ?q={word} as well as the path, Google is named "Google"
        // (not "Google Dictionary"), and YouGlish uses {text} so a whole
        // phrase can be looked up, not just its first word.
        new() { Name = "Oxford", UrlTemplate = "https://www.oxfordlearnersdictionaries.com/definition/english/{word}?q={word}" },
        new() { Name = "Cambridge", UrlTemplate = "https://dictionary.cambridge.org/dictionary/english/{word}" },
        new() { Name = "Google", UrlTemplate = "https://www.google.com/search?q=define+{text}" },
        new() { Name = "YouGlish", UrlTemplate = "https://youglish.com/pronounce/{text}/english" },
        // Cambridge Vi (mac parity, 2026-11-11): English–Vietnamese, added
        // LAST in the defaults, like on mac. {word} is lowercased + first
        // word only (UrlFor rule below).
        new() { Name = "Cambridge Vi", UrlTemplate = "https://dictionary.cambridge.org/dictionary/english-vietnamese/{word}" },
    ];

    /// <summary>Fresh copies of the defaults (new ids, new objects).
    /// Use this instead of <see cref="Defaults"/>.ToList(): the default
    /// entries are shared instances, and editing one in place (Settings
    /// update mutates the selected link) would corrupt the defaults.</summary>
    public static List<DictionaryLink> FreshDefaults()
        => Defaults.Select(l => new DictionaryLink { Name = l.Name, UrlTemplate = l.UrlTemplate }).ToList();

    /// <summary>One-time migration for links saved by v0.1 with the old
    /// defaults (Oxford without ?q, YouGlish with {word}, "Google
    /// Dictionary"). Only links whose template matches an old default
    /// exactly are touched — user-edited templates are left alone.
    /// Returns true when anything changed.</summary>
    public static bool MigrateDefaults(List<DictionaryLink> links)
    {
        var changed = false;
        foreach (var link in links)
        {
            if (string.Equals(link.UrlTemplate,
                    "https://www.oxfordlearnersdictionaries.com/definition/english/{word}",
                    StringComparison.Ordinal))
            {
                link.UrlTemplate = "https://www.oxfordlearnersdictionaries.com/definition/english/{word}?q={word}";
                changed = true;
            }
            if (string.Equals(link.UrlTemplate,
                    "https://youglish.com/pronounce/{word}/english",
                    StringComparison.Ordinal))
            {
                link.UrlTemplate = "https://youglish.com/pronounce/{text}/english";
                changed = true;
            }
            if (string.Equals(link.Name, "Google Dictionary", StringComparison.Ordinal)
                && string.Equals(link.UrlTemplate,
                    "https://www.google.com/search?q=define+{text}",
                    StringComparison.Ordinal))
            {
                link.Name = "Google";
                changed = true;
            }
        }
        // Cambridge Vi (2026-11-12): append for users who never edited
        // their list — still exactly the 4 old defaults, in order (mac
        // rule: edited lists add it by hand in Settings).
        var oldDefaults = new[]
        {
            "https://www.oxfordlearnersdictionaries.com/definition/english/{word}?q={word}",
            "https://dictionary.cambridge.org/dictionary/english/{word}",
            "https://www.google.com/search?q=define+{text}",
            "https://youglish.com/pronounce/{text}/english",
        };
        const string cambridgeVi = "https://dictionary.cambridge.org/dictionary/english-vietnamese/{word}";
        if (links.Count == oldDefaults.Length
            && links.Select(l => l.UrlTemplate).SequenceEqual(oldDefaults, StringComparer.Ordinal))
        {
            links.Add(new DictionaryLink { Name = "Cambridge Vi", UrlTemplate = cambridgeVi });
            changed = true;
        }
        return changed;
    }

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
            // Lowercase first: Oxford/Cambridge paths are case-sensitive,
            // so a capitalised first word ("Word" at a sentence start)
            // would open a not-found page. Same rule as the mac app.
            var encoded = PercentEncode(firstWord.ToLowerInvariant());
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
