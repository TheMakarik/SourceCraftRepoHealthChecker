namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public interface IAnalyzeRepositoryUseCase
{
    public Task<AnalyzeRepositoryResult> AnalyzeAsync(AnalyzeRepositoryRequest request, CancellationToken cancellationToken);
}
