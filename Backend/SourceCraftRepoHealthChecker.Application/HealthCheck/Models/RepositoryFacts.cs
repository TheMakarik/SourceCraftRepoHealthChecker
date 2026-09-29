using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record RepositoryFacts(
    SourceCraftRepository Repository,
    DataStatus CommitAvailability,
    CommitActivity? Commits,
    DataStatus ContributorAvailability,
    IReadOnlyCollection<Contributor> Contributors,
    DataStatus ReleaseAvailability,
    IReadOnlyCollection<ReleaseInfo> Releases,
    DataStatus IssuesAvailability,
    IReadOnlyCollection<IssueInfo> Issues,
    DataStatus MergeRequestAvailability,
    IReadOnlyCollection<MergeRequestInfo> MergeRequests,
    DataStatus SecurityAvailability,
    IReadOnlyCollection<SecurityFinding> Findings,
    DataStatus PipelineAvailability,
    IReadOnlyCollection<PipelineRun> PipelineRuns,
    DataStatus CodeHealthAvailability,
    CodeHealthReport? CodeHealth,
    DataStatus DocumentationAvailability,
    DocumentationReport? Documentation,
    bool IssuesPartial = false,
    bool MergeRequestsPartial = false);
