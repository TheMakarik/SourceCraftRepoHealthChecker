using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.Options;

public sealed class AiModelsOptions
{
    public required IReadOnlyDictionary<AiProviders, IReadOnlyList<string>> Models { get; init; }
}
