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

        var repositories = dbContext.Repositories.AsNoTracking().Where(x => !x.IsPrivate);
        if (query.Languages.Count > 0)
        {
            var languages = query.Languages;
            repositories = repositories.Where(x => languages.Contains(x.Language.ToLower()));
        }

        var rows = await repositories
            .Select(x => new LeaderboardRow
            {
                SourceCraftId = x.SourceCraftId,
                Name = x.Name,
                FullName = x.FullName,
                Url = x.Url,
                LikesCount = x.LikesCount,
                Language = x.Language,
                LastActivityAt = x.LastActivityAt,
                Score = x.AnalysisRuns
                    .Where(a => a.Status == AnalysisStatus.Completed && a.DataStatus == DataStatus.Available)
                    .OrderByDescending(a => a.CompletedAt)
                    .Select(a => a.Score)
                    .FirstOrDefault(),
                AnalyzedAt = x.AnalysisRuns
                    .Where(a => a.Status == AnalysisStatus.Completed && a.DataStatus == DataStatus.Available)
                    .OrderByDescending(a => a.CompletedAt)
                    .Select(a => (DateTimeOffset?)a.CompletedAt)
                    .FirstOrDefault(),
                PreviousScore = x.AnalysisRuns
                    .Where(a => a.Status == AnalysisStatus.Completed && a.DataStatus == DataStatus.Available)
                    .OrderByDescending(a => a.CompletedAt)
                    .Skip(1)
                    .Select(a => a.Score)
                    .FirstOrDefault(),
                HasCi = x.AnalysisRuns
                    .Where(a => a.Status == AnalysisStatus.Completed && a.DataStatus == DataStatus.Available)
                    .Any(a => a.Metrics.Any(metric => metric.Code == MetricCode.CiCdPresence && metric.RawValue > 0))
            })
            .ToListAsync(cancellationToken);

        var candidates = ApplyFilters(rows, query);
        var ordered = SortCandidates(candidates, query.Sort);
        var previousPlaces = BuildPreviousPlaces(candidates, query.Sort);

        var totalCount = ordered.Count;
        var offset = (page - 1) * pageSize;
        var items = ordered
            .Skip(offset)
            .Take(pageSize)
            .Select((row, index) => ToItem(row, offset + index + 1, previousPlaces))
            .ToArray();

        return new RepositoryLeaderboardPage(items, totalCount, page, pageSize);
    }

    private static IReadOnlyList<LeaderboardRow> ApplyFilters(IReadOnlyList<LeaderboardRow> rows, RepositoryLeaderboardQuery query)
    {
        var minScore = query.MinScore;
        var maxScore = query.MaxScore;
        if (!query.IsScoreRangeOrdered)
            (minScore, maxScore) = (maxScore, minScore);

        var filtered = rows.AsEnumerable();
        if (query.HasCi is bool hasCi)
            filtered = filtered.Where(row => row.HasCi == hasCi);
        if (minScore is not null)
            filtered = filtered.Where(row => row.Score >= minScore);
        if (maxScore is not null)
            filtered = filtered.Where(row => row.Score <= maxScore);

        return filtered.ToArray();
    }

    private static IReadOnlyList<LeaderboardRow> SortCandidates(IReadOnlyList<LeaderboardRow> candidates, RepositoryLeaderboardSort sort) => sort switch
    {
        RepositoryLeaderboardSort.Likes => candidates
            .OrderByDescending(row => row.LikesCount)
            .ThenByDescending(row => row.Score ?? -1)
            .ToArray(),
        RepositoryLeaderboardSort.Activity => candidates
            .OrderByDescending(row => row.LastActivityAt)
            .ThenByDescending(row => row.Score ?? -1)
            .ToArray(),
        _ => candidates
            .OrderByDescending(row => row.Score ?? -1)
            .ThenByDescending(row => row.LikesCount)
            .ToArray()
    };

    private static IReadOnlyDictionary<string, int> BuildPreviousPlaces(IReadOnlyList<LeaderboardRow> candidates, RepositoryLeaderboardSort sort)
    {
        var previousPlaces = new Dictionary<string, int>(StringComparer.Ordinal);
        if (sort != RepositoryLeaderboardSort.Score)
            return previousPlaces;

        var ordered = candidates
            .OrderByDescending(row => row.PreviousScore ?? -1)
            .ThenByDescending(row => row.LikesCount)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
            previousPlaces[ordered[index].SourceCraftId] = index + 1;

        return previousPlaces;
    }

    private static RepositoryLeaderboardItem ToItem(LeaderboardRow row, int place, IReadOnlyDictionary<string, int> previousPlaces)
    {
        int? placeDelta = null;
        if (row.PreviousScore is not null && previousPlaces.TryGetValue(row.SourceCraftId, out var previousPlace))
            placeDelta = previousPlace - place;

        return new RepositoryLeaderboardItem(
            place,
            row.SourceCraftId,
            row.Name,
            row.FullName,
            row.Url,
            row.Score,
            row.LikesCount,
            row.Language,
            row.LastActivityAt,
            row.AnalyzedAt,
            placeDelta);
    }
}
