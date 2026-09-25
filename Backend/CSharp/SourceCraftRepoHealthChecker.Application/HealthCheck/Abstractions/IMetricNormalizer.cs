namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface IMetricNormalizer
{
    public double Normalize(double value, double worstValue, double bestValue);
}
