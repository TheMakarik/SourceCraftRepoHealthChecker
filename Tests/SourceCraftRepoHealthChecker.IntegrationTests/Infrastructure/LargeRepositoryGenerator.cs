using LibGit2Sharp;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public static class LargeRepositoryGenerator
{
    private static readonly DateTimeOffset BaseDate = new(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);

    public static void Create(string repositoryPath, int files, int commits, int authors)
    {
        if (Directory.Exists(repositoryPath))
            Directory.Delete(repositoryPath, recursive: true);

        Directory.CreateDirectory(repositoryPath);
        Repository.Init(repositoryPath);

        File.WriteAllText(
            Path.Join(repositoryPath, "README.md"),
            "# Large Repository\n\n## Getting Started\n\nRun locally with the following commands.\n\n## Build and test\n\n```sh\ndotnet build\ndotnet test\n```\n");
        File.WriteAllText(
            Path.Join(repositoryPath, "LICENSE"),
            "MIT License\n\nCopyright (c) 2026 Large Repository\n\nPermission is hereby granted, free of charge, to any person obtaining a copy.\n");

        using var repository = new Repository(repositoryPath);

        Commands.Stage(repository, "README.md");
        Commands.Stage(repository, "LICENSE");

        var filesPerCommit = (int)Math.Ceiling((double)files / commits);
        var filesCreated = 0;
        var firstCommitTagged = false;

        for (var commitIndex = 0; commitIndex < commits; commitIndex++)
        {
            var authorIndex = commitIndex % authors;
            var signature = new Signature($"Author {authorIndex}", $"author{authorIndex}@example.com", BaseDate.AddMinutes(commitIndex));

            var filesThisCommit = Math.Min(filesPerCommit, files - filesCreated);
            if (filesThisCommit > 0)
            {
                var batchDirectoryName = $"batch-{commitIndex:D5}";
                var batchDirectory = Path.Join(repositoryPath, "src", batchDirectoryName);
                Directory.CreateDirectory(batchDirectory);

                for (var fileIndex = 0; fileIndex < filesThisCommit; fileIndex++)
                {
                    var globalIndex = filesCreated + fileIndex;
                    var content = $"namespace Large;\n\n// file {globalIndex}\npublic static class File{globalIndex}\n{{\n    public static int Value => {globalIndex};\n}}\n";

                    if (globalIndex % 10 == 0)
                        content += $"// TODO: refactor file {globalIndex}\n";

                    if (globalIndex % 25 == 0)
                        content += $"// FIXME: fix file {globalIndex}\n";

                    File.WriteAllText(Path.Join(batchDirectory, $"File{globalIndex:D6}.cs"), content);
                }

                filesCreated += filesThisCommit;
                Commands.Stage(repository, Path.Join("src", batchDirectoryName));
            }

            var commit = repository.Commit($"Batch {commitIndex}", signature, signature, new CommitOptions { AllowEmptyCommit = true });
            if (firstCommitTagged)
                continue;

            repository.Tags.Add("v1.0.0", commit, signature, "First release");
            firstCommitTagged = true;
        }

        repository.Tags.Add(
            "v2.0.0",
            repository.Head.Tip,
            new Signature("Large Repository Author", "large-repository@example.com", BaseDate.AddDays(1)),
            "Latest release");
    }
}
