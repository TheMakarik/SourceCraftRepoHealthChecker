namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record SourceCraftUser(
    string Id,
    string Login,
    string DisplayName,
    string? Email,
    string? AvatarUrl = null);
