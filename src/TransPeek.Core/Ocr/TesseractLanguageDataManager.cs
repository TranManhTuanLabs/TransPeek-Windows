// Owns Tesseract *.traineddata files (SPEC §3.3).
// Mirrors the mac app's TesseractLanguageDataManager: languages download
// on demand from tessdata_best (most accurate) into
// %LOCALAPPDATA%\TransPeek\tessdata. Since 2026-11-15 (Manh) eng + vie
// are BUNDLED with the app (CI seeds them from tessdata_best into a
// tessdata folder next to the exe), so OCR works out of the box — the
// provider copies a bundled file into the local dir on first use, no
// download. Extra languages still download on demand to the local dir.

using TransPeek.Core.Dictionary;

namespace TransPeek.Core.Ocr;

public static class TesseractLanguageDataManager
{
    /// <summary>tessdata_best raw file URL, e.g. .../vie.traineddata.</summary>
    public static Uri DownloadUrl(string code)
        => new($"https://github.com/tesseract-ocr/tessdata_best/raw/main/{code}.traineddata");

    public static string TessdataDirectory => AppPaths.TessdataDirectory;

    /// <summary>Language data bundled with the app: the tessdata folder
    /// next to the exe (CI seeds eng + vie from tessdata_best at build
    /// time). The engine never reads from here directly — a bundled file
    /// is copied into <see cref="TessdataDirectory"/> on first use (see
    /// <see cref="DownloadAsync"/>), so the engine always has one
    /// directory to read from.</summary>
    public static string BundledTessdataDirectory
        => Path.Combine(AppContext.BaseDirectory, "tessdata");

