using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Application.Ai.UseCases;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.UnitTests.Ai;

public sealed class AiSummaryUseCaseTests
{
    private readonly IGetRepositoryAnalysisUseCase _getRepositoryAnalysisUseCase = A.Fake<IGetRepositoryAnalysisUseCase>();
    private readonly IChatClientFactory _chatClientFactory = A.Fake<IChatClientFactory>();
    private readonly IAiRuntimeSettingsProvider _settingsProvider = A.Fake<IAiRuntimeSettingsProvider>();

    [Fact]
    public async Task SummarizeAsync_WhenConfigured_ReturnsModelSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var chatClient = A.Fake<IChatClient>();
        A.CallTo(() => chatClient.GetResponseAsync(A<IEnumerable<ChatMessage>>._, A<ChatOptions?>._, A<CancellationToken>._))
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "AI SUMMARY")));
        A.CallTo(() => _chatClientFactory.Create(AiProviders.Yandex, null, "yandexgpt", "api-key")).Returns(chatClient);
        A.CallTo(() => _settingsProvider.GetAsync(userId, A<CancellationToken>._))
            .Returns(new AiRuntimeSettings(AiProviders.Yandex, null, "yandexgpt", "api-key"));
        A.CallTo(() => _getRepositoryAnalysisUseCase.GetAsync("sc-1", A<CancellationToken>._)).Returns(CreateAnalysis());
        var systemUnderTests = new AiSummaryUseCase(_getRepositoryAnalysisUseCase, _chatClientFactory, _settingsProvider, Options.Create(CreateOptions()));

        // Act
        var actual = await systemUnderTests.SummarizeAsync("sc-1", userId, CancellationToken.None);

        // Assert
        actual.Summary.Should().Be("AI SUMMARY");
        actual.Provider.Should().Be(AiProviders.Yandex);
        actual.Model.Should().Be("yandexgpt");
    }

    [Fact]
    public async Task SummarizeAsync_WhenUserHasNoAiSettings_Throws()
    {
        // Arrange
        var systemUnderTests = new AiSummaryUseCase(_getRepositoryAnalysisUseCase, _chatClientFactory, _settingsProvider, Options.Create(CreateOptions()));

        // Act
        var act = () => systemUnderTests.SummarizeAsync("sc-1", Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    private static RepositoryAnalysis CreateAnalysis() =>
        new("sc-1", "demo", "owner/demo", "https://sourcecraft.dev/owner/demo", "C#", false, null, 5, 80, DataStatus.Available, DateTimeOffset.UtcNow,
            [new RepositoryAnalysisCategory(ScoreCategory.Security, 90, DataStatus.Available)],
            [],
            [],
            [],
            [],
            []);

    private static AiOptions CreateOptions() => new()
    {
        SystemPrompt = "prompt",
        TestPrompt = "prompt",
        RecommendationsPrompt = "prompt",
        ExplanationPrompt = "prompt",
        ActionPlanPrompt = "prompt",
        SecurityTriagePrompt = "prompt",
        RiskForecastPrompt = "prompt",
        OpenAiBaseUrl = "https://api.openai.com/v1",
        AnthropicBaseUrl = "https://api.anthropic.com/v1/",
        GoogleGeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        YandexBaseUrl = "https://llm.api.cloud.yandex.net/v1",
        DeepSeekBaseUrl = "https://api.deepseek.com/v1",
        MaxOutputTokens = 1024
    };
}
