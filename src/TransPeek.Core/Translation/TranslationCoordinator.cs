// Translation fallback (SPEC §3.1): primary -> fallback -> free Google.
// Mirrors the mac app's ordered-slots behaviour. Pure ordering logic —
// the coordinator never hides an error: when every provider fails it
// reports the first provider's error (the one the user actually picked).

using TransPeek.Core.Settings;

namespace TransPeek.Core.Translation;

/// <summary>Translation result: the translated text and the provider
/// that actually produced it (the panel shows "via Google Translate",
/// etc.).</summary>
public sealed record TranslationResult(string Text, string ProviderName);

public sealed class TranslationCoordinator
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<ITranslationProvider> _allProviders;

    public TranslationCoordinator(AppSettings settings, IEnumerable<ITranslationProvider> providers)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _allProviders = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>Providers in the order they will be tried: primary first,
    /// then the fallback, then any other enabled provider, then Google
    /// (free) last as the safety net. Disabled providers are skipped.</summary>
    public IReadOnlyList<ITranslationProvider> OrderedProviders()
    {
        var byId = _allProviders.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var ordered = new List<ITranslationProvider>();
        void Add(string id)
        {
            if (byId.TryGetValue(id, out var p)
                && _settings.IsProviderEnabled(p.Id)
                && !ordered.Contains(p))
                ordered.Add(p);
        }

        Add(_settings.PrimaryProviderId);
        Add(_settings.FallbackProviderId);
        foreach (var p in _allProviders)
            if (_settings.IsProviderEnabled(p.Id) && !ordered.Contains(p))
                ordered.Add(p);
        // Safety net: free Google Translate last, even if the user did not
        // pick it — it needs no key and is almost always reachable.
        Add(GoogleDefaults.Id);
        return ordered;
    }

    /// <summary>Translate, trying providers in order until one succeeds.
    /// Throws the first provider's error when all of them fail.</summary>
    public async Task<TranslationResult> TranslateAsync(string text, string? sourceLang, string targetLang,
        CancellationToken cancellationToken = default)
    {
        var ordered = OrderedProviders();
        if (ordered.Count == 0)
            throw new TranslationException("No translation provider is enabled. Enable one in Settings.");

        Exception? firstError = null;
        foreach (var provider in ordered)
        {
            if (!provider.IsConfigured)
            {
                firstError ??= TranslationException.MissingApiKey(provider.DisplayName);
                continue;
            }
            try
            {
                var result = await provider.TranslateAsync(text, sourceLang, targetLang, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result))
                    return new TranslationResult(result, provider.DisplayName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                firstError ??= ex;
            }
        }

        throw firstError ?? TranslationException.EmptyResult();
    }

    /// <summary>IDs of the built-in providers (used by Settings defaults).</summary>
    public static class GoogleDefaults
    {
        public const string Id = "google";
    }
}
