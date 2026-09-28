using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftAuthenticationAdapter(
    ISourceCraftApi api,
    IYandexIdClient yandexIdClient,
    IOptions<SourceCraftServiceOptions> options) : ISourceCraftAuthentication
{
    public Task<Uri> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!yandexIdClient.IsConfigured)
            throw new SourceCraftException("Yandex ID OAuth application is not configured");

        return Task.FromResult(yandexIdClient.GetAuthorizationUrl(state));
    }

    public async Task<SourceCraftUser> CompleteAuthorizationAsync(string authorizationCode, string state, CancellationToken cancellationToken)
    {
        var user = await yandexIdClient.AuthenticateAsync(authorizationCode, cancellationToken);
        return new SourceCraftUser(
            user.Id,
            user.Login,
            user.DisplayName,
            string.IsNullOrEmpty(user.DefaultEmail) ? null : user.DefaultEmail,
            user.AvatarUrl);
    }

    public async Task<SourceCraftUser> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await api.GetCurrentUserAsync(AuthorizationHeader(accessToken), cancellationToken);
            return new SourceCraftUser(profile.Id ?? string.Empty, profile.Username ?? string.Empty, profile.DisplayName ?? string.Empty, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SourceCraftException(SourceCraftFailure.Describe(exception));
        }
    }

    public async Task<IReadOnlyCollection<SourceCraftRepository>> GetAvailableRepositoriesAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var authorization = AuthorizationHeader(accessToken);
            var repositories = await SourceCraftPagination.CollectAsync(
                0,
                settings.MaxPages,
                (pageToken, token) => FetchMyRepositoriesAsync(authorization, pageToken, token),
                cancellationToken);
            return repositories.Select(SourceCraftRepositoryMapper.Map).ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SourceCraftException(SourceCraftFailure.Describe(exception));
        }
    }

    private async Task<(IReadOnlyCollection<RepositoryDto> Items, string? NextPageToken)> FetchMyRepositoriesAsync(
        string authorization,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetMyRepositoriesAsync(authorization, options.Value.PageSize, pageToken, cancellationToken);
        return (page.Repositories ?? [], page.NextPageToken);
    }

    private static string AuthorizationHeader(string accessToken) => "Bearer " + accessToken;
}
