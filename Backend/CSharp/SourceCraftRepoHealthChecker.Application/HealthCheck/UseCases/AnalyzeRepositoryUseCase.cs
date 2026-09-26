using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using DomainMetricScore = SourceCraftRepoHealthChecker.Domain.Entities.MetricScore;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed class AnalyzeRepositoryUseCase(
    IRepoHealthCheckerDbContext dbContext,
    ISourceCraftRepositoryCatalog catalog,
    ISourceCraftActivitySource activitySource,
    ISourceCraftCollaborationSource collaborationSource,
    ISourceCraftSecuritySource securitySource,
    ISourceCraftPipelineSource pipelineSource,
    ISourceCraftCodeHealthSource codeHealthSource,
    ISourceCraftDocumentationSource documentationSource,
    IHealthCheckEngine healthCheckEngine,
    IOptions<RepositoryOptions> repositoryOptions,
    IOptions<RecommendationOptions> recommendationOptions,
    TimeProvider timeProvider,
    ILogger<AnalyzeRepositoryUseCase> logger) : IAnalyzeRepositoryUseCase
{
    public async Task<AnalyzeRepositoryResult> AnalyzeAsync(AnalyzeRepositoryRequest request, CancellationToken cancellationToken)
    {
        var repositoryResult = await catalog.GetRepositoryAsync(request.RepositoryId, cancellationToken);
        if (repositoryResult.Status != DataStatus.Available || repositoryResult.Data is null)
            throw new RepositoryNotFoundException($"Repository '{request.RepositoryId}' is not available: {repositoryResult.Status}");

        logger.LogInformation("Analyzing repository {RepositoryId}", request.RepositoryId);

        var commits = await activitySource.GetCommitActivityAsync(request.RepositoryId, cancellationToken);
        var contributors = await activitySource.GetContributorsAsync(request.RepositoryId, cancellationToken);
        var releases = await activitySource.GetReleasesAsync(request.RepositoryId, cancellationToken);
        var issues = await collaborationSource.GetIssuesAsync(request.RepositoryId, cancellationToken);
        var mergeRequests = await collaborationSource.GetMergeRequestsAsync(request.RepositoryId, cancellationToken);
        var findings = await securitySource.GetFindingsAsync(request.RepositoryId, cancellationToken);
        var pipelineRuns = await pipelineSource.GetPipelineRunsAsync(request.RepositoryId, cancellationToken);
        var codeHealth = await codeHealthSource.GetCodeHealthAsync(request.RepositoryId, cancellationToken);
        var documentation = await documentationSource.GetDocumentationAsync(request.RepositoryId, cancellationToken);

        var facts = new RepositoryFacts(
            repositoryResult.Data,
            Combine(commits.Status, contributors.Status, releases.Status),
            commits.Data,
            contributors.Data ?? [],
            releases.Data ?? [],
            Combine(issues.Status, mergeRequests.Status),
            issues.Data ?? [],
            mergeRequests.Data ?? [],
            findings.Status,
            findings.Data ?? [],
            pipelineRuns.Status,
            pipelineRuns.Data ?? [],
            codeHealth.Status,
            codeHealth.Data,
            documentation.Status,
            documentation.Data);

        var healthCheck = await healthCheckEngine.CheckAsync(facts, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var repository = await UpsertRepositoryAsync(repositoryResult.Data, request.UserId, now, cancellationToken);
        var analysisRun = CreateAnalysisRun(request, repository.Id, healthCheck, now);

        dbContext.AnalysisRuns.Add(analysisRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Repository {RepositoryId} analyzed with score {Score} (run {AnalysisRunId})", request.RepositoryId, healthCheck.Score, analysisRun.Id);

        return new AnalyzeRepositoryResult(healthCheck, analysisRun.Id);
    }

    private AnalysisRun CreateAnalysisRun(AnalyzeRepositoryRequest request, Guid repositoryId, HealthCheckResult healthCheck, DateTimeOffset now)
    {
        var options = recommendationOptions.Value;
        var analysisRun = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            RepositoryId = repositoryId,
            UserId = request.UserId,
            Score = healthCheck.Score,
            Status = AnalysisStatus.Completed,
            DataStatus = healthCheck.Categories.Any(x => x.DataStatus == DataStatus.Available) ? DataStatus.Available : DataStatus.NoData,
            StartedAt = now,
            CompletedAt = now
        };

        foreach (var category in healthCheck.Categories)
        {
            analysisRun.CategoryScores.Add(new CategoryScore
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = analysisRun.Id,
                Category = category.Category,
                Score = category.Score,
                DataStatus = category.DataStatus
            });

            foreach (var metric in category.Metrics)
            {
                analysisRun.Metrics.Add(new DomainMetricScore
                {
                    Id = Guid.NewGuid(),
                    AnalysisRunId = analysisRun.Id,
                    Code = metric.Code,
                    RawValue = metric.RawValue,
                    NormalizedScore = metric.NormalizedScore,
                    Weight = metric.Weight,
                    DataStatus = metric.DataStatus
                });
            }
        }

        foreach (var recommendation in healthCheck.Recommendations)
        {
            analysisRun.Recommendations.Add(new Recommendation
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = analysisRun.Id,
                Priority = recommendation.Priority,
                Title = Truncate(recommendation.Problem, options.MaxTitleLength),
                Problem = Truncate(recommendation.Problem, options.MaxProblemLength),
                WhyImportant = Truncate(recommendation.WhyImportant, options.MaxWhyImportantLength),
                Evidence = Truncate(recommendation.Evidence, options.MaxEvidenceLength),
                Action = Truncate(recommendation.Action, options.MaxActionLength),
                ExpectedImpact = Truncate(recommendation.ExpectedImpact, options.MaxExpectedImpactLength),
                SourceReference = Truncate(recommendation.SourceReference, options.MaxSourceReferenceLength)
            });
        }

        return analysisRun;
    }

    private async Task<Repository> UpsertRepositoryAsync(SourceCraftRepository source, Guid? ownerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var options = repositoryOptions.Value;
        var repository = await dbContext.Repositories.FirstOrDefaultAsync(x => x.SourceCraftId == source.Id, cancellationToken);
        if (repository is null)
        {
            repository = new Repository { Id = Guid.NewGuid(), SourceCraftId = Truncate(source.Id, options.MaxSourceCraftIdLength), CreatedAt = now };
            dbContext.Repositories.Add(repository);
        }

        repository.Name = Truncate(source.Name, options.MaxNameLength);
        repository.FullName = Truncate(source.FullName, options.MaxFullNameLength);
        repository.Url = Truncate(source.Url, options.MaxUrlLength);
        repository.Language = Truncate(source.Language, options.MaxLanguageLength);
        repository.IsPrivate = source.IsPrivate;
        if (ownerId is not null)
            repository.OwnerId = ownerId;
        repository.LikesCount = source.LikesCount;
        repository.LastActivityAt = source.LastActivityAt;
        repository.AnalyzedAt = now;

        return repository;
    }

    private static DataStatus Combine(params DataStatus[] statuses)
    {
        if (statuses.Any(x => x == DataStatus.Unavailable))
            return DataStatus.Unavailable;
        if (statuses.All(x => x == DataStatus.NoData))
            return DataStatus.NoData;

        return DataStatus.Available;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
