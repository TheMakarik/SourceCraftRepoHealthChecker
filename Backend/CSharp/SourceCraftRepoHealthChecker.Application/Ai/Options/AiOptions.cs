namespace SourceCraftRepoHealthChecker.Application.Ai.Options;

public sealed class AiOptions
{
    public required string SystemPrompt { get; init; }
    public required string OpenAiBaseUrl { get; init; }
    public required string OllamaBaseUrl { get; init; }
    public required string AnthropicBaseUrl { get; init; }
    public required string GoogleGeminiBaseUrl { get; init; }
    public required string YandexBaseUrl { get; init; }
    public required string XAiBaseUrl { get; init; }
    public required string DeepSeekBaseUrl { get; init; }
}
