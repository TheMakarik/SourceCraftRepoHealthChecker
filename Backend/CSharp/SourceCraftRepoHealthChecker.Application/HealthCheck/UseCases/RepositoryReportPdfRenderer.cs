using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public static class RepositoryReportPdfRenderer
{
    static RepositoryReportPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Render(RepositoryAnalysis analysis)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text($"Repo Health: {analysis.FullName}").SemiBold().FontSize(18);
                    column.Item().Text($"Repo Health Score: {analysis.Score}/100").FontSize(14);
                    column.Item().Text($"Ссылка: {analysis.Url}");
                    column.Item().Text($"Язык: {analysis.Language}");
                    column.Item().Text($"Лайки: {analysis.LikesCount}");
                    column.Item().Text(analysis.AnalyzedAt is null
                        ? "Дата последнего анализа: нет данных"
                        : $"Дата последнего анализа: {analysis.AnalyzedAt.Value:yyyy-MM-dd HH:mm} UTC");

                    column.Item().Text("Категории").SemiBold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        table.Cell().Text("Категория").SemiBold();
                        table.Cell().Text("Оценка").SemiBold();
                        table.Cell().Text("Статус").SemiBold();

                        foreach (var category in analysis.Categories)
                        {
                            table.Cell().Text(category.Category.ToString());
                            table.Cell().Text(category.Score.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Text(StatusText(category.DataStatus));
                        }
                    });

                    foreach (var highlight in new (string Title, IReadOnlyCollection<RepositoryAnalysisCategory> Categories)[]
                    {
                        ("Сильные стороны", analysis.Strengths),
                        ("Слабые стороны", analysis.Weaknesses)
                    })
                    {
                        column.Item().Text(highlight.Title).SemiBold().FontSize(14);
                        if (highlight.Categories.Count == 0)
                        {
                            column.Item().Text("Нет.");
                        }
                        else
                        {
                            foreach (var category in highlight.Categories)
                                column.Item().Text($"{category.Category}: {category.Score}");
                        }
                    }

                    column.Item().Text("Рекомендации").SemiBold().FontSize(14);
                    if (analysis.Recommendations.Count == 0)
                    {
                        column.Item().Text("Рекомендаций нет.");
                    }
                    else
                    {
                        foreach (var recommendation in analysis.Recommendations)
                        {
                            column.Item().Text($"[{recommendation.Priority}] {recommendation.Title}").SemiBold();
                            column.Item().Text($"Проблема: {recommendation.Problem}");
                            column.Item().Text($"Действие: {recommendation.Action}");
                            column.Item().Text($"Ожидаемый эффект: {recommendation.ExpectedImpact}");
                            column.Item().Text($"Источник: {recommendation.SourceReference}");
                        }
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string StatusText(DataStatus status) => status switch
    {
        DataStatus.Available => "Данные есть",
        DataStatus.NoData => "Нет данных",
        _ => "Источник недоступен"
    };
}
