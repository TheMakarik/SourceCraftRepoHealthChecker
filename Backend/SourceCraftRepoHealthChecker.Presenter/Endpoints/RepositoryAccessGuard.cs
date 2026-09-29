using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Presenter.Authentication;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public sealed class RepositoryAccessGuard(IRepoHealthCheckerDbContext dbContext)
{
    public async Task<RepositoryAccessDecision> EvaluateAsync(string sourceCraftId, HttpContext context, CancellationToken cancellationToken)
    {
        var repository = await dbContext.Repositories
            .FirstOrDefaultAsync(item => item.SourceCraftId == sourceCraftId, cancellationToken);
        if (repository is null)
            return RepositoryAccessDecision.Unknown;

        return IsAccessible(repository.IsPrivate, repository.OwnerId, context.GetCurrentUserId())
            ? RepositoryAccessDecision.Allowed
            : RepositoryAccessDecision.Forbidden;
    }

    public async Task<IReadOnlySet<string>> GetAccessibleRepositoryIdsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var currentUserId = context.GetCurrentUserId();
        var repositories = await dbContext.Repositories
            .Select(item => new { item.SourceCraftId, item.IsPrivate, item.OwnerId })
            .ToListAsync(cancellationToken);

        return repositories
            .Where(item => IsAccessible(item.IsPrivate, item.OwnerId, currentUserId))
            .Select(item => item.SourceCraftId)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool IsAccessible(bool isPrivate, Guid? ownerId, Guid? currentUserId) =>
        !isPrivate || (ownerId is not null && ownerId == currentUserId);
}
