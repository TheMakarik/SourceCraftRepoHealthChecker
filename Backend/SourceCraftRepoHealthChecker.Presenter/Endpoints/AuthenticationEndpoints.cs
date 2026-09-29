using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using SourceCraftRepoHealthChecker.Application.Analysis;
using SourceCraftRepoHealthChecker.Application.Authentication.UseCases;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Presenter.Authentication;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", async (HttpContext context, IAuthenticateUserUseCase useCase, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var state = Guid.NewGuid().ToString("N");
            var sameSite = ResolveCookieSameSite(configuration);
            context.Response.Cookies.Append("oauth_state", state, new CookieOptions { HttpOnly = true, SameSite = sameSite, Secure = context.Request.IsHttps || sameSite == SameSiteMode.None });
            var url = await useCase.StartAsync(state, cancellationToken);
            return Results.Redirect(url.ToString());
        });

        endpoints.MapGet("/auth/callback", async (string code, string state, HttpContext context, IAuthenticateUserUseCase useCase, UserTicketProtector ticketProtector, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var expectedState = context.Request.Cookies["oauth_state"];
            if (string.IsNullOrEmpty(expectedState) || expectedState != state)
                return Results.BadRequest(new { error = "invalid_state" });

            var user = await useCase.CompleteAsync(code, state, cancellationToken);
            var sameSite = ResolveCookieSameSite(configuration);
            context.Response.Cookies.Append(
                HttpContextUserExtensions.TicketCookieName,
                ticketProtector.Protect(user.UserId),
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = sameSite,
                    Secure = context.Request.IsHttps || sameSite == SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.Add(ticketProtector.Lifetime)
                });
            context.Response.Cookies.Delete("oauth_state");
            var frontendRedirect = configuration["Authentication:FrontendRedirectUrl"];
            return string.IsNullOrWhiteSpace(frontendRedirect) ? Results.Ok(user) : Results.Redirect(frontendRedirect);
        });

        endpoints.MapPost("/auth/logout", (HttpContext context, IConfiguration configuration) =>
        {
            var cookieOptions = CreateCookieOptions(context, configuration);
            context.Response.Cookies.Delete(HttpContextUserExtensions.TicketCookieName, cookieOptions);
            context.Response.Cookies.Delete("oauth_state", cookieOptions);
            return Results.NoContent();
        });

        endpoints.MapGet("/api/me", async (HttpContext context, IGetCurrentUserUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            var user = await useCase.GetAsync(userId.Value, cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        endpoints.MapGet("/api/me/tokens", async (HttpContext context, IGetTokenOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            return Results.Ok(await useCase.GetAsync(userId.Value, cancellationToken));
        });

        endpoints.MapPost("/api/me/sourcecraft-token", async (SourceCraftTokenRequest request, HttpContext context, IStoreSourceCraftTokenUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "empty_token" });

            return await useCase.StoreAsync(userId.Value, request.Token, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        });

        endpoints.MapGet("/api/me/repositories", async (HttpContext context, IGetUserRepositoriesUseCase useCase, IResolveSourceCraftTokenUseCase tokenUseCase, CancellationToken cancellationToken) =>
        {
            var token = await ResolveTokenAsync(context, tokenUseCase, cancellationToken);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            return Results.Ok(await useCase.GetAsync(token, cancellationToken));
        });

        endpoints.MapPost("/api/me/repositories/{id}/analyze", async (
            string id,
            HttpContext context,
            IAnalyzeRepositoryUseCase useCase,
            IResolveSourceCraftTokenUseCase tokenUseCase,
            ISourceCraftAccessTokenAccessor accessTokenAccessor,
            IAnalysisStatusHub statusHub,
            CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();

            var token = await ResolveTokenAsync(context, tokenUseCase, cancellationToken);
            if (!string.IsNullOrEmpty(token))
                accessTokenAccessor.Token = token;

            statusHub.Publish(id, AnalysisStatuses.Running, null);
            try
            {
                var result = await useCase.AnalyzeAsync(new AnalyzeRepositoryRequest(id, userId), cancellationToken);
                statusHub.Publish(id, AnalysisStatuses.Completed, result.HealthCheck.Score);
                return Results.Ok(result);
            }
            catch (Exception)
            {
                statusHub.Publish(id, AnalysisStatuses.Failed, null);
                throw;
            }
        });

        return endpoints;
    }

    private static async Task<string?> ResolveTokenAsync(HttpContext context, IResolveSourceCraftTokenUseCase tokenUseCase, CancellationToken cancellationToken)
    {
        var token = context.Request.GetBearerToken();
        if (!string.IsNullOrEmpty(token))
            return token;

        var userId = context.GetCurrentUserId();
        return userId is null ? null : await tokenUseCase.ResolveAsync(userId.Value, cancellationToken);
    }

    private static CookieOptions CreateCookieOptions(HttpContext context, IConfiguration configuration)
    {
        var sameSite = ResolveCookieSameSite(configuration);
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = sameSite,
            Secure = context.Request.IsHttps || sameSite == SameSiteMode.None
        };
    }

    private static SameSiteMode ResolveCookieSameSite(IConfiguration configuration)
    {
        var value = configuration["Authentication:CookieSameSite"];
        return Enum.TryParse<SameSiteMode>(value, ignoreCase: true, out var sameSite) ? sameSite : SameSiteMode.Lax;
    }
}
