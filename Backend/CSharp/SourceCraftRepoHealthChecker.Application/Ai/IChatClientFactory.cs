using Microsoft.Extensions.AI;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai;

public interface IChatClientFactory
{
    public IChatClient Create(AiProviders provider, string? baseUrl, string model, string apiKey);
}
