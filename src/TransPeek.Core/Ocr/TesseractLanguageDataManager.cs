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
