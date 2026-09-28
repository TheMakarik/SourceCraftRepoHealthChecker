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
    IOptions<GitOptions> gitOptions,
    IOptions<SourceCraftServiceOptions> sourceCraftOptions,
    ILogger<GitWorkingCopyProvider> logger)
{
    public async Task<SourceCraftResult<GitWorkingCopy>> AcquireAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var repositoryResult = await catalog.GetRepositoryAsync(repositoryId, cancellationToken);
        if (repositoryResult.Status != DataStatus.Available || repositoryResult.Data is null)
            return new SourceCraftResult<GitWorkingCopy>(repositoryResult.Status, null, repositoryResult.Reason);

        var options = gitOptions.Value;
        var cloneDirectory = Path.Join(ResolveWorkDirectory(options), $"srhc-{Guid.NewGuid():n}.git");

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

        return new SourceCraftResult<GitWorkingCopy>(DataStatus.Available, new GitWorkingCopy(cloneDirectory), null);
    }

    private async Task CloneAsync(SourceCraftRepository repository, string cloneDirectory, UsernamePasswordCredentials? credentials, GitOptions options, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.CloneTimeoutSeconds > 0)
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.CloneTimeoutSeconds));

        var cloneToken = timeoutSource.Token;
        var fetchOptions = new FetchOptions
        {
            OnTransferProgress = _ =>
            {
                cloneToken.ThrowIfCancellationRequested();
                return true;
            }
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
        await Task.Run(() => Repository.Clone(cloneUrl, cloneDirectory, cloneOptions), cloneToken);
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
        var url = repository.Url.TrimEnd('/');
        return url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url : url + ".git";
    }

    private static string ResolveWorkDirectory(GitOptions options)
    {
        var workDirectory = options.WorkDir;
        if (string.IsNullOrWhiteSpace(workDirectory))
            return Path.GetTempPath();

        Directory.CreateDirectory(workDirectory);
        return workDirectory;
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
