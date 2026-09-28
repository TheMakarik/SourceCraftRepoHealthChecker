using Refit;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public interface IAppSecApi
{
    [Get("/v1/defect-groups")]
    public Task<AppSecDefectGroupPageDto> GetDefectGroupsAsync(
        [AliasAs("gitRepo")] string gitRepo,
        [AliasAs("pageSize")] int pageSize,
        [AliasAs("pageToken")] string? pageToken,
        CancellationToken cancellationToken);
}
