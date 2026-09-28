namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record PipelineRunPageDto
{
    public IReadOnlyCollection<PipelineRunDto>? Runs { get; init; }
    public string? NextPageToken { get; init; }
}
