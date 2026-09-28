using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.Models;

public sealed record AiSummaryResult(string Summary, AiProviders Provider, string Model);
