using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class AiSummaryUseCase(
    IGetRepositoryAnalysisUseCase getRepositoryAnalysisUseCase,
    IChatClientFactory chatClientFactory,
    IAiRuntimeSettingsProvider settingsProvider,
    IOptions<AiOptions> options) : IAiSummaryUseCase
{
    public async Task<AiSummaryResult> SummarizeAsync(string sourceCraftId, Guid userId, CancellationToken cancellationToken)
    {
        var settings = await settingsProvider.GetAsync(userId, cancellationToken)
            ?? throw new SourceCraftOperationException("AI provider is not configured for the current user");

        var analysis = await getRepositoryAnalysisUseCase.GetAsync(sourceCraftId, cancellationToken)
            ?? throw new RepositoryNotFoundException($"Repository '{sourceCraftId}' has no completed analysis");

        using var chatClient = chatClientFactory.Create(settings.Provider, settings.BaseUrl, settings.Model, settings.Token);

        var completion = await chatClient.GetResponseAsync(BuildPrompt(analysis), chatClientFactory.CreateOptions(settings.Provider), cancellationToken);
        return new AiSummaryResult(AiCompletionText.Best(completion), settings.Provider, settings.Model);
    }

    private string BuildPrompt(RepositoryAnalysis analysis)
    {
        var categories = string.Join(", ", analysis.Categories.Select(category => $"{category.Category}: {category.Score}"));
        return $"{options.Value.SystemPrompt}{Environment.NewLine}{Environment.NewLine}Репозиторий: {analysis.FullName}{Environment.NewLine}Repo Health Score: {analysis.Score}/100{Environment.NewLine}Категории: {categories}{Environment.NewLine}Рекомендаций: {analysis.Recommendations.Count}";
    }
}
