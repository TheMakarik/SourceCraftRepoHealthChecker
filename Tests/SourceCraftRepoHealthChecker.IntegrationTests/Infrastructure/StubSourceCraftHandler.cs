using System.Net;
using System.Text;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public sealed class StubSourceCraftHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var body = BuildEnvelope(path);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        await Task.CompletedTask;
        return response;
    }

    private static string BuildEnvelope(string path) => path switch
    {
        "/auth/url" => Envelope("{\"url\":\"https://oauth.example/authorize\"}"),
        "/auth/token" or "/auth/me" => Envelope("{\"id\":\"u1\",\"login\":\"alice\",\"displayName\":\"Alice\",\"email\":null}"),
        "/auth/repositories" => Envelope("[]"),
        "/repositories" => Envelope("[]"),
        _ when path.EndsWith("/security/findings") => Envelope("[]"),
        _ when path.EndsWith("/activity/contributors") => Envelope("[]"),
        _ when path.EndsWith("/activity/releases") => Envelope("[]"),
        _ when path.EndsWith("/issues") => Envelope("[]"),
        _ when path.EndsWith("/merge-requests") => Envelope("[]"),
        _ when path.EndsWith("/pipelines") => Envelope("[]"),
        _ when path.EndsWith("/structure") => Envelope("{\"totalFiles\":10,\"totalDirectories\":3,\"maxDepth\":2,\"rootFiles\":2,\"largestDirectory\":\"src\",\"largestDirectoryFiles\":5}"),
        _ when path.EndsWith("/code-health") => Envelope("{\"todoCount\":1,\"fixmeCount\":0,\"totalCommentCount\":1,\"oldestCommentAge\":null}"),
        _ when path.EndsWith("/documentation") => Envelope("{\"hasReadme\":true,\"hasLicense\":true,\"hasContributing\":false,\"hasCodeOwners\":false,\"hasLocalRunInstructions\":true,\"hasBuildAndTestInstructions\":true}"),
        _ when path.EndsWith("/activity/commits") => Envelope("{\"totalCount\":3,\"firstCommitAt\":\"2026-01-01T00:00:00Z\",\"lastCommitAt\":\"2026-01-10T00:00:00Z\",\"commitsByDay\":{\"2026-01-10\":3}}"),
        _ when path.StartsWith("/repositories/") && !path["/repositories/".Length..].Contains('/') => Envelope("{\"id\":\"r1\",\"name\":\"demo\",\"fullName\":\"owner/demo\",\"url\":\"https://sourcecraft.dev/owner/demo\",\"language\":\"C#\",\"likesCount\":5,\"lastActivityAt\":\"2026-01-10T00:00:00Z\",\"isPrivate\":false,\"defaultBranch\":\"main\"}"),
        _ => Envelope("[]")
    };

    private static string Envelope(string data) =>
        $"{{\"requestId\":\"test\",\"collectedAt\":\"2026-01-01T00:00:00Z\",\"status\":\"Available\",\"data\":{data}}}";
}
