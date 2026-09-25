using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Authentication.UseCases;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", async (HttpContext context, IAuthenticateUserUseCase useCase, CancellationToken cancellationToken) =>
        {
            var state = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append("oauth_state", state, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps });
            var url = await useCase.StartAsync(state, cancellationToken);
            return Results.Redirect(url.ToString());
        });

        endpoints.MapGet("/auth/callback", async (string code, string state, HttpContext context, IAuthenticateUserUseCase useCase, CancellationToken cancellationToken) =>
        {
            var expectedState = context.Request.Cookies["oauth_state"];
            if (string.IsNullOrEmpty(expectedState) || expectedState != state)
                return Results.BadRequest(new { error = "invalid_state" });

            var user = await useCase.CompleteAsync(code, state, cancellationToken);
            context.Response.Cookies.Append("user_id", user.UserId.ToString(), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps });
            context.Response.Cookies.Delete("oauth_state");
            return Results.Ok(user);
        });

        endpoints.MapGet("/api/me", async (HttpContext context, IRepoHealthCheckerDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var cookie = context.Request.Cookies["user_id"];
            if (!Guid.TryParse(cookie, out var userId))
                return Results.Unauthorized();

            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
            return user is null
                ? Results.NotFound()
                : Results.Ok(new { user.Id, user.YaId, user.Login, user.DisplayName });
        });

        endpoints.MapGet("/api/me/repositories", async (HttpContext context, IGetUserRepositoriesUseCase useCase, CancellationToken cancellationToken) =>
        {
            var token = context.Request.GetBearerToken();
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            return Results.Ok(await useCase.GetAsync(token, cancellationToken));
        });

        endpoints.MapPost("/api/me/repositories/{id}/analyze", async (string id, HttpContext context, IAnalyzeRepositoryUseCase useCase, CancellationToken cancellationToken) =>
        {
            Guid? userId = Guid.TryParse(context.Request.Cookies["user_id"], out var parsed) ? parsed : null;
            var result = await useCase.AnalyzeAsync(new AnalyzeRepositoryRequest(id, userId), cancellationToken);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
