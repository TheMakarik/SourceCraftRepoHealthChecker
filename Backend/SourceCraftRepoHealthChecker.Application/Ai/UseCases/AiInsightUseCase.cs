using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class AiInsightUseCase(
    IGetRepositoryAnalysisUseCase getRepositoryAnalysisUseCase,
    IChatClientFactory chatClientFactory,
    IAiRuntimeSettingsProvider settingsProvider,
    IOptions<AiOptions> options) : IAiInsightUseCase
{
    public async Task<AiInsightResult> GenerateAsync(string sourceCraftId, Guid userId, AiInsightKind kind, CancellationToken cancellationToken)
    {
        var settings = await settingsProvider.GetAsync(userId, cancellationToken)
            ?? throw new SourceCraftOperationException("AI provider is not configured for the current user");

        var analysis = await getRepositoryAnalysisUseCase.GetAsync(sourceCraftId, cancellationToken)
            ?? throw new RepositoryNotFoundException($"Repository '{sourceCraftId}' has no completed analysis");

        using var chatClient = chatClientFactory.Create(settings.Provider, settings.BaseUrl, settings.Model, settings.Token);

        try
        {
            var completion = await chatClient.GetResponseAsync(BuildPrompt(analysis, kind), chatClientFactory.CreateOptions(settings.Provider), cancellationToken);
            return new AiInsightResult(kind, AiCompletionText.Best(completion), settings.Provider, settings.Model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AiProviderException($"AI-провайдер {settings.Provider} вернул ошибку: {exception.Message}", exception);
        }
    }

    private string BuildPrompt(RepositoryAnalysis analysis, AiInsightKind kind) =>
        $"{ResolveInstruction(kind)}{Environment.NewLine}{Environment.NewLine}{BuildFactSheet(analysis)}";

    private string ResolveInstruction(AiInsightKind kind) => kind switch
    {
        AiInsightKind.Recommendations => options.Value.RecommendationsPrompt,
        AiInsightKind.Explanation => options.Value.ExplanationPrompt,
        AiInsightKind.ActionPlan => options.Value.ActionPlanPrompt,
        AiInsightKind.SecurityTriage => options.Value.SecurityTriagePrompt,
        AiInsightKind.RiskForecast => options.Value.RiskForecastPrompt,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown AI insight kind")
    };

    private static string BuildFactSheet(RepositoryAnalysis analysis)
    {
        var categories = string.Join(Environment.NewLine, analysis.Categories.Select(category => $"- {category.Category}: {category.Score}/100 ({category.DataStatus})"));
        var weaknesses = string.Join(", ", analysis.Weaknesses.Select(item => $"{item.Category} ({item.Score})"));
        var metrics = string.Join(Environment.NewLine, analysis.Metrics
            .Where(metric => metric.DataStatus == DataStatus.Available)
            .OrderBy(metric => metric.NormalizedScore)
            .Take(10)
            .Select(metric => $"- {metric.Code}: raw={metric.RawValue:0.##}, score={metric.NormalizedScore:0.#}"));
        var recommendations = string.Join(Environment.NewLine, analysis.Recommendations.Select(item => $"- [{item.Priority}] {item.Problem} → {item.Action}"));
        var findings = string.Join(Environment.NewLine, analysis.Findings.Select(item => $"- {item.Severity} {item.Kind} {item.Title} ({item.Status})"));

        return string.Join(Environment.NewLine,
            $"Репозиторий: {analysis.FullName}",
            $"Repo Health Score: {analysis.Score}/100",
            $"Категории:{Environment.NewLine}{categories}",
            $"Слабые стороны: {weaknesses}",
            $"Метрики с наименьшим баллом:{Environment.NewLine}{metrics}",
            $"Детерминированные рекомендации:{Environment.NewLine}{recommendations}",
            $"Находки безопасности:{Environment.NewLine}{findings}");
    }
}
