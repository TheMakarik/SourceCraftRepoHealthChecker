using SourceCraftRepoHealthChecker.Application.Ai.Models;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface IGetAiModelsUseCase
{
    public IReadOnlyList<AiModelsResult> Get();
}
