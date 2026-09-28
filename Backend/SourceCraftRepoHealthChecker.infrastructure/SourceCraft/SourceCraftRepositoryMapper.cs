using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal static class SourceCraftRepositoryMapper
{
    public static SourceCraftRepository Map(RepositoryDto repository)
    {
        var likes = 0;
        foreach (var reaction in repository.Rating?.ReactionCounts ?? [])
        {
            if (int.TryParse(reaction.Count, out var count))
                likes += count;
        }

        var name = string.IsNullOrEmpty(repository.Slug) ? repository.Name ?? string.Empty : repository.Slug;
        var organizationSlug = repository.Organization?.Slug ?? string.Empty;

        return new SourceCraftRepository(
            repository.Id ?? string.Empty,
            name,
            organizationSlug + "/" + name,
            repository.WebUrl ?? string.Empty,
            repository.Language?.Name ?? string.Empty,
            likes,
            repository.LastUpdated,
            !string.Equals(repository.Visibility, "public", StringComparison.Ordinal),
            repository.DefaultBranch ?? string.Empty);
    }
}
