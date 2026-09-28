namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record YandexIdUser(
    string Id,
    string Login,
    string DisplayName,
    string DefaultEmail);
