using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public sealed class GetRepositoryLeaderboardUseCase(IRepoHealthCheckerDbContext dbContext) : IGetRepositoryLeaderboardUseCase
{
    private const int MaximumPageSize = 100;

    public async Task<RepositoryLeaderboardPage> GetAsync(RepositoryLeaderboardQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize < 1 ? 20 : query.PageSize, 1, MaximumPageSize);

        var repositories = dbContext.Repositories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Language))
            repositories = repositories.Where(x => x.Language == query.Language);

        var projected = repositories.Select(x => new
        {
            x.SourceCraftId,
            x.Name,
            x.FullName,
            x.Url,
            x.LikesCount,
            x.Language,
            x.LastActivityAt,
            Score = x.AnalysisRuns
                .Where(a => a.Status == AnalysisStatus.Completed)
                .OrderByDescending(a => a.CompletedAt)
                .Select(a => (int?)a.Score)
                .FirstOrDefault(),
            AnalyzedAt = x.AnalysisRuns
                .Where(a => a.Status == AnalysisStatus.Completed)
                .OrderByDescending(a => a.CompletedAt)
                .Select(a => (DateTimeOffset?)a.CompletedAt)
                .FirstOrDefault()
        });

        projected = query.Sort switch
        {
            RepositoryLeaderboardSort.Likes => projected.OrderByDescending(x => x.LikesCount).ThenByDescending(x => x.Score ?? -1),
            RepositoryLeaderboardSort.Activity => projected.OrderByDescending(x => x.LastActivityAt).ThenByDescending(x => x.Score ?? -1),
            _ => projected.OrderByDescending(x => x.Score ?? -1).ThenByDescending(x => x.LikesCount)
        };

        var totalCount = await projected.CountAsync(cancellationToken);
        var rows = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var offset = (page - 1) * pageSize;
        var items = rows
            .Select((row, index) => new RepositoryLeaderboardItem(
                offset + index + 1,
                row.SourceCraftId,
                row.Name,
                row.FullName,
                row.Url,
                row.Score,
                row.LikesCount,
                row.Language,
                row.LastActivityAt,
                row.AnalyzedAt))
            .ToArray();

        return new RepositoryLeaderboardPage(items, totalCount, page, pageSize);
    }
}
