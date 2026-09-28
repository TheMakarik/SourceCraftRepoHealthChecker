using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.Models;

public sealed record AiSettingsResult(AiProviders Provider, string? BaseUrl, string Model);
