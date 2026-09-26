using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed class GetRepositoryAnalysisUseCase(
    IRepoHealthCheckerDbContext dbContext,
    IOptions<HealthCheckOptions> healthCheckOptions) : IGetRepositoryAnalysisUseCase
{
    public async Task<RepositoryAnalysis?> GetAsync(string sourceCraftId, CancellationToken cancellationToken)
    {
        var repository = await dbContext.Repositories
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.CategoryScores)
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.Metrics)
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.Recommendations)
            .FirstOrDefaultAsync(item => item.SourceCraftId == sourceCraftId, cancellationToken);

        if (repository is null)
            return null;

        var run = repository.AnalysisRuns
            .Where(item => item.Status == AnalysisStatus.Completed)
            .OrderByDescending(item => item.CompletedAt)
            .FirstOrDefault();

        if (run is null)
            return null;

        var thresholds = healthCheckOptions.Value.Recommendations;
        var categories = run.CategoryScores
            .Select(item => new RepositoryAnalysisCategory(item.Category, item.Score, item.DataStatus))
            .ToArray();

        var strengths = categories
            .Where(item => item.DataStatus == DataStatus.Available && item.Score >= thresholds.StrengthScore)
            .ToArray();
        var weaknesses = categories
            .Where(item => item.DataStatus == DataStatus.Available && item.Score < thresholds.MinimumAcceptableScore)
            .ToArray();

        var metrics = run.Metrics
            .Select(item => new RepositoryAnalysisMetric(item.Code, item.RawValue, item.NormalizedScore, item.Weight, item.DataStatus))
            .ToArray();

        var recommendations = run.Recommendations
            .OrderByDescending(item => item.Priority)
            .Select(item => new RepositoryAnalysisRecommendation(
                item.Priority,
                item.Title,
                item.Problem,
                item.WhyImportant,
                item.Evidence,
                item.Action,
                item.ExpectedImpact,
                item.SourceReference ?? string.Empty))
            .ToArray();

        return new RepositoryAnalysis(
            repository.SourceCraftId,
            repository.Name,
            repository.FullName,
            repository.Url,
            repository.Language,
            repository.IsPrivate,
            repository.OwnerId,
            repository.LikesCount,
            run.Score,
            run.CompletedAt,
            categories,
            metrics,
            strengths,
            weaknesses,
            recommendations);
    }
}
