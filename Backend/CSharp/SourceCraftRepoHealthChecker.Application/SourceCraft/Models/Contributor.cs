namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record Contributor(
    string Login,
    int CommitsCount,
    bool IsBot);
