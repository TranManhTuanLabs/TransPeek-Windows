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
    Task<TranslationProviderResult> TranslateAsync(string text, string? sourceLang, string targetLang,
        CancellationToken cancellationToken = default);
}

/// <summary>What a provider returns: the translated text, plus the
/// source language it actually detected (BCP 47, e.g. "en") when the
/// caller asked for auto-detect. Null when the provider does not
/// report it (OpenAI-compatible endpoints) or the source was given
/// explicitly — the caller then just uses the requested source
/// language. The panel's source speaker needs it: with source "auto"
/// the text's language is unknown until the provider detects it, and
/// speaking English text in the default (Vietnamese "An") voice is
/// the "always An" bug (Manh 2026-11-16).</summary>
public sealed record TranslationProviderResult(string Text, string? DetectedSourceLanguage);

/// <summary>Errors surfaced to the user in the float panel. Messages must
/// stay short and actionable — never fail silently (SPEC §3.1).</summary>
public sealed class TranslationException : Exception
{
    public TranslationException(string message) : base(message) { }
    public TranslationException(string message, Exception inner) : base(message, inner) { }

    /// <summary>HTTP status code when the failure came from an API
    /// response (e.g. 429 rate-limit); null for local failures. The
    /// coordinator uses it to decide whether another provider may be
    /// tried — a 429 is never fallen back from (Manh 2026-11-15).</summary>
    public int? StatusCode { get; private set; }

    public static TranslationException InvalidResponse(string detail)
        => new($"Invalid response from server. {detail}".Trim());

    public static TranslationException ApiError(int statusCode, string detail)
        => new($"API error ({statusCode}): {detail}".Trim()) { StatusCode = statusCode };

    /// <summary>Google rate-limit after all retries: a plain message the
    /// user can act on (wait a few minutes), not the raw 429 body.</summary>
    public static TranslationException RateLimited()
        => new("Google Translate is rate-limiting requests. Try again in a few minutes.")
        { StatusCode = 429 };

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
