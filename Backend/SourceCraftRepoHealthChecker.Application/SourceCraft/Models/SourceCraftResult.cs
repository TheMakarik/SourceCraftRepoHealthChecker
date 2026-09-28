using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record SourceCraftResult<T>(
    DataStatus Status,
    T? Data,
    string? Reason);
