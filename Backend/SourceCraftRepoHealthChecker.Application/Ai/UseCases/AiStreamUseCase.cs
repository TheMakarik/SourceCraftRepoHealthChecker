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

public sealed class AiStreamUseCase(
    IGetRepositoryAnalysisUseCase getRepositoryAnalysisUseCase,
    IChatClientFactory chatClientFactory,
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector,
    IOptions<AiOptions> options) : IAiStreamUseCase
{
    public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
        string sourceCraftId,
        Guid userId,
        AiInsightKind? kind,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userAi = (await dbContext.UserAis.Where(item => item.UserId == userId).ToListAsync(cancellationToken)).FirstOrDefault();
        if (userAi is null || string.IsNullOrWhiteSpace(userAi.AiToken))
        {
            yield return new AiStreamEvent("error", "AI-провайдер не настроен: выберите провайдера и модель и сохраните токен.");
            yield break;
        }

        var analysis = await getRepositoryAnalysisUseCase.GetAsync(sourceCraftId, cancellationToken);
        if (analysis is null)
        {
            yield return new AiStreamEvent("error", "У репозитория ещё нет завершённого анализа.");
            yield break;
        }

        using var chatClient = chatClientFactory.Create(userAi.AiProvider, userAi.AiBaseUrl, userAi.AiModel, secretProtector.Unprotect(userAi.AiToken));
        var prompt = BuildPrompt(analysis, kind);
        var chatOptions = chatClientFactory.CreateOptions(userAi.AiProvider);
        var emittedText = false;

        await foreach (var update in chatClient.GetStreamingResponseAsync(prompt, chatOptions, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                emittedText = true;
                yield return new AiStreamEvent("delta", update.Text);
            }
            else
            {
                yield return new AiStreamEvent("reasoning");
            }
        }

        if (!emittedText)
        {
            // Reasoning-модели (например, deepseek-flash) могут не отдать текст в потоке — добираем обычным запросом.
            var completion = await chatClient.GetResponseAsync(prompt, chatOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(completion.Text))
                yield return new AiStreamEvent("delta", completion.Text);
            else
                yield return new AiStreamEvent("error", "Модель не вернула текст (вероятно, reasoning-модель израсходовала лимит или отдала ответ только в reasoning). Выберите обычную модель, например deepseek-chat, или другой провайдер.");
        }

        yield return new AiStreamEvent("done");
    }

    private string BuildPrompt(RepositoryAnalysis analysis, AiInsightKind? kind) =>
        $"{ResolveInstruction(kind)}{Environment.NewLine}{Environment.NewLine}{BuildFactSheet(analysis)}";

    private string ResolveInstruction(AiInsightKind? kind) => kind switch
    {
        null => options.Value.SystemPrompt,
        AiInsightKind.Recommendations => options.Value.RecommendationsPrompt,
        AiInsightKind.Explanation => options.Value.ExplanationPrompt,
        AiInsightKind.ActionPlan => options.Value.ActionPlanPrompt,
        AiInsightKind.SecurityTriage => options.Value.SecurityTriagePrompt,
        AiInsightKind.RiskForecast => options.Value.RiskForecastPrompt,
        _ => options.Value.SystemPrompt
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
