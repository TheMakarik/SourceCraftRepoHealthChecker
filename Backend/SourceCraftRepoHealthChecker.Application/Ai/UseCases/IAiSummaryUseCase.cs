using SourceCraftRepoHealthChecker.Application.Ai.Models;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface IAiSummaryUseCase
{
    public Task<AiSummaryResult> SummarizeAsync(string sourceCraftId, Guid userId, CancellationToken cancellationToken);
}
