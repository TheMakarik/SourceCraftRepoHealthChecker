using Refit;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal static class SourceCraftFailure
{
    public static SourceCraftResult<T> Unavailable<T>(Exception exception) =>
        new(DataStatus.Unavailable, default, Describe(exception));

    public static SourceCraftResult<T> NoData<T>(string reason) =>
        new(DataStatus.NoData, default, reason);

    public static string Describe(Exception exception) => exception switch
    {
        ApiException apiException => $"SourceCraft API returned {(int)apiException.StatusCode}",
        HttpRequestException => "SourceCraft API is unavailable",
        _ => exception.Message
    };
}
