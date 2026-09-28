using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed class GetRepositoryHistoryUseCase(
    IRepoHealthCheckerDbContext dbContext,
    IOptions<RepositoryHistoryOptions> options) : IGetRepositoryHistoryUseCase
{
    public async Task<IReadOnlyCollection<RepositoryHistoryPoint>> GetAsync(string sourceCraftId, CancellationToken cancellationToken)
    {
        var repositories = await dbContext.Repositories
            .Where(item => item.SourceCraftId == sourceCraftId)
            .ToListAsync(cancellationToken);
        var repository = repositories.FirstOrDefault();
        if (repository is null)
            return [];

        var runs = await dbContext.AnalysisRuns
            .Where(item => item.RepositoryId == repository.Id && item.Status == AnalysisStatus.Completed)
            .ToListAsync(cancellationToken);

        return runs
            .OrderByDescending(item => item.CompletedAt ?? item.StartedAt)
            .Take(options.Value.MaxPoints)
            .OrderBy(item => item.CompletedAt ?? item.StartedAt)
            .Select(item => new RepositoryHistoryPoint(item.CompletedAt ?? item.StartedAt, item.Score))
            .ToArray();
    }
}
