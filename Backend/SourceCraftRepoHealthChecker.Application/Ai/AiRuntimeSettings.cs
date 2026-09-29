using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai;

public sealed record AiRuntimeSettings(AiProviders Provider, string? BaseUrl, string Model, string Token);
