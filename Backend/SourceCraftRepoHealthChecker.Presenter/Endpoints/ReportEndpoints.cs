using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Presenter.Authentication;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class ReportEndpoints
{
    private static readonly char[] InvalidFileNameCharacters = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0'];

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/repositories/{id}/report", async (string id, string? format, HttpContext context, IGetRepositoryAnalysisUseCase analysisUseCase, IExportRepositoryReportUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!await CanAccessAsync(id, context, analysisUseCase, cancellationToken))
                return Results.NotFound();

            var reportFormat = ParseFormat(format);
            var report = await useCase.GetAsync(id, reportFormat, cancellationToken);
            return report is null ? Results.NotFound() : Results.File(report.Content, report.ContentType, BuildFileName(id, reportFormat));
        });

        endpoints.MapGet("/api/repositories/{id}/report.md", async (string id, HttpContext context, IGetRepositoryAnalysisUseCase analysisUseCase, IExportRepositoryReportUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!await CanAccessAsync(id, context, analysisUseCase, cancellationToken))
                return Results.NotFound();

            var report = await useCase.GetAsync(id, ReportFormat.Markdown, cancellationToken);
            return report is null ? Results.NotFound() : Results.File(report.Content, report.ContentType, BuildFileName(id, ReportFormat.Markdown));
        });

        return endpoints;
    }

    private static async Task<bool> CanAccessAsync(string id, HttpContext context, IGetRepositoryAnalysisUseCase analysisUseCase, CancellationToken cancellationToken)
    {
        var analysis = await analysisUseCase.GetAsync(id, cancellationToken);
        return analysis is not null && (!analysis.IsPrivate || analysis.OwnerUserId == context.GetCurrentUserId());
    }

    public static ReportFormat ParseFormat(string? value) => value?.ToLowerInvariant() switch
    {
        "json" => ReportFormat.Json,
        "html" => ReportFormat.Html,
        "pdf" => ReportFormat.Pdf,
        _ => ReportFormat.Markdown
    };

    private static string BuildFileName(string id, ReportFormat format)
    {
        var safeId = string.Join('_', id.Split(InvalidFileNameCharacters, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeId))
            safeId = "repository";

        return $"{safeId}-report{Extension(format)}";
    }

    private static string Extension(ReportFormat format) => format switch
    {
        ReportFormat.Json => ".json",
        ReportFormat.Html => ".html",
        ReportFormat.Pdf => ".pdf",
        _ => ".md"
    };
}
