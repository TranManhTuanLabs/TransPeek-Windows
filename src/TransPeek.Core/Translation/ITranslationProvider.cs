// TransPeek.Core — Translation providers.
//
// Mirrors the mac app's TranslationProvider protocol (SPEC v0.1 §3.1), cut
// down for Windows v0.1: only Google Translate (free, no key) and custom
// OpenAI-compatible endpoints. No Apple Translation on Windows — it does
// not exist there, so it is deliberately absent (see README).

namespace TransPeek.Core.Translation;

/// <summary>A translation backend. Providers are self-contained: they own
/// their request logic and their own settings keys.</summary>
public interface ITranslationProvider
{
    /// <summary>Stable id used for settings keys, e.g. "google", "custom:openai".</summary>
    string Id { get; }

    /// <summary>Display name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>Whether the provider needs network access.</summary>
    bool NeedsNetwork { get; }

    /// <summary>Whether the provider needs a user-supplied API key.</summary>
    bool NeedsApiKey { get; }

    /// <summary>Whether the provider is available on this machine right now
    /// (e.g. a local engine being installed). Network providers are always
    /// available; unavailability is reported as an error at call time.</summary>
    bool IsAvailable { get; }

    /// <summary>Whether the provider is configured (API key set, when one
    /// is required).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Translate <paramref name="text"/> from <paramref name="sourceLang"/>
    /// (BCP 47, or null for auto-detect) to <paramref name="targetLang"/>
    /// (BCP 47). Throws <see cref="TranslationException"/> on failure.
    /// </summary>
    Task<string> TranslateAsync(string text, string? sourceLang, string targetLang,
        CancellationToken cancellationToken = default);
}

/// <summary>Errors surfaced to the user in the float panel. Messages must
/// stay short and actionable — never fail silently (SPEC §3.1).</summary>
public sealed class TranslationException : Exception
{
    public TranslationException(string message) : base(message) { }
    public TranslationException(string message, Exception inner) : base(message, inner) { }

    public static TranslationException InvalidResponse(string detail)
        => new($"Invalid response from server. {detail}".Trim());

    public static TranslationException ApiError(int statusCode, string detail)
        => new($"API error ({statusCode}): {detail}".Trim());

    public static TranslationException MissingApiKey(string providerName)
        => new($"API key not configured for {providerName}. Open Settings to set it up.");

    public static TranslationException EmptyResult()
        => new("Translation returned an empty result.");
}

/// <summary>BCP 47 language codes used by the app, and per-provider code
/// mapping. v0.1 ships EN + VI as the defaults (SPEC §3.3); the lists are
/// data, not hardcoded UI — Settings can offer any of them.</summary>
public static class LanguageCode
{
    public const string Auto = "auto";
    public const string English = "en";
    public const string Vietnamese = "vi";

    /// <summary>Languages offered in Settings (BCP 47, display name).</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Supported = new[]
    {
        ("en", "English"),
        ("vi", "Tiếng Việt"),
        ("zh-Hans", "中文 (简体)"),
        ("zh-Hant", "中文 (繁體)"),
        ("ja", "日本語"),
        ("ko", "한국어"),
        ("fr", "Français"),
        ("de", "Deutsch"),
        ("es", "Español"),
        ("pt", "Português"),
        ("ru", "Русский"),
        ("it", "Italiano"),
        ("th", "ไทย"),
        ("ar", "العربية"),
    };

    /// <summary>Resolve a UI language code to the code a provider expects.
    /// Google's free endpoint wants "auto" for detection and plain "zh-CN"
    /// / "zh-TW" for Chinese.</summary>
    public static string ForGoogle(string? code, bool isTarget)
    {
        if (string.IsNullOrWhiteSpace(code))
            return isTarget ? English : Auto;
        return code switch
        {
            "zh-Hans" => "zh-CN",
            "zh-Hant" => "zh-TW",
            _ => code.Split('-')[0],
        };
    }

    /// <summary>Display name for a BCP 47 code, falling back to the code itself.</summary>
    public static string DisplayName(string code)
    {
        foreach (var (c, name) in Supported)
            if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase))
                return name;
        return code;
    }
}
