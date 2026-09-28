namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record PipelineRunDatesDto
{
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
}
