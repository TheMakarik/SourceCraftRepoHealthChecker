namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public interface IGetRepositoryLanguagesUseCase
{
    public Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken);
}
