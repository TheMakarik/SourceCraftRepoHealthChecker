using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record RepositoryAnalysisRecommendation(
    RecommendationPriority Priority,
    string Title,
    string Problem,
    string WhyImportant,
    string Evidence,
    string Action,
    string ExpectedImpact,
    string SourceReference);
