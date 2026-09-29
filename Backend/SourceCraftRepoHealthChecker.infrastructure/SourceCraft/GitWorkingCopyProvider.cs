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
    private const string OriginRemoteName = "origin";
    private const string HeadReferenceName = "HEAD";

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
        var credentials = await ResolveCredentialsAsync(options, cancellationToken);
        var maximumAttempts = Math.Max(1, options.CloneMaxAttempts);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var sizeExceeded = await CloneAsync(repositoryResult.Data, cloneDirectory, credentials, options, cancellationToken);
                if (sizeExceeded)
                {
                    TryDelete(cloneDirectory);
                    logger.LogWarning("Repository {RepositoryId} exceeded the analysis size limit during clone", repositoryId);
                    return new SourceCraftResult<GitWorkingCopy>(DataStatus.Unavailable, null, "Repository exceeds analysis size limit");
                }

                break;
            }
            catch (OperationCanceledException)
            {
                TryDelete(cloneDirectory);
                throw;
            }
            catch (Exception exception)
            {
                TryDelete(cloneDirectory);
                if (attempt >= maximumAttempts)
                {
                    logger.LogWarning(exception, "Failed to clone repository {RepositoryId} after {AttemptCount} attempts", repositoryId, attempt);
                    return new SourceCraftResult<GitWorkingCopy>(DataStatus.Unavailable, null, "Failed to obtain a working copy of the repository");
                }

                logger.LogWarning(exception, "Clone attempt {Attempt} for repository {RepositoryId} failed, retrying", attempt, repositoryId);
                await DelayBeforeRetryAsync(options, attempt, cancellationToken);
            }
        }

        if (options.MaxRepositoryMegabytes > 0 && GetDirectorySize(cloneDirectory) > options.MaxRepositoryMegabytes * 1024 * 1024)
        {
            TryDelete(cloneDirectory);
            return new SourceCraftResult<GitWorkingCopy>(DataStatus.Unavailable, null, "Repository exceeds analysis size limit");
        }

        logger.LogInformation("Cloned repository {RepositoryId} into a working copy", repositoryId);
        return new SourceCraftResult<GitWorkingCopy>(DataStatus.Available, new GitWorkingCopy(cloneDirectory, deleteOnDispose: false), null);
    }

    // LibGit2Sharp cannot request a blobless/treeless partial clone, so the working copy is fetched
    // as a single branch (the repository default branch) and optionally shallow, which keeps the
    // transfer small for large repositories while the size cap is enforced while bytes arrive.
    private async Task<bool> CloneAsync(
        SourceCraftRepository repository,
        string cloneDirectory,
        UsernamePasswordCredentials? credentials,
        GitOptions options,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.CloneTimeoutSeconds > 0)
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.CloneTimeoutSeconds));

        var cloneToken = timeoutSource.Token;
        var sizeExceeded = false;
        var maximumBytes = options.MaxRepositoryMegabytes > 0
            ? options.MaxRepositoryMegabytes * 1024 * 1024
            : 0;

        var fetchOptions = new FetchOptions
        {
            Depth = options.ShallowCloneDepth,
            // libgit2 invokes this callback from native code: throwing here would crash the process,
            // so cancellation and the size cap are both signalled by returning false.
            OnTransferProgress = progress =>
            {
                if (cloneToken.IsCancellationRequested)
                    return false;

                if (maximumBytes > 0 && progress.ReceivedBytes > maximumBytes)
                {
                    sizeExceeded = true;
                    return false;
                }

                return true;
            }
        };
        if (credentials is not null)
            fetchOptions.CredentialsProvider = (_, _, _) => credentials;

        var cloneUrl = ResolveCloneUrl(repository);
        try
        {
            await Task.Run(() => Clone(repository.DefaultBranch, cloneUrl, cloneDirectory, fetchOptions), cloneToken);
            if (cloneToken.IsCancellationRequested)
                ThrowCloneCancellation(cancellationToken);
        }
        catch (UserCancelledException) when (sizeExceeded)
        {
            return true;
        }
        catch (UserCancelledException) when (cloneToken.IsCancellationRequested)
        {
            ThrowCloneCancellation(cancellationToken);
        }

        return false;
    }

    private static void Clone(string defaultBranch, string cloneUrl, string cloneDirectory, FetchOptions fetchOptions)
    {
        var branchName = NormalizeBranchName(defaultBranch);
        if (branchName.Length == 0)
        {
            var cloneOptions = new CloneOptions(fetchOptions)
            {
                IsBare = true,
                Checkout = false
            };
            Repository.Clone(cloneUrl, cloneDirectory, cloneOptions);
            return;
        }

        Repository.Init(cloneDirectory, isBare: true);
        using var repository = new Repository(cloneDirectory);
        repository.Network.Remotes.Add(OriginRemoteName, cloneUrl);
        var referenceSpecification = $"+refs/heads/{branchName}:refs/heads/{branchName}";
        Commands.Fetch(repository, OriginRemoteName, [referenceSpecification], fetchOptions, null);
        repository.Refs.UpdateTarget(HeadReferenceName, $"refs/heads/{branchName}");
    }

    private static string NormalizeBranchName(string branchName)
    {
        var trimmed = branchName.Trim();
        if (trimmed.StartsWith("refs/heads/", StringComparison.Ordinal))
            return trimmed["refs/heads/".Length..];

        if (trimmed.StartsWith("origin/", StringComparison.Ordinal))
            return trimmed["origin/".Length..];

        return trimmed;
    }

    private static void ThrowCloneCancellation(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        throw new TimeoutException("Repository clone exceeded the configured timeout");
    }

    private static async Task DelayBeforeRetryAsync(GitOptions options, int attempt, CancellationToken cancellationToken)
    {
        var baseDelayMilliseconds = Math.Max(1, options.CloneRetryDelayMilliseconds);
        var maximumDelayMilliseconds = Math.Max(baseDelayMilliseconds, options.CloneRetryMaxDelayMilliseconds);
        var exponentialMilliseconds = baseDelayMilliseconds * Math.Pow(2, attempt - 1);
        var cappedMilliseconds = Math.Min(exponentialMilliseconds, maximumDelayMilliseconds);
        var jitterMilliseconds = Random.Shared.NextDouble() * baseDelayMilliseconds;
        await Task.Delay(TimeSpan.FromMilliseconds(cappedMilliseconds + jitterMilliseconds), cancellationToken);
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
