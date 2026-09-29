using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using SourceCraftRepoHealthChecker.Application.Ai;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.Ai;

public sealed class OpenAiChatClientFactory(IOptions<AiOptions> options) : IChatClientFactory
{
    public IChatClient Create(AiProviders provider, string? baseUrl, string model, string apiKey)
    {
        var endpoint = string.IsNullOrWhiteSpace(baseUrl) ? ResolveBaseUrl(provider) : baseUrl;
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
        return client.GetChatClient(model).AsIChatClient();
    }

    public ChatOptions CreateOptions(AiProviders provider)
    {
        var chatOptions = new ChatOptions { MaxOutputTokens = options.Value.MaxOutputTokens };
        if (provider == AiProviders.DeepSeek)
            chatOptions.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None };
        return chatOptions;
    }

    private string ResolveBaseUrl(AiProviders provider) => provider switch
    {
        AiProviders.OpenAI => options.Value.OpenAiBaseUrl,
        AiProviders.Anthropic => options.Value.AnthropicBaseUrl,
        AiProviders.GoogleGemini => options.Value.GoogleGeminiBaseUrl,
        AiProviders.Yandex => options.Value.YandexBaseUrl,
        AiProviders.DeepSeek => options.Value.DeepSeekBaseUrl,
        _ => options.Value.OpenAiBaseUrl
    };
}
