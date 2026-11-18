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

    /// <summary>All 249 languages Google Translate supports, plus the
    /// "pt" alias below — BCP 47 code, native display name, and English
    /// name (for the picker's search box). Sorted by English name A–Z.
    /// Google translates server-side, so 250 entries cost a few KB and
    /// the app does not get bigger (Manh 2026-11-19).</summary>
    public static readonly IReadOnlyList<(string Code, string Name, string EnglishName)> Supported = new[]
    {
        ("ab", "Аԥсшәа", "Abkhaz"),
        ("ace", "Bahsa Acèh", "Acehnese"),
        ("ach", "Acholi", "Acholi"),
        ("aa", "Qafaraf", "Afar"),
        ("af", "Afrikaans", "Afrikaans"),
        ("sq", "Shqip", "Albanian"),
        ("alz", "Lur", "Alur"),
        ("am", "አማርኛ", "Amharic"),
        ("ar", "العربية", "Arabic"),
        ("hy", "հայերեն", "Armenian"),
        ("as", "অসমীয়া", "Assamese"),
        ("av", "МагӀарул мацӀ", "Avar"),
        ("awa", "अवधी", "Awadhi"),
        ("ay", "Aymar aru", "Aymara"),
        ("az", "Azərbaycan dili", "Azerbaijani"),
        ("ban", "ᬩᬲᬩᬮᬶ", "Balinese"),
        ("bal", "Balóchí", "Baluchi"),
        ("bm", "Bamanankan", "Bambara"),
        ("bci", "Wawle", "Baoulé"),
        ("ba", "Башҡортса", "Bashkir"),
        ("eu", "Euskara", "Basque"),
        ("btx", "Batak Karo", "Batak Karo"),
        ("bts", "Bahasa Simalungun", "Batak Simalungun"),
        ("bbc", "Hata Batak", "Batak Toba"),
        ("be", "Беларуская", "Belarusian"),
        ("bem", "Ichibemba", "Bemba"),
        ("bn", "বাংলা", "Bengali"),
        ("bew", "Basa Betawi", "Betawi"),
        ("bho", "भोजपुरी", "Bhojpuri"),
        ("bik", "Sentral Bikol", "Bikol"),
        ("bs", "Bosanski", "Bosnian"),
        ("br", "Brezhoneg", "Breton"),
        ("bg", "Български", "Bulgarian"),
        ("bua", "Буряад хэлэн", "Buryat"),
        ("yue", "粵語", "Cantonese"),
        ("ca", "Català", "Catalan"),
        ("ceb", "Bisaya", "Cebuano"),
        ("ch", "Chamorru", "Chamorro"),
        ("ce", "Нохчийн мотт", "Chechen"),
        ("ny", "Chichewa", "Chichewa"),
        ("zh-Hans", "中文 (简体)", "Chinese (Simplified)"),
        ("zh-Hant", "中文 (繁體)", "Chinese (Traditional)"),
        ("chk", "Fóósun Chuuk", "Chuukese"),
        ("cv", "Чăваш чĕлхи", "Chuvash"),
        ("co", "Corsu", "Corsican"),
        ("crh", "Къырымтатар тили", "Crimean Tatar (Cyrillic)"),
        ("crh-Latn", "Qırımtatar tili", "Crimean Tatar (Latin)"),
        ("hr", "Hrvatski", "Croatian"),
        ("cs", "Čeština", "Czech"),
        ("da", "Dansk", "Danish"),
        ("fa-AF", "دری", "Dari"),
        ("dv", "ދިވެހިބަސް", "Dhivehi"),
        ("din", "Thuɔŋjäŋ", "Dinka"),
        ("doi", "डोगरी", "Dogri"),
        ("dov", "ChiDombe", "Dombe"),
        ("nl", "Nederlands", "Dutch"),
        ("dyu", "Julakan", "Dyula"),
        ("dz", "རྫོང་ཁ་", "Dzongkha"),
        ("en", "English", "English"),
        ("eo", "Esperanto", "Esperanto"),
        ("et", "Eesti keel", "Estonian"),
        ("ee", "Eʋegbe", "Ewe"),
        ("fo", "Føroyskt mál", "Faroese"),
        ("fj", "Vosa Vakaviti", "Fijian"),
        ("tl", "Filipino", "Filipino"),
        ("fi", "Suomi", "Finnish"),
        ("fon", "Fɔ̀ngbè", "Fon"),
        ("fr", "Français", "French"),
        ("fr-CA", "Français (Canada)", "French (Canada)"),
        ("fy", "West-Frysk", "Frisian"),
        ("fur", "Furlan", "Friulian"),
        ("ff", "Fulfulde", "Fulani"),
        ("gaa", "Gã", "Ga"),
        ("gl", "Galego", "Galician"),
        ("ka", "ქართული", "Georgian"),
        ("de", "Deutsch", "German"),
        ("el", "Ελληνικά", "Greek"),
        ("gn", "Avañe'ẽ", "Guarani"),
        ("gu", "ગુજરાતી", "Gujarati"),
        ("ht", "Kreyòl ayisyen", "Haitian Creole"),
        ("cnh", "Laica", "Hakha Chin"),
        ("ha", "Hausa", "Hausa"),
        ("haw", "‘Ōlelo Hawai‘i", "Hawaiian"),
        ("iw", "עברית", "Hebrew"),
        ("hil", "Hiligaynon", "Hiligaynon"),
        ("hi", "हिन्दी", "Hindi"),
        ("hmn", "Hmoob", "Hmong"),
        ("hu", "Magyar", "Hungarian"),
        ("hrx", "Hunsrickisch", "Hunsrik"),
        ("iba", "Jaku Iban", "Iban"),
        ("is", "Íslenska", "Icelandic"),
        ("ig", "Igbo", "Igbo"),
        ("ilo", "Ilokano", "Ilocano"),
        ("id", "Bahasa Indonesia", "Indonesian"),
        ("iu-Latn", "Inuktitut", "Inuktut (Latin)"),
        ("iu", "ᐃᓄᒃᑎᑐᑦ", "Inuktut (Syllabics)"),
        ("ga", "Gaeilge", "Irish"),
        ("it", "Italiano", "Italian"),
        ("jam", "Patwa", "Jamaican Patois"),
        ("ja", "日本語", "Japanese"),
        ("jw", "Basa Jawa", "Javanese"),
        ("kac", "Jinghpaw", "Jingpo"),
        ("kl", "Kalaallisut", "Kalaallisut"),
        ("kn", "ಕನ್ನಡ", "Kannada"),
        ("kr", "Kanuri", "Kanuri"),
        ("pam", "Kapampángan", "Kapampangan"),
        ("kk", "Qazaq tılı", "Kazakh"),
        ("kha", "Khasi", "Khasi"),
        ("km", "ខ្មែរ", "Khmer"),
        ("cgg", "Kiga", "Kiga"),
        ("kg", "Kikongo", "Kikongo"),
        ("rw", "Ikinyarwanda", "Kinyarwanda"),
        ("ktu", "Kituba", "Kituba"),
        ("trp", "Kok Borok", "Kokborok"),
        ("kv", "Коми кыв", "Komi"),
        ("gom", "गोंयची कोंकणी", "Konkani"),
        ("ko", "한국어", "Korean"),
        ("kri", "Krio", "Krio"),
        ("ku", "Kurdî", "Kurdish (Kurmanji)"),
        ("ckb", "کوردیی ناوەندی", "Kurdish (Sorani)"),
        ("ky", "Кыргыз тили", "Kyrgyz"),
        ("lo", "ລາວ", "Lao"),
        ("ltg", "Latgaļu volūda", "Latgalian"),
        ("la", "Latina", "Latin"),
        ("lv", "Latviešu valoda", "Latvian"),
        ("lij", "Lìgure", "Ligurian"),
        ("li", "Limburgs", "Limburgish"),
        ("ln", "Lingála", "Lingala"),
        ("lt", "Lietuvių kalba", "Lithuanian"),
        ("lmo", "Lombard", "Lombard"),
        ("lg", "Luganda", "Luganda"),
        ("luo", "Dholuo", "Luo"),
        ("lb", "Lëtzebuergesch", "Luxembourgish"),
        ("mk", "Македонски", "Macedonian"),
        ("mad", "Basa Madura", "Madurese"),
        ("mai", "मैथिली", "Maithili"),
        ("mak", "Basa Mangkasara'", "Makassar"),
        ("mg", "Malagasy", "Malagasy"),
        ("ms", "Bahasa Melayu", "Malay"),
        ("ms-Arab", "بهاس ملايو", "Malay (Jawi)"),
        ("ml", "മലയാളം", "Malayalam"),
        ("mt", "Malti", "Maltese"),
        ("mam", "Ta yol Mam", "Mam"),
        ("gv", "Gaelg", "Manx"),
        ("mi", "Te reo Māori", "Maori"),
        ("mr", "मराठी", "Marathi"),
        ("mh", "Kajin Ṃajōḷ", "Marshallese"),
        ("mwr", "मारवाड़ी", "Marwadi"),
        ("mfe", "Morisien", "Mauritian Creole"),
        ("chm", "Олык марий", "Meadow Mari"),
        ("mni-Mtei", "ꯃꯤꯇꯩ ꯂꯣꯟ", "Meiteilon (Manipuri)"),
        ("min", "Bahaso Minangkabau", "Minang"),
        ("lus", "Mizo ṭawng", "Mizo"),
        ("mn", "Монгол хэл", "Mongolian"),
        ("my", "မြန်မာ", "Myanmar (Burmese)"),
        ("nhe", "Nahuatl", "Nahuatl (Eastern Huasteca)"),
        ("ndc-ZW", "Cindawu", "Ndau"),
        ("nr", "IsiNdebele", "Ndebele (South)"),
        ("new", "नेवाः भाय्", "Nepalbhasa (Newari)"),
        ("ne", "नेपाली", "Nepali"),
        ("bm-Nkoo", "ߓߡߊߣߊ߲ߞߊ߲", "NKo"),
        ("no", "Norsk", "Norwegian (Bokmål)"),
        ("nus", "Thok Naath", "Nuer"),
        ("oc", "Occitan", "Occitan"),
        ("or", "ଓଡ଼ିଆ", "Odia (Oriya)"),
        ("om", "Afaan Oromoo", "Oromo"),
        ("os", "Ирон æвзаг", "Ossetian"),
        ("pag", "Pangasinan", "Pangasinan"),
        ("pap", "Papiamento", "Papiamento"),
        ("ps", "پښتو", "Pashto"),
        ("fa", "فارسی", "Persian"),
        ("pl", "Polski", "Polish"),
        ("pt", "Português", "Portuguese"),
        ("pt-BR", "Português (Brasil)", "Portuguese (Brazil)"),
        ("pt-PT", "Português (Portugal)", "Portuguese (Portugal)"),
        ("pa", "ਪੰਜਾਬੀ", "Punjabi (Gurmukhi)"),
        ("pa-Arab", "پنجابی", "Punjabi (Shahmukhi)"),
        ("qu", "Runasimi", "Quechua"),
        ("kek", "Q'eqchi'", "Qʼeqchiʼ"),
        ("rom", "Romani chib", "Romani"),
        ("ro", "Română", "Romanian"),
        ("rn", "Ikirundi", "Rundi"),
        ("ru", "Русский", "Russian"),
        ("se", "Sámegiella", "Sami (North)"),
        ("sm", "Gagana Samoa", "Samoan"),
        ("sg", "Sängö", "Sango"),
        ("sa", "संस्कृतम्", "Sanskrit"),
        ("sat-Latn", "Sāntāṛī", "Santali (Latin)"),
        ("sat", "ᱥᱟᱱᱛᱟᱲᱤ", "Santali (Ol Chiki)"),
        ("gd", "Gàidhlig na h-Alba", "Scots Gaelic"),
        ("nso", "Sesotho sa Leboa", "Sepedi"),
        ("sr", "Српски", "Serbian"),
        ("st", "Sesotho", "Sesotho"),
        ("crs", "Kreol seselwa", "Seychellois Creole"),
        ("shn", "လိၵ်ႈတႆး", "Shan"),
        ("sn", "ChiShona", "Shona"),
        ("scn", "Sicilianu", "Sicilian"),
        ("szl", "Ślōnskŏ gŏdka", "Silesian"),
        ("sd", "سنڌي", "Sindhi"),
        ("si", "සිංහල", "Sinhala"),
        ("sk", "Slovenčina", "Slovak"),
        ("sl", "Slovenščina", "Slovenian"),
        ("so", "Soomaali", "Somali"),
        ("es", "Español", "Spanish"),
        ("su", "Basa Sunda", "Sundanese"),
        ("sus", "Sosoxui", "Susu"),
        ("sw", "Kiswahili", "Swahili"),
        ("ss", "Siswati", "Swati"),
        ("sv", "Svenska", "Swedish"),
        ("ty", "Reo tahiti", "Tahitian"),
        ("tg", "Забони тоҷикӣ", "Tajik"),
        ("ber-Latn", "Tamaziɣt", "Tamazight"),
        ("ber", "ⵜⴰⵎⴰⵣⵉⵖⵜ", "Tamazight (Tifinagh)"),
        ("ta", "தமிழ்", "Tamil"),
        ("tt", "Татар теле", "Tatar"),
        ("te", "తెలుగు", "Telugu"),
        ("tet", "Tetun", "Tetum"),
        ("th", "ภาษาไทย", "Thai"),
        ("bo", "བོད་ཡིག", "Tibetan"),
        ("ti", "ትግርኛ", "Tigrinya"),
        ("tiv", "Tiv", "Tiv"),
        ("tpi", "Tok Pisin", "Tok Pisin"),
        ("to", "Lea fakatonga", "Tongan"),
        ("lua", "Tshiluba", "Tshiluba"),
        ("ts", "Xitsonga", "Tsonga"),
        ("tn", "Setswana", "Tswana"),
        ("tcy", "ತುಳು", "Tulu"),
        ("tum", "chiTumbuka", "Tumbuka"),
        ("tr", "Türkçe", "Turkish"),
        ("tk", "Türkmençe", "Turkmen"),
        ("tyv", "Тыва дыл", "Tuvan"),
        ("ak", "Akan", "Twi"),
        ("udm", "Удмурт кыл", "Udmurt"),
        ("uk", "Українська", "Ukrainian"),
        ("ur", "اردو", "Urdu"),
        ("ug", "ئۇيغۇرچە", "Uyghur"),
        ("uz", "Ўзбек тили", "Uzbek"),
        ("ve", "Tshivenḓa", "Venda"),
        ("vec", "Vèneto", "Venetian"),
        ("vi", "Tiếng Việt", "Vietnamese"),
        ("war", "Waray-Waray", "Waray"),
        ("cy", "Cymraeg", "Welsh"),
        ("wo", "Wolof", "Wolof"),
        ("xh", "IsiXhosa", "Xhosa"),
        ("sah", "Саха тыла", "Yakut"),
        ("yi", "יידיש", "Yiddish"),
        ("yo", "Èdè Yorùbá", "Yoruba"),
        ("yua", "Màaya t'àan", "Yucatec Maya"),
        ("zap", "Diidxazá", "Zapotec"),
        ("zu", "IsiZulu", "Zulu"),
    };

    // "pt" is NOT one of Google's 249 (the web UI lists only pt-BR and
    // pt-PT), but it is a valid code the endpoint accepts and settings
    // saved before 0.1.4 may already use it — keep it as an alias so
    // those settings keep working. pt-BR / pt-PT are in the list too.

    /// <summary>The 20 most-used languages, pinned at the top of the
    /// picker in this order (Manh's pick from 14/20/30/50): the 14 the
    /// app already had + Hindi, Indonesian, Turkish, Dutch, Polish,
    /// Ukrainian.</summary>
    public static readonly IReadOnlyList<string> Popular = new[]
    {
        "en", "vi", "zh-Hans", "zh-Hant", "ja", "ko", "fr", "de", "es", "pt",
        "ru", "it", "th", "ar", "hi", "id", "tr", "nl", "pl", "uk",
    };

    /// <summary>Resolve a UI language code to the code a provider expects.
    /// Google's free endpoint wants "auto" for detection and plain "zh-CN"
    /// / "zh-TW" for Chinese. Every other code passes through UNCHANGED:
    /// cutting at "-" (the old code) turned Dari (fa-AF) into Persian
    /// (fa) and dropped scripts (crh-Latn, ms-Arab, ...) — the endpoint
    /// accepts the full variant codes (Manh 2026-11-19).</summary>
    public static string ForGoogle(string? code, bool isTarget)
    {
        if (string.IsNullOrWhiteSpace(code))
            return isTarget ? English : Auto;
        return code switch
        {
            "zh-Hans" => "zh-CN",
            "zh-Hant" => "zh-TW",
            _ => code,
        };
    }

    /// <summary>Display name for a BCP 47 code, falling back to the code itself.</summary>
    public static string DisplayName(string code)
    {
        foreach (var (c, name, _) in Supported)
            if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase))
                return name;
        return code;
    }
}
