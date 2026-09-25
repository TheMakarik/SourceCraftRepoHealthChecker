using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Services;

public sealed class RecommendationGenerator(IOptions<HealthCheckOptions> options) : IRecommendationGenerator
{
    public IReadOnlyCollection<RecommendationDraft> Generate(IReadOnlyCollection<CategoryScoreResult> categories)
    {
        var settings = options.Value.Recommendations;
        var recommendations = new List<RecommendationDraft>();

        foreach (var category in categories)
        {
            if (category.DataStatus != DataStatus.Available || category.Score >= settings.MinimumAcceptableScore)
                continue;

            recommendations.Add(new RecommendationDraft(
                PriorityFor(category.Score, settings),
                $"Категория «{category.Category}» имеет низкий балл: {category.Score}.",
                ActionFor(category.Category),
                $"Доведение категории до {settings.MinimumAcceptableScore} повысит итоговый Repo Health Score.",
                null));
        }

        return recommendations;
    }

    private static RecommendationPriority PriorityFor(int score, RecommendationScoringOptions settings) =>
        score < settings.CriticalPriorityScore ? RecommendationPriority.Critical
        : score < settings.HighPriorityScore ? RecommendationPriority.High
        : RecommendationPriority.Medium;

    private static string ActionFor(ScoreCategory category) => category switch
    {
        ScoreCategory.Security => "Устраните уязвимости по данным AppSec SourceCraft.",
        ScoreCategory.CodeHealth => "Сократите TODO/FIXME и старый технический долг.",
        ScoreCategory.Activity => "Повысьте активность: коммиты, contributors, релизы.",
        ScoreCategory.Documentation => "Добавьте или улучшите README, лицензию, CONTRIBUTING и инструкции запуска.",
        ScoreCategory.CiCd => "Настройте CI и стабилизируйте пайплайны.",
        ScoreCategory.Issues => "Сократите число зависших issue и ускорьте ответы.",
        _ => "Улучшите показатели категории."
    };
}
