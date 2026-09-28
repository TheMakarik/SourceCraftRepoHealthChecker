using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SourceCraftRepoHealthChecker.Presenter.Authentication;

public static class HttpContextUserExtensions
{
    public const string TicketCookieName = "user_ticket";

    public static Guid? GetCurrentUserId(this HttpContext context)
    {
        var ticket = context.Request.Cookies[TicketCookieName];
        var protector = context.RequestServices.GetRequiredService<UserTicketProtector>();
        return protector.Unprotect(ticket);
    }
}
