using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

namespace SourceCraftRepoHealthChecker.infrastructure.Authentication;

public sealed class SourceCraftAccessTokenAccessor : ISourceCraftAccessTokenAccessor
{
    private readonly AsyncLocal<string?> _currentToken = new();

    public string? Token
    {
        get => _currentToken.Value;
        set => _currentToken.Value = value;
    }
}
