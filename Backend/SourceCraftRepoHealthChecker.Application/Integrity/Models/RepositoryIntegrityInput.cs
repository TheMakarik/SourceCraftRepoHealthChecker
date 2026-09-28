using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

namespace SourceCraftRepoHealthChecker.Application.Integrity.Models;

public sealed record RepositoryIntegrityInput(
    RepositoryFacts Facts,
    int LikesCount,
    DateTimeOffset LastActivityAt,
    IReadOnlyCollection<string> PersistedAnomalySignals);
