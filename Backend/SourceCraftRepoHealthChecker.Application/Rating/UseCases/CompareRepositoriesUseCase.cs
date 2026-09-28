using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Rating.Models;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public sealed class CompareRepositoriesUseCase(
    IRepoHealthCheckerDbContext dbContext,
    IGetRepositoryAnalysisUseCase getRepositoryAnalysisUseCase) : ICompareRepositoriesUseCase
{
    public async Task<RepositoryComparison> GetAsync(IReadOnlyCollection<string> sourceCraftIds, CancellationToken cancellationToken)
    {
        var repositories = await dbContext.Repositories
            .Where(item => !item.IsPrivate && sourceCraftIds.Contains(item.SourceCraftId))
            .ToDictionaryAsync(item => item.SourceCraftId, cancellationToken);

        var items = new List<RepositoryComparisonItem>();
        foreach (var sourceCraftId in sourceCraftIds)
        {
            if (!repositories.TryGetValue(sourceCraftId, out var repository))
                continue;

            var analysis = await getRepositoryAnalysisUseCase.GetAsync(sourceCraftId, cancellationToken);

            items.Add(new RepositoryComparisonItem(
                repository.SourceCraftId,
                repository.Name,
                repository.FullName,
                repository.Url,
                repository.Language,
                repository.LikesCount,
                repository.LastActivityAt,
                analysis?.Score,
                analysis?.AnalyzedAt,
                analysis?.Categories ?? [],
                analysis?.Metrics ?? [],
                analysis?.Strengths ?? [],
                analysis?.Weaknesses ?? []));
        }

        return new RepositoryComparison(items);
    }
}
