using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record SourceCraftPage<T>(
    DataStatus Status,
    T? Data,
    string? Reason,
    string? NextPageToken)
{
    public SourceCraftResult<T> ToResult() => new(Status, Data, Reason);
}
