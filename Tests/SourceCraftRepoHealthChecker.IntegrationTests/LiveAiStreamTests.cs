using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class LiveAiStreamTests : IClassFixture<LiveSourceCraftApiFactory>
{
    private const string ZaggyCodeId = "01a0e3c3-7dad-7d7d-b75f-55556f91b53d";
    private readonly LiveSourceCraftApiFactory _factory;

    public LiveAiStreamTests(LiveSourceCraftApiFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
    }

    [Fact]
    public async Task Stream_ReturnsTextOrReasoning()
    {
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_TOKEN");
        var pat = Environment.GetEnvironmentVariable("SOURCECRAFT_PAT");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(pat))
            return;

        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var cookie = await LoginAsync(client);

        using var storePat = new HttpRequestMessage(HttpMethod.Post, "/api/me/sourcecraft-token") { Content = JsonContent.Create(new { token = pat }) };
        storePat.Headers.Add("Cookie", cookie);
        (await client.SendAsync(storePat)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var analyze = new HttpRequestMessage(HttpMethod.Post, $"/api/me/repositories/{ZaggyCodeId}/analyze");
        analyze.Headers.Add("Cookie", cookie);
        analyze.Headers.Add("Authorization", $"Bearer {pat}");
        (await client.SendAsync(analyze)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var storeAi = new HttpRequestMessage(HttpMethod.Put, "/api/me/ai")
        {
            Content = JsonContent.Create(new { provider = "DeepSeek", baseUrl = (string?)null, model = "deepseek-flash", token = key })
        };
        storeAi.Headers.Add("Cookie", cookie);
        (await client.SendAsync(storeAi)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var stream = new HttpRequestMessage(HttpMethod.Post, $"/api/repositories/{ZaggyCodeId}/ai-summary/stream");
        stream.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(stream);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        File.WriteAllText("/tmp/opencode/ai-stream.txt", body);
        body.Should().NotBeNullOrWhiteSpace();
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
