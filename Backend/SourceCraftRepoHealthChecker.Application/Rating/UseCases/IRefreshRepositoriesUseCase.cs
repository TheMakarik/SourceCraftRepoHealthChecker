namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public interface IRefreshRepositoriesUseCase
{
    public Task<int> RefreshAsync(CancellationToken cancellationToken);
}
