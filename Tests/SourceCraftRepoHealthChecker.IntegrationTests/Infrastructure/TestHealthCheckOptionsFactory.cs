using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public static class TestHealthCheckOptionsFactory
{
    public static HealthCheckOptions Create() => new()
    {
        MethodologyVersion = "test",
        ScoreScale = new ScoreScaleOptions { MinimumScore = 0, MaximumScore = 100 },
        CategoryWeights = new CategoryWeightsOptions
        {
            Security = 20,
            CodeHealth = 20,
            Activity = 15,
            Documentation = 15,
            CiCd = 15,
            Issues = 15
        },
        Security = new SecurityScoringOptions
        {
            CriticalPenalty = 20,
            HighPenalty = 10,
            MediumPenalty = 5,
            LowPenalty = 2,
            FixedFindingCredit = 3
        },
        CodeHealth = new CodeHealthScoringOptions
        {
            TodoPenalty = 1,
            FixmePenalty = 2,
            StaleCommentAgeDays = 60,
            StaleCommentPenalty = 5
        },
        Activity = new ActivityScoringOptions
        {
            ActiveWithinDays = 30,
            StaleAfterDays = 180,
            CommitFrequencyForFullScore = 20,
            ContributorsForFullScore = 5,
            ReleasesForFullScore = 3,
            MergeRequestsForFullScore = 4
        },
        Documentation = new DocumentationScoringOptions
        {
            ReadmeWeight = 3,
            LicenseWeight = 2,
            ContributingWeight = 1,
            CodeOwnersWeight = 1,
            LocalRunWeight = 1,
            BuildAndTestWeight = 1
        },
        CiCd = new CiCdScoringOptions
        {
            PipelineRunsForFullScore = 10,
            MinimumSuccessRatio = 0.9,
            MaxPipelineDurationMinutes = 60
        },
        Issues = new IssuesScoringOptions
        {
            StaleIssueAgeDays = 30,
            MaxFirstResponseDays = 7,
            MaxCloseDays = 14,
            MaxOpenIssues = 10
        },
        Recommendations = new RecommendationScoringOptions
        {
            MinimumAcceptableScore = 70,
            StrengthScore = 85,
            HighPriorityScore = 50,
            CriticalPriorityScore = 30
        }
    };
}
