using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/repositories/{id}/report", async (string id, string? format, IExportRepositoryReportUseCase useCase, CancellationToken cancellationToken) =>
        {
            var report = await useCase.GetAsync(id, ParseFormat(format), cancellationToken);
            return report is null ? Results.NotFound() : Results.File(report.Content, report.ContentType);
        });

        endpoints.MapGet("/api/repositories/{id}/report.md", async (string id, IExportRepositoryReportUseCase useCase, CancellationToken cancellationToken) =>
        {
            var report = await useCase.GetAsync(id, ReportFormat.Markdown, cancellationToken);
            return report is null ? Results.NotFound() : Results.File(report.Content, report.ContentType);
        });

        return endpoints;
    }

    public static ReportFormat ParseFormat(string? value) => value?.ToLowerInvariant() switch
    {
        "json" => ReportFormat.Json,
        "html" => ReportFormat.Html,
        "pdf" => ReportFormat.Pdf,
        _ => ReportFormat.Markdown
    };
}
