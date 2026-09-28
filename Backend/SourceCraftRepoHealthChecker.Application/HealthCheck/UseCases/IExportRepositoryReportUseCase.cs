namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public interface IExportRepositoryReportUseCase
{
    Task<ReportFile?> GetAsync(string sourceCraftId, ReportFormat format, CancellationToken cancellationToken);
}
