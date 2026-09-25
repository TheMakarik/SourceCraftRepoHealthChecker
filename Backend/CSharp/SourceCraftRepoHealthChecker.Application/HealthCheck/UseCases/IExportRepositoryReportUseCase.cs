namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public interface IExportRepositoryReportUseCase
{
    public Task<string?> GetMarkdownAsync(string sourceCraftId, CancellationToken cancellationToken);
}
