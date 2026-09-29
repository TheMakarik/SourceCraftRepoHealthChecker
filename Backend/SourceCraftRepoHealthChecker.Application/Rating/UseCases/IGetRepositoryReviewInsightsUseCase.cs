using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public interface IGetRepositoryReviewInsightsUseCase
{
    public Task<SourceCraftResult<RepositoryReviewInsights>> GetAsync(string repositoryId, CancellationToken cancellationToken);
}
