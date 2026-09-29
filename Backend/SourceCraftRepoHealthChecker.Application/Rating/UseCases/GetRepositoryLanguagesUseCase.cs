using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public sealed class GetRepositoryLanguagesUseCase(IRepoHealthCheckerDbContext dbContext) : IGetRepositoryLanguagesUseCase
{
    public async Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken)
    {
        var languages = await dbContext.Repositories
            .Where(repository => !repository.IsPrivate && !string.IsNullOrWhiteSpace(repository.Language))
            .Select(repository => repository.Language)
            .Distinct()
            .OrderBy(language => language.ToLower())
            .ToListAsync(cancellationToken);

        return languages;
    }
}
