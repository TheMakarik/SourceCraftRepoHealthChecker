using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftAuthenticationAdapter(SourceCraftHttpClient client) : ISourceCraftAuthentication
{
    public async Task<Uri> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken)
    {
        var page = await client.PostAsync<object, AuthorizationUrlResponse>("/auth/url", new { state }, cancellationToken);
        if (page.Status != DataStatus.Available || page.Data is null)
            throw new SourceCraftException(page.Reason ?? "authorization url is unavailable");

        return new Uri(page.Data.Url);
    }

    public async Task<SourceCraftUser> CompleteAuthorizationAsync(string authorizationCode, string state, CancellationToken cancellationToken)
    {
        var page = await client.PostAsync<object, SourceCraftUser>("/auth/token", new { code = authorizationCode, state }, cancellationToken);
        if (page.Status != DataStatus.Available || page.Data is null)
            throw new SourceCraftException(page.Reason ?? "authorization failed");

        return page.Data;
    }

    public async Task<SourceCraftUser> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        var page = await client.GetAsync<SourceCraftUser>("/auth/me", accessToken, cancellationToken);
        if (page.Status != DataStatus.Available || page.Data is null)
            throw new SourceCraftException(page.Reason ?? "access token rejected");

        return page.Data;
    }

    public async Task<IReadOnlyCollection<SourceCraftRepository>> GetAvailableRepositoriesAsync(string accessToken, CancellationToken cancellationToken)
    {
        var page = await client.GetAsync<IReadOnlyCollection<SourceCraftRepository>>("/auth/repositories", accessToken, cancellationToken);
        if (page.Status == DataStatus.Unavailable)
            throw new SourceCraftException(page.Reason ?? "repositories unavailable");

        return page.Data ?? [];
    }
}
