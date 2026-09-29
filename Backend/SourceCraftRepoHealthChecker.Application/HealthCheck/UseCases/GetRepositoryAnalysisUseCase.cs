using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
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
                .ThenInclude(run => run.Findings)
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.Recommendations)
            .FirstOrDefaultAsync(item => item.SourceCraftId == sourceCraftId, cancellationToken);

        if (repository is null)
            return null;

        var validRun = repository.AnalysisRuns
            .Where(item => item.Status == AnalysisStatus.Completed && item.DataStatus == DataStatus.Available)
            .OrderByDescending(item => item.CompletedAt)
            .FirstOrDefault();

        var latestAttempt = repository.AnalysisRuns
            .OrderByDescending(item => item.CompletedAt ?? item.StartedAt)
            .FirstOrDefault();

        if (latestAttempt is null)
            return null;

        var run = validRun ?? latestAttempt;
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

        var findings = run.Findings
            .OrderByDescending(item => item.Severity)
            .Select(item => new RepositoryAnalysisFinding(
                item.Kind.ToString(),
                item.Severity.ToString(),
                item.Status.ToString(),
                item.Title,
                item.Package,
                item.FilePath,
                item.CvssScore,
                item.ExternalId,
                item.FileLine,
                item.CommitSha))
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
            validRun?.Score,
            validRun?.DataStatus ?? latestAttempt.DataStatus,
            validRun?.CompletedAt,
            categories,
            metrics,
            strengths,
            weaknesses,
            recommendations,
            findings,
            latestAttempt.Status,
            latestAttempt.CompletedAt ?? latestAttempt.StartedAt,
            latestAttempt.DataStatus);
    }
}
