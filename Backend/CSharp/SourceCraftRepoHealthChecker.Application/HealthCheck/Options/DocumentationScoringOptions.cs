namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class DocumentationScoringOptions
{
    public required double ReadmeWeight { get; init; }
    public required double LicenseWeight { get; init; }
    public required double ContributingWeight { get; init; }
    public required double CodeOwnersWeight { get; init; }
    public required double LocalRunWeight { get; init; }
    public required double BuildAndTestWeight { get; init; }
}
