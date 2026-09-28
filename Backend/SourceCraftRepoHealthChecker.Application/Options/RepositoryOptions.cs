namespace SourceCraftRepoHealthChecker.Application.Options;

public sealed class RepositoryOptions
{
    public required int MaxSourceCraftIdLength { get; init; }
    public required int MaxNameLength { get; init; }
    public required int MaxFullNameLength { get; init; }
    public required int MaxUrlLength { get; init; }
    public required int MaxLanguageLength { get; init; }
}
