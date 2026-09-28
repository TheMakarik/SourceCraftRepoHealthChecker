namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public interface IGetTokenOverviewUseCase
{
    public Task<TokenOverview> GetAsync(Guid userId, CancellationToken cancellationToken);
}