    /// <summary>Language codes bundled with the app (tessdata next to
    /// the exe). Empty when the app was built without the bundle.</summary>
    public static IReadOnlyList<string> BundledCodes()
    {
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            var dir = BundledTessdataDirectory;
            if (!Directory.Exists(dir))
                return [];
            foreach (var file in Directory.EnumerateFiles(dir, "*.traineddata"))
                codes.Add(Path.GetFileNameWithoutExtension(file));
        }
        catch
        {
            // A directory we cannot list means "nothing bundled".
        }
        return codes.ToList();
    }

    /// <summary>Language codes usable right now: bundled with the app OR
    /// already in the local tessdata dir. For status display only — the
    /// engine reads from <see cref="TessdataDirectory"/>, and a bundled
    /// file is copied there on first use.</summary>
    public static IReadOnlyList<string> AvailableCodes()
    {
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        codes.UnionWith(BundledCodes());
        codes.UnionWith(InstalledCodes());
        return codes.ToList();
    }

    /// <summary>Installed language codes ("vie", "eng", ...) in the tessdata dir.</summary>
    public static IReadOnlyList<string> InstalledCodes()
    {
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            if (!Directory.Exists(TessdataDirectory))
                return [];
            foreach (var file in Directory.EnumerateFiles(TessdataDirectory, "*.traineddata"))
                codes.Add(Path.GetFileNameWithoutExtension(file));
        }
        catch
        {
            // A directory we cannot list means "nothing installed".
        }
        return codes.ToList();
    }

    public static bool IsInstalled(string code)
        => InstalledCodes().Contains(code, StringComparer.Ordinal);

    /// <summary>Make one language file available in the tessdata dir
    /// (atomic replace of any previous file). Prefers the copy bundled
    /// with the app (eng + vie, no network — OCR works out of the box)
    /// and downloads from tessdata_best only when the app does not
    /// bundle that language.</summary>
    public static async Task DownloadAsync(string code, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(TessdataDirectory);
        var destination = Path.Combine(TessdataDirectory, code + ".traineddata");
        var bundled = Path.Combine(BundledTessdataDirectory, code + ".traineddata");
        if (File.Exists(bundled))
        {
            // Bundled with the app (CI seeds eng+vie): copy locally
            // instead of downloading — same atomic replace, no network.
            var temp = destination + ".download";
            File.Copy(bundled, temp, overwrite: true);
            File.Move(temp, destination, overwrite: true);
            return;
        }
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var response = await http.GetAsync(DownloadUrl(code), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw OcrException.EngineFailed($"Could not download {code} language data (HTTP {(int)response.StatusCode}).");
        var tempDownload = destination + ".download";
        await using (var file = File.Create(tempDownload))
            await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        File.Move(tempDownload, destination, overwrite: true);
    }

    /// <summary>Progress of a batch download: which language is being
    /// fetched now, and how many of the batch are done (downloaded or
    /// failed) so far.</summary>
    public record DownloadProgress(string Code, int Done, int Total);

    /// <summary>Result of a batch download: the codes that landed in
    /// the tessdata dir, and the per-file failures (code + reason) so
    /// the UI can report each one instead of stopping at the first.</summary>
    public record DownloadBatchResult(
        IReadOnlyList<string> Downloaded,
        IReadOnlyList<(string Code, string Error)> Failed);

    /// <summary>Download several languages, one at a time. A single
    /// file failing (network, bad response, too small to be real data)
    /// does not stop the rest — its error is collected in
    /// <see cref="DownloadBatchResult.Failed"/>. Each downloaded file
    /// is verified (exists and is not suspiciously small) before it
    /// counts as downloaded.</summary>
    public static async Task<DownloadBatchResult> DownloadManyAsync(
        IReadOnlyList<string> codes,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloaded = new List<string>();
        var failed = new List<(string Code, string Error)>();
        var done = 0;
        foreach (var code in codes)
        {
            try
            {
                await DownloadAsync(code, cancellationToken).ConfigureAwait(false);
                VerifyDownloaded(code);
                downloaded.Add(code);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed.Add((code, ex.Message));
            }
            done++;
            progress?.Report(new DownloadProgress(code, done, codes.Count));
            cancellationToken.ThrowIfCancellationRequested();
        }
        return new DownloadBatchResult(downloaded, failed);
    }

    /// <summary>A downloaded language file must exist and hold real
    /// data. A file under 1 KB is not a traineddata model (a truncated
    /// download or an error page saved as a file) — delete it and fail
    /// so the next attempt retries instead of leaving junk behind.</summary>
    private static void VerifyDownloaded(string code)
    {
        var path = Path.Combine(TessdataDirectory, code + ".traineddata");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1024)
        {
            try { if (info.Exists) info.Delete(); } catch { /* best effort */ }
            throw OcrException.EngineFailed(
                $"Downloaded {code} language data looks incomplete (file too small).");
        }
    }
    public static IReadOnlyList<string> MissingCodes(
        IReadOnlyList<string> languages, Func<string, bool> isInstalled)
    {
        var codes = languages
            .Select(TessCodeForBcp47)
            .Where(c => c is not null)
            .Distinct()
            .ToList();
        if (codes.Count == 0)
            codes = ["eng"];
        return codes.Where(c => !isInstalled(c!)).ToList();
    }

    /// <summary>BCP 47 -&gt; Tesseract code. Unknown codes return null (caller drops them).</summary>
    public static string? TessCodeForBcp47(string code) => code switch
    {
        "vi-VN" or "vi" => "vie",
        "en-US" or "en-GB" or "en" => "eng",
        "zh-Hans" => "chi_sim",
        "zh-Hant" => "chi_tra",
        "ja-JP" or "ja" => "jpn",
        "ko-KR" or "ko" => "kor",
        "fr-FR" or "fr" => "fra",
        "de-DE" or "de" => "deu",
        "es-ES" or "es" => "spa",
        "pt-BR" or "pt" => "por",
        "ru-RU" or "ru" => "rus",
        "ar-SA" or "ar" => "ara",
        "it-IT" or "it" => "ita",
        "th-TH" or "th" => "tha",
        // Extended 2026-11-16 (Tesseract language downloads): the
        // major languages users are likely to OCR. Only mappings we
        // are sure about — the Tesseract code is the ISO 639-2/3 code.
        "nl-NL" or "nl" => "nld",
        "sv-SE" or "sv" => "swe",
        "da-DK" or "da" => "dan",
        "fi-FI" or "fi" => "fin",
        "nb-NO" or "nb" or "nn-NO" or "nn" or "no" => "nor",
        "pl-PL" or "pl" => "pol",
        "cs-CZ" or "cs" => "ces",
        "hu-HU" or "hu" => "hun",
        "ro-RO" or "ro" => "ron",
        "bg-BG" or "bg" => "bul",
        "el-GR" or "el" => "ell",
        "he-IL" or "he" => "heb",
        "hi-IN" or "hi" => "hin",
        "id-ID" or "id" => "ind",
        "ms-MY" or "ms" => "msa",
        "tr-TR" or "tr" => "tur",
        "uk-UA" or "uk" => "ukr",
        "af-ZA" or "af" => "afr",
        "ca-ES" or "ca" => "cat",
        "hr-HR" or "hr" => "hrv",
        "sk-SK" or "sk" => "slk",
        "sl-SI" or "sl" => "slv",
        "et-EE" or "et" => "est",
        "lv-LV" or "lv" => "lav",
        "lt-LT" or "lt" => "lit",
        "sr-RS" or "sr" => "srp",
        "sq-AL" or "sq" => "sqi",
        "hy-AM" or "hy" => "hye",
        "ka-GE" or "ka" => "kat",
        "kk-KZ" or "kk" => "kaz",
        "ky-KG" or "ky" => "kir",
        "mk-MK" or "mk" => "mkd",
        "fa-IR" or "fa" => "fas",
        "ur-PK" or "ur" => "urd",
        "bn-IN" or "bn" => "ben",
        "ta-IN" or "ta" => "tam",
        "te-IN" or "te" => "tel",
        "mr-IN" or "mr" => "mar",
        "gu-IN" or "gu" => "guj",
        "pa-IN" or "pa" => "pan",
        "ne-NP" or "ne" => "nep",
        "si-LK" or "si" => "sin",
        "km-KH" or "km" => "khm",
        "lo-LA" or "lo" => "lao",
        "my-MM" or "my" => "mya",
        "is-IS" or "is" => "isl",
        "ga-IE" or "ga" => "gle",
        "cy-GB" or "cy" => "cym",
        "eu-ES" or "eu" => "eus",
        "gl-ES" or "gl" => "glg",
        "mt-MT" or "mt" => "mlt",
        "sw-KE" or "sw" => "swa",
        "fil-PH" or "fil" => "fil",
        "az-AZ" or "az" => "aze",
        "be-BY" or "be" => "bel",
        "bs-BA" or "bs" => "bos",
        "eo" => "epo",
        "la" => "lat",
        _ => null,
    };

    /// <summary>Tesseract code -&gt; BCP 47, for display / SupportedLanguages.</summary>
    public static string Bcp47ForTessCode(string code) => code switch
    {
        "vie" => "vi-VN",
        "eng" => "en-US",
        "chi_sim" => "zh-Hans",
        "chi_tra" => "zh-Hant",
        "jpn" => "ja-JP",
        "kor" => "ko-KR",
        "fra" => "fr-FR",
        "deu" => "de-DE",
        "spa" => "es-ES",
        "por" => "pt-BR",
        "rus" => "ru-RU",
        "ara" => "ar-SA",
        "ita" => "it-IT",
        "tha" => "th-TH",
        "nld" => "nl-NL",
        "swe" => "sv-SE",
        "dan" => "da-DK",
        "fin" => "fi-FI",
        "nor" => "nb-NO",
        "pol" => "pl-PL",
        "ces" => "cs-CZ",
        "hun" => "hu-HU",
        "ron" => "ro-RO",
        "bul" => "bg-BG",
        "ell" => "el-GR",
        "heb" => "he-IL",
        "hin" => "hi-IN",
        "ind" => "id-ID",
        "msa" => "ms-MY",
        "tur" => "tr-TR",
        "ukr" => "uk-UA",
        "afr" => "af-ZA",
        "cat" => "ca-ES",
        "hrv" => "hr-HR",
        "slk" => "sk-SK",
        "slv" => "sl-SI",
        "est" => "et-EE",
        "lav" => "lv-LV",
        "lit" => "lt-LT",
        "srp" => "sr-RS",
        "sqi" => "sq-AL",
        "hye" => "hy-AM",
        "kat" => "ka-GE",
        "kaz" => "kk-KZ",
        "kir" => "ky-KG",
        "mkd" => "mk-MK",
        "fas" => "fa-IR",
        "urd" => "ur-PK",
        "ben" => "bn-IN",
        "tam" => "ta-IN",
        "tel" => "te-IN",
        "mar" => "mr-IN",
        "guj" => "gu-IN",
        "pan" => "pa-IN",
        "nep" => "ne-NP",
        "sin" => "si-LK",
        "khm" => "km-KH",
        "lao" => "lo-LA",
        "mya" => "my-MM",
        "isl" => "is-IS",
        "gle" => "ga-IE",
        "cym" => "cy-GB",
        "eus" => "eu-ES",
        "glg" => "gl-ES",
        "mlt" => "mt-MT",
        "swa" => "sw-KE",
        "fil" => "fil-PH",
        "aze" => "az-AZ",
        "bel" => "be-BY",
        "bos" => "bs-BA",
        "epo" => "eo",
        "lat" => "la",
        _ => code,
    };
}
