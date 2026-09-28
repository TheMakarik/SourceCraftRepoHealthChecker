using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class LiveAiDeepSeekTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LiveAiDeepSeekTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
    }

    [Fact]
    public async Task AiFeatures_OnDeepSeek_ReturnContent()
    {
        var token = Environment.GetEnvironmentVariable("DEEPSEEK_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            return;

        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var cookie = await LoginAsync(client);

        using var analyze = new HttpRequestMessage(HttpMethod.Post, "/api/me/repositories/r1/analyze");
        analyze.Headers.Add("Cookie", cookie);
        (await client.SendAsync(analyze)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var storeAi = new HttpRequestMessage(HttpMethod.Put, "/api/me/ai")
        {
            Content = JsonContent.Create(new { provider = "DeepSeek", baseUrl = (string?)null, model = "deepseek-chat", token })
        };
        storeAi.Headers.Add("Cookie", cookie);
        (await client.SendAsync(storeAi)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var summaryRequest = new HttpRequestMessage(HttpMethod.Post, "/api/repositories/r1/ai-summary");
        summaryRequest.Headers.Add("Cookie", cookie);
        var summary = await client.SendAsync(summaryRequest);
        summary.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaryBody = await summary.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        summaryBody!["summary"].ToString().Should().NotBeNullOrWhiteSpace();

        foreach (var kind in new[] { "recommendations", "explanation", "action-plan", "security-triage", "risk-forecast" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/repositories/r1/ai-insights/{kind}");
            request.Headers.Add("Cookie", cookie);
            var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"kind={kind}");
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            body!["content"].ToString().Should().NotBeNullOrWhiteSpace($"kind={kind}");
        }
    }

    [Fact]
    public async Task SessionEndpoints_WithLogin_Work()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var cookie = await LoginAsync(client);

        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        me.Headers.Add("Cookie", cookie);
        var meResponse = await client.SendAsync(me);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await meResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        user!["login"].ToString().Should().Be("alice");

        using var store = new HttpRequestMessage(HttpMethod.Post, "/api/me/sourcecraft-token")
        {
            Content = JsonContent.Create(new { token = "test-pat" })
        };
        store.Headers.Add("Cookie", cookie);
        (await client.SendAsync(store)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var repositories = new HttpRequestMessage(HttpMethod.Get, "/api/me/repositories");
        repositories.Headers.Add("Cookie", cookie);
        (await client.SendAsync(repositories)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var login = await client.GetAsync("/auth/login");
        login.StatusCode.Should().Be(HttpStatusCode.Found);
        var state = ReadSetCookie(login, "oauth_state");
        state.Should().NotBeNullOrEmpty();

        using var callbackRequest = new HttpRequestMessage(HttpMethod.Get, $"/auth/callback?code=good-code&state={state}");
        callbackRequest.Headers.Add("Cookie", $"oauth_state={state}");
        var callback = await client.SendAsync(callbackRequest);
        callback.StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = ReadSetCookie(callback, "user_ticket");
        ticket.Should().NotBeNullOrEmpty();
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
