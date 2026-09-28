using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.Models;

public sealed record AiInsightResult(AiInsightKind Kind, string Content, AiProviders Provider, string Model);
