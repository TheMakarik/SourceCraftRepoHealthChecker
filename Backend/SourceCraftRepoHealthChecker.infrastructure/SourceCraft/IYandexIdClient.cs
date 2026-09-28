namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public interface IYandexIdClient
{
    public bool IsConfigured { get; }

    public Uri GetAuthorizationUrl(string state);

    public Task<YandexIdUser> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken);
}
