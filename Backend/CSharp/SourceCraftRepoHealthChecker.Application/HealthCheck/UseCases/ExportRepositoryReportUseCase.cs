using System.Text;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed class ExportRepositoryReportUseCase(IGetRepositoryAnalysisUseCase getRepositoryAnalysisUseCase) : IExportRepositoryReportUseCase
{
    public async Task<ReportFile?> GetAsync(string sourceCraftId, ReportFormat format, CancellationToken cancellationToken)
    {
        var analysis = await getRepositoryAnalysisUseCase.GetAsync(sourceCraftId, cancellationToken);
        if (analysis is null)
            return null;

        return format switch
        {
            ReportFormat.Markdown => new ReportFile("text/markdown; charset=utf-8", Encoding.UTF8.GetBytes(RepositoryReportMarkdownRenderer.Render(analysis))),
            ReportFormat.Json => new ReportFile("application/json", RepositoryReportJsonRenderer.Render(analysis)),
            ReportFormat.Html => new ReportFile("text/html; charset=utf-8", Encoding.UTF8.GetBytes(RepositoryReportHtmlRenderer.Render(analysis))),
            ReportFormat.Pdf => new ReportFile("application/pdf", RepositoryReportPdfRenderer.Render(analysis)),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
}
