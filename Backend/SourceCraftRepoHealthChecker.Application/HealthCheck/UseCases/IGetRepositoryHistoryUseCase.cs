namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public interface IGetRepositoryHistoryUseCase
{
    public Task<IReadOnlyCollection<RepositoryHistoryPoint>> GetAsync(string sourceCraftId, CancellationToken cancellationToken);
}
