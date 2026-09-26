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

public sealed class AiSummaryUseCaseTests
{
    private readonly IGetRepositoryAnalysisUseCase _getRepositoryAnalysisUseCase = A.Fake<IGetRepositoryAnalysisUseCase>();
    private readonly IChatClientFactory _chatClientFactory = A.Fake<IChatClientFactory>();
    private readonly ISecretProtector _secretProtector = A.Fake<ISecretProtector>();

    [Fact]
    public async Task SummarizeAsync_WhenConfigured_ReturnsModelSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var chatClient = A.Fake<IChatClient>();
        A.CallTo(() => chatClient.GetResponseAsync(A<IEnumerable<ChatMessage>>._, A<ChatOptions?>._, A<CancellationToken>._))
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "AI SUMMARY")));
        A.CallTo(() => _chatClientFactory.Create(AiProviders.Yandex, null, "yandexgpt", "api-key")).Returns(chatClient);
        A.CallTo(() => _secretProtector.Unprotect("encrypted")).Returns("api-key");
        A.CallTo(() => _getRepositoryAnalysisUseCase.GetAsync("sc-1", A<CancellationToken>._)).Returns(CreateAnalysis());
        var systemUnderTests = new AiSummaryUseCase(_getRepositoryAnalysisUseCase, _chatClientFactory, CreateDbContext(userId), _secretProtector, Options.Create(CreateOptions()));

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
        var systemUnderTests = new AiSummaryUseCase(_getRepositoryAnalysisUseCase, _chatClientFactory, CreateDbContext(userId: null), _secretProtector, Options.Create(CreateOptions()));

        // Act
        var act = () => systemUnderTests.SummarizeAsync("sc-1", Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    private static RepositoryAnalysis CreateAnalysis() =>
        new("sc-1", "demo", "owner/demo", "https://sourcecraft.dev/owner/demo", "C#", false, null, 5, 80, DateTimeOffset.UtcNow,
            [new RepositoryAnalysisCategory(ScoreCategory.Security, 90, DataStatus.Available)],
            [],
            [],
            [],
            []);

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
        OpenAiBaseUrl = "https://api.openai.com/v1",
        OllamaBaseUrl = "http://localhost:11434/v1",
        AnthropicBaseUrl = "https://api.anthropic.com/v1/",
        GoogleGeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        YandexBaseUrl = "https://llm.api.cloud.yandex.net/v1",
        XAiBaseUrl = "https://api.x.ai/v1",
        DeepSeekBaseUrl = "https://api.deepseek.com/v1"
    };
}
