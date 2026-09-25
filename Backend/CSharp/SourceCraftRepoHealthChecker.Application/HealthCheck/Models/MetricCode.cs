namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public enum MetricCode
{
    DocumentationReadme,
    DocumentationLicense,
    DocumentationContributing,
    DocumentationCodeOwners,
    DocumentationLocalRun,
    DocumentationBuildAndTest,
    CiCdPresence,
    CiCdSuccessRatio,
    CiCdPipelineDuration,
    CiCdStability,
    SecuritySast,
    SecuritySca,
    SecuritySecretScanning,
    SecurityCriticalFindings,
    SecurityFixedFindings,
    ActivityCommitFrequency,
    ActivityLastActivity,
    ActivityContributors,
    ActivityMergeRequests,
    ActivityReleases,
    IssuesOpen,
    IssuesClosed,
    IssuesStale,
    IssuesFirstResponse,
    IssuesCloseTime,
    CodeHealthTodo,
    CodeHealthFixme,
    CodeHealthStaleComments,
    CodeHealthTechDebt
}
