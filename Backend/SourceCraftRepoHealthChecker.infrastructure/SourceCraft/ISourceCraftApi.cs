using Refit;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public interface ISourceCraftApi
{
    [Get("/user")]
    public Task<UserProfileDto> GetCurrentUserAsync([Header("Authorization")] string authorization, CancellationToken cancellationToken);

    [Get("/repos")]
    public Task<RepositoryPageDto> GetRepositoriesAsync(
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        [AliasAs("sort_by")] string? sortBy,
        CancellationToken cancellationToken);

    [Get("/repos/id:{id}")]
    public Task<RepositoryDto> GetRepositoryAsync(string id, CancellationToken cancellationToken);

    [Get("/me/repos")]
    public Task<RepositoryPageDto> GetMyRepositoriesAsync(
        [Header("Authorization")] string authorization,
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        CancellationToken cancellationToken);

    [Get("/repos/id:{id}/issues")]
    public Task<IssuePageDto> GetIssuesAsync(
        string id,
        [AliasAs("sort_by")] string sortBy,
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        CancellationToken cancellationToken);

    [Get("/issues/id:{id}/comments")]
    public Task<IssueCommentPageDto> GetIssueCommentsAsync(
        string id,
        [AliasAs("sort_by")] string sortBy,
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        CancellationToken cancellationToken);

    [Get("/repos/id:{id}/pulls")]
    public Task<PullRequestPageDto> GetPullRequestsAsync(
        string id,
        [AliasAs("sort_by")] string sortBy,
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        CancellationToken cancellationToken);

    [Get("/pulls/id:{id}/comments")]
    public Task<PullRequestCommentPageDto> GetPullRequestCommentsAsync(
        string id,
        [AliasAs("sort_by")] string sortBy,
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        CancellationToken cancellationToken);

    [Get("/repos/id:{id}/releases")]
    public Task<ReleasePageDto> GetReleasesAsync(
        string id,
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        CancellationToken cancellationToken);

    [Get("/repos/id:{id}/cicd/runs")]
    public Task<PipelineRunPageDto> GetPipelineRunsAsync(
        string id,
        [AliasAs("page_size")] int pageSize,
        [AliasAs("page_token")] string? pageToken,
        CancellationToken cancellationToken);
}
