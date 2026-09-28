using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed record StoreAiSettingsRequest(
    AiProviders Provider,
    string? BaseUrl,
    string Model,
    string Token);
