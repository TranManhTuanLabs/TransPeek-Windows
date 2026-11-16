// Hardcoded catalog of Tesseract OCR languages (Manh 2026-11-15,
// implemented 2026-11-16). tessdata_best has 125 *.traineddata files
// in the repo root (languages) plus ~30 script models in the script/
// subdir. This catalog lists LANGUAGES ONLY — no scripts (Manh: only
// languages), and no special models: "equ" (math equations) and "osd"
// (orientation/script detection) are not languages, so they are
// excluded too. The list is hardcoded so the page works offline; the
// Refresh button fetches the live list from GitHub to find languages
// added later (see FetchLiveCodesAsync).

using System.Text.Json;

namespace TransPeek.Core.Ocr;

public static class TesseractLanguageCatalog
{
    /// <summary>One language: the Tesseract code (file name without
    /// .traineddata) and the English display name.</summary>
    public record Language(string Code, string Name);

    /// <summary>Special tessdata_best models that are not languages:
    /// "equ" recognises math equations, "osd" detects orientation and
    /// script. Excluded from the catalog and from live fetches.</summary>
    public static readonly IReadOnlySet<string> NonLanguageCodes =
        new HashSet<string>(StringComparer.Ordinal) { "equ", "osd" };

    /// <summary>All languages tessdata_best ships (123), sorted by
    /// code. Verified against the GitHub API listing 2026-11-16.</summary>
    public static readonly IReadOnlyList<Language> Languages =
    [
        new("afr", "Afrikaans"),
        new("amh", "Amharic"),
        new("ara", "Arabic"),
        new("asm", "Assamese"),
        new("aze", "Azerbaijani"),
        new("aze_cyrl", "Azerbaijani (Cyrillic)"),
        new("bel", "Belarusian"),
        new("ben", "Bengali"),
        new("bod", "Tibetan"),
        new("bos", "Bosnian"),
        new("bre", "Breton"),
        new("bul", "Bulgarian"),
        new("cat", "Catalan"),
        new("ceb", "Cebuano"),
        new("ces", "Czech"),
        new("chi_sim", "Chinese (Simplified)"),
        new("chi_sim_vert", "Chinese (Simplified, vertical)"),
        new("chi_tra", "Chinese (Traditional)"),
        new("chi_tra_vert", "Chinese (Traditional, vertical)"),
        new("chr", "Cherokee"),
        new("cos", "Corsican"),
        new("cym", "Welsh"),
        new("dan", "Danish"),
        new("deu", "German"),
        new("deu_latf", "German (Fraktur)"),
        new("div", "Dhivehi"),
        new("dzo", "Dzongkha"),
        new("ell", "Greek"),
        new("eng", "English"),
        new("enm", "Middle English"),
        new("epo", "Esperanto"),
        new("est", "Estonian"),
        new("eus", "Basque"),
        new("fao", "Faroese"),
        new("fas", "Persian"),
        new("fil", "Filipino"),
        new("fin", "Finnish"),
        new("fra", "French"),
        new("frm", "Middle French"),
        new("fry", "Western Frisian"),
        new("gla", "Scottish Gaelic"),
        new("gle", "Irish"),
        new("glg", "Galician"),
        new("grc", "Ancient Greek"),
        new("guj", "Gujarati"),
        new("hat", "Haitian Creole"),
        new("heb", "Hebrew"),
        new("hin", "Hindi"),
        new("hrv", "Croatian"),
        new("hun", "Hungarian"),
        new("hye", "Armenian"),
        new("iku", "Inuktitut"),
        new("ind", "Indonesian"),
        new("isl", "Icelandic"),
        new("ita", "Italian"),
        new("ita_old", "Old Italian"),
        new("jav", "Javanese"),
        new("jpn", "Japanese"),
        new("jpn_vert", "Japanese (vertical)"),
        new("kan", "Kannada"),
        new("kat", "Georgian"),
        new("kat_old", "Old Georgian"),
        new("kaz", "Kazakh"),
        new("khm", "Khmer"),
        new("kir", "Kyrgyz"),
        new("kmr", "Kurmanji"),
        new("kor", "Korean"),
        new("kor_vert", "Korean (vertical)"),
        new("lao", "Lao"),
        new("lat", "Latin"),
        new("lav", "Latvian"),
        new("lit", "Lithuanian"),
        new("ltz", "Luxembourgish"),
        new("mal", "Malayalam"),
        new("mar", "Marathi"),
        new("mkd", "Macedonian"),
        new("mlt", "Maltese"),
        new("mon", "Mongolian"),
        new("mri", "Maori"),
        new("msa", "Malay"),
        new("mya", "Burmese"),
        new("nep", "Nepali"),
        new("nld", "Dutch"),
        new("nor", "Norwegian"),
        new("oci", "Occitan"),
        new("ori", "Odia"),
        new("pan", "Punjabi"),
        new("pol", "Polish"),
        new("por", "Portuguese"),
        new("pus", "Pashto"),
        new("que", "Quechua"),
        new("ron", "Romanian"),
        new("rus", "Russian"),
        new("san", "Sanskrit"),
        new("sin", "Sinhala"),
        new("slk", "Slovak"),
        new("slv", "Slovenian"),
        new("snd", "Sindhi"),
        new("spa", "Spanish"),
        new("spa_old", "Old Spanish"),
        new("sqi", "Albanian"),
        new("srp", "Serbian"),
        new("srp_latn", "Serbian (Latin)"),
        new("sun", "Sundanese"),
        new("swa", "Swahili"),
        new("swe", "Swedish"),
        new("syr", "Syriac"),
        new("tam", "Tamil"),
        new("tat", "Tatar"),
        new("tel", "Telugu"),
        new("tgk", "Tajik"),
        new("tha", "Thai"),
        new("tir", "Tigrinya"),
        new("ton", "Tongan"),
        new("tur", "Turkish"),
        new("uig", "Uyghur"),
        new("ukr", "Ukrainian"),
        new("urd", "Urdu"),
        new("uzb", "Uzbek"),
        new("uzb_cyrl", "Uzbek (Cyrillic)"),
        new("vie", "Vietnamese"),
        new("yid", "Yiddish"),
        new("yor", "Yoruba"),
    ];

