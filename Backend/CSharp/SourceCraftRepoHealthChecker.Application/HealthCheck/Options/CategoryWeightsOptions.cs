namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class CategoryWeightsOptions
{
    public required double Security { get; init; }
    public required double CodeHealth { get; init; }
    public required double Activity { get; init; }
    public required double Documentation { get; init; }
    public required double CiCd { get; init; }
    public required double Issues { get; init; }
}
