namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public enum ActivityAnomalyKind
{
    CommitBurst,
    MergeRequestBurst,
    InactiveAuthorSmallChanges
}
