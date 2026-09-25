using System.Diagnostics;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public static class TestRepositoryFactory
{
    public static void Create(string repositoryPath, string scriptFileName)
    {
        var scriptPath = Path.Join(AppContext.BaseDirectory, "Scripts", scriptFileName);

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add(repositoryPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Не удалось запустить pwsh.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Скрипт '{scriptFileName}' завершился с кодом {process.ExitCode}.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
    }
}
