using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface IAiInsightUseCase
{
    public Task<AiInsightResult> GenerateAsync(string sourceCraftId, Guid userId, AiInsightKind kind, CancellationToken cancellationToken);
}
