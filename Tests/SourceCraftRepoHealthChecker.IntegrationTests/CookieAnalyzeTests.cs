using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class CookieAnalyzeTests : IClassFixture<LiveSourceCraftApiFactory>
{
    private const string ZaggyCodeId = "01a0e3c3-7dad-7d7d-b75f-55556f91b53d";
    private readonly LiveSourceCraftApiFactory _factory;

    public CookieAnalyzeTests(LiveSourceCraftApiFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
    }

    [Fact]
    public async Task Analyze_WithStoredPatAndCookie_Works()
    {
        var pat = Environment.GetEnvironmentVariable("SOURCECRAFT_PAT");
        if (string.IsNullOrWhiteSpace(pat))
            return;

        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var cookie = await LoginAsync(client);

        using var store = new HttpRequestMessage(HttpMethod.Post, "/api/me/sourcecraft-token")
        {
            Content = JsonContent.Create(new { token = pat })
        };
        store.Headers.Add("Cookie", cookie);
        (await client.SendAsync(store)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var analyze = new HttpRequestMessage(HttpMethod.Post, $"/api/me/repositories/{ZaggyCodeId}/analyze");
        analyze.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(analyze);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var login = await client.GetAsync("/auth/login");
        var state = ReadSetCookie(login, "oauth_state");
        using var callbackRequest = new HttpRequestMessage(HttpMethod.Get, $"/auth/callback?code=good-code&state={state}");
        callbackRequest.Headers.Add("Cookie", $"oauth_state={state}");
        var callback = await client.SendAsync(callbackRequest);
        var ticket = ReadSetCookie(callback, "user_ticket");
        return $"user_ticket={ticket}";
    }

    private static string? ReadSetCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        foreach (var value in values)
        {
            if (!value.StartsWith(name + "=", StringComparison.Ordinal))
                continue;
            var cookieValue = value[(name.Length + 1)..];
            var separator = cookieValue.IndexOf(';');
            return separator < 0 ? cookieValue : cookieValue[..separator];
        }

        return null;
    }
}
