using SourceCraftRepoHealthChecker.Application.Rating.Models;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public interface ICompareRepositoriesUseCase
{
    public Task<RepositoryComparison> GetAsync(IReadOnlyCollection<string> sourceCraftIds, CancellationToken cancellationToken);
}
