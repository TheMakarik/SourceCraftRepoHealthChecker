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
    IAnomalyDetector anomalyDetector,
    IOptions<RepositoryOptions> repositoryOptions,
    IOptions<RecommendationOptions> recommendationOptions,
    IOptions<SecurityFindingOptions> securityFindingOptions,
    TimeProvider timeProvider,
    ILogger<AnalyzeRepositoryUseCase> logger) : IAnalyzeRepositoryUseCase
{
    public async Task<AnalyzeRepositoryResult> AnalyzeAsync(AnalyzeRepositoryRequest request, CancellationToken cancellationToken)
    {
        var repositoryResult = await catalog.GetRepositoryAsync(request.RepositoryId, cancellationToken);
        if (repositoryResult.Status != DataStatus.Available || repositoryResult.Data is null)
            throw new RepositoryNotFoundException($"Repository '{request.RepositoryId}' is not available: {repositoryResult.Status}");

        var source = repositoryResult.Data;
        var existingRepository = await dbContext.Repositories.FirstOrDefaultAsync(x => x.SourceCraftId == source.Id, cancellationToken);
        if (!IsAccessible(source, existingRepository, request.UserId))
            throw new RepositoryAccessDeniedException($"User is not allowed to analyze repository '{request.RepositoryId}'");

        logger.LogInformation("Analyzing repository {RepositoryId}", request.RepositoryId);

        var startedAt = timeProvider.GetUtcNow();
        var repository = UpsertRepository(source, existingRepository, startedAt);
        var analysisRun = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            UserId = request.UserId,
            Status = AnalysisStatus.Running,
            DataStatus = DataStatus.NoData,
            StartedAt = startedAt
        };

        dbContext.AnalysisRuns.Add(analysisRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
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
                source,
                commits.Status,
                commits.Data,
                contributors.Status,
                contributors.Data ?? [],
                releases.Status,
                releases.Data ?? [],
                issues.Status,
                issues.Data ?? [],
                mergeRequests.Status,
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
            var anomalies = anomalyDetector.Detect(facts);

            PopulateAnalysisRun(analysisRun, healthCheck, anomalies, facts.Findings);
            if (healthCheck.DataStatus == DataStatus.Available)
                repository.AnalyzedAt = startedAt;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Repository {RepositoryId} analyzed with score {Score} (run {AnalysisRunId})", request.RepositoryId, healthCheck.Score, analysisRun.Id);

            return new AnalyzeRepositoryResult(healthCheck, analysisRun.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            analysisRun.Status = AnalysisStatus.Failed;
            analysisRun.DataStatus = DataStatus.NoData;
            analysisRun.CompletedAt = timeProvider.GetUtcNow();
            analysisRun.ErrorMessage = exception.Message;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool IsAccessible(SourceCraftRepository source, Repository? repository, Guid? userId)
    {
        if (!source.IsPrivate)
            return true;

        if (userId is null)
            return true;

        if (repository?.OwnerId is not null)
            return repository.OwnerId == userId;

        return true;
    }

    private void PopulateAnalysisRun(AnalysisRun analysisRun, HealthCheckResult healthCheck, IReadOnlyCollection<ActivityAnomaly> anomalies, IReadOnlyCollection<SecurityFinding> findings)
    {
        var options = recommendationOptions.Value;
        analysisRun.Score = healthCheck.Score;
        analysisRun.Status = AnalysisStatus.Completed;
        analysisRun.DataStatus = healthCheck.DataStatus;
        analysisRun.CompletedAt = timeProvider.GetUtcNow();

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
                ExpectedImpact = recommendation.ExpectedImpact,
                SourceReference = Truncate(recommendation.SourceReference, options.MaxSourceReferenceLength)
            });
        }

        foreach (var anomaly in anomalies)
        {
            analysisRun.Recommendations.Add(new Recommendation
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = analysisRun.Id,
                Priority = RecommendationPriority.High,
                Title = Truncate($"Аномалия активности: {anomaly.AuthorLogin}", options.MaxTitleLength),
                Problem = Truncate(anomaly.Description, options.MaxProblemLength),
                WhyImportant = Truncate("Возможная накрутка активности или подозрительная активность.", options.MaxWhyImportantLength),
                Evidence = Truncate(anomaly.Description, options.MaxEvidenceLength),
                Action = Truncate($"Проверьте активность автора {anomaly.AuthorLogin}.", options.MaxActionLength),
                ExpectedImpact = null,
                SourceReference = Truncate("Activity:anomaly", options.MaxSourceReferenceLength)
            });
        }

        var findingOptions = securityFindingOptions.Value;
        foreach (var finding in findings)
        {
            analysisRun.Findings.Add(new AnalysisFinding
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = analysisRun.Id,
                Kind = finding.Kind,
                Severity = finding.Severity,
                Status = finding.Status,
                Title = Truncate(finding.Title, findingOptions.MaxTitleLength),
                Package = finding.Package is null ? null : Truncate(finding.Package, findingOptions.MaxPackageLength),
                FilePath = finding.FilePath is null ? null : Truncate(finding.FilePath, findingOptions.MaxFilePathLength),
                CvssScore = finding.CvssScore,
                ExternalId = finding.Id,
                FileLine = finding.FileLine,
                CommitSha = finding.CommitSha
            });
        }
    }

    private Repository UpsertRepository(SourceCraftRepository source, Repository? repository, DateTimeOffset now)
    {
        var options = repositoryOptions.Value;
        if (repository is null)
        {
            repository = new Repository
            {
                Id = Guid.NewGuid(),
                SourceCraftId = Truncate(source.Id, options.MaxSourceCraftIdLength),
                CreatedAt = now,
                OwnerId = source.OwnerUserId
            };
            dbContext.Repositories.Add(repository);
        }

        repository.Name = Truncate(source.Name, options.MaxNameLength);
        repository.FullName = Truncate(source.FullName, options.MaxFullNameLength);
        repository.Url = Truncate(source.Url, options.MaxUrlLength);
        repository.Language = Truncate(source.Language, options.MaxLanguageLength);
        repository.IsPrivate = source.IsPrivate;
        repository.LikesCount = source.LikesCount;
        repository.LastActivityAt = source.LastActivityAt;

        return repository;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
