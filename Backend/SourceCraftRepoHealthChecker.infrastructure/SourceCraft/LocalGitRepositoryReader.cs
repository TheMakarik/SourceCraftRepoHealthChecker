using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class LocalGitRepositoryReader(
    TimeProvider timeProvider,
    IOptions<RepositoryBrowsingOptions>? browsingOptions = null) : IGitRepositoryReader
{
    private const string TodoMarker = "TODO";
    private const string FixmeMarker = "FIXME";

    private readonly long _maxFileBytes = browsingOptions?.Value.MaxFileBytes ?? 0;

    private static readonly Regex RunInstructionsPattern = new(
        @"\b(run|getting started|quick start|usage)\b|запуск",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuildAndTestPattern = new(
        @"\b(build|test|make)\b|сборка|тест",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> KnownStructureDirectories = new(StringComparer.Ordinal)
    {
        "src", "source", "tests", "test", "docs", "doc", "lib", "libs", "packages", "cmd",
        "internal", "app", "api", "backend", "frontend", "scripts", "examples", "samples"
    };

    public Task<CommitActivity> GetCommitActivityAsync(string repositoryPath, CancellationToken cancellationToken) =>
        Task.Run(() => ReadCommitActivity(repositoryPath, cancellationToken), cancellationToken);

    public Task<IReadOnlyCollection<Contributor>> GetContributorsAsync(string repositoryPath, CancellationToken cancellationToken) =>
        Task.Run(() => ReadContributors(repositoryPath, cancellationToken), cancellationToken);

    public Task<IReadOnlyCollection<ReleaseInfo>> GetReleasesAsync(string repositoryPath, CancellationToken cancellationToken) =>
        Task.Run(() => ReadReleases(repositoryPath, cancellationToken), cancellationToken);

    public Task<CodeHealthReport> GetCodeHealthAsync(string repositoryPath, CancellationToken cancellationToken) =>
        Task.Run(() => ReadCodeHealth(repositoryPath, cancellationToken), cancellationToken);

    public Task<DocumentationReport> GetDocumentationAsync(string repositoryPath, CancellationToken cancellationToken) =>
        Task.Run(() => ReadDocumentation(repositoryPath, cancellationToken), cancellationToken);

    private static CommitActivity ReadCommitActivity(string repositoryPath, CancellationToken cancellationToken)
    {
        using var repository = new Repository(repositoryPath);

        var commitsByDay = new Dictionary<DateOnly, int>();
        var totalCount = 0;
        DateTimeOffset? firstCommitAt = null;
        DateTimeOffset? lastCommitAt = null;

        foreach (var commit in repository.Commits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var authoredAt = commit.Author.When.ToUniversalTime();
            totalCount++;

            var day = DateOnly.FromDateTime(authoredAt.UtcDateTime);
            commitsByDay[day] = commitsByDay.GetValueOrDefault(day) + 1;

            if (firstCommitAt is null || authoredAt < firstCommitAt)
                firstCommitAt = authoredAt;
            if (lastCommitAt is null || authoredAt > lastCommitAt)
                lastCommitAt = authoredAt;
        }

        return new CommitActivity(totalCount, firstCommitAt, lastCommitAt, commitsByDay);
    }

    private static IReadOnlyCollection<Contributor> ReadContributors(string repositoryPath, CancellationToken cancellationToken)
    {
        using var repository = new Repository(repositoryPath);

        var byKey = new Dictionary<string, GitContributorAccumulator>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var commit in repository.Commits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = commit.Author.Name ?? string.Empty;
            var email = commit.Author.Email ?? string.Empty;
            var key = email.Length == 0 ? "name:" + name : email.ToLowerInvariant();

            if (!byKey.TryGetValue(key, out var accumulator))
            {
                accumulator = new GitContributorAccumulator(name, IsBot(name, email));
                byKey[key] = accumulator;
                order.Add(key);
            }

            var authoredAt = commit.Author.When.ToUniversalTime();
            if (accumulator.CommitsCount == 0 || authoredAt < accumulator.FirstCommitAt)
                accumulator.FirstCommitAt = authoredAt;
            if (accumulator.CommitsCount == 0 || authoredAt > accumulator.LastCommitAt)
                accumulator.LastCommitAt = authoredAt;
            accumulator.CommitsCount++;
        }

        var contributors = new List<Contributor>(order.Count);
        foreach (var key in order)
        {
            var accumulator = byKey[key];
            contributors.Add(new Contributor(
                accumulator.Name,
                accumulator.CommitsCount,
                accumulator.IsBot,
                accumulator.FirstCommitAt,
                accumulator.LastCommitAt));
        }

        return contributors;
    }

    private static IReadOnlyCollection<ReleaseInfo> ReadReleases(string repositoryPath, CancellationToken cancellationToken)
    {
        using var repository = new Repository(repositoryPath);

        var releases = new List<ReleaseInfo>();
        foreach (var tag in repository.Tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = tag.PeeledTarget as Commit ?? tag.Target as Commit;
            var publishedAt = tag.Annotation?.Tagger?.When
                ?? target?.Committer.When
                ?? DateTimeOffset.UtcNow;
            var name = string.IsNullOrWhiteSpace(tag.Annotation?.Message)
                ? tag.FriendlyName
                : tag.Annotation.Message.Trim();

            releases.Add(new ReleaseInfo(name, tag.FriendlyName, publishedAt));
        }

        releases.Sort((left, right) => right.PublishedAt.CompareTo(left.PublishedAt));
        return releases;
    }

    private CodeHealthReport ReadCodeHealth(string repositoryPath, CancellationToken cancellationToken)
    {
        using var repository = new Repository(repositoryPath);

        var head = repository.Head.Tip;
        if (head is null)
            return new CodeHealthReport(0, 0, 0, null);

        var todoCount = 0;
        var fixmeCount = 0;

        foreach (var (_, blob) in GitTreeReader.EnumerateBlobs(repository))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsWithinSizeLimit(blob) || blob.IsBinary)
                continue;

            var text = blob.GetContentText();
            foreach (var line in text.Split('\n'))
            {
                if (ContainsMarkerWord(line, TodoMarker))
                    todoCount++;
                if (ContainsMarkerWord(line, FixmeMarker))
                    fixmeCount++;
            }
        }

        var oldestAt = FindOldestMarkerCommit(repository, cancellationToken);
        var oldestAge = oldestAt is null
            ? (TimeSpan?)null
            : ClampNonNegative(timeProvider.GetUtcNow() - oldestAt.Value);

        return new CodeHealthReport(todoCount, fixmeCount, todoCount + fixmeCount, oldestAge);
    }

    private DocumentationReport ReadDocumentation(string repositoryPath, CancellationToken cancellationToken)
    {
        using var repository = new Repository(repositoryPath);

        var head = repository.Head.Tip;
        if (head is null)
            return new DocumentationReport(false, false, false, false, false, false);

        var hasReadme = false;
        var hasLicense = false;
        var hasContributing = false;
        var hasCodeOwners = false;
        var instructions = new StringBuilder();
        var licenseId = (string?)null;
        var topLevelDirectories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, blob) in GitTreeReader.EnumerateBlobs(repository))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var separatorIndex = path.IndexOf('/');
            if (separatorIndex > 0)
                topLevelDirectories.Add(path[..separatorIndex].ToLowerInvariant());

            var name = Path.GetFileName(path).ToLowerInvariant();
            if (name.StartsWith("readme", StringComparison.Ordinal))
            {
                hasReadme = true;
                AppendContent(blob, instructions);
            }
            else if (name.StartsWith("license", StringComparison.Ordinal) || name.StartsWith("licence", StringComparison.Ordinal) || name == "copying")
            {
                hasLicense = true;
                licenseId ??= DetectLicenseId(name, blob);
            }
            else if (name.StartsWith("contributing", StringComparison.Ordinal))
            {
                hasContributing = true;
                AppendContent(blob, instructions);
            }
            else if (name == "codeowners")
            {
                hasCodeOwners = true;
            }
        }

        var documentationText = instructions.ToString();
        return new DocumentationReport(
            hasReadme,
            hasLicense,
            hasContributing,
            hasCodeOwners,
            RunInstructionsPattern.IsMatch(documentationText),
            BuildAndTestPattern.IsMatch(documentationText),
            licenseId,
            topLevelDirectories.Overlaps(KnownStructureDirectories) || topLevelDirectories.Count >= 3);
    }

    private string? DetectLicenseId(string fileName, Blob blob)
    {
        var fromName = DetectLicenseFromFileName(fileName);
        if (fromName is not null)
            return fromName;

        if (!IsWithinSizeLimit(blob) || blob.IsBinary)
            return null;

        return DetectLicenseFromContent(blob.GetContentText());
    }

    private static string? DetectLicenseFromFileName(string fileName)
    {
        var normalized = fileName.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

        if (normalized.Contains("apache", StringComparison.Ordinal))
            return "Apache-2.0";
        if (normalized.Contains("mit", StringComparison.Ordinal))
            return "MIT";
        if (normalized.Contains("agpl", StringComparison.Ordinal))
            return "AGPL-3.0";
        if (normalized.Contains("lgpl", StringComparison.Ordinal))
            return "LGPL-3.0";
        if (normalized.Contains("gpl3", StringComparison.Ordinal) || normalized.Contains("gplv3", StringComparison.Ordinal))
            return "GPL-3.0";
        if (normalized.Contains("gpl2", StringComparison.Ordinal) || normalized.Contains("gplv2", StringComparison.Ordinal))
            return "GPL-2.0";
        if (normalized.Contains("mpl", StringComparison.Ordinal))
            return "MPL-2.0";
        if (normalized.Contains("bsd3", StringComparison.Ordinal))
            return "BSD-3-Clause";
        if (normalized.Contains("bsd2", StringComparison.Ordinal))
            return "BSD-2-Clause";
        if (normalized.Contains("unlicense", StringComparison.Ordinal))
            return "Unlicense";
        if (normalized.Contains("isc", StringComparison.Ordinal))
            return "ISC";

        return null;
    }

    private static string? DetectLicenseFromContent(string content)
    {
        var normalized = content.ToLowerInvariant();

        if (normalized.Contains("gnu affero general public license", StringComparison.Ordinal))
            return "AGPL-3.0";
        if (normalized.Contains("gnu lesser general public license", StringComparison.Ordinal))
            return "LGPL-3.0";
        if (normalized.Contains("gnu general public license", StringComparison.Ordinal))
            return normalized.Contains("version 3", StringComparison.Ordinal) ? "GPL-3.0" : "GPL-2.0";
        if (normalized.Contains("apache license", StringComparison.Ordinal) && normalized.Contains("version 2.0", StringComparison.Ordinal))
            return "Apache-2.0";
        if (normalized.Contains("mozilla public license", StringComparison.Ordinal))
            return "MPL-2.0";
        if (normalized.Contains("permission is hereby granted, free of charge", StringComparison.Ordinal))
            return "MIT";
        if (normalized.Contains("permission to use, copy, modify, and/or distribute this software", StringComparison.Ordinal))
            return "ISC";
        if (normalized.Contains("this is free and unencumbered software released into the public domain", StringComparison.Ordinal))
            return "Unlicense";
        if (normalized.Contains("redistribution and use in source and binary forms", StringComparison.Ordinal))
            return normalized.Contains("neither the name", StringComparison.Ordinal) ? "BSD-3-Clause" : "BSD-2-Clause";

        return null;
    }

    private static DateTimeOffset? FindOldestMarkerCommit(Repository repository, CancellationToken cancellationToken)
    {
        DateTimeOffset? oldest = null;

        foreach (var commit in repository.Commits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parents = commit.Parents.ToList();
            if (parents.Count > 1)
                continue;

            Tree? parentTree = parents.Count == 1 ? parents[0].Tree : null;
            var patch = repository.Diff.Compare<Patch>(parentTree, commit.Tree);
            if (!PatchTouchesMarker(patch))
                continue;

            var authoredAt = commit.Author.When.ToUniversalTime();
            if (oldest is null || authoredAt < oldest)
                oldest = authoredAt;
        }

        return oldest;
    }

    private static bool PatchTouchesMarker(Patch patch)
    {
        foreach (var change in patch)
        {
            foreach (var line in change.AddedLines)
            {
                if (ContainsMarkerWord(line.Content, TodoMarker) || ContainsMarkerWord(line.Content, FixmeMarker))
                    return true;
            }

            foreach (var line in change.DeletedLines)
            {
                if (ContainsMarkerWord(line.Content, TodoMarker) || ContainsMarkerWord(line.Content, FixmeMarker))
                    return true;
            }
        }

        return false;
    }

    private static bool ContainsMarkerWord(string line, string marker)
    {
        var index = line.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var startsAtBoundary = index == 0 || !IsWordCharacter(line[index - 1]);
            var endIndex = index + marker.Length;
            var endsAtBoundary = endIndex >= line.Length || !IsWordCharacter(line[endIndex]);
            if (startsAtBoundary && endsAtBoundary)
                return true;

            index = line.IndexOf(marker, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsWordCharacter(char value) =>
        value is >= 'a' and <= 'z'
        or >= 'A' and <= 'Z'
        or >= '0' and <= '9'
        or '_';

    private static bool IsBot(string name, string email)
    {
        var normalizedName = name.ToLowerInvariant();
        var normalizedEmail = email.ToLowerInvariant();

        return normalizedName.EndsWith("[bot]", StringComparison.Ordinal)
            || normalizedName.EndsWith("-bot", StringComparison.Ordinal)
            || normalizedName.EndsWith(" bot", StringComparison.Ordinal)
            || normalizedEmail.Contains("[bot]", StringComparison.Ordinal)
            || normalizedEmail.StartsWith("bot@", StringComparison.Ordinal)
            || normalizedName.Contains("dependabot", StringComparison.Ordinal)
            || normalizedName.Contains("renovate", StringComparison.Ordinal);
    }

    private void AppendContent(Blob blob, StringBuilder builder)
    {
        if (!IsWithinSizeLimit(blob) || blob.IsBinary)
            return;

        builder.Append(blob.GetContentText()).Append('\n');
    }

    private bool IsWithinSizeLimit(Blob blob) => _maxFileBytes <= 0 || blob.Size <= _maxFileBytes;

    private static TimeSpan ClampNonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