    /// <summary>Display name for a code; the code itself when the
    /// catalog has no entry (e.g. a language added live by Refresh).</summary>
    public static string DisplayName(string code)
        => Languages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.Ordinal))?.Name ?? code;

    /// <summary>Fetch the live language list from tessdata_best (GitHub
    /// API, repo root listing). Returns the language codes found there,
    /// excluding the non-language models. Used by Refresh to spot
    /// languages added after this catalog was hardcoded. Throws
    /// OcrException when the request fails (e.g. no network, GitHub
    /// rate limit) — the caller shows the message and keeps the
    /// hardcoded list.</summary>
    public static async Task<IReadOnlyList<string>> FetchLiveCodesAsync(
        HttpClient? http = null, CancellationToken cancellationToken = default)
    {
        var client = http ?? new HttpClient();
        var owned = http is null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/tesseract-ocr/tessdata_best/contents/");
            // GitHub API requires a User-Agent; without one it returns 403.
            request.Headers.UserAgent.ParseAdd("TransPeek");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw OcrException.EngineFailed(
                    $"Could not fetch the language list from GitHub (HTTP {(int)response.StatusCode}).");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var codes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameProp))
                    continue;
                var name = nameProp.GetString();
                if (name is null || !name.EndsWith(".traineddata", StringComparison.Ordinal))
                    continue;
                var code = name[..^".traineddata".Length];
                if (!NonLanguageCodes.Contains(code))
                    codes.Add(code);
            }
            return codes.ToList();
        }
        finally
        {
            if (owned)
                client.Dispose();
        }
    }
}
