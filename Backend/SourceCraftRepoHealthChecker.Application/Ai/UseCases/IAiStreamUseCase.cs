using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface IAiStreamUseCase
{
    public IAsyncEnumerable<AiStreamEvent> StreamAsync(string sourceCraftId, Guid userId, AiInsightKind? kind, CancellationToken cancellationToken);
}
