using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class GitWorkingCopyCache : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _repositoryLocks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GitCacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly GitCacheOptions _cacheOptions;
    private readonly GitOptions _gitOptions;
    private readonly ILogger<GitWorkingCopyCache> _logger;
    private readonly object _gate = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public GitWorkingCopyCache(
        IOptions<GitCacheOptions> cacheOptions,
        IOptions<GitOptions> gitOptions,
        ILogger<GitWorkingCopyCache> logger)
    {
        _cacheOptions = cacheOptions.Value;
        _gitOptions = gitOptions.Value;
        _logger = logger;
        _cleanupTimer = new Timer(_ => CleanupExpired(), null, CleanupInterval, CleanupInterval);
    }

    public async Task<SourceCraftResult<GitWorkingCopy>> GetOrCreateAsync(
        string repositoryId,
        Func<string, CancellationToken, Task<SourceCraftResult<GitWorkingCopy>>> cloneFactory,
        CancellationToken cancellationToken)
    {
        var repositoryLock = _repositoryLocks.GetOrAdd(repositoryId, _ => new SemaphoreSlim(1, 1));
        await repositoryLock.WaitAsync(cancellationToken);
        try
        {
            var cachedPath = TryGetFreshPath(repositoryId);
            if (cachedPath is not null)
                return Available(cachedPath);

            RemoveEntry(repositoryId);

            var targetDirectory = Path.Join(ResolveWorkDirectory(), $"srhc-{Guid.NewGuid():n}.git");
            SourceCraftResult<GitWorkingCopy> result;
            try
            {
                result = await cloneFactory(targetDirectory, cancellationToken);
            }
            catch
            {
                TryDelete(targetDirectory);
                throw;
            }

            if (result.Status != DataStatus.Available || result.Data is null)
            {
                TryDelete(targetDirectory);
                return result;
            }

            StoreEntry(repositoryId, result.Data.Path);
            EvictOverLimit();
            return Available(result.Data.Path);
        }
        finally
        {
            repositoryLock.Release();
        }
    }

    private string? TryGetFreshPath(string repositoryId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(repositoryId, out var entry))
                return null;

            if (DateTimeOffset.UtcNow - entry.LastAccessUtc >= Ttl)
                return null;

            if (!Directory.Exists(entry.Path))
            {
                _entries.Remove(repositoryId);
                return null;
            }

            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            return entry.Path;
        }
    }

    private void StoreEntry(string repositoryId, string path)
    {
        lock (_gate)
            _entries[repositoryId] = new GitCacheEntry { Path = path, LastAccessUtc = DateTimeOffset.UtcNow };
    }

    private void RemoveEntry(string repositoryId)
    {
        string? expiredPath = null;
        lock (_gate)
        {
            if (_entries.Remove(repositoryId, out var entry))
                expiredPath = entry.Path;
        }

        if (expiredPath is not null)
            TryDelete(expiredPath);
    }

    private void EvictOverLimit()
    {
        List<string> directoriesToDelete = [];
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _entries.Where(pair => now - pair.Value.LastAccessUtc >= Ttl).ToArray())
            {
                _entries.Remove(pair.Key);
                directoriesToDelete.Add(pair.Value.Path);
            }

            foreach (var pair in _entries.OrderBy(pair => pair.Value.LastAccessUtc).ToArray())
            {
                if (_entries.Count <= MaxCachedRepositories)
                    break;

                if (IsRepositoryBusy(pair.Key))
                    continue;

                _entries.Remove(pair.Key);
                directoriesToDelete.Add(pair.Value.Path);
            }
        }

        foreach (var directory in directoriesToDelete)
            TryDelete(directory);
    }

    private void CleanupExpired()
    {
        if (_disposed)
            return;

        List<string> directoriesToDelete = [];
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _entries.Where(pair => now - pair.Value.LastAccessUtc >= Ttl).ToArray())
            {
                if (IsRepositoryBusy(pair.Key))
                    continue;

                _entries.Remove(pair.Key);
                directoriesToDelete.Add(pair.Value.Path);
            }
        }

        foreach (var directory in directoriesToDelete)
            TryDelete(directory);
    }

    private bool IsRepositoryBusy(string repositoryId)
        => _repositoryLocks.TryGetValue(repositoryId, out var repositoryLock) && repositoryLock.CurrentCount == 0;

    private string ResolveWorkDirectory()
    {
        var workDirectory = _gitOptions.WorkDir;
        if (string.IsNullOrWhiteSpace(workDirectory))
            return Path.GetTempPath();

        Directory.CreateDirectory(workDirectory);
        return workDirectory;
    }

    private TimeSpan Ttl => TimeSpan.FromMinutes(Math.Max(1, _cacheOptions.TtlMinutes));

    private TimeSpan CleanupInterval => Ttl;

    private int MaxCachedRepositories => Math.Max(1, _cacheOptions.MaxCachedRepositories);

    private static SourceCraftResult<GitWorkingCopy> Available(string path)
    {
        var workingCopy = new GitWorkingCopy(path, deleteOnDispose: false);
        return new SourceCraftResult<GitWorkingCopy>(DataStatus.Available, workingCopy, null);
    }

    private void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException exception)
        {
            _logger.LogDebug(exception, "Failed to delete cached working copy {Directory}", directory);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogDebug(exception, "Failed to delete cached working copy {Directory}", directory);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer.Dispose();

        List<string> directoriesToDelete = [];
        lock (_gate)
        {
            foreach (var entry in _entries.Values)
                directoriesToDelete.Add(entry.Path);

            _entries.Clear();
        }

        foreach (var directory in directoriesToDelete)
            TryDelete(directory);

        foreach (var repositoryLock in _repositoryLocks.Values)
            repositoryLock.Dispose();

        _repositoryLocks.Clear();
    }
}
