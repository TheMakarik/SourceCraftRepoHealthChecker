using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public static class GitWorkingCopyContentReader
{
    private const string TodoMarker = "TODO";
    private const string FixmeMarker = "FIXME";

    private static readonly string[] TestDirectoryNames =
    [
        "test", "tests", "testing", "spec", "specs", "__tests__", "__test__"
    ];

    private static readonly Regex TestFilePattern = new(
        @"(^|[._-])(tests?|specs?)\.[a-z0-9]+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> LanguageByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "cs",
        [".csx"] = "cs",
        [".fs"] = "fsharp",
        [".fsx"] = "fsharp",
        [".ts"] = "ts",
        [".tsx"] = "tsx",
        [".js"] = "js",
        [".jsx"] = "jsx",
        [".mjs"] = "js",
        [".cjs"] = "js",
        [".vue"] = "vue",
        [".svelte"] = "svelte",
        [".py"] = "python",
        [".go"] = "go",
        [".java"] = "java",
        [".kt"] = "kotlin",
        [".kts"] = "kotlin",
        [".rb"] = "ruby",
        [".rs"] = "rust",
        [".php"] = "php",
        [".c"] = "c",
        [".h"] = "c",
        [".cpp"] = "cpp",
        [".cc"] = "cpp",
        [".cxx"] = "cpp",
        [".hpp"] = "cpp",
        [".swift"] = "swift",
        [".scala"] = "scala",
        [".dart"] = "dart",
        [".lua"] = "lua",
        [".r"] = "r",
        [".sh"] = "shell",
        [".bash"] = "shell",
        [".zsh"] = "shell",
        [".ps1"] = "powershell",
        [".psm1"] = "powershell",
        [".md"] = "markdown",
        [".mdx"] = "markdown",
        [".json"] = "json",
        [".jsonc"] = "json",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".toml"] = "toml",
        [".xml"] = "xml",
        [".html"] = "html",
        [".htm"] = "html",
        [".css"] = "css",
        [".scss"] = "scss",
        [".less"] = "less",
        [".sql"] = "sql",
        [".proto"] = "protobuf",
        [".tf"] = "terraform",
        [".ini"] = "ini"
    };

    public static RepositoryFileContent? ReadFile(string repositoryPath, string path, long maxFileBytes)
    {
        using var repository = new Repository(repositoryPath);
        var head = repository.Head.Tip;
        if (head is null)
            return null;

        var normalizedPath = NormalizePath(path);
        if (normalizedPath.Length == 0)
            return null;

        var blob = FindBlob(head.Tree, normalizedPath);
        if (blob is null)
            return null;

        var language = DetectLanguage(normalizedPath);
        if (blob.IsBinary)
            return new RepositoryFileContent(normalizedPath, string.Empty, language, false, true);

        var (content, truncated) = ReadText(blob, maxFileBytes);
        return new RepositoryFileContent(normalizedPath, content, language, truncated, false);
    }

    public static IReadOnlyList<RepositoryFolderAnalysis> ReadFolders(string repositoryPath, CancellationToken cancellationToken)
    {
        using var repository = new Repository(repositoryPath);
        if (repository.Head.Tip is null)
            return [];

        var filesByFolder = new Dictionary<string, List<(string Name, Blob Blob)>>(StringComparer.Ordinal);
        foreach (var (path, blob) in GitTreeReader.EnumerateBlobs(repository))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slashIndex = path.LastIndexOf('/');
            var folder = slashIndex < 0 ? string.Empty : path[..slashIndex];
            var name = slashIndex < 0 ? path : path[(slashIndex + 1)..];

            if (!filesByFolder.TryGetValue(folder, out var files))
            {
                files = [];
                filesByFolder[folder] = files;
            }

            files.Add((name, blob));
        }

        var folders = new List<RepositoryFolderAnalysis>(filesByFolder.Count);
        foreach (var (folder, files) in filesByFolder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            folders.Add(AnalyzeFolder(folder, files, cancellationToken));
        }

        folders.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        return folders;
    }

    private static RepositoryFolderAnalysis AnalyzeFolder(
        string folder,
        IReadOnlyList<(string Name, Blob Blob)> files,
        CancellationToken cancellationToken)
    {
        var todoCount = 0;
        var fixmeCount = 0;
        var readme = false;
        var license = false;
        var tests = IsTestDirectory(folder);

        foreach (var (name, blob) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedName = name.ToLowerInvariant();
            if (normalizedName.StartsWith("readme", StringComparison.Ordinal))
                readme = true;
            if (IsLicenseName(normalizedName))
                license = true;
            if (IsTestFile(name, normalizedName))
                tests = true;

            if (blob.IsBinary)
                continue;

            foreach (var line in blob.GetContentText().Split('\n'))
            {
                if (ContainsMarkerWord(line, TodoMarker))
                    todoCount++;
                if (ContainsMarkerWord(line, FixmeMarker))
                    fixmeCount++;
            }
        }

        return new RepositoryFolderAnalysis(folder, files.Count, todoCount, fixmeCount, readme, license, tests);
    }

    private static (string Content, bool Truncated) ReadText(Blob blob, long maxFileBytes)
    {
        if (maxFileBytes <= 0 || blob.Size <= maxFileBytes)
            return (blob.GetContentText(), false);

        using var stream = blob.GetContentStream();
        var length = (int)Math.Min(maxFileBytes, int.MaxValue);
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = stream.Read(buffer, offset, length - offset);
            if (read <= 0)
                break;
            offset += read;
        }

        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(buffer, 0, offset);
        return (content, true);
    }

    private static Blob? FindBlob(Tree tree, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;

        var current = tree;
        for (var index = 0; index < segments.Length; index++)
        {
            var entry = FindEntry(current, segments[index]);
            if (entry is null)
                return null;
            if (index == segments.Length - 1)
                return entry.Target as Blob;
            if (entry.Target is not Tree child)
                return null;
            current = child;
        }

        return null;
    }

    private static TreeEntry? FindEntry(Tree tree, string name)
    {
        foreach (var entry in tree)
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim().Trim('/');

    private static string DetectLanguage(string path)
    {
        var name = Path.GetFileName(path);
        if (string.Equals(name, "Dockerfile", StringComparison.OrdinalIgnoreCase))
            return "dockerfile";
        if (string.Equals(name, "Makefile", StringComparison.OrdinalIgnoreCase))
            return "makefile";

        var extension = Path.GetExtension(path);
        if (LanguageByExtension.TryGetValue(extension, out var language))
            return language;

        return extension.Length > 1 ? extension[1..].ToLowerInvariant() : string.Empty;
    }

    private static bool IsLicenseName(string normalizedName) =>
        normalizedName.StartsWith("license", StringComparison.Ordinal)
        || normalizedName.StartsWith("licence", StringComparison.Ordinal)
        || normalizedName.StartsWith("copying", StringComparison.Ordinal)
        || normalizedName.StartsWith("unlicense", StringComparison.Ordinal);

    private static bool IsTestFile(string name, string normalizedName) =>
        TestFilePattern.IsMatch(normalizedName)
        || name.EndsWith("Test.cs", StringComparison.Ordinal)
        || name.EndsWith("Tests.cs", StringComparison.Ordinal);

    private static bool IsTestDirectory(string folder)
    {
        if (folder.Length == 0)
            return false;

        foreach (var segment in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = segment.ToLowerInvariant();
            if (TestDirectoryNames.Contains(normalized, StringComparer.Ordinal))
                return true;
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
}
