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
            BinaryMetric(MetricCode.DocumentationBuildAndTest, report.HasBuildAndTestInstructions, settings.BuildAndTestWeight),
            BinaryMetric(MetricCode.DocumentationProjectStructure, report.HasProjectStructure, settings.ProjectStructureWeight)
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

        var metrics = new List<MetricScore>
        {
            PenaltyMetric(MetricCode.SecurityCriticalFindings, critical, settings.CriticalPenalty),
            PenaltyMetric(MetricCode.SecurityHighFindings, high, settings.HighPenalty),
            PenaltyMetric(MetricCode.SecurityMediumFindings, medium, settings.MediumPenalty),
            PenaltyMetric(MetricCode.SecurityLowFindings, low, settings.LowPenalty),
            Metric(MetricCode.SecurityFixedFindings, fixedFindings, Math.Clamp(minimum + fixedFindings * settings.FixedFindingCredit, minimum, maximum), 0, DataStatus.Available)
        };

        return FromMetrics(ScoreCategory.Security, metrics);
    }

    private CategoryScoreResult CalculateActivity(RepositoryFacts facts)
    {
        var settings = _options.Activity;
        var now = timeProvider.GetUtcNow();
        var minimum = _options.ScoreScale.MinimumScore;
        var metrics = new List<MetricScore>();

        if (facts.CommitAvailability == DataStatus.Available)
        {
            var lastActivity = facts.Commits?.LastCommitAt ?? facts.Repository.LastActivityAt;
            var daysSinceLastActivity = Math.Max(0, (now - lastActivity).TotalDays);
            var lastActivityScore = normalizer.Normalize(daysSinceLastActivity, settings.StaleAfterDays, settings.ActiveWithinDays);
            metrics.Add(Metric(MetricCode.ActivityLastActivity, Math.Round(daysSinceLastActivity, 2), lastActivityScore, 1, DataStatus.Available));

            var windowStart = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-settings.ActiveWithinDays));
            var commitsInWindow = facts.Commits is null
                ? 0
                : facts.Commits.CommitsByDay.Where(x => x.Key >= windowStart).Sum(x => x.Value);
            var commitScore = normalizer.Normalize(commitsInWindow, 0, settings.CommitFrequencyForFullScore);
            metrics.Add(Metric(MetricCode.ActivityCommitFrequency, commitsInWindow, commitScore, 1, DataStatus.Available));
            AddCommitTrend(metrics, facts, now, settings);
        }
        else
        {
            metrics.Add(Metric(MetricCode.ActivityLastActivity, 0, minimum, 1, facts.CommitAvailability));
            metrics.Add(Metric(MetricCode.ActivityCommitFrequency, 0, minimum, 1, facts.CommitAvailability));
        }

        if (facts.ContributorAvailability == DataStatus.Available)
        {
            var contributors = facts.Contributors.Count(x => !x.IsBot);
            var contributorScore = normalizer.Normalize(contributors, 0, settings.ContributorsForFullScore);
            metrics.Add(Metric(MetricCode.ActivityContributors, contributors, contributorScore, 1, DataStatus.Available));
        }
        else
            metrics.Add(Metric(MetricCode.ActivityContributors, 0, minimum, 1, facts.ContributorAvailability));

        if (facts.ReleaseAvailability == DataStatus.Available)
        {
            var releases = facts.Releases.Count;
            var releaseScore = normalizer.Normalize(releases, 0, settings.ReleasesForFullScore);
            metrics.Add(Metric(MetricCode.ActivityReleases, releases, releaseScore, 1, DataStatus.Available));
        }
        else
            metrics.Add(Metric(MetricCode.ActivityReleases, 0, minimum, 1, facts.ReleaseAvailability));

        if (facts.MergeRequestAvailability == DataStatus.Available)
        {
            var mergeRequests = facts.MergeRequests.Count;
            var mergeRequestScore = normalizer.Normalize(mergeRequests, 0, settings.MergeRequestsForFullScore);
            metrics.Add(Metric(MetricCode.ActivityMergeRequests, mergeRequests, mergeRequestScore, 1, DataStatus.Available));
            AddMergeRequestResponse(metrics, facts, settings);
        }

        return FromMetrics(ScoreCategory.Activity, metrics);
    }

    private void AddCommitTrend(List<MetricScore> metrics, RepositoryFacts facts, DateTimeOffset now, ActivityScoringOptions settings)
    {
        if (facts.Commits is null)
            return;

        var recentStart = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-settings.TrendWindowDays));
        var previousStart = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-2 * settings.TrendWindowDays));
        var recentCommits = facts.Commits.CommitsByDay.Where(x => x.Key >= recentStart).Sum(x => x.Value);
        var previousCommits = facts.Commits.CommitsByDay
            .Where(x => x.Key >= previousStart && x.Key < recentStart)
            .Sum(x => x.Value);
        var total = recentCommits + previousCommits;
        var trendRatio = total == 0 ? 0.5 : (double)recentCommits / total;
        var trendScore = normalizer.Normalize(trendRatio, 0, 1);

        metrics.Add(Metric(MetricCode.ActivityCommitTrend, recentCommits, trendScore, settings.TrendWeight, DataStatus.Available));
    }

    private void AddMergeRequestResponse(List<MetricScore> metrics, RepositoryFacts facts, ActivityScoringOptions settings)
    {
        var evaluated = facts.MergeRequests.Where(x => x.FirstResponseEvaluated).ToArray();
        if (evaluated.Length == 0)
            return;

        var responded = evaluated
            .Where(x => x.FirstResponseAt is not null)
            .Select(x => (x.FirstResponseAt!.Value - x.CreatedAt).TotalDays)
            .ToArray();
        var unresponded = evaluated.Count(x => x.FirstResponseAt is null);
        if (responded.Length == 0 && unresponded == 0)
            return;

        var responseDays = responded
            .Concat(Enumerable.Repeat((double)settings.MaxMergeRequestResponseDays, unresponded))
            .ToArray();
        var averageDays = responseDays.Average();
        var score = normalizer.Normalize(averageDays, settings.MaxMergeRequestResponseDays, 0);

        metrics.Add(Metric(MetricCode.ActivityMergeRequestResponse, Math.Round(averageDays, 2), score, settings.MergeRequestResponseWeight, DataStatus.Available));
    }

    private CategoryScoreResult CalculateCiCd(RepositoryFacts facts)
    {
        if (facts.PipelineAvailability != DataStatus.Available)
            return NoData(ScoreCategory.CiCd);

        var settings = _options.CiCd;
        var minimum = _options.ScoreScale.MinimumScore;
        var runs = facts.PipelineRuns;

        if (runs.Count == 0)
            return new CategoryScoreResult(ScoreCategory.CiCd, minimum, WeightFor(ScoreCategory.CiCd), DataStatus.Available,
                new List<MetricScore> { Metric(MetricCode.CiCdPresence, 0, minimum, 1, DataStatus.Available) });

        var presenceScore = normalizer.Normalize(runs.Count, 0, settings.PipelineRunsForFullScore);

        var finished = runs.Where(x => x.Status is PipelineStatus.Success or PipelineStatus.Failed).ToArray();
        var successRatio = finished.Length == 0 ? 0 : (double)finished.Count(x => x.Status == PipelineStatus.Success) / finished.Length;
        var successStatus = finished.Length == 0 ? DataStatus.NoData : DataStatus.Available;
        var successScore = finished.Length == 0 ? minimum : normalizer.Normalize(successRatio, 0, settings.MinimumSuccessRatio);

        var durations = runs
            .Where(x => x.FinishedAt is not null)
            .Select(x => (x.FinishedAt!.Value - x.StartedAt).TotalMinutes)
            .ToArray();
        var durationStatus = durations.Length == 0 ? DataStatus.NoData : DataStatus.Available;
        var averageMinutes = durations.Length == 0 ? 0 : durations.Average();
        var durationScore = durations.Length == 0 ? minimum : normalizer.Normalize(averageMinutes, settings.MaxPipelineDurationMinutes, 0);

        var metrics = new List<MetricScore>
        {
            Metric(MetricCode.CiCdPresence, runs.Count, presenceScore, 1, DataStatus.Available),
            Metric(MetricCode.CiCdSuccessRatio, Math.Round(successRatio, 4), successScore, 1, successStatus),
            Metric(MetricCode.CiCdPipelineDuration, Math.Round(averageMinutes, 2), durationScore, 1, durationStatus),
            CalculateStability(runs, settings)
        };

        return FromMetrics(ScoreCategory.CiCd, metrics);
    }

    private MetricScore CalculateStability(IReadOnlyCollection<PipelineRun> runs, CiCdScoringOptions settings)
    {
        var finished = runs
            .Where(x => x.Status is PipelineStatus.Success or PipelineStatus.Failed)
            .OrderByDescending(x => x.StartedAt)
            .Take(settings.StabilityRunsForTrend)
            .OrderBy(x => x.StartedAt)
            .ToArray();

        if (finished.Length < 2)
            return Metric(MetricCode.CiCdStability, 0, _options.ScoreScale.MinimumScore, settings.StabilityWeight, DataStatus.NoData);

        var transitions = 0;
        for (var index = 1; index < finished.Length; index++)
            if (finished[index].Status != finished[index - 1].Status)
                transitions++;

        var stabilityRatio = 1 - (double)transitions / (finished.Length - 1);
        var stabilityScore = normalizer.Normalize(stabilityRatio, 0, 1);

        return Metric(MetricCode.CiCdStability, transitions, stabilityScore, settings.StabilityWeight, DataStatus.Available);
    }

    private CategoryScoreResult CalculateIssues(RepositoryFacts facts)
    {
        if (facts.IssuesAvailability != DataStatus.Available)
            return NoData(ScoreCategory.Issues);

        var settings = _options.Issues;
        var now = timeProvider.GetUtcNow();
        var issues = facts.Issues;

        var open = issues.Where(x => x.State == IssueState.Open).ToArray();
        var closed = issues.Where(x => x.State == IssueState.Closed).ToArray();
        var stale = open.Count(x => (now - (x.UpdatedAt ?? x.CreatedAt)).TotalDays > settings.StaleIssueAgeDays);
        var staleRatio = open.Length == 0 ? 0 : (double)stale / open.Length;

        var openScore = normalizer.Normalize(open.Length, settings.MaxOpenIssues, 0);
        var staleScore = normalizer.Normalize(staleRatio, 1, 0);

        var closedStatus = issues.Count == 0 ? DataStatus.NoData : DataStatus.Available;
        var closedScore = closedStatus == DataStatus.Available
            ? normalizer.Normalize(closed.Length, 0, settings.ClosedIssuesForFullScore)
            : _options.ScoreScale.MinimumScore;

        var metrics = new List<MetricScore>
        {
            Metric(MetricCode.IssuesOpen, open.Length, openScore, 1, DataStatus.Available),
            Metric(MetricCode.IssuesStale, Math.Round(staleRatio, 4), staleScore, 1, DataStatus.Available),
            Metric(MetricCode.IssuesClosed, closed.Length, closedScore, settings.ClosedWeight, closedStatus)
        };

        AddIssueDynamics(metrics, issues, closed, now, settings);

        var respondedDays = issues
            .Where(x => x.FirstResponseAt is not null)
            .Select(x => (x.FirstResponseAt!.Value - x.CreatedAt).TotalDays)
            .ToList();
        var unrespondedOpen = open.Count(x => x.FirstResponseAt is null && x.FirstResponseEvaluated);
        if (respondedDays.Count == 0 && unrespondedOpen == 0)
            metrics.Add(Metric(MetricCode.IssuesFirstResponse, 0, _options.ScoreScale.MinimumScore, 1, DataStatus.NoData));
        else
        {
            var responseDays = respondedDays
                .Concat(Enumerable.Repeat((double)settings.MaxFirstResponseDays, unrespondedOpen))
                .ToArray();
            var averageResponseDays = responseDays.Average();
            metrics.Add(Metric(MetricCode.IssuesFirstResponse, Math.Round(averageResponseDays, 2), normalizer.Normalize(averageResponseDays, settings.MaxFirstResponseDays, 0), 1, DataStatus.Available));
        }

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

    private void AddIssueDynamics(List<MetricScore> metrics, IReadOnlyCollection<IssueInfo> issues, IReadOnlyCollection<IssueInfo> closed, DateTimeOffset now, IssuesScoringOptions settings)
    {
        var windowStart = now.AddDays(-settings.DynamicsWindowDays);
        var createdInWindow = issues.Count(x => x.CreatedAt >= windowStart);
        var closedInWindow = closed.Count(x => x.ClosedAt is not null && x.ClosedAt >= windowStart);
        var hasData = createdInWindow > 0 || closedInWindow > 0;
        var resolutionRatio = createdInWindow == 0
            ? closedInWindow > 0 ? 1 : 0.5
            : Math.Min(1, (double)closedInWindow / createdInWindow);
        var dynamicsScore = hasData ? normalizer.Normalize(resolutionRatio, 0, 1) : _options.ScoreScale.MinimumScore;
        var status = hasData ? DataStatus.Available : DataStatus.NoData;

        metrics.Add(Metric(MetricCode.IssuesCreated, createdInWindow, _options.ScoreScale.MinimumScore, 0, status));
        metrics.Add(Metric(MetricCode.IssuesDynamics, closedInWindow, dynamicsScore, settings.DynamicsWeight, status));
    }

    private CategoryScoreResult CalculateCodeHealth(RepositoryFacts facts)
    {
        if (facts.CodeHealthAvailability != DataStatus.Available || facts.CodeHealth is null)
            return NoData(ScoreCategory.CodeHealth);

        var settings = _options.CodeHealth;
        var scale = _options.ScoreScale;
        var report = facts.CodeHealth;

        var oldestCommentDays = report.OldestCommentAge?.TotalDays ?? 0;
        var freshness = normalizer.Normalize(oldestCommentDays, settings.StaleCommentMaxAgeDays, settings.StaleCommentAgeDays);
        var freshnessRange = scale.MaximumScore - scale.MinimumScore;
        var freshnessRatio = freshnessRange <= 0 ? 1 : (freshness - scale.MinimumScore) / freshnessRange;
        var staleScore = scale.MaximumScore - (1 - freshnessRatio) * settings.StaleCommentPenalty;

        var metrics = new List<MetricScore>
        {
            PenaltyMetric(MetricCode.CodeHealthTodo, report.TodoCount, settings.TodoPenalty),
            PenaltyMetric(MetricCode.CodeHealthFixme, report.FixmeCount, settings.FixmePenalty),
            Metric(MetricCode.CodeHealthStaleComments, oldestCommentDays, Math.Clamp(staleScore, scale.MinimumScore, scale.MaximumScore), settings.StaleCommentWeight, DataStatus.Available)
        };

        return FromMetrics(ScoreCategory.CodeHealth, metrics);
    }

    private MetricScore PenaltyMetric(MetricCode code, double count, double penalty)
    {
        var maximum = _options.ScoreScale.MaximumScore;
        var minimum = _options.ScoreScale.MinimumScore;

        return new MetricScore(code, count, Math.Clamp(maximum - count * penalty, minimum, maximum), penalty, DataStatus.Available);
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
}
