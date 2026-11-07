using TransPeek.Core.Settings;
using TransPeek.Core.Translation;
using Xunit;

namespace TransPeek.Tests;

public sealed class TranslationCoordinatorTests
{
    private sealed class FakeProvider : ITranslationProvider
    {
        private readonly Func<string, Task<string>> _translate;

        public FakeProvider(string id, string name, Func<string, Task<string>>? translate = null,
            bool isConfigured = true)
        {
            Id = id;
            DisplayName = name;
            _translate = translate ?? (text => Task.FromResult($"[{name}] {text}"));
            IsConfigured = isConfigured;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public bool NeedsNetwork => false;
        public bool NeedsApiKey => false;
        public bool IsAvailable => true;
        public bool IsConfigured { get; }
        public Task<string> TranslateAsync(string text, string? sourceLang, string targetLang,
            CancellationToken cancellationToken = default) => _translate(text);
    }

    private static AppSettings Settings(string primary, string fallback) => new()
    {
        PrimaryProviderId = primary,
        FallbackProviderId = fallback,
    };

    [Fact]
    public void OrderedProviders_PrimaryFirstFallbackSecond()
    {
        var google = new FakeProvider("google", "Google Translate");
        var custom = new FakeProvider("custom:openai", "OpenAI");
        var coordinator = new TranslationCoordinator(Settings("custom:openai", "google"), [custom, google]);
        var ordered = coordinator.OrderedProviders();
        Assert.Equal(["custom:openai", "google"], ordered.Select(p => p.Id).ToArray());
    }

    [Fact]
    public void OrderedProviders_DisabledProviderSkipped()
    {
        var google = new FakeProvider("google", "Google Translate");
        var custom = new FakeProvider("custom:openai", "OpenAI");
        var settings = Settings("custom:openai", "google");
        settings.SetProviderEnabled("custom:openai", false);
        var coordinator = new TranslationCoordinator(settings, [custom, google]);
        var ordered = coordinator.OrderedProviders();
        Assert.DoesNotContain(ordered, p => p.Id == "custom:openai");
        Assert.Equal("google", ordered[0].Id);
    }

    [Fact]
    public void OrderedProviders_GoogleSafetyNetEvenWhenNotPicked()
    {
        var custom = new FakeProvider("custom:openai", "OpenAI");
        var google = new FakeProvider("google", "Google Translate");
        var coordinator = new TranslationCoordinator(Settings("custom:openai", "custom:openai"), [custom, google]);
        var ordered = coordinator.OrderedProviders();
        Assert.Contains(ordered, p => p.Id == "google");
        Assert.Equal("google", ordered[^1].Id);
    }

    [Fact]
    public async Task TranslateAsync_UsesPrimaryAndReportsItsName()
    {
        var google = new FakeProvider("google", "Google Translate");
        var custom = new FakeProvider("custom:openai", "OpenAI");
        var coordinator = new TranslationCoordinator(Settings("custom:openai", "google"), [custom, google]);
        var result = await coordinator.TranslateAsync("hello", null, "vi");
        Assert.Equal("[OpenAI] hello", result.Text);
        Assert.Equal("OpenAI", result.ProviderName);
    }

    [Fact]
    public async Task TranslateAsync_FallsBackWhenPrimaryFails()
    {
        var failing = new FakeProvider("custom:openai", "OpenAI",
            _ => throw new TranslationException("boom"));
        var google = new FakeProvider("google", "Google Translate");
        var coordinator = new TranslationCoordinator(Settings("custom:openai", "google"), [failing, google]);
        var result = await coordinator.TranslateAsync("hello", null, "vi");
        Assert.Equal("[Google Translate] hello", result.Text);
        Assert.Equal("Google Translate", result.ProviderName);
    }

    [Fact]
    public async Task TranslateAsync_AllFail_ReportsFirstProvidersError()
    {
        var first = new FakeProvider("custom:openai", "OpenAI",
            _ => throw new TranslationException("first error"));
        var second = new FakeProvider("google", "Google Translate",
            _ => throw new TranslationException("second error"));
        var coordinator = new TranslationCoordinator(Settings("custom:openai", "google"), [first, second]);
        var ex = await Assert.ThrowsAsync<TranslationException>(
            () => coordinator.TranslateAsync("hello", null, "vi"));
        Assert.Equal("first error", ex.Message);
    }

    [Fact]
    public async Task TranslateAsync_UnconfiguredProvider_SkippedWithClearError()
    {
        var unconfigured = new FakeProvider("custom:openai", "OpenAI", isConfigured: false);
        var google = new FakeProvider("google", "Google Translate");
        var coordinator = new TranslationCoordinator(Settings("custom:openai", "google"), [unconfigured, google]);
        var result = await coordinator.TranslateAsync("hello", null, "vi");
        Assert.Equal("Google Translate", result.ProviderName);
    }
}
