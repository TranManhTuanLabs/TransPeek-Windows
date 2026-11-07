// Custom OpenAI-compatible endpoint (OpenAI, DeepSeek, OpenRouter, local
// Ollama / LM Studio, ...). Mirrors the mac app's OpenAICompatibleProvider
// minus streaming: v0.1 does a single non-streaming chat/completions call
// and returns the full text. Streaming is a follow-up, not a v0.1 gap that
// blocks translation.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TransPeek.Core.Translation;

/// <summary>Definition of one user-configured OpenAI-compatible provider.
/// Persisted by <see cref="Settings.AppSettings"/> as JSON.</summary>
public sealed class CustomProviderDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string SystemPrompt { get; set; } =
        "Translate the following text to {targetLang}. Only output the translation, nothing else.";

    public static CustomProviderDefinition OpenAiDefault() => new()
    {
        Id = "custom:openai",
        Name = "OpenAI",
        BaseUrl = "https://api.openai.com/v1",
        Model = "gpt-4o-mini",
    };

    public static CustomProviderDefinition DeepSeekDefault() => new()
    {
        Id = "custom:deepseek",
        Name = "DeepSeek",
        BaseUrl = "https://api.deepseek.com/v1",
        Model = "deepseek-chat",
    };
}

public sealed class OpenAiCompatibleProvider : ITranslationProvider
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    private readonly CustomProviderDefinition _definition;

    public OpenAiCompatibleProvider(CustomProviderDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string Id => _definition.Id;
    public string DisplayName => _definition.Name;
    public bool NeedsNetwork => true;
    public bool NeedsApiKey => true;
    public bool IsAvailable => true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_definition.ApiKey);

    public async Task<string> TranslateAsync(string text, string? sourceLang, string targetLang,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw TranslationException.MissingApiKey(DisplayName);
        if (string.IsNullOrWhiteSpace(text))
            throw new TranslationException("Nothing to translate.");

        var targetName = LanguageCode.DisplayName(targetLang);
        var systemPrompt = _definition.SystemPrompt.Replace("{targetLang}", targetName, StringComparison.Ordinal);
        var userPrompt = string.IsNullOrWhiteSpace(sourceLang) || sourceLang == LanguageCode.Auto
            ? text
            : $"Translate from {LanguageCode.DisplayName(sourceLang)} to {targetName}:\n{text}";

        var payload = new ChatCompletionsRequest
        {
            Model = _definition.Model,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt },
            ],
            Temperature = 0,
        };

        var url = _definition.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _definition.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await SharedClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationException($"{DisplayName} timed out. Check your connection and try again.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException($"Could not reach {DisplayName}. Check the base URL and your connection.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw TranslationException.ApiError((int)response.StatusCode, body);

        ChatCompletionsResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ChatCompletionsResponse>(body);
        }
        catch (Exception ex)
        {
            throw TranslationException.InvalidResponse(ex.Message);
        }

        var translated = parsed?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(translated))
            throw TranslationException.EmptyResult();
        return translated;
    }

    // --- Request / response models (OpenAI chat/completions shape) ---

    internal sealed class ChatCompletionsRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = [];
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
    }

    internal sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    internal sealed class ChatCompletionsResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    internal sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }

    /// <summary>Builds the request JSON for tests without hitting the network.</summary>
    internal static string BuildRequestJson(CustomProviderDefinition definition, string text,
        string? sourceLang, string targetLang)
    {
        var targetName = LanguageCode.DisplayName(targetLang);
        var payload = new ChatCompletionsRequest
        {
            Model = definition.Model,
            Messages =
            [
                new ChatMessage { Role = "system", Content = definition.SystemPrompt.Replace("{targetLang}", targetName, StringComparison.Ordinal) },
                new ChatMessage { Role = "user", Content = text },
            ],
            Temperature = 0,
        };
        return JsonSerializer.Serialize(payload);
    }
}
