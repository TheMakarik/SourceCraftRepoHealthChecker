using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;

public static class RepositoryFactsBuilder
{
    public static RepositoryFacts Build(
        SourceCraftRepository? repository = null,
        DataStatus activityAvailability = DataStatus.Available,
        CommitActivity? commits = null,
        IReadOnlyCollection<Contributor>? contributors = null,
        IReadOnlyCollection<ReleaseInfo>? releases = null,
        DataStatus collaborationAvailability = DataStatus.Available,
        IReadOnlyCollection<IssueInfo>? issues = null,
        IReadOnlyCollection<MergeRequestInfo>? mergeRequests = null,
        DataStatus securityAvailability = DataStatus.Available,
        IReadOnlyCollection<SecurityFinding>? findings = null,
        DataStatus pipelineAvailability = DataStatus.Available,
        IReadOnlyCollection<PipelineRun>? pipelineRuns = null,
        DataStatus codeHealthAvailability = DataStatus.Available,
        CodeHealthReport? codeHealth = null,
        DataStatus documentationAvailability = DataStatus.Available,
        DocumentationReport? documentation = null) => new(
        repository ?? HealthCheckTestData.CreateRepository(),
        activityAvailability,
        commits,
        contributors ?? [],
        releases ?? [],
        collaborationAvailability,
        issues ?? [],
        mergeRequests ?? [],
        securityAvailability,
        findings ?? [],
        pipelineAvailability,
        pipelineRuns ?? [],
        codeHealthAvailability,
        codeHealth,
        documentationAvailability,
        documentation);
}
