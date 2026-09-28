using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class GetAiModelsUseCase(IOptions<AiModelsOptions> options) : IGetAiModelsUseCase
{
    public IReadOnlyList<AiModelsResult> Get() =>
        Enum.GetValues<AiProviders>()
            .Select(provider => new AiModelsResult(provider, ResolveModels(provider)))
            .ToList();

    private IReadOnlyList<string> ResolveModels(AiProviders provider) =>
        options.Value.Models.TryGetValue(provider, out var models) ? models : [];
}
