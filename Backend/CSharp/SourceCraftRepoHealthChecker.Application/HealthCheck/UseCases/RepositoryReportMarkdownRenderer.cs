using System.Globalization;
using System.Text;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public static class RepositoryReportMarkdownRenderer
{
    public static string Render(RepositoryAnalysis analysis)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"# Repo Health: {analysis.FullName}"));
        builder.AppendLine();
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Ссылка: {analysis.Url}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Язык: {analysis.Language}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Лайки: {analysis.LikesCount}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Repo Health Score: {analysis.Score}/100"));
        builder.AppendLine(analysis.AnalyzedAt is null
            ? "- Дата последнего анализа: нет данных"
            : string.Create(CultureInfo.InvariantCulture, $"- Дата последнего анализа: {analysis.AnalyzedAt.Value:yyyy-MM-dd HH:mm} UTC"));
        builder.AppendLine();

        builder.AppendLine("## Категории");
        builder.AppendLine();
        builder.AppendLine("| Категория | Оценка | Статус |");
        builder.AppendLine("|---|---|---|");
        foreach (var category in analysis.Categories)
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {category.Category} | {category.Score} | {StatusText(category.DataStatus)} |"));
        builder.AppendLine();

        builder.AppendLine("## Метрики");
        builder.AppendLine();
        if (analysis.Metrics.Count == 0)
        {
            builder.AppendLine("Нет данных.");
        }
        else
        {
            builder.AppendLine("| Метрика | Значение | Балл | Вес | Статус |");
            builder.AppendLine("|---|---|---|---|---|");
            foreach (var metric in analysis.Metrics)
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {metric.Code} | {metric.RawValue:0.##} | {metric.NormalizedScore:0.##} | {metric.Weight:0.##} | {StatusText(metric.DataStatus)} |"));
        }
        builder.AppendLine();

        AppendHighlights(builder, "Сильные стороны", analysis.Strengths);
        AppendHighlights(builder, "Слабые стороны", analysis.Weaknesses);

        builder.AppendLine("## Рекомендации");
        builder.AppendLine();
        if (analysis.Recommendations.Count == 0)
        {
            builder.AppendLine("Рекомендаций нет.");
        }
        else
        {
            foreach (var recommendation in analysis.Recommendations)
            {
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"### [{recommendation.Priority}] {recommendation.Title}"));
                builder.AppendLine();
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Проблема: {recommendation.Problem}"));
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Почему важно: {recommendation.WhyImportant}"));
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Подтверждающие факты: {recommendation.Evidence}"));
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Действие: {recommendation.Action}"));
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Ожидаемый эффект: {recommendation.ExpectedImpact}"));
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Источник: {recommendation.SourceReference}"));
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static void AppendHighlights(StringBuilder builder, string title, IReadOnlyCollection<RepositoryAnalysisCategory> categories)
    {
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"## {title}"));
        builder.AppendLine();
        if (categories.Count == 0)
            builder.AppendLine("Нет.");
        else
            foreach (var category in categories)
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {category.Category}: {category.Score}"));
        builder.AppendLine();
    }

    private static string StatusText(DataStatus status) => status switch
    {
        DataStatus.Available => "Данные есть",
        DataStatus.NoData => "Нет данных",
        _ => "Источник недоступен"
    };
}
