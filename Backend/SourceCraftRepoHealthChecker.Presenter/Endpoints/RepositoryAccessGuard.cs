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
            .AsNoTracking()
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
        var repositories = dbContext.Repositories.AsNoTracking();
        var accessible = currentUserId is null
            ? repositories.Where(item => !item.IsPrivate)
            : repositories.Where(item => !item.IsPrivate || item.OwnerId == currentUserId);

        var sourceCraftIds = await accessible.Select(item => item.SourceCraftId).ToListAsync(cancellationToken);
        return sourceCraftIds.ToHashSet(StringComparer.Ordinal);
    }

    public static bool IsAccessible(bool isPrivate, Guid? ownerId, Guid? currentUserId) =>
        !isPrivate || (ownerId is not null && ownerId == currentUserId);
}
