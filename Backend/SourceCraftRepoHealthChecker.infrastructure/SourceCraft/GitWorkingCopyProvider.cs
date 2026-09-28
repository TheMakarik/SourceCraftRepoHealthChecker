using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class GitWorkingCopyProvider(
    ISourceCraftRepositoryCatalog catalog,
    ISourceCraftAuthentication authentication,
    ISourceCraftAccessTokenAccessor accessTokenAccessor,
    GitWorkingCopyCache workingCopyCache,
    IOptions<GitOptions> gitOptions,
    IOptions<SourceCraftServiceOptions> sourceCraftOptions,
    ILogger<GitWorkingCopyProvider> logger)
{
    public Task<SourceCraftResult<GitWorkingCopy>> AcquireAsync(string repositoryId, CancellationToken cancellationToken)
    {
        return workingCopyCache.GetOrCreateAsync(
            repositoryId,
            (cloneDirectory, cloneToken) => CreateAsync(repositoryId, cloneDirectory, cloneToken),
            cancellationToken);
    }

    private async Task<SourceCraftResult<GitWorkingCopy>> CreateAsync(string repositoryId, string cloneDirectory, CancellationToken cancellationToken)
    {
        var repositoryResult = await catalog.GetRepositoryAsync(repositoryId, cancellationToken);
        if (repositoryResult.Status != DataStatus.Available || repositoryResult.Data is null)
            return new SourceCraftResult<GitWorkingCopy>(repositoryResult.Status, null, repositoryResult.Reason);

        var options = gitOptions.Value;

        try
        {
            var credentials = await ResolveCredentialsAsync(options, cancellationToken);
            await CloneAsync(repositoryResult.Data, cloneDirectory, credentials, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryDelete(cloneDirectory);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(cloneDirectory);
            logger.LogWarning(exception, "Failed to clone repository {RepositoryId}", repositoryId);
            return new SourceCraftResult<GitWorkingCopy>(DataStatus.Unavailable, null, "Failed to obtain a working copy of the repository");
        }

        if (options.MaxRepositoryMegabytes > 0 && GetDirectorySize(cloneDirectory) > options.MaxRepositoryMegabytes * 1024 * 1024)
        {
            TryDelete(cloneDirectory);
            return new SourceCraftResult<GitWorkingCopy>(DataStatus.Unavailable, null, "Repository exceeds analysis size limit");
        }

        logger.LogInformation("Cloned repository {RepositoryId} into a working copy", repositoryId);
        return new SourceCraftResult<GitWorkingCopy>(DataStatus.Available, new GitWorkingCopy(cloneDirectory, deleteOnDispose: false), null);
    }

    private async Task CloneAsync(SourceCraftRepository repository, string cloneDirectory, UsernamePasswordCredentials? credentials, GitOptions options, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.CloneTimeoutSeconds > 0)
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.CloneTimeoutSeconds));

        var cloneToken = timeoutSource.Token;
        var fetchOptions = new FetchOptions
        {
            // libgit2 invokes this callback from native code: throwing here would crash the process,
            // so cancellation is signalled by returning false, which surfaces as UserCancelledException.
            OnTransferProgress = _ => !cloneToken.IsCancellationRequested
        };
        if (credentials is not null)
            fetchOptions.CredentialsProvider = (_, _, _) => credentials;

        var cloneOptions = new CloneOptions(fetchOptions)
        {
            IsBare = true,
            Checkout = false
        };
        if (!string.IsNullOrWhiteSpace(repository.DefaultBranch))
            cloneOptions.BranchName = repository.DefaultBranch;

        var cloneUrl = ResolveCloneUrl(repository);
        try
        {
            await Task.Run(() => Repository.Clone(cloneUrl, cloneDirectory, cloneOptions), cloneToken);
            if (cloneToken.IsCancellationRequested)
                ThrowCloneCancellation(cancellationToken);
        }
        catch (UserCancelledException) when (cloneToken.IsCancellationRequested)
        {
            ThrowCloneCancellation(cancellationToken);
        }
    }

    private static void ThrowCloneCancellation(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        throw new TimeoutException("Repository clone exceeded the configured timeout");
    }

    private async Task<UsernamePasswordCredentials?> ResolveCredentialsAsync(GitOptions options, CancellationToken cancellationToken)
    {
        var token = accessTokenAccessor.Token;
        if (string.IsNullOrWhiteSpace(token))
            token = options.ServiceToken;
        if (string.IsNullOrWhiteSpace(token))
            token = sourceCraftOptions.Value.InternalToken;
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var username = options.GitUsername;
        if (string.IsNullOrWhiteSpace(username))
        {
            try
            {
                username = (await authentication.GetCurrentUserAsync(token, cancellationToken)).Login;
            }
            catch (Exception)
            {
                username = "oauth2";
            }
        }

        return new UsernamePasswordCredentials { Username = username, Password = token };
    }

    private static string ResolveCloneUrl(SourceCraftRepository repository)
    {
        if (!string.IsNullOrWhiteSpace(repository.CloneUrl))
            return StripCredentials(repository.CloneUrl!);

        var url = repository.Url.TrimEnd('/');
        return url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url : url + ".git";
    }

    private static string StripCredentials(string cloneUrl)
    {
        if (!Uri.TryCreate(cloneUrl, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
            return cloneUrl;

        return new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri.ToString();
    }

    private static long GetDirectorySize(string directory)
    {
        var total = 0L;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (IOException)
            {
            }
        }

        return total;
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
