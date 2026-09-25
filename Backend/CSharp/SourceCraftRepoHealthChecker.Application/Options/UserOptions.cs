namespace SourceCraftRepoHealthChecker.Application.Options;

public sealed class UserOptions
{
    public required int MaxYaIdLength { get; init; }
    public required int MaxLoginLength { get; init; }
    public required int MaxDisplayNameLength { get; init; }
    public required int MaxEmailLength { get; init; }
}
