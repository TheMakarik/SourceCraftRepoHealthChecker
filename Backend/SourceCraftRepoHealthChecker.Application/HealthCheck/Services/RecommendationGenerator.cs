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
        var availableWeight = categories
            .Where(category => category.DataStatus == DataStatus.Available)
            .Sum(category => category.Weight);

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
                WhyImportantFor(category.Category),
                EvidenceFor(category),
                ActionFor(category.Category),
                ExpectedImpactFor(category, settings.MinimumAcceptableScore, availableWeight),
                SourceReferenceFor(category)));
        }

        return recommendations;
    }

    private static RecommendationDraft SecurityDataNotice() => new(
        RecommendationPriority.Low,
        "Оценка безопасности недоступна. Подключите AppSec SourceCraft.",
        "Без результата AppSec SourceCraft категория Security остаётся со статусом «Нет данных» и не участвует в итоговом Score.",
        "Источник AppSec SourceCraft не подключён: нет данных SAST, SCA и secret scanning.",
        "Подключите AppSec SourceCraft.",
        "Появится оценка security-категории; статус «Нет данных» не штрафует итоговый Score.",
        "AppSec SourceCraft; метрика Security:DataStatus");

    private static RecommendationPriority PriorityFor(int score, RecommendationScoringOptions settings) =>
        score < settings.CriticalPriorityScore ? RecommendationPriority.Critical
        : score < settings.HighPriorityScore ? RecommendationPriority.High
        : RecommendationPriority.Medium;

    private static string ProblemFor(CategoryScoreResult category) => category.Category switch
    {
        ScoreCategory.Security => "Открытые уязвимости снижают категорию Security.",
        ScoreCategory.CodeHealth => "Технический долг в коде снижает категорию Code health.",
        ScoreCategory.Activity => "Активность проекта ниже приемлемого уровня.",
        ScoreCategory.Documentation => "Документация проекта неполная.",
        ScoreCategory.CiCd => "CI/CD настроен недостаточно надёжно.",
        ScoreCategory.Issues => "Работа с issue требует улучшения.",
        _ => $"Категория «{category.Category}» имеет низкий балл: {category.Score}."
    };

    private static string WhyImportantFor(ScoreCategory category) => category switch
    {
        ScoreCategory.Security => "Уязвимости влияют на безопасность пользователей и напрямую снижают итоговый Repo Health Score.",
        ScoreCategory.CodeHealth => "Накопленный технический долг замедляет развитие и повышает риск регрессий.",
        ScoreCategory.Activity => "Регулярная активность показывает, что проект поддерживается и развивается.",
        ScoreCategory.Documentation => "Документация снижает порог входа и упрощает сопровождение проекта.",
        ScoreCategory.CiCd => "Стабильный CI/CD ловит дефекты до релиза и ускоряет поставку.",
        ScoreCategory.Issues => "Быстрая обработка issue поддерживает доверие сообщества.",
        _ => "Показатель влияет на итоговую оценку здоровья репозитория."
    };

    private static string EvidenceFor(CategoryScoreResult category) => category.Category switch
    {
        ScoreCategory.Security => SecurityEvidence(category),
        ScoreCategory.CodeHealth => CodeHealthEvidence(category),
        ScoreCategory.Activity => ActivityEvidence(category),
        ScoreCategory.Documentation => DocumentationEvidence(category),
        ScoreCategory.CiCd => CiCdEvidence(category),
        ScoreCategory.Issues => IssuesEvidence(category),
        _ => $"Балл категории — {category.Score} из 100."
    };

    private static string SecurityEvidence(CategoryScoreResult category)
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

    private static string CodeHealthEvidence(CategoryScoreResult category)
    {
        var todo = RawValue(category, MetricCode.CodeHealthTodo);
        var fixme = RawValue(category, MetricCode.CodeHealthFixme);
        var oldestCommentDays = RawValue(category, MetricCode.CodeHealthStaleComments);

        return $"Технический долг: TODO — {todo:0}, FIXME — {fixme:0}, самый старый комментарий — {oldestCommentDays:0} дн.";
    }

    private static string ActivityEvidence(CategoryScoreResult category)
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

    private static string DocumentationEvidence(CategoryScoreResult category)
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

    private static string CiCdEvidence(CategoryScoreResult category)
    {
        var runs = RawValue(category, MetricCode.CiCdPresence);
        var successRatio = RawValue(category, MetricCode.CiCdSuccessRatio);
        var durationMinutes = RawValue(category, MetricCode.CiCdPipelineDuration);

        return $"CI/CD: запусков — {runs:0}, успешность — {successRatio:P0}, средняя длительность — {durationMinutes:0.#} мин.";
    }

    private static string IssuesEvidence(CategoryScoreResult category)
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

    private static string ExpectedImpactFor(CategoryScoreResult category, int minimumAcceptableScore, double availableWeight)
    {
        var gain = availableWeight <= 0
            ? 0
            : (int)Math.Round((minimumAcceptableScore - category.Score) * (category.Weight / availableWeight), MidpointRounding.AwayFromZero);

        return $"+{gain} балл(ов) к итоговому Repo Health Score при доведении категории до {minimumAcceptableScore}/100.";
    }

    private static string SourceReferenceFor(CategoryScoreResult category)
    {
        var source = category.Category switch
        {
            ScoreCategory.Security => "AppSec SourceCraft",
            ScoreCategory.CiCd => "SourceCraft CI/CD (pipeline runs)",
            ScoreCategory.Issues => "SourceCraft Issues",
            ScoreCategory.Activity => "SourceCraft Activity (commits, contributors, releases, merge requests)",
            ScoreCategory.Documentation => "Repository files (README, LICENSE, CONTRIBUTING, CODEOWNERS)",
            ScoreCategory.CodeHealth => "Source code comments (TODO/FIXME)",
            _ => category.Category.ToString()
        };

        return category.Metrics.Count == 0
            ? source
            : $"{source}; метрики: {string.Join(", ", category.Metrics.Select(metric => metric.Code))}";
    }

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
