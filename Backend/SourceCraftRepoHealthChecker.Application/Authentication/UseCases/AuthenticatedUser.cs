namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public sealed record AuthenticatedUser(
    Guid UserId,
    string YaId,
    string Login,
    string DisplayName);
