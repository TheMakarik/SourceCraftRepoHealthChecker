using DotNetEnv;
using SourceCraftRepoHealthChecker.Application.Configuration;

namespace SourceCraftRepoHealthChecker.infrastructure.Configuration;

public sealed class DotEnvEnvironmentFileLoader : IEnvironmentFileLoader
{
    public void Load(string path)
    {
        if (File.Exists(path))
            Env.Load(path);
    }
}
