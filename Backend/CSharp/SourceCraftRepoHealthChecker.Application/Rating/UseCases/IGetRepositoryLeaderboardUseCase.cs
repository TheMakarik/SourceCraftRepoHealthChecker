using SourceCraftRepoHealthChecker.Application.Rating.Models;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public interface IGetRepositoryLeaderboardUseCase
{
    public Task<RepositoryLeaderboardPage> GetAsync(RepositoryLeaderboardQuery query, CancellationToken cancellationToken);
}
