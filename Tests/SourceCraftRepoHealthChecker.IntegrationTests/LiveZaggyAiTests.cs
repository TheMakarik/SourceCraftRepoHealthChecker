using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class LiveZaggyAiTests : IClassFixture<LiveSourceCraftApiFactory>
{
    private const string ZaggyCodeId = "01a0e3c3-7dad-7d7d-b75f-55556f91b53d";
    private readonly LiveSourceCraftApiFactory _factory;

    public LiveZaggyAiTests(LiveSourceCraftApiFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
    }

    [Fact]
    public async Task ZaggyCode_AnalyzedAndExplained_ByDeepSeek()
    {
        var deepSeekToken = Environment.GetEnvironmentVariable("DEEPSEEK_TOKEN");
        var sourceCraftToken = Environment.GetEnvironmentVariable("SOURCECRAFT_PAT");
        if (string.IsNullOrWhiteSpace(deepSeekToken) || string.IsNullOrWhiteSpace(sourceCraftToken))
            return;

        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var cookie = await LoginAsync(client);

        using var analyze = new HttpRequestMessage(HttpMethod.Post, $"/api/me/repositories/{ZaggyCodeId}/analyze");
        analyze.Headers.Add("Cookie", cookie);
        analyze.Headers.Add("Authorization", $"Bearer {sourceCraftToken}");
        var analyzeResponse = await client.SendAsync(analyze);
        analyzeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var storeAi = new HttpRequestMessage(HttpMethod.Put, "/api/me/ai")
        {
            Content = JsonContent.Create(new { provider = "DeepSeek", baseUrl = (string?)null, model = "deepseek-chat", token = deepSeekToken })
        };
        storeAi.Headers.Add("Cookie", cookie);
        (await client.SendAsync(storeAi)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var answers = new StringBuilder();
        answers.AppendLine("===== AI-SUMMARY (zaggy-code) =====");
        answers.AppendLine(await CallAsync(client, cookie, $"/api/repositories/{ZaggyCodeId}/ai-summary", "summary"));

        foreach (var kind in new[] { "recommendations", "explanation", "action-plan", "security-triage", "risk-forecast" })
        {
            answers.AppendLine();
            answers.AppendLine($"===== AI-INSIGHT: {kind} (zaggy-code) =====");
            answers.AppendLine(await CallAsync(client, cookie, $"/api/repositories/{ZaggyCodeId}/ai-insights/{kind}", "content"));
        }

        File.WriteAllText("/tmp/opencode/deepseek-zaggy-answers.txt", answers.ToString());
    }

    private static async Task<string> CallAsync(HttpClient client, string cookie, string path, string field)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var content = body![field].ToString();
        content.Should().NotBeNullOrWhiteSpace(path);
        return content;
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
