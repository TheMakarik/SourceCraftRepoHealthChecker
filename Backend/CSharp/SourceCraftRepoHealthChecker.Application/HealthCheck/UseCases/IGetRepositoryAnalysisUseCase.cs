namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public interface IGetRepositoryAnalysisUseCase
{
    public Task<RepositoryAnalysis?> GetAsync(string sourceCraftId, CancellationToken cancellationToken);
}
