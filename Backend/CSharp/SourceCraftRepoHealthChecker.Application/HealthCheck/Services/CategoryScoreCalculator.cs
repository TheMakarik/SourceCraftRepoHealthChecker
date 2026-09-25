using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Services;

public sealed class CategoryScoreCalculator(
    IOptions<HealthCheckOptions> options,
    IMetricNormalizer normalizer,
    TimeProvider timeProvider) : ICategoryScoreCalculator
{
    private readonly HealthCheckOptions _options = options.Value;

    public CategoryScoreResult Calculate(ScoreCategory category, RepositoryFacts facts) => category switch
    {
        ScoreCategory.Documentation => CalculateDocumentation(facts),
        ScoreCategory.CiCd => CalculateCiCd(facts),
        ScoreCategory.Security => CalculateSecurity(facts),
        ScoreCategory.Activity => CalculateActivity(facts),
        ScoreCategory.Issues => CalculateIssues(facts),
        ScoreCategory.CodeHealth => CalculateCodeHealth(facts),
        _ => NoData(category)
    };

    private CategoryScoreResult CalculateDocumentation(RepositoryFacts facts)
    {
        if (facts.DocumentationAvailability != DataStatus.Available || facts.Documentation is null)
            return NoData(ScoreCategory.Documentation);

        var settings = _options.Documentation;
        var maximum = _options.ScoreScale.MaximumScore;
        var minimum = _options.ScoreScale.MinimumScore;
        var report = facts.Documentation;

        var metrics = new List<MetricScore>
        {
            BinaryMetric(MetricCode.DocumentationReadme, report.HasReadme, settings.ReadmeWeight),
            BinaryMetric(MetricCode.DocumentationLicense, report.HasLicense, settings.LicenseWeight),
            BinaryMetric(MetricCode.DocumentationContributing, report.HasContributing, settings.ContributingWeight),
            BinaryMetric(MetricCode.DocumentationCodeOwners, report.HasCodeOwners, settings.CodeOwnersWeight),
            BinaryMetric(MetricCode.DocumentationLocalRun, report.HasLocalRunInstructions, settings.LocalRunWeight),
            BinaryMetric(MetricCode.DocumentationBuildAndTest, report.HasBuildAndTestInstructions, settings.BuildAndTestWeight)
        };

        return FromMetrics(ScoreCategory.Documentation, metrics);
    }

    private CategoryScoreResult CalculateSecurity(RepositoryFacts facts)
    {
        if (facts.SecurityAvailability != DataStatus.Available)
            return NoData(ScoreCategory.Security);

        var settings = _options.Security;
        var maximum = _options.ScoreScale.MaximumScore;
        var minimum = _options.ScoreScale.MinimumScore;
        var findings = facts.Findings;

        var critical = CountOpen(findings, SecuritySeverity.Critical);
        var high = CountOpen(findings, SecuritySeverity.High);
        var medium = CountOpen(findings, SecuritySeverity.Medium);
        var low = CountOpen(findings, SecuritySeverity.Low);
        var fixedFindings = findings.Count(x => x.Status == SecurityFindingStatus.Fixed);

        var penalty = critical * settings.CriticalPenalty
            + high * settings.HighPenalty
            + medium * settings.MediumPenalty
            + low * settings.LowPenalty
            - fixedFindings * settings.FixedFindingCredit;
        var score = Math.Clamp(maximum - penalty, minimum, maximum);

        var metrics = new List<MetricScore>
        {
            Metric(MetricCode.SecuritySast, CountKind(findings, SecurityFindingKind.Sast), Math.Clamp(maximum - CountKind(findings, SecurityFindingKind.Sast) * settings.LowPenalty, minimum, maximum), settings.LowPenalty, DataStatus.Available),
            Metric(MetricCode.SecuritySca, CountKind(findings, SecurityFindingKind.Sca), Math.Clamp(maximum - CountKind(findings, SecurityFindingKind.Sca) * settings.LowPenalty, minimum, maximum), settings.LowPenalty, DataStatus.Available),
            Metric(MetricCode.SecuritySecretScanning, CountKind(findings, SecurityFindingKind.SecretScanning), Math.Clamp(maximum - CountKind(findings, SecurityFindingKind.SecretScanning) * settings.LowPenalty, minimum, maximum), settings.LowPenalty, DataStatus.Available),
            Metric(MetricCode.SecurityCriticalFindings, critical, Math.Clamp(maximum - critical * settings.CriticalPenalty, minimum, maximum), settings.CriticalPenalty, DataStatus.Available),
            Metric(MetricCode.SecurityFixedFindings, fixedFindings, Math.Clamp(minimum + fixedFindings * settings.FixedFindingCredit, minimum, maximum), settings.FixedFindingCredit, DataStatus.Available)
        };

        return new CategoryScoreResult(ScoreCategory.Security, (int)Math.Round(score, MidpointRounding.AwayFromZero), WeightFor(ScoreCategory.Security), DataStatus.Available, metrics);
    }

    private CategoryScoreResult CalculateActivity(RepositoryFacts facts)
    {
        if (facts.ActivityAvailability != DataStatus.Available)
            return NoData(ScoreCategory.Activity);

        var settings = _options.Activity;
        var now = timeProvider.GetUtcNow();

        var lastActivity = facts.Commits?.LastCommitAt ?? facts.Repository.LastActivityAt;
        var daysSinceLastActivity = Math.Max(0, (now - lastActivity).TotalDays);
        var lastActivityScore = normalizer.Normalize(daysSinceLastActivity, settings.StaleAfterDays, settings.ActiveWithinDays);

        var windowStart = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-settings.ActiveWithinDays));
        var commitsInWindow = facts.Commits is null
            ? 0
            : facts.Commits.CommitsByDay.Where(x => x.Key >= windowStart).Sum(x => x.Value);
        var commitScore = normalizer.Normalize(commitsInWindow, 0, settings.CommitFrequencyForFullScore);

        var contributors = facts.Contributors.Count(x => !x.IsBot);
        var contributorScore = normalizer.Normalize(contributors, 0, settings.ContributorsForFullScore);

        var releases = facts.Releases.Count;
        var releaseScore = normalizer.Normalize(releases, 0, settings.ReleasesForFullScore);

        var metrics = new List<MetricScore>
        {
            Metric(MetricCode.ActivityLastActivity, Math.Round(daysSinceLastActivity, 2), lastActivityScore, 1, DataStatus.Available),
            Metric(MetricCode.ActivityCommitFrequency, commitsInWindow, commitScore, 1, DataStatus.Available),
            Metric(MetricCode.ActivityContributors, contributors, contributorScore, 1, DataStatus.Available),
            Metric(MetricCode.ActivityReleases, releases, releaseScore, 1, DataStatus.Available)
        };

        if (facts.CollaborationAvailability == DataStatus.Available)
        {
            var mergeRequests = facts.MergeRequests.Count;
            var mergeRequestScore = normalizer.Normalize(mergeRequests, 0, settings.ContributorsForFullScore);
            metrics.Add(Metric(MetricCode.ActivityMergeRequests, mergeRequests, mergeRequestScore, 1, DataStatus.Available));
        }

        return FromMetrics(ScoreCategory.Activity, metrics);
    }

    private CategoryScoreResult CalculateCiCd(RepositoryFacts facts)
    {
        if (facts.PipelineAvailability != DataStatus.Available)
            return NoData(ScoreCategory.CiCd);

        var settings = _options.CiCd;
        var maximum = _options.ScoreScale.MaximumScore;
        var minimum = _options.ScoreScale.MinimumScore;
        var runs = facts.PipelineRuns;

        if (runs.Count == 0)
            return new CategoryScoreResult(ScoreCategory.CiCd, minimum, WeightFor(ScoreCategory.CiCd), DataStatus.Available,
                new List<MetricScore> { Metric(MetricCode.CiCdPresence, 0, minimum, 1, DataStatus.Available) });

        var presenceScore = normalizer.Normalize(runs.Count, 0, settings.PipelineRunsForFullScore);

        var finished = runs.Where(x => x.Status is PipelineStatus.Success or PipelineStatus.Failed).ToArray();
        var successRatio = finished.Length == 0 ? 0 : (double)finished.Count(x => x.Status == PipelineStatus.Success) / finished.Length;
        var successScore = normalizer.Normalize(successRatio, 0, settings.MinimumSuccessRatio);

        var durations = runs
            .Where(x => x.FinishedAt is not null)
            .Select(x => (x.FinishedAt!.Value - x.StartedAt).TotalMinutes)
            .ToArray();
        double durationScore;
        double averageMinutes;
        if (durations.Length == 0)
        {
            averageMinutes = 0;
            durationScore = maximum;
        }
        else
        {
            averageMinutes = durations.Average();
            durationScore = normalizer.Normalize(averageMinutes, settings.MaxPipelineDurationMinutes, 0);
        }

        var metrics = new List<MetricScore>
        {
            Metric(MetricCode.CiCdPresence, runs.Count, presenceScore, 1, DataStatus.Available),
            Metric(MetricCode.CiCdSuccessRatio, Math.Round(successRatio, 4), successScore, 1, DataStatus.Available),
            Metric(MetricCode.CiCdPipelineDuration, Math.Round(averageMinutes, 2), durationScore, 1, DataStatus.Available)
        };

        return FromMetrics(ScoreCategory.CiCd, metrics);
    }

    private CategoryScoreResult CalculateIssues(RepositoryFacts facts)
    {
        if (facts.CollaborationAvailability != DataStatus.Available)
            return NoData(ScoreCategory.Issues);

        var settings = _options.Issues;
        var now = timeProvider.GetUtcNow();
        var maximum = _options.ScoreScale.MaximumScore;
        var issues = facts.Issues;

        var open = issues.Where(x => x.State == IssueState.Open).ToArray();
        var closed = issues.Where(x => x.State == IssueState.Closed).ToArray();
        var stale = open.Count(x => (now - x.CreatedAt).TotalDays > settings.StaleIssueAgeDays);

        var openScore = normalizer.Normalize(open.Length, settings.MaxOpenIssues, 0);
        var total = issues.Count;
        var closedRatio = total == 0 ? 0 : (double)closed.Length / total;
        var closedScore = total == 0 ? maximum : normalizer.Normalize(closedRatio, 0, 1);
        var staleRatio = open.Length == 0 ? 0 : (double)stale / open.Length;
        var staleScore = normalizer.Normalize(staleRatio, 1, 0);

        var metrics = new List<MetricScore>
        {
            Metric(MetricCode.IssuesOpen, open.Length, openScore, 1, DataStatus.Available),
            Metric(MetricCode.IssuesClosed, closed.Length, closedScore, 1, DataStatus.Available),
            Metric(MetricCode.IssuesStale, stale, staleScore, 1, DataStatus.Available)
        };

        var responseDays = issues
            .Where(x => x.FirstResponseAt is not null)
            .Select(x => (x.FirstResponseAt!.Value - x.CreatedAt).TotalDays)
            .ToArray();
        if (responseDays.Length == 0)
            metrics.Add(Metric(MetricCode.IssuesFirstResponse, 0, _options.ScoreScale.MinimumScore, 1, DataStatus.NoData));
        else
            metrics.Add(Metric(MetricCode.IssuesFirstResponse, Math.Round(responseDays.Average(), 2), normalizer.Normalize(responseDays.Average(), settings.MaxFirstResponseDays, 0), 1, DataStatus.Available));

        var closeDays = closed
            .Where(x => x.ClosedAt is not null)
            .Select(x => (x.ClosedAt!.Value - x.CreatedAt).TotalDays)
            .ToArray();
        if (closeDays.Length == 0)
            metrics.Add(Metric(MetricCode.IssuesCloseTime, 0, _options.ScoreScale.MinimumScore, 1, DataStatus.NoData));
        else
            metrics.Add(Metric(MetricCode.IssuesCloseTime, Math.Round(closeDays.Average(), 2), normalizer.Normalize(closeDays.Average(), settings.MaxCloseDays, 0), 1, DataStatus.Available));

        return FromMetrics(ScoreCategory.Issues, metrics);
    }

    private CategoryScoreResult CalculateCodeHealth(RepositoryFacts facts)
    {
        if (facts.CodeHealthAvailability != DataStatus.Available || facts.CodeHealth is null)
            return NoData(ScoreCategory.CodeHealth);

        var settings = _options.CodeHealth;
        var maximum = _options.ScoreScale.MaximumScore;
        var minimum = _options.ScoreScale.MinimumScore;
        var report = facts.CodeHealth;

        var oldestCommentDays = report.OldestCommentAge?.TotalDays;
        var isStale = oldestCommentDays is not null && oldestCommentDays > settings.StaleCommentAgeDays;

        var penalty = report.TodoCount * settings.TodoPenalty + report.FixmeCount * settings.FixmePenalty;
        if (isStale)
            penalty += settings.StaleCommentPenalty;
        penalty = Math.Min(penalty, settings.MaxPenalty);

        var score = Math.Clamp(maximum - penalty, minimum, maximum);

        var metrics = new List<MetricScore>
        {
            Metric(MetricCode.CodeHealthTodo, report.TodoCount, Math.Clamp(maximum - report.TodoCount * settings.TodoPenalty, minimum, maximum), 1, DataStatus.Available),
            Metric(MetricCode.CodeHealthFixme, report.FixmeCount, Math.Clamp(maximum - report.FixmeCount * settings.FixmePenalty, minimum, maximum), 1, DataStatus.Available),
            Metric(MetricCode.CodeHealthStaleComments, oldestCommentDays ?? 0, isStale ? Math.Clamp(maximum - settings.StaleCommentPenalty, minimum, maximum) : maximum, 1, DataStatus.Available),
            Metric(MetricCode.CodeHealthTechDebt, Math.Round(penalty, 2), score, 1, DataStatus.Available)
        };

        return new CategoryScoreResult(ScoreCategory.CodeHealth, (int)Math.Round(score, MidpointRounding.AwayFromZero), WeightFor(ScoreCategory.CodeHealth), DataStatus.Available, metrics);
    }

    private MetricScore BinaryMetric(MetricCode code, bool isPresent, double weight)
    {
        var maximum = _options.ScoreScale.MaximumScore;
        var minimum = _options.ScoreScale.MinimumScore;

        return new MetricScore(code, isPresent ? 1 : 0, isPresent ? maximum : minimum, weight, DataStatus.Available);
    }

    private static MetricScore Metric(MetricCode code, double rawValue, double normalizedScore, double weight, DataStatus status) =>
        new(code, rawValue, normalizedScore, weight, status);

    private CategoryScoreResult FromMetrics(ScoreCategory category, IReadOnlyCollection<MetricScore> metrics)
    {
        var available = metrics.Where(x => x.DataStatus == DataStatus.Available).ToArray();
        if (available.Length == 0)
            return NoData(category);

        var totalWeight = available.Sum(x => x.Weight);
        var score = totalWeight <= 0
            ? available.Average(x => x.NormalizedScore)
            : available.Sum(x => x.NormalizedScore * x.Weight) / totalWeight;

        return new CategoryScoreResult(category, (int)Math.Round(score, MidpointRounding.AwayFromZero), WeightFor(category), DataStatus.Available, metrics);
    }

    private CategoryScoreResult NoData(ScoreCategory category) =>
        new(category, _options.ScoreScale.MinimumScore, WeightFor(category), DataStatus.NoData, []);

    private double WeightFor(ScoreCategory category) => category switch
    {
        ScoreCategory.Security => _options.CategoryWeights.Security,
        ScoreCategory.CodeHealth => _options.CategoryWeights.CodeHealth,
        ScoreCategory.Activity => _options.CategoryWeights.Activity,
        ScoreCategory.Documentation => _options.CategoryWeights.Documentation,
        ScoreCategory.CiCd => _options.CategoryWeights.CiCd,
        ScoreCategory.Issues => _options.CategoryWeights.Issues,
        _ => 0
    };

    private static int CountOpen(IReadOnlyCollection<SecurityFinding> findings, SecuritySeverity severity) =>
        findings.Count(x => x.Status == SecurityFindingStatus.Open && x.Severity == severity);

    private static int CountKind(IReadOnlyCollection<SecurityFinding> findings, SecurityFindingKind kind) =>
        findings.Count(x => x.Kind == kind);
}
