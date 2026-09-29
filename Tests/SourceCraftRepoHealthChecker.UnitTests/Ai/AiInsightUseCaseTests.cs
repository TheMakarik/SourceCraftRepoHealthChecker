using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Application.Ai.UseCases;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.Scheduling.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.Ai;

public sealed class AiInsightUseCaseTests
{
    private readonly IGetRepositoryAnalysisUseCase _getRepositoryAnalysisUseCase = A.Fake<IGetRepositoryAnalysisUseCase>();
    private readonly IChatClientFactory _chatClientFactory = A.Fake<IChatClientFactory>();
    private readonly ISecretProtector _secretProtector = A.Fake<ISecretProtector>();

    [Fact]
    public async Task GenerateAsync_WhenConfigured_ReturnsInsightForKind()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var chatClient = A.Fake<IChatClient>();
        A.CallTo(() => chatClient.GetResponseAsync(A<IEnumerable<ChatMessage>>._, A<ChatOptions?>._, A<CancellationToken>._))
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "AI INSIGHT")));
        A.CallTo(() => _chatClientFactory.Create(AiProviders.Yandex, null, "yandexgpt", "api-key")).Returns(chatClient);
        A.CallTo(() => _secretProtector.Unprotect("encrypted")).Returns("api-key");
        A.CallTo(() => _getRepositoryAnalysisUseCase.GetAsync("sc-1", A<CancellationToken>._)).Returns(CreateAnalysis());
        var systemUnderTests = new AiInsightUseCase(_getRepositoryAnalysisUseCase, _chatClientFactory, CreateDbContext(userId), _secretProtector, Options.Create(CreateOptions()));

        // Act
        var actual = await systemUnderTests.GenerateAsync("sc-1", userId, AiInsightKind.Recommendations, CancellationToken.None);

        // Assert
        actual.Kind.Should().Be(AiInsightKind.Recommendations);
        actual.Content.Should().Be("AI INSIGHT");
        actual.Provider.Should().Be(AiProviders.Yandex);
        actual.Model.Should().Be("yandexgpt");
    }

    [Fact]
    public async Task GenerateAsync_WhenSecurityTriage_UsesTriageInstruction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var chatClient = A.Fake<IChatClient>();
        IEnumerable<ChatMessage>? capturedMessages = null;
        A.CallTo(() => chatClient.GetResponseAsync(A<IEnumerable<ChatMessage>>._, A<ChatOptions?>._, A<CancellationToken>._))
            .Invokes((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) => capturedMessages = messages)
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "AI INSIGHT")));
        A.CallTo(() => _chatClientFactory.Create(AiProviders.Yandex, null, "yandexgpt", "api-key")).Returns(chatClient);
        A.CallTo(() => _secretProtector.Unprotect("encrypted")).Returns("api-key");
        A.CallTo(() => _getRepositoryAnalysisUseCase.GetAsync("sc-1", A<CancellationToken>._)).Returns(CreateAnalysis());
        var systemUnderTests = new AiInsightUseCase(_getRepositoryAnalysisUseCase, _chatClientFactory, CreateDbContext(userId), _secretProtector, Options.Create(CreateOptions()));

        // Act
        await systemUnderTests.GenerateAsync("sc-1", userId, AiInsightKind.SecurityTriage, CancellationToken.None);

        // Assert
        capturedMessages.Should().ContainSingle();
        capturedMessages!.Single().Text.Should().Contain("TRIAGE-INSTRUCTION");
    }

    [Fact]
    public async Task GenerateAsync_WhenUserHasNoAiSettings_Throws()
    {
        // Arrange
        var systemUnderTests = new AiInsightUseCase(_getRepositoryAnalysisUseCase, _chatClientFactory, CreateDbContext(userId: null), _secretProtector, Options.Create(CreateOptions()));

        // Act
        var act = () => systemUnderTests.GenerateAsync("sc-1", Guid.NewGuid(), AiInsightKind.ActionPlan, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    private static RepositoryAnalysis CreateAnalysis() =>
        new("sc-1", "demo", "owner/demo", "https://sourcecraft.dev/owner/demo", "C#", false, null, 5, 80, DataStatus.Available, DateTimeOffset.UtcNow,
            [
                new RepositoryAnalysisCategory(ScoreCategory.Security, 90, DataStatus.Available),
                new RepositoryAnalysisCategory(ScoreCategory.Documentation, 20, DataStatus.Available)
            ],
            [new RepositoryAnalysisMetric(MetricCode.DocumentationReadme, 0, 0, 0.3, DataStatus.Available)],
            [new RepositoryAnalysisCategory(ScoreCategory.Security, 90, DataStatus.Available)],
            [new RepositoryAnalysisCategory(ScoreCategory.Documentation, 20, DataStatus.Available)],
            [new RepositoryAnalysisRecommendation(RecommendationPriority.Critical, "title", "problem", "why", "evidence", "action", 5, "source")],
            [new RepositoryAnalysisFinding("Sast", "Medium", "Open", "exec-detected", "python", "prepare_modules.py", null)]);

    private static IRepoHealthCheckerDbContext CreateDbContext(Guid? userId)
    {
        var userAis = userId is null
            ? new List<UserAi>()
            : [new UserAi { Id = Guid.NewGuid(), UserId = userId.Value, AiProvider = AiProviders.Yandex, AiModel = "yandexgpt", AiToken = "encrypted" }];

        var dbContext = A.Fake<IRepoHealthCheckerDbContext>();
        A.CallTo(() => dbContext.UserAis).Returns(CreateDbSet(userAis));
        return dbContext;
    }

    private static DbSet<UserAi> CreateDbSet(IReadOnlyList<UserAi> items)
    {
        var queryable = (IQueryable<UserAi>)new TestAsyncEnumerable<UserAi>(items);
        var dbSet = A.Fake<DbSet<UserAi>>(options => options.Implements(typeof(IQueryable<UserAi>)));
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).Provider).Returns(queryable.Provider);
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).Expression).Returns(queryable.Expression);
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).ElementType).Returns(queryable.ElementType);
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).GetEnumerator()).ReturnsLazily(() => queryable.GetEnumerator());
        return dbSet;
    }

    private static AiOptions CreateOptions() => new()
    {
        SystemPrompt = "prompt",
        TestPrompt = "prompt",
        RecommendationsPrompt = "RECOMMENDATIONS-INSTRUCTION",
        ExplanationPrompt = "EXPLANATION-INSTRUCTION",
        ActionPlanPrompt = "ACTION-PLAN-INSTRUCTION",
        SecurityTriagePrompt = "TRIAGE-INSTRUCTION",
        RiskForecastPrompt = "RISK-INSTRUCTION",
        OpenAiBaseUrl = "https://api.openai.com/v1",
        AnthropicBaseUrl = "https://api.anthropic.com/v1/",
        GoogleGeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        YandexBaseUrl = "https://llm.api.cloud.yandex.net/v1",
        DeepSeekBaseUrl = "https://api.deepseek.com/v1",
        MaxOutputTokens = 1024
    };
}
