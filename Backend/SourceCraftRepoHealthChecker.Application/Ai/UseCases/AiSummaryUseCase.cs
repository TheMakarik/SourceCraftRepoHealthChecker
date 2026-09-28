using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class AiSummaryUseCase(
    IGetRepositoryAnalysisUseCase getRepositoryAnalysisUseCase,
    IChatClientFactory chatClientFactory,
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector,
    IOptions<AiOptions> options) : IAiSummaryUseCase
{
    public async Task<AiSummaryResult> SummarizeAsync(string sourceCraftId, Guid userId, CancellationToken cancellationToken)
    {
        var userAi = (await dbContext.UserAis.Where(item => item.UserId == userId).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new SourceCraftOperationException("AI provider is not configured for the current user");

        var analysis = await getRepositoryAnalysisUseCase.GetAsync(sourceCraftId, cancellationToken)
            ?? throw new RepositoryNotFoundException($"Repository '{sourceCraftId}' has no completed analysis");

        using var chatClient = chatClientFactory.Create(userAi.AiProvider, userAi.AiBaseUrl, userAi.AiModel, secretProtector.Unprotect(userAi.AiToken));

        var completion = await chatClient.GetResponseAsync(BuildPrompt(analysis), cancellationToken: cancellationToken);
        return new AiSummaryResult(completion.Text, userAi.AiProvider, userAi.AiModel);
    }

    private string BuildPrompt(RepositoryAnalysis analysis)
    {
        var categories = string.Join(", ", analysis.Categories.Select(category => $"{category.Category}: {category.Score}"));
        return $"{options.Value.SystemPrompt}{Environment.NewLine}{Environment.NewLine}Репозиторий: {analysis.FullName}{Environment.NewLine}Repo Health Score: {analysis.Score}/100{Environment.NewLine}Категории: {categories}{Environment.NewLine}Рекомендаций: {analysis.Recommendations.Count}";
    }
}
