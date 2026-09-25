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
            if (category.Category == ScoreCategory.Security && category.DataStatus != DataStatus.Available)
            {
                recommendations.Add(SecurityDataNotice());
                continue;
            }

            if (category.DataStatus != DataStatus.Available || category.Score >= settings.MinimumAcceptableScore)
                continue;

            recommendations.Add(new RecommendationDraft(
                PriorityFor(category.Score, settings),
                ProblemFor(category),
                ActionFor(category.Category),
                $"Доведение категории до {settings.MinimumAcceptableScore} повысит итоговый Repo Health Score.",
                SourceReferenceFor(category)));
        }

        return recommendations;
    }

    private static RecommendationDraft SecurityDataNotice() => new(
        RecommendationPriority.Low,
        "Оценка безопасности недоступна. Подключите AppSec SourceCraft.",
        "Подключите AppSec SourceCraft.",
        "Появится оценка security-категории; статус «Нет данных» не штрафует итоговый Score.",
        "Security:DataStatus");

    private static RecommendationPriority PriorityFor(int score, RecommendationScoringOptions settings) =>
        score < settings.CriticalPriorityScore ? RecommendationPriority.Critical
        : score < settings.HighPriorityScore ? RecommendationPriority.High
        : RecommendationPriority.Medium;

    private static string ProblemFor(CategoryScoreResult category) => category.Category switch
    {
        ScoreCategory.Security => SecurityProblem(category),
        ScoreCategory.CodeHealth => CodeHealthProblem(category),
        ScoreCategory.Activity => ActivityProblem(category),
        ScoreCategory.Documentation => DocumentationProblem(category),
        ScoreCategory.CiCd => CiCdProblem(category),
        ScoreCategory.Issues => IssuesProblem(category),
        _ => $"Категория «{category.Category}» имеет низкий балл: {category.Score}."
    };

    private static string SecurityProblem(CategoryScoreResult category)
    {
        var critical = RawValue(category, MetricCode.SecurityCriticalFindings);
        var high = RawValue(category, MetricCode.SecurityHighFindings);
        var medium = RawValue(category, MetricCode.SecurityMediumFindings);
        var low = RawValue(category, MetricCode.SecurityLowFindings);

        var parts = new List<string>();
        if (critical > 0)
            parts.Add($"критических {critical:0}");
        if (high > 0)
            parts.Add($"высоких {high:0}");
        if (medium > 0)
            parts.Add($"средних {medium:0}");
        if (low > 0)
            parts.Add($"низких {low:0}");

        return parts.Count == 0
            ? $"Security требует внимания: {category.Score} из 100."
            : $"Открытые уязвимости AppSec: {string.Join(", ", parts)}.";
    }

    private static string CodeHealthProblem(CategoryScoreResult category)
    {
        var todo = RawValue(category, MetricCode.CodeHealthTodo);
        var fixme = RawValue(category, MetricCode.CodeHealthFixme);
        var oldestCommentDays = RawValue(category, MetricCode.CodeHealthStaleComments);

        return $"Технический долг: TODO — {todo:0}, FIXME — {fixme:0}, самый старый комментарий — {oldestCommentDays:0} дн.";
    }

    private static string ActivityProblem(CategoryScoreResult category)
    {
        var parts = new List<string>
        {
            $"последняя активность {RawValue(category, MetricCode.ActivityLastActivity):0.#} дн. назад",
            $"коммитов за период — {RawValue(category, MetricCode.ActivityCommitFrequency):0}",
            $"участников — {RawValue(category, MetricCode.ActivityContributors):0}",
            $"релизов — {RawValue(category, MetricCode.ActivityReleases):0}"
        };

        var mergeRequests = MetricValue(category, MetricCode.ActivityMergeRequests);
        if (mergeRequests is not null)
            parts.Add($"merge request — {mergeRequests.RawValue:0}");

        return $"Активность снижена: {string.Join(", ", parts)}.";
    }

    private static string DocumentationProblem(CategoryScoreResult category)
    {
        var missing = new List<string>();
        if (RawValue(category, MetricCode.DocumentationReadme) < 1)
            missing.Add("README");
        if (RawValue(category, MetricCode.DocumentationLicense) < 1)
            missing.Add("лицензия");
        if (RawValue(category, MetricCode.DocumentationContributing) < 1)
            missing.Add("CONTRIBUTING");
        if (RawValue(category, MetricCode.DocumentationCodeOwners) < 1)
            missing.Add("CODEOWNERS");
        if (RawValue(category, MetricCode.DocumentationLocalRun) < 1)
            missing.Add("инструкция локального запуска");
        if (RawValue(category, MetricCode.DocumentationBuildAndTest) < 1)
            missing.Add("инструкция сборки и тестов");

        return missing.Count == 0
            ? $"Документация требует внимания: {category.Score} из 100."
            : $"Отсутствует: {string.Join(", ", missing)}.";
    }

    private static string CiCdProblem(CategoryScoreResult category)
    {
        var runs = RawValue(category, MetricCode.CiCdPresence);
        var successRatio = RawValue(category, MetricCode.CiCdSuccessRatio);
        var durationMinutes = RawValue(category, MetricCode.CiCdPipelineDuration);

        return $"CI/CD: запусков — {runs:0}, успешность — {successRatio:P0}, средняя длительность — {durationMinutes:0.#} мин.";
    }

    private static string IssuesProblem(CategoryScoreResult category)
    {
        var parts = new List<string>
        {
            $"открытых — {RawValue(category, MetricCode.IssuesOpen):0}",
            $"закрытых — {RawValue(category, MetricCode.IssuesClosed):0}",
            $"зависших — {RawValue(category, MetricCode.IssuesStale):0}"
        };

        var firstResponse = MetricValue(category, MetricCode.IssuesFirstResponse);
        if (firstResponse is { DataStatus: DataStatus.Available })
            parts.Add($"первый ответ — {firstResponse.RawValue:0.#} дн.");

        var closeTime = MetricValue(category, MetricCode.IssuesCloseTime);
        if (closeTime is { DataStatus: DataStatus.Available })
            parts.Add($"закрытие — {closeTime.RawValue:0.#} дн.");

        return $"Issues требуют внимания: {string.Join(", ", parts)}.";
    }

    private static string SourceReferenceFor(CategoryScoreResult category) =>
        category.Metrics.Count == 0
            ? category.Category.ToString()
            : string.Join(", ", category.Metrics.Select(metric => metric.Code.ToString()));

    private static double RawValue(CategoryScoreResult category, MetricCode code) =>
        MetricValue(category, code)?.RawValue ?? 0;

    private static MetricScore? MetricValue(CategoryScoreResult category, MetricCode code) =>
        category.Metrics.FirstOrDefault(metric => metric.Code == code);

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
