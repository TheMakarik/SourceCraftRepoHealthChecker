namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record SourceCraftEnvelope<T>(
    string RequestId,
    DateTimeOffset CollectedAt,
    string Status,
    T? Data,
    string? Reason,
    string? NextPageToken);
