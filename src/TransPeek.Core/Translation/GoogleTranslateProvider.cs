// Google Translate via the free GTX endpoint (no API key).
// Mirrors the mac app's GoogleTranslateProvider: same endpoint, same
// response shape, same User-Agent. Unofficial API — may be rate-limited;
// the coordinator falls back to the next provider on failure.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TransPeek.Core.Translation;

public sealed class GoogleTranslateProvider : ITranslationProvider
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public string Id => "google";
    public string DisplayName => "Google Translate";
    public bool NeedsNetwork => true;
    public bool NeedsApiKey => false;
    public bool IsAvailable => true;
    public bool IsConfigured => true;

    public async Task<string> TranslateAsync(string text, string? sourceLang, string targetLang,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new TranslationException("Nothing to translate.");

        var sl = LanguageCode.ForGoogle(sourceLang, isTarget: false);
        var tl = LanguageCode.ForGoogle(targetLang, isTarget: true);

        var url = "https://translate.google.com/translate_a/single"
            + "?client=gtx"
            + "&sl=" + Uri.EscapeDataString(sl)
            + "&tl=" + Uri.EscapeDataString(tl)
            + "&dt=t&dj=1&ie=UTF-8"
            + "&q=" + Uri.EscapeDataString(text);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

        HttpResponseMessage response;
        try
        {
            response = await SharedClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationException("Google Translate timed out. Check your connection and try again.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException("Could not reach Google Translate. Check your connection and try again.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw TranslationException.ApiError((int)response.StatusCode, body);

        return ParseTranslation(body);
    }

    /// <summary>Parse a GTX JSON body into the translated text. Internal
    /// so tests can verify it without hitting the network.</summary>
    internal static string ParseTranslation(string body)
    {
        GtxResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GtxResponse>(body);
        }
        catch (Exception ex)
        {
            throw TranslationException.InvalidResponse(ex.Message);
        }

        var translated = string.Concat(parsed?.Sentences?.Select(s => s.Trans) ?? Enumerable.Empty<string>());
        if (string.IsNullOrWhiteSpace(translated))
            throw TranslationException.EmptyResult();
        return translated;
    }

    // --- Response model (only the fields we use) ---

    internal sealed class GtxResponse
    {
        [JsonPropertyName("sentences")]
        public List<GtxSentence>? Sentences { get; set; }
    }

    internal sealed class GtxSentence
    {
        [JsonPropertyName("trans")]
        public string? Trans { get; set; }
    }
}
