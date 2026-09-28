using LibGit2Sharp;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftStructureAdapter(
    GitWorkingCopyProvider workingCopyProvider) : ISourceCraftStructureSource
{
    public async Task<SourceCraftResult<RepositoryStructureReport>> GetStructureAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<RepositoryStructureReport>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var report = await Task.Run(() => BuildStructure(workingCopy.Path), cancellationToken);
            return new SourceCraftResult<RepositoryStructureReport>(DataStatus.Available, report, null);
        }
    }

    private static RepositoryStructureReport BuildStructure(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        var files = GitTreeReader.ListTrackedPaths(repository);

        var rootFiles = 0;
        var maxDepth = 0;
        var directories = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var slashIndex = file.LastIndexOf('/');
            if (slashIndex < 0)
            {
                rootFiles++;
                continue;
            }

            var directory = file[..slashIndex];
            directories[directory] = directories.GetValueOrDefault(directory) + 1;

            var depth = 0;
            foreach (var character in file)
            {
                if (character == '/')
                    depth++;
            }

            if (depth > maxDepth)
                maxDepth = depth;
        }

        var largestDirectory = string.Empty;
        var largestDirectoryFiles = 0;
        foreach (var file in files)
        {
            var slashIndex = file.LastIndexOf('/');
            if (slashIndex < 0)
                continue;

            var directory = file[..slashIndex];
            var count = directories[directory];
            if (count > largestDirectoryFiles)
            {
                largestDirectoryFiles = count;
                largestDirectory = directory;
            }
        }

        return new RepositoryStructureReport(
            files.Count,
            directories.Count,
            maxDepth,
            rootFiles,
            largestDirectory,
            largestDirectoryFiles);
    }
}
