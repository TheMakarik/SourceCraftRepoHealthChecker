namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record PipelineRunDto
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
    public string? Status { get; init; }
    public PipelineRunDatesDto? Dates { get; init; }
    public string? EventType { get; init; }
    public PullRequestEmbeddedDto? Pull { get; init; }
}
