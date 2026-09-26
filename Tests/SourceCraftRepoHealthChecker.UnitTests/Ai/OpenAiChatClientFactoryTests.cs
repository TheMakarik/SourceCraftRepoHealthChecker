using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Ai;

namespace SourceCraftRepoHealthChecker.UnitTests.Ai;

public sealed class OpenAiChatClientFactoryTests
{
    private readonly OpenAiChatClientFactory systemUnderTests = new(Options.Create(CreateOptions()));

    [Theory]
    [InlineData(AiProviders.OpenAI)]
    [InlineData(AiProviders.Ollama)]
    [InlineData(AiProviders.Anthropic)]
    [InlineData(AiProviders.GoogleGemini)]
    [InlineData(AiProviders.Yandex)]
    [InlineData(AiProviders.XAi)]
    [InlineData(AiProviders.DeepSeek)]
    public void Create_ForEveryProvider_ReturnsClient(AiProviders provider)
    {
        // Act
        using var actual = systemUnderTests.Create(provider, null, "model", "api-key");

        // Assert
        actual.Should().NotBeNull();
    }

    [Fact]
    public void Create_WhenBaseUrlProvided_UsesIt()
    {
        // Act
        using var actual = systemUnderTests.Create(AiProviders.Yandex, "https://custom.example/v1", "model", "api-key");

        // Assert
        actual.Should().NotBeNull();
    }

    private static AiOptions CreateOptions() => new()
    {
        SystemPrompt = "prompt",
        OpenAiBaseUrl = "https://api.openai.com/v1",
        OllamaBaseUrl = "http://localhost:11434/v1",
        AnthropicBaseUrl = "https://api.anthropic.com/v1/",
        GoogleGeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        YandexBaseUrl = "https://llm.api.cloud.yandex.net/v1",
        XAiBaseUrl = "https://api.x.ai/v1",
        DeepSeekBaseUrl = "https://api.deepseek.com/v1"
    };
}
