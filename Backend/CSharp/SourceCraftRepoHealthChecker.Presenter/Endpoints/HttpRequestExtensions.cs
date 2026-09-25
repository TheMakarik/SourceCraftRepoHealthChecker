using Microsoft.AspNetCore.Http;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class HttpRequestExtensions
{
    public static string? GetBearerToken(this HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
    }
}
