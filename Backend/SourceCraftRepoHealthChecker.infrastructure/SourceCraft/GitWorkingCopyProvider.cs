using System.Security.Cryptography;
using System.Text;
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

    private const string PrivateRepositoryRequiresUserToken = "Private repository is available only with the owner's SourceCraft token";

    // Every acquisition, including a cache hit, first asks SourceCraft for the repository with the caller's
    // token, so access is re-checked per request. A private repository is never cloned with the service
    // token, and its cached working copy is keyed by the caller's token, so it cannot leak to other users.
    public async Task<SourceCraftResult<GitWorkingCopy>> AcquireAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var repositoryResult = await catalog.GetRepositoryAsync(repositoryId, cancellationToken);
        if (repositoryResult.Status != DataStatus.Available || repositoryResult.Data is null)
            return new SourceCraftResult<GitWorkingCopy>(repositoryResult.Status, null, repositoryResult.Reason);

        var repository = repositoryResult.Data;
        var userToken = accessTokenAccessor.Token;
        if (repository.IsPrivate && string.IsNullOrWhiteSpace(userToken))
        {
            logger.LogWarning("Refused to clone private repository {RepositoryId} without a user token", repositoryId);
            return new SourceCraftResult<GitWorkingCopy>(DataStatus.Unavailable, null, PrivateRepositoryRequiresUserToken);
        }

        var cacheKey = repository.IsPrivate ? $"{repositoryId}:{TokenFingerprint(userToken!)}" : repositoryId;
        return await workingCopyCache.GetOrCreateAsync(
            cacheKey,
            (cloneDirectory, cloneToken) => CreateAsync(repository, cloneDirectory, cloneToken),
            cancellationToken);
    }

    private static string TokenFingerprint(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..16];

    private async Task<SourceCraftResult<GitWorkingCopy>> CreateAsync(SourceCraftRepository repository, string cloneDirectory, CancellationToken cancellationToken)
    {
        var repositoryId = repository.Id;
        var options = gitOptions.Value;
        var credentials = await ResolveCredentialsAsync(options, repository.IsPrivate, cancellationToken);
        var maximumAttempts = Math.Max(1, options.CloneMaxAttempts);

        if (!HasCapacityForClone(options, cloneDirectory))
        {
            TryDelete(cloneDirectory);
            logger.LogWarning("Not enough local storage for repository {RepositoryId} within the {Megabytes} MB analysis size limit", repositoryId, options.MaxRepositoryMegabytes);
            return new SourceCraftResult<GitWorkingCopy>(DataStatus.Unavailable, null, "Not enough local storage for repository analysis");
        }

        // The clone timeout is a single budget for the whole operation, including all retries and the
        // backoff delays between them, so a dead remote cannot keep the request alive indefinitely.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.CloneTimeoutSeconds > 0)
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.CloneTimeoutSeconds));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var sizeExceeded = await CloneAsync(repository, cloneDirectory, credentials, options, timeoutSource.Token, cancellationToken);
                if (sizeExceeded)
                {
                    TryDelete(cloneDirectory);
                    logger.LogWarning("Repository {RepositoryId} exceeded the analysis size limit during clone", repositoryId);
                    return SizeLimitExceeded();
                }

                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryDelete(cloneDirectory);
                throw;
            }
            catch (TimeoutException exception)
            {
                TryDelete(cloneDirectory);
                logger.LogWarning(exception, "Clone of repository {RepositoryId} exceeded the configured timeout", repositoryId);
                return CloneTimedOut();
            }
            catch (OperationCanceledException)
            {
                TryDelete(cloneDirectory);
                logger.LogWarning("Clone of repository {RepositoryId} exceeded the configured timeout", repositoryId);
                return CloneTimedOut();
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
                try
                {
                    await DelayBeforeRetryAsync(options, attempt, timeoutSource.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    TryDelete(cloneDirectory);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    TryDelete(cloneDirectory);
                    logger.LogWarning("Clone of repository {RepositoryId} exceeded the configured timeout while waiting to retry", repositoryId);
                    return CloneTimedOut();
                }
            }
        }

        if (options.MaxRepositoryMegabytes > 0 && GetDirectorySize(cloneDirectory) > options.MaxRepositoryMegabytes * 1024 * 1024)
        {
            TryDelete(cloneDirectory);
            return SizeLimitExceeded();
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
        CancellationToken cloneToken,
        CancellationToken cancellationToken)
    {
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

    private static bool HasCapacityForClone(GitOptions options, string cloneDirectory)
    {
        if (options.MaxRepositoryMegabytes <= 0)
            return true;

        var maximumBytes = options.MaxRepositoryMegabytes * 1024 * 1024;
        if (Directory.Exists(cloneDirectory) && GetDirectorySize(cloneDirectory) > maximumBytes)
            return false;

        // DriveInfo only resolves existing paths, so walk up to the work directory that must exist.
        var existingDirectory = cloneDirectory;
        while (!string.IsNullOrEmpty(existingDirectory) && !Directory.Exists(existingDirectory))
            existingDirectory = Path.GetDirectoryName(existingDirectory);

        if (string.IsNullOrEmpty(existingDirectory))
            return true;

        try
        {
            var drive = new DriveInfo(existingDirectory);
            return !drive.IsReady || drive.AvailableFreeSpace <= 0 || drive.AvailableFreeSpace >= maximumBytes;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Free-space probing is best-effort: if it cannot be determined, let the clone start and
            // rely on the transfer-progress cap so a healthy repository is never rejected.
            return true;
        }
    }

    private static SourceCraftResult<GitWorkingCopy> SizeLimitExceeded() =>
        new(DataStatus.Unavailable, null, "Repository exceeds analysis size limit");

    private static SourceCraftResult<GitWorkingCopy> CloneTimedOut() =>
        new(DataStatus.Unavailable, null, "Repository clone exceeded the configured timeout");

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

    private async Task<UsernamePasswordCredentials?> ResolveCredentialsAsync(GitOptions options, bool isPrivate, CancellationToken cancellationToken)
    {
        var token = accessTokenAccessor.Token;
        // Service tokens must never open a private repository: only the caller's own token may.
        if (string.IsNullOrWhiteSpace(token) && !isPrivate)
            token = options.ServiceToken;
        if (string.IsNullOrWhiteSpace(token) && !isPrivate)
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
