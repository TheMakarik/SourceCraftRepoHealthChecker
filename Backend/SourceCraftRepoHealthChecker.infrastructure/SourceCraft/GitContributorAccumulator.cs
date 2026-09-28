namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal sealed class GitContributorAccumulator(string name, bool isBot)
{
    public string Name { get; } = name;
    public bool IsBot { get; } = isBot;
    public int CommitsCount { get; set; }
    public DateTimeOffset FirstCommitAt { get; set; }
    public DateTimeOffset LastCommitAt { get; set; }
}
