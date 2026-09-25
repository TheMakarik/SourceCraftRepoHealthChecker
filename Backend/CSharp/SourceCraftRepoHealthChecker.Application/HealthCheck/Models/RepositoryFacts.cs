using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record RepositoryFacts(
    SourceCraftRepository Repository,
    DataStatus ActivityAvailability,
    CommitActivity? Commits,
    IReadOnlyCollection<Contributor> Contributors,
    IReadOnlyCollection<ReleaseInfo> Releases,
    DataStatus CollaborationAvailability,
    IReadOnlyCollection<IssueInfo> Issues,
    IReadOnlyCollection<MergeRequestInfo> MergeRequests,
    DataStatus SecurityAvailability,
    IReadOnlyCollection<SecurityFinding> Findings,
    DataStatus PipelineAvailability,
    IReadOnlyCollection<PipelineRun> PipelineRuns,
    DataStatus CodeHealthAvailability,
    CodeHealthReport? CodeHealth,
    DataStatus DocumentationAvailability,
    DocumentationReport? Documentation);
