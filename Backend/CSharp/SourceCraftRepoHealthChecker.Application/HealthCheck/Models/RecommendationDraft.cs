using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record RecommendationDraft(
    RecommendationPriority Priority,
    string Problem,
    string Action,
    string ExpectedImpact,
    string SourceReference);
