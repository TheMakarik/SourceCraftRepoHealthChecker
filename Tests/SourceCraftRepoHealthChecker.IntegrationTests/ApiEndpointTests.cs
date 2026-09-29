using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class ApiEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/healthz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RatingPage_ReturnsHtml()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/rating");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task AnalysisPage_ForUnknownRepository_ReturnsNotFound()
    {
        using var client = CreateClient();
        (await client.GetAsync("/repositories/unknown")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Repositories_ReturnsLeaderboard()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Repositories_WithFilters_ReturnsLeaderboard()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories?hasCi=true&minScore=0&maxScore=100&sort=score");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReviewInsights_ReturnsSourceCraftEnvelope()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories/r1/review-insights");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Available");
        document.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task ReviewInsights_ForPrivateRepositoryAnonymous_ReturnsNotFound()
    {
        _factory.SeedRepository("private-review", isPrivate: true, ownerId: Guid.NewGuid());
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories/private-review/review-insights");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RefreshRepositories_WithoutAuthentication_ReturnsOk()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/repositories/refresh", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Analysis_ForUnknownRepository_ReturnsNotFound()
    {
        using var client = CreateClient();
        (await client.GetAsync("/api/repositories/unknown/analysis")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Report_ForUnknownRepository_ReturnsNotFound()
    {
        using var client = CreateClient();
        (await client.GetAsync("/api/repositories/unknown/report?format=markdown")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/repositories/unknown/report.md")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Structure_ReturnsData()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories/r1/structure");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Structure_ForPublicRepositoryAnonymous_ReturnsOk()
    {
        _factory.SeedRepository("public-repo", isPrivate: false, ownerId: null);
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories/public-repo/structure");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Structure_ForPrivateRepositoryAnonymous_ReturnsNotFound()
    {
        _factory.SeedRepository("private-repo", isPrivate: true, ownerId: Guid.NewGuid());
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories/private-repo/structure");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Structure_ForPrivateRepositoryWithoutOwnerAnonymous_ReturnsNotFound()
    {
        _factory.SeedRepository("orphan-private", isPrivate: true, ownerId: null);
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories/orphan-private/structure");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analysis_ForPrivateRepositoryAnonymous_ReturnsNotFound()
    {
        _factory.SeedRepository("private-repo", isPrivate: true, ownerId: Guid.NewGuid());
        using var client = CreateClient();
        var response = await client.GetAsync("/api/repositories/private-repo/analysis");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnalysisPage_ForPrivateRepositoryAnonymous_ReturnsNotFound()
    {
        _factory.SeedRepository("private-repo", isPrivate: true, ownerId: Guid.NewGuid());
        using var client = CreateClient();
        var response = await client.GetAsync("/repositories/private-repo");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Me_WithoutSession_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MyRepositories_WithoutToken_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        (await client.GetAsync("/api/me/repositories")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MyRepositories_WithToken_ReturnsOk()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me/repositories");
        request.Headers.Add("Authorization", "Bearer test-pat");
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StoreAiSettings_WithoutSession_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync("/api/me/ai", new { provider = "OpenAI", baseUrl = (string?)null, model = "gpt-4o-mini", token = "key" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnalyzeRepository_WithoutSession_ForPublicRepository_ReturnsOk()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/me/repositories/r1/analyze", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Registration_LoginAndCallback_IssuesTicketAndMeWorks()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

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

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        meRequest.Headers.Add("Cookie", $"user_ticket={ticket}");
        var me = await client.SendAsync(meRequest);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await me.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        user!["login"].ToString().Should().Be("alice");

        using var aiRequest = new HttpRequestMessage(HttpMethod.Put, "/api/me/ai")
        {
            Content = JsonContent.Create(new { provider = "OpenAI", baseUrl = (string?)null, model = "gpt-4o-mini", token = "key" })
        };
        aiRequest.Headers.Add("Cookie", $"user_ticket={ticket}");
        (await client.SendAsync(aiRequest)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Registration_WithInvalidState_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/auth/callback?code=good-code&state=wrong");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
