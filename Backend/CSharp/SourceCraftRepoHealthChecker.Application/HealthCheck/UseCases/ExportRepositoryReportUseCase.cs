namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed class ExportRepositoryReportUseCase(IGetRepositoryAnalysisUseCase getRepositoryAnalysisUseCase) : IExportRepositoryReportUseCase
{
    public async Task<string?> GetMarkdownAsync(string sourceCraftId, CancellationToken cancellationToken)
    {
        var analysis = await getRepositoryAnalysisUseCase.GetAsync(sourceCraftId, cancellationToken);
        return analysis is null ? null : RepositoryReportMarkdownRenderer.Render(analysis);
    }
}
