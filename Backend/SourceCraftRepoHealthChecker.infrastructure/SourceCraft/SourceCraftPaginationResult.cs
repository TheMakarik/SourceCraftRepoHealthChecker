namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal sealed class SourceCraftPaginationResult<TItem> : List<TItem>
{
    public bool Truncated { get; set; }
}
