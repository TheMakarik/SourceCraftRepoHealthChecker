namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
