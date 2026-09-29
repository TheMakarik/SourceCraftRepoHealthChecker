using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.Models;

public sealed record AiProviderToken(AiProviders Provider, string? TokenPrefix);
