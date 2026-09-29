namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public static class PublicRepositoryScoreFactory
{
    public static PublicRepositoryScore? Create(RepositoryAnalysis analysis, string methodologyVersion)
    {
        if (analysis.IsPrivate)
            return null;

        var categories = analysis.Categories
            .Select(item => new PublicRepositoryScoreCategory(item.Category, item.Score, item.DataStatus))
            .ToArray();

        return new PublicRepositoryScore(
            analysis.SourceCraftId,
            analysis.FullName,
            analysis.Score,
            Grade(analysis.Score),
            analysis.AnalyzedAt,
            methodologyVersion,
            categories);
    }

    public static string? Grade(int? score) => score switch
    {
        null => null,
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        >= 50 => "E",
        _ => "F"
    };
}
