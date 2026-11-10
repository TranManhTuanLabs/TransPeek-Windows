// Owns Tesseract *.traineddata files (SPEC §3.3).
// Mirrors the mac app's TesseractLanguageDataManager: languages download
// on demand from tessdata_best (most accurate) into
// %LOCALAPPDATA%\TransPeek\tessdata — never bundled with the app, so the
// installed size stays small. v0.1 languages: vie + eng.

using TransPeek.Core.Dictionary;

namespace TransPeek.Core.Ocr;

public static class TesseractLanguageDataManager
{
    /// <summary>tessdata_best raw file URL, e.g. .../vie.traineddata.</summary>
    public static Uri DownloadUrl(string code)
        => new($"https://github.com/tesseract-ocr/tessdata_best/raw/main/{code}.traineddata");

    public static string TessdataDirectory => AppPaths.TessdataDirectory;

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

    /// <summary>Download one language file into the tessdata dir (atomic
    /// replace of any previous download).</summary>
    public static async Task DownloadAsync(string code, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(TessdataDirectory);
        var destination = Path.Combine(TessdataDirectory, code + ".traineddata");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var response = await http.GetAsync(DownloadUrl(code), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw OcrException.EngineFailed($"Could not download {code} language data (HTTP {(int)response.StatusCode}).");
        var temp = destination + ".download";
        await using (var file = File.Create(temp))
            await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        File.Move(temp, destination, overwrite: true);
    }

    /// <summary>Language codes the user asked for (BCP 47) that are not
    /// installed yet, as Tesseract codes. Pure function of (languages,
    /// isInstalled) so tests can cover it without touching disk or network.
    /// No usable code in settings: match TesseractOcrProvider, which falls
    /// back to English on its own.</summary>
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
        _ => code,
    };
}
