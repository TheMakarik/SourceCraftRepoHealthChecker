namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record PipelineRun(
    string Id,
    PipelineStatus Status,
    string Branch,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);
