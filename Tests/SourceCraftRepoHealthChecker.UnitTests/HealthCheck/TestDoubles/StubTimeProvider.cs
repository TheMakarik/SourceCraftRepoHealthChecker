namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

public sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
