using System.Net;
using System.Text;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Presenter;

public static class AnalysisPageRenderer
{
    public static string Render(RepositoryAnalysis analysis)
    {
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\">");
        builder.Append($"<title>{WebUtility.HtmlEncode(analysis.FullName)} — Repo Health</title>");
        builder.Append("<style>body{font-family:sans-serif;margin:2rem}table{border-collapse:collapse;width:100%}");
        builder.Append("th,td{border:1px solid #ccc;padding:.4rem .6rem;text-align:left}th{background:#f4f4f4}</style></head><body>");
        builder.Append("<p><a href=\"/rating\">← К рейтингу</a></p>");
        builder.Append($"<h1>{WebUtility.HtmlEncode(analysis.FullName)}</h1>");
        builder.Append($"<p>Repo Health Score: <b>{analysis.Score}/100</b> · <a href=\"{WebUtility.HtmlEncode(analysis.Url)}\">репозиторий</a>");
        builder.Append($" · язык {WebUtility.HtmlEncode(analysis.Language)} · лайки {analysis.LikesCount}</p>");
        builder.Append(analysis.AnalyzedAt is null
            ? "<p>Дата последнего анализа: нет данных</p>"
            : $"<p>Дата последнего анализа: {analysis.AnalyzedAt.Value:yyyy-MM-dd HH:mm} UTC</p>");
        builder.Append($"<p><a href=\"/api/repositories/{WebUtility.UrlEncode(analysis.SourceCraftId)}/report.md\">Скачать отчёт (Markdown)</a></p>");

        builder.Append("<h2>Категории</h2><table><thead><tr><th>Категория</th><th>Оценка</th><th>Статус</th></tr></thead><tbody>");
        foreach (var category in analysis.Categories)
            builder.Append($"<tr><td>{category.Category}</td><td>{category.Score}</td><td>{StatusText(category.DataStatus)}</td></tr>");
        builder.Append("</tbody></table>");

        builder.Append("<h2>Метрики</h2>");
        if (analysis.Metrics.Count == 0)
        {
            builder.Append("<p>Нет данных.</p>");
        }
        else
        {
            builder.Append("<table><thead><tr><th>Метрика</th><th>Значение</th><th>Балл</th><th>Вес</th><th>Статус</th></tr></thead><tbody>");
            foreach (var metric in analysis.Metrics)
                builder.Append($"<tr><td>{metric.Code}</td><td>{metric.RawValue:0.##}</td><td>{metric.NormalizedScore:0.##}</td><td>{metric.Weight:0.##}</td><td>{StatusText(metric.DataStatus)}</td></tr>");
            builder.Append("</tbody></table>");
        }

        AppendList(builder, "Сильные стороны", analysis.Strengths);
        AppendList(builder, "Слабые стороны", analysis.Weaknesses);

        builder.Append("<h2>Рекомендации</h2>");
        if (analysis.Recommendations.Count == 0)
        {
            builder.Append("<p>Рекомендаций нет.</p>");
        }
        else
        {
            builder.Append("<ul>");
            foreach (var recommendation in analysis.Recommendations)
            {
                builder.Append($"<li><b>[{recommendation.Priority}]</b> {WebUtility.HtmlEncode(recommendation.Problem)}");
                builder.Append($"<br>Почему важно: {WebUtility.HtmlEncode(recommendation.WhyImportant)}");
                builder.Append($"<br>Подтверждающие факты: {WebUtility.HtmlEncode(recommendation.Evidence)}");
                builder.Append($"<br>→ {WebUtility.HtmlEncode(recommendation.Action)}");
                builder.Append($"<br><i>{WebUtility.HtmlEncode(recommendation.ExpectedImpact)}</i></li>");
            }
            builder.Append("</ul>");
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, string title, IReadOnlyCollection<RepositoryAnalysisCategory> categories)
    {
        builder.Append($"<h2>{title}</h2>");
        if (categories.Count == 0)
        {
            builder.Append("<p>Нет.</p>");
            return;
        }

        builder.Append("<ul>");
        foreach (var category in categories)
            builder.Append($"<li>{category.Category}: {category.Score}</li>");
        builder.Append("</ul>");
    }

    private static string StatusText(DataStatus status) => status switch
    {
        DataStatus.Available => "Данные есть",
        DataStatus.NoData => "Нет данных",
        _ => "Источник недоступен"
    };
}
