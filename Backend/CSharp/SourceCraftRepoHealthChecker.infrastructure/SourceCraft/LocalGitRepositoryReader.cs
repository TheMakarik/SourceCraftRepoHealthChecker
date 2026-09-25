using System.Text;
using LibGit2Sharp;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class LocalGitRepositoryReader : IGitRepositoryReader
{
    private const string TodoMarker = "TODO";
    private const string FixmeMarker = "FIXME";

    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".go", ".md", ".txt", ".ps1", ".sh", ".yml", ".yaml", ".json",
        ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".kt", ".rb", ".rs",
        ".c", ".h", ".cpp", ".hpp", ".csproj", ".sln", ".slnx", ".props",
        ".targets", ".xml", ".html", ".css", ".scss", ".toml", ".cfg", ".ini"
    };

    private static readonly HashSet<string> TextFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dockerfile", "Makefile"
    };

    private static readonly string[] LocalRunIndicators =
    [
        "getting started", "quick start", "quickstart", "local run", "run locally",
        "how to run", "usage", "installation", "установка", "запуск", "локальный запуск"
    ];

    private static readonly string[] BuildAndTestIndicators =
    [
        "dotnet build", "dotnet test", "npm test", "npm run build", "go test",
        "pytest", "make test", "build and test", "how to build", "сборка", "тестирование"
    ];

    public Task<CommitActivity> GetCommitActivityAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var repository = new Repository(repositoryPath);
        var commits = repository.Commits.ToList();

        if (commits.Count == 0)
            return Task.FromResult(new CommitActivity(0, null, null, new Dictionary<DateOnly, int>()));

        var commitsByDay = commits
            .GroupBy(commit => DateOnly.FromDateTime(commit.Author.When.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.Count());

        var ordered = commits.OrderBy(commit => commit.Author.When).ToList();
        var firstCommitAt = ordered[0].Author.When;
        var lastCommitAt = ordered[^1].Author.When;

        return Task.FromResult(new CommitActivity(commits.Count, firstCommitAt, lastCommitAt, commitsByDay));
    }

    public Task<IReadOnlyCollection<Contributor>> GetContributorsAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var repository = new Repository(repositoryPath);
        var contributors = repository.Commits
            .GroupBy(commit => ResolveIdentity(commit.Author))
            .Select(group =>
            {
                var signature = group.First().Author;
                var login = string.IsNullOrWhiteSpace(signature.Name) ? signature.Email : signature.Name;
                return new Contributor(login, group.Count(), IsBot(signature));
            })
            .Where(contributor => !string.IsNullOrWhiteSpace(contributor.Login))
            .ToList();

        return Task.FromResult<IReadOnlyCollection<Contributor>>(contributors);
    }

    public Task<IReadOnlyCollection<ReleaseInfo>> GetReleasesAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var repository = new Repository(repositoryPath);
        var releases = repository.Tags
            .Select(CreateReleaseInfo)
            .OrderByDescending(release => release.PublishedAt)
            .ToList();

        return Task.FromResult<IReadOnlyCollection<ReleaseInfo>>(releases);
    }

    public Task<CodeHealthReport> GetCodeHealthAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var repository = new Repository(repositoryPath);
        var trackedFiles = GetTrackedFiles(repository);

        var todoCount = 0;
        var fixmeCount = 0;
        var totalCommentCount = 0;
        var oldestCommentDate = (DateTimeOffset?)null;

        foreach (var relativePath in trackedFiles)
        {
            if (!IsTextFile(relativePath))
                continue;

            var fullPath = Path.Join(repositoryPath, relativePath);
            if (!File.Exists(fullPath))
                continue;

            var lines = File.ReadAllLines(fullPath);
            var occurrenceLines = new List<int>();

            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var line = lines[lineNumber];

                if (IsCommentLine(line))
                    totalCommentCount++;

                if (line.Contains(TodoMarker, StringComparison.Ordinal))
                {
                    todoCount++;
                    occurrenceLines.Add(lineNumber + 1);
                }

                if (line.Contains(FixmeMarker, StringComparison.Ordinal))
                {
                    fixmeCount++;
                    occurrenceLines.Add(lineNumber + 1);
                }
            }

            if (occurrenceLines.Count == 0)
                continue;

            var earliestDate = FindEarliestOccurrenceDate(repository, relativePath, occurrenceLines);
            if (earliestDate is not null && (oldestCommentDate is null || earliestDate < oldestCommentDate))
                oldestCommentDate = earliestDate;
        }

        var oldestCommentAge = oldestCommentDate is null
            ? (TimeSpan?)null
            : DateTimeOffset.UtcNow - oldestCommentDate.Value;

        return Task.FromResult(new CodeHealthReport(todoCount, fixmeCount, totalCommentCount, oldestCommentAge));
    }

    public Task<DocumentationReport> GetDocumentationAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var repository = new Repository(repositoryPath);
        var trackedFiles = GetTrackedFiles(repository);

        var documentationText = ReadDocumentationText(repositoryPath, trackedFiles);

        var report = new DocumentationReport(
            HasReadme: trackedFiles.Any(IsReadme),
            HasLicense: trackedFiles.Any(IsLicense),
            HasContributing: trackedFiles.Any(IsContributing),
            HasCodeOwners: trackedFiles.Any(IsCodeOwners),
            HasLocalRunInstructions: ContainsAny(documentationText, LocalRunIndicators),
            HasBuildAndTestInstructions: ContainsAny(documentationText, BuildAndTestIndicators));

        return Task.FromResult(report);
    }

    private static ReleaseInfo CreateReleaseInfo(Tag tag)
    {
        var target = tag.PeeledTarget as Commit;
        var publishedAt = tag.Annotation?.Tagger?.When
            ?? target?.Committer.When
            ?? DateTimeOffset.UtcNow;
        var name = string.IsNullOrWhiteSpace(tag.Annotation?.Message)
            ? tag.FriendlyName
            : tag.Annotation.Message.Trim();

        return new ReleaseInfo(name, tag.FriendlyName, publishedAt);
    }

    private static string ResolveIdentity(Signature signature) =>
        string.IsNullOrWhiteSpace(signature.Email) ? signature.Name : signature.Email.ToLowerInvariant();

    private static bool IsBot(Signature signature)
    {
        var name = signature.Name ?? string.Empty;
        var email = signature.Email ?? string.Empty;

        return name.Contains("bot", StringComparison.OrdinalIgnoreCase)
            || email.Contains("bot", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> GetTrackedFiles(Repository repository)
    {
        var files = new List<string>();
        var tip = repository.Head.Tip;
        if (tip is null)
            return files;

        CollectFiles(tip.Tree, files);
        return files;
    }

    private static void CollectFiles(Tree tree, ICollection<string> files)
    {
        foreach (var entry in tree)
        {
            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                if (entry.Target is Tree childTree)
                    CollectFiles(childTree, files);
                continue;
            }

            if (entry.TargetType == TreeEntryTargetType.Blob)
                files.Add(entry.Path);
        }
    }

    private static DateTimeOffset? FindEarliestOccurrenceDate(Repository repository, string relativePath, IReadOnlyCollection<int> lineNumbers)
    {
        try
        {
            var blame = repository.Blame(relativePath);
            DateTimeOffset? earliest = null;

            foreach (var lineNumber in lineNumbers)
            {
                var hunk = blame.FirstOrDefault(candidate =>
                    lineNumber >= candidate.FinalStartLineNumber
                    && lineNumber < candidate.FinalStartLineNumber + candidate.LineCount);

                if (hunk is null)
                    continue;

                var commitDate = hunk.FinalCommit.Committer.When;
                if (earliest is null || commitDate < earliest)
                    earliest = commitDate;
            }

            return earliest;
        }
        catch (LibGit2SharpException)
        {
            var oldestCommit = repository.Commits
                .Where(commit => commit[relativePath] is not null)
                .LastOrDefault();

            return oldestCommit?.Committer.When;
        }
    }

    private static bool IsTextFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        if (TextFileNames.Contains(fileName))
            return true;

        var extension = Path.GetExtension(relativePath);
        return !string.IsNullOrEmpty(extension) && TextFileExtensions.Contains(extension);
    }

    private static bool IsCommentLine(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal)
            || trimmed.StartsWith("#", StringComparison.Ordinal)
            || trimmed.StartsWith("<!--", StringComparison.Ordinal)
            || trimmed.StartsWith(";", StringComparison.Ordinal);
    }

    private static bool IsReadme(string relativePath) =>
        Path.GetFileName(relativePath).StartsWith("readme", StringComparison.OrdinalIgnoreCase);

    private static bool IsLicense(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.StartsWith("license", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("copying", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsContributing(string relativePath) =>
        Path.GetFileName(relativePath).StartsWith("contributing", StringComparison.OrdinalIgnoreCase);

    private static bool IsCodeOwners(string relativePath) =>
        Path.GetFileName(relativePath).Equals("CODEOWNERS", StringComparison.OrdinalIgnoreCase);

    private static string ReadDocumentationText(string repositoryPath, IReadOnlyCollection<string> trackedFiles)
    {
        var builder = new StringBuilder();

        foreach (var relativePath in trackedFiles)
        {
            if (!IsReadme(relativePath) && !IsContributing(relativePath))
                continue;

            var fullPath = Path.Join(repositoryPath, relativePath);
            if (File.Exists(fullPath))
                builder.AppendLine(File.ReadAllText(fullPath));
        }

        return builder.ToString();
    }

    private static bool ContainsAny(string text, IReadOnlyCollection<string> indicators)
    {
        foreach (var indicator in indicators)
        {
            if (text.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
