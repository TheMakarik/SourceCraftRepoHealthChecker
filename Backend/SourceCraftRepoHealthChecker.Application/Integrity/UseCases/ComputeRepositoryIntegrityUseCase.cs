using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.Integrity.Models;
using SourceCraftRepoHealthChecker.Application.Integrity.Services;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Integrity.UseCases;

public sealed class ComputeRepositoryIntegrityUseCase(
    IRepoHealthCheckerDbContext dbContext,
    RepositoryIntegrityCalculator calculator) : IComputeRepositoryIntegrityUseCase
{
    private const string ActivityAnomalySourceReference = "Activity:anomaly";

    public async Task<RepositoryIntegrity?> GetAsync(string sourceCraftId, CancellationToken cancellationToken)
    {
        var repository = await dbContext.Repositories
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.Metrics)
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.Recommendations)
            .FirstOrDefaultAsync(item => item.SourceCraftId == sourceCraftId, cancellationToken);
        if (repository is null)
            return null;

        var latestRun = repository.AnalysisRuns
            .Where(run => run.Status == AnalysisStatus.Completed)
            .OrderByDescending(run => run.CompletedAt)
            .FirstOrDefault();

        var persistedAnomalies = latestRun?.Recommendations
            .Where(recommendation => string.Equals(recommendation.SourceReference, ActivityAnomalySourceReference, StringComparison.Ordinal))
            .Select(recommendation => recommendation.Problem)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        var facts = BuildPersistedFacts(repository);
        var input = new RepositoryIntegrityInput(facts, repository.LikesCount, repository.LastActivityAt, persistedAnomalies);
        return calculator.Calculate(input);
    }

    private static RepositoryFacts BuildPersistedFacts(Repository repository)
    {
        var sourceRepository = new SourceCraftRepository(
            repository.SourceCraftId,
            repository.Name,
            repository.FullName,
            repository.Url,
            repository.Language,
            repository.LikesCount,
            repository.LastActivityAt,
            repository.IsPrivate,
            string.Empty);

        return new RepositoryFacts(
            sourceRepository,
            DataStatus.NoData,
            null,
            [],
            [],
            DataStatus.NoData,
            [],
            [],
            DataStatus.NoData,
            [],
            DataStatus.NoData,
            [],
            DataStatus.NoData,
            null,
            DataStatus.NoData,
            null);
    }
}
