namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public sealed record AnalysisJob(
    string RepositoryId,
    Guid? RequestedByUserId = null);
