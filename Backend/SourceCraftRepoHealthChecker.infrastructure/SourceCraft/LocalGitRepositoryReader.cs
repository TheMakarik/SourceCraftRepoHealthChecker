using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class LocalGitRepositoryReader(TimeProvider timeProvider) : IGitRepositoryReader
{
    private const string TodoMarker = "TODO";
    private const string FixmeMarker = "FIXME";

    private static readonly Regex RunInstructionsPattern = new(
        @"\b(run|getting started|quick start|usage)\b|запуск",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuildAndTestPattern = new(
        @"\b(build|test|make)\b|сборка|тест",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

            if (blob.IsBinary)
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

    private static DocumentationReport ReadDocumentation(string repositoryPath, CancellationToken cancellationToken)
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

        foreach (var (path, blob) in GitTreeReader.EnumerateBlobs(repository))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(path).ToLowerInvariant();
            if (name.StartsWith("readme", StringComparison.Ordinal))
            {
                hasReadme = true;
                instructions.Append(blob.GetContentText()).Append('\n');
            }
            else if (name.StartsWith("license", StringComparison.Ordinal) || name.StartsWith("licence", StringComparison.Ordinal) || name == "copying")
            {
                hasLicense = true;
            }
            else if (name.StartsWith("contributing", StringComparison.Ordinal))
            {
                hasContributing = true;
                instructions.Append(blob.GetContentText()).Append('\n');
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
            BuildAndTestPattern.IsMatch(documentationText));
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

    private static TimeSpan ClampNonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
