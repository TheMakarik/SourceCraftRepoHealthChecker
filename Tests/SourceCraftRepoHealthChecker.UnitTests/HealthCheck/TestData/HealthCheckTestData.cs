using FakeItEasy;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;

public static class HealthCheckTestData
{
    public static readonly DateTimeOffset Now = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    public static SourceCraftRepository CreateRepository(DateTimeOffset? lastActivityAt = null) => new(
        "repo-1",
        "health-checker",
        "owner/health-checker",
        "https://sourcecraft.dev/owner/health-checker",
        "C#",
        0,
        lastActivityAt ?? Now,
        false,
        "main");

    public static HealthCheckOptions CreateOptions(
        ScoreScaleOptions? scoreScale = null,
        CategoryWeightsOptions? categoryWeights = null,
        SecurityScoringOptions? security = null,
        CodeHealthScoringOptions? codeHealth = null,
        ActivityScoringOptions? activity = null,
        DocumentationScoringOptions? documentation = null,
        CiCdScoringOptions? ciCd = null,
        IssuesScoringOptions? issues = null,
        RecommendationScoringOptions? recommendations = null) => new()
    {
        MethodologyVersion = "test-methodology-2026.1",
        ScoreScale = scoreScale ?? new ScoreScaleOptions { MinimumScore = 0, MaximumScore = 100 },
        CategoryWeights = categoryWeights ?? new CategoryWeightsOptions
        {
            Security = 20,
            CodeHealth = 20,
            Activity = 15,
            Documentation = 15,
            CiCd = 15,
            Issues = 15
        },
        Security = security ?? new SecurityScoringOptions
        {
            CriticalPenalty = 20,
            HighPenalty = 10,
            MediumPenalty = 5,
            LowPenalty = 2,
            FixedFindingCredit = 3
        },
        CodeHealth = codeHealth ?? new CodeHealthScoringOptions
        {
            TodoPenalty = 1,
            FixmePenalty = 2,
            StaleCommentAgeDays = 60,
            StaleCommentPenalty = 5,
            MaxPenalty = 50
        },
        Activity = activity ?? new ActivityScoringOptions
        {
            ActiveWithinDays = 30,
            StaleAfterDays = 180,
            CommitFrequencyForFullScore = 20,
            ContributorsForFullScore = 5,
            ReleasesForFullScore = 3
        },
        Documentation = documentation ?? new DocumentationScoringOptions
        {
            ReadmeWeight = 3,
            LicenseWeight = 2,
            ContributingWeight = 1,
            CodeOwnersWeight = 1,
            LocalRunWeight = 1,
            BuildAndTestWeight = 1
        },
        CiCd = ciCd ?? new CiCdScoringOptions
        {
            PipelineRunsForFullScore = 10,
            MinimumSuccessRatio = 0.9,
            MaxPipelineDurationMinutes = 60
        },
        Issues = issues ?? new IssuesScoringOptions
        {
            StaleIssueAgeDays = 30,
            MaxFirstResponseDays = 7,
            MaxCloseDays = 14,
            MaxOpenIssues = 10
        },
        Recommendations = recommendations ?? new RecommendationScoringOptions
        {
            MinimumAcceptableScore = 70,
            HighPriorityScore = 50,
            CriticalPriorityScore = 30
        }
    };

    public static IOptions<HealthCheckOptions> CreateOptionsWrapper(HealthCheckOptions? options = null)
    {
        var fakeOptions = A.Fake<IOptions<HealthCheckOptions>>();
        A.CallTo(() => fakeOptions.Value).Returns(options ?? CreateOptions());

        return fakeOptions;
    }
}
