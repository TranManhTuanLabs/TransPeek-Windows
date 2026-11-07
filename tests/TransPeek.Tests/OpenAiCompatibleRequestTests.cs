using System.Text.Json;
using TransPeek.Core.Translation;
using Xunit;

namespace TransPeek.Tests;

public sealed class OpenAiCompatibleRequestTests
{
    [Fact]
    public void BuildRequestJson_ContainsModelAndMessages()
    {
        var definition = CustomProviderDefinition.OpenAiDefault();
        var json = OpenAiCompatibleProvider.BuildRequestJson(definition, "hello", null, "vi");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
        Assert.Equal(0, root.GetProperty("temperature").GetDouble());
        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("Tiếng Việt", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("hello", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public void BuildRequestJson_DeepSeekDefault_HasDeepSeekModel()
    {
        var definition = CustomProviderDefinition.DeepSeekDefault();
        Assert.Equal("deepseek-chat", definition.Model);
        Assert.Equal("https://api.deepseek.com/v1", definition.BaseUrl);
        var json = OpenAiCompatibleProvider.BuildRequestJson(definition, "hi", "en", "vi");
        Assert.Contains("deepseek-chat", json);
    }

    [Fact]
    public void BuildRequestJson_NonStreaming()
    {
        var definition = CustomProviderDefinition.OpenAiDefault();
        var json = OpenAiCompatibleProvider.BuildRequestJson(definition, "hi", null, "vi");
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("stream").GetBoolean());
    }
}
