using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Entities;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public sealed class RefreshRepositoriesUseCase(
    IRepoHealthCheckerDbContext dbContext,
    ISourceCraftRepositoryCatalog catalog,
    IOptions<RepositoryOptions> repositoryOptions,
    TimeProvider timeProvider,
    ILogger<RefreshRepositoriesUseCase> logger) : IRefreshRepositoriesUseCase
{
    public async Task<int> RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await catalog.GetOpenRepositoriesAsync(cancellationToken);
        if (result.Data is null)
            throw new SourceCraftOperationException($"Repository catalog is not available: {result.Status}");

        var options = repositoryOptions.Value;
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.Repositories.ToDictionaryAsync(x => x.SourceCraftId, cancellationToken);
        var updated = 0;

        foreach (var source in result.Data)
        {
            var sourceCraftId = Truncate(source.Id, options.MaxSourceCraftIdLength);
            if (!existing.TryGetValue(sourceCraftId, out var repository))
            {
                repository = new Repository { Id = Guid.NewGuid(), SourceCraftId = sourceCraftId, CreatedAt = now };
                dbContext.Repositories.Add(repository);
            }

            repository.Name = Truncate(source.Name, options.MaxNameLength);
            repository.FullName = Truncate(source.FullName, options.MaxFullNameLength);
            repository.Url = Truncate(source.Url, options.MaxUrlLength);
            repository.Language = Truncate(source.Language, options.MaxLanguageLength);
            repository.IsPrivate = source.IsPrivate;
            repository.LikesCount = source.LikesCount;
            repository.LastActivityAt = source.LastActivityAt;
            updated++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refreshed {Count} repositories", updated);

        return updated;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
