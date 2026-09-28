using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class YandexIdClient(HttpClient httpClient, IOptions<YandexIdOptions> options) : IYandexIdClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly YandexIdOptions _options = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_options.ClientId) && !string.IsNullOrEmpty(_options.ClientSecret);

    public Uri GetAuthorizationUrl(string state)
    {
        Dictionary<string, string?> query = new()
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["state"] = state
        };
        if (!string.IsNullOrEmpty(_options.RedirectUri))
            query["redirect_uri"] = _options.RedirectUri;
        if (!string.IsNullOrEmpty(_options.Scope))
            query["scope"] = _options.Scope;

        var url = QueryHelpers.AddQueryString(_options.OAuthUrl.TrimEnd('/') + "/authorize", query);
        return new Uri(url);
    }

    public async Task<YandexIdUser> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken)
    {
        var accessToken = await ExchangeCodeAsync(authorizationCode, cancellationToken);
        return await FetchUserAsync(accessToken, cancellationToken);
    }

    private async Task<string> ExchangeCodeAsync(string authorizationCode, CancellationToken cancellationToken)
    {
        Dictionary<string, string> form = new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.OAuthUrl.TrimEnd('/') + "/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.ClientId + ":" + _options.ClientSecret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await SendAsync(request, cancellationToken);
        var body = await ReadTokenResponseAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(body.AccessToken))
            return body.AccessToken;
        if (body.Error is "invalid_grant" or "bad_verification_code")
            throw new InvalidAuthorizationCodeException("Yandex ID authorization code is invalid, expired or already used");
        if ((int)response.StatusCode >= 500)
            throw new YandexIdUnavailableException($"Yandex ID token endpoint returned {(int)response.StatusCode}");

        throw new InvalidOperationException($"Yandex ID token exchange failed: {(int)response.StatusCode} {body.Error}: {body.ErrorDescription}");
    }

    private async Task<YandexIdUser> FetchUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.LoginUrl.TrimEnd('/') + "/info?format=json");
        request.Headers.TryAddWithoutValidation("Authorization", "OAuth " + accessToken);

        using var response = await SendAsync(request, cancellationToken);
        if ((int)response.StatusCode >= 500)
            throw new YandexIdUnavailableException($"Yandex ID user info returned {(int)response.StatusCode}");
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Yandex ID user info returned {(int)response.StatusCode}");

        var info = await response.Content.ReadFromJsonAsync<YandexUserInfoResponse>(JsonOptions, cancellationToken);
        if (info is null || string.IsNullOrEmpty(info.Id))
            throw new InvalidOperationException("Yandex ID user info has no id");

        var displayName = FirstNonEmpty(info.DisplayName, info.RealName, info.Login);
        return new YandexIdUser(info.Id, info.Login ?? string.Empty, displayName, info.DefaultEmail ?? string.Empty);
    }

    private async Task<YandexTokenResponse> ReadTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<YandexTokenResponse>(JsonOptions, cancellationToken);
            return body ?? new YandexTokenResponse();
        }
        catch (JsonException) when (response.StatusCode != HttpStatusCode.OK)
        {
            return new YandexTokenResponse();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new YandexIdUnavailableException("Yandex ID is unavailable", exception);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return string.Empty;
    }
}
