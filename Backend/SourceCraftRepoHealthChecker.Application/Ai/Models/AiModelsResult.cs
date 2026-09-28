using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.Models;

public sealed record AiModelsResult(AiProviders Provider, IReadOnlyList<string> Models);
